using Boxroom_Books;
using MelonLoader;
using SteamShelf.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BR_BookSystem
{
    /// <summary>
    /// Adapts the imported PageFlip prefab to a BookData archive. It owns the
    /// temporary page textures, page navigation, and UI lifetime so closing the
    /// reader releases potentially large comic images instead of retaining them.
    /// </summary>
    public sealed class PageFlipReaderController : MonoBehaviour
    {
        private Canvas canvas;
        private Button nextButton;
        private Button previousButton;
        private AutoFlip flip;
        private Book pageFlipBook;
        private readonly List<Sprite> loadedSprites = new();
        private readonly List<Texture2D> loadedTextures = new();

        public static PageFlipReaderController Instance { get; private set; }
        public bool IsOpen { get; private set; }

        public static bool Open(BookData data)
        {
            if (Instance == null && !Create()) return false;
            return Instance.OpenInternal(data);
        }

        private static bool Create()
        {
            GameObject instance = BookAssetBundle.InstantiateBookReaderPrefab();
            if (instance == null) return false;
            DontDestroyOnLoad(instance);
            PageFlipReaderController controller = instance.AddComponent<PageFlipReaderController>();
            return controller.Initialize();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private bool Initialize()
        {
            canvas = GetComponentInChildren<Canvas>(true);
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                if (button.name == "btn_next") nextButton = button;
                else if (button.name == "btn_prev") previousButton = button;
            }
            if (canvas == null || nextButton == null || previousButton == null)
            {
                MelonLogger.Error("BookReader prefab is missing its Canvas or page buttons.");
                Destroy(gameObject);
                return false;
            }
            canvas.enabled = false;
            return true;
        }

        private bool OpenInternal(BookData data)
        {
            try
            {
                ClearCurrentBook();
                Transform bookRoot = transform.Find("Canvas/Book");
                if (bookRoot == null) throw new InvalidOperationException("BookReader is missing Canvas/Book.");

                pageFlipBook = bookRoot.gameObject.AddComponent<Book>();
                flip = bookRoot.gameObject.AddComponent<AutoFlip>();
                WireBook();
                pageFlipBook.bookPages = ReadPages(data);
                if (pageFlipBook.bookPages.Length == 0) throw new InvalidOperationException("The CBZ contains no readable images.");
                pageFlipBook.background = BookAssetBundle.LoadSprite("transparent");

                nextButton.onClick.RemoveAllListeners();
                previousButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(flip.FlipRightPage);
                previousButton.onClick.AddListener(flip.FlipLeftPage);

                canvas.enabled = true;
                IsOpen = true;
                Singleton<InputManager>.Instance.SwapToInputMap(EInputMap.UI);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Could not open PageFlip reader: {ex}");
                Close();
                return false;
            }
        }

        private void WireBook()
        {
            pageFlipBook.Canvas = canvas;
            pageFlipBook.BookPanel = Find<RectTransform>("Canvas/Book");
            pageFlipBook.Right = Find<Image>("Canvas/Book/Right");
            pageFlipBook.RightNext = Find<Image>("Canvas/Book/RightNext");
            pageFlipBook.Left = Find<Image>("Canvas/Book/Left");
            pageFlipBook.LeftNext = Find<Image>("Canvas/Book/LeftNext");
            pageFlipBook.Shadow = Find<Image>("Canvas/Book/Right/Shadow");
            pageFlipBook.ShadowLTR = Find<Image>("Canvas/Book/Left/ShadowLTR");
            pageFlipBook.ClippingPlane = Find<Image>("Canvas/Book/TurnPageClip");
            pageFlipBook.NextPageClip = Find<Image>("Canvas/Book/NextPageClip");
            pageFlipBook.OnFlip ??= new UnityEngine.Events.UnityEvent();
            pageFlipBook.interactable = false;
            pageFlipBook.enableShadowEffect = true;
            pageFlipBook.currentPage = 0;

            flip.ControledBook = pageFlipBook;
            flip.Mode = FlipMode.RightToLeft;
            flip.PageFlipTime = 0.5f;
            flip.TimeBetweenPages = 0.5f;
            flip.DelayBeforeStarting = 0f;
            flip.AnimationFramesCount = 40;
            flip.AutoStartFlip = false;
        }

        private Sprite[] ReadPages(BookData data)
        {
            var pages = new List<Sprite>();
            string archivePath = ComicArchive.Find(data.FolderPath);
            if (archivePath == null) throw new FileNotFoundException("No CBZ or CBR was found for the Book.", data.FolderPath);
            foreach (ComicPage page in ComicArchive.ReadPages(archivePath))
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, page.Bytes, false))
                {
                    Destroy(texture);
                    continue;
                }
                texture.name = page.Name;
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name;
                loadedTextures.Add(texture);
                loadedSprites.Add(sprite);
                pages.Add(sprite);
            }
            return pages.ToArray();
        }

        private T Find<T>(string path) where T : Component
        {
            Transform child = transform.Find(path);
            if (child == null || !child.TryGetComponent(out T component))
                throw new InvalidOperationException($"BookReader is missing {path} ({typeof(T).Name}).");
            return component;
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Keyboard.current?.bKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true || Mouse.current?.rightButton.wasPressedThisFrame == true)
                Close();
        }

        public void Close()
        {
            if (canvas != null) canvas.enabled = false;
            IsOpen = false;
            ClearCurrentBook();
            if (Singleton<InputManager>.HasInstance()) Singleton<InputManager>.Instance.SwapToInputMap(EInputMap.Player);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ClearCurrentBook()
        {
            nextButton?.onClick.RemoveAllListeners();
            previousButton?.onClick.RemoveAllListeners();
            if (flip != null) Destroy(flip);
            if (pageFlipBook != null) Destroy(pageFlipBook);
            flip = null;
            pageFlipBook = null;
            foreach (Sprite sprite in loadedSprites) if (sprite != null) Destroy(sprite);
            foreach (Texture2D texture in loadedTextures) if (texture != null) Destroy(texture);
            loadedSprites.Clear();
            loadedTextures.Clear();
        }

        private void OnDestroy()
        {
            ClearCurrentBook();
            if (Instance == this) Instance = null;
        }

    }
}

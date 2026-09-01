using Boxroom_Books;
using MelonLoader;
using PDFtoImage;
using SkiaSharp;
using SteamShelf.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
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
        private readonly Dictionary<int, PdfPageAsset> pdfPageCache = new();
        private byte[] pdfBytes;
        private List<EpubReaderPage> epubPages;
        private Texture2D pdfPlaceholderTexture;
        private Sprite pdfPlaceholder;
        private string activeBookId;
        private bool rightToLeftReading;
        private int sourcePageCount;

        private sealed class PdfPageAsset
        {
            internal Texture2D Texture;
            internal Sprite Sprite;
        }

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
                rightToLeftReading = string.Equals(data.BookType, "Manga", StringComparison.OrdinalIgnoreCase);
                pageFlipBook.background = BookAssetBundle.LoadSprite("transparent");
                pageFlipBook.bookPages = ReadPages(data);
                if (pageFlipBook.bookPages.Length == 0) throw new InvalidOperationException("The CBZ contains no readable images.");
                activeBookId = data.Id;
                pageFlipBook.currentPage = ReadingProgress.Get(activeBookId, pageFlipBook.bookPages.Length, rightToLeftReading);
                if (pdfBytes != null || epubPages != null) RenderPdfWindow(pageFlipBook.currentPage);
                FitReaderToPages(pageFlipBook.bookPages);

                nextButton.onClick.RemoveAllListeners();
                previousButton.onClick.RemoveAllListeners();
                if (pdfBytes != null || epubPages != null)
                {
                    nextButton.onClick.AddListener(PrepareNextPdfPages);
                    previousButton.onClick.AddListener(PreparePreviousPdfPages);
                }
                pageFlipBook.OnFlip.AddListener(HandlePageFlip);
                nextButton.onClick.AddListener(rightToLeftReading ? flip.FlipLeftPage : flip.FlipRightPage);
                previousButton.onClick.AddListener(rightToLeftReading ? flip.FlipRightPage : flip.FlipLeftPage);

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
            if (string.Equals(data.Extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                return ReadPdfPages(data.ContentPath);
            if (string.Equals(data.Extension, ".epub", StringComparison.OrdinalIgnoreCase))
                return ReadEpubPages(data.ContentPath);

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
            if (rightToLeftReading && pages.Count > 0)
            {
                pages.Reverse();
                // A Manga begins with its cover on the left and an unopened blank
                // side on the right. Appending the blank makes PageFlip show the
                // reversed-array cover at currentPage - 1.
                pages.Add(pageFlipBook.background);
            }
            return pages.ToArray();
        }

        private Sprite[] ReadPdfPages(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("The PDF file was not found.", path);

            pdfBytes = File.ReadAllBytes(path);
            int pageCount = Conversion.GetPageCount(pdfBytes);
            if (pageCount <= 0) throw new InvalidOperationException("The PDF contains no readable pages.");
            if (pageCount > 5000) throw new InvalidOperationException($"The PDF has an unsupported page count ({pageCount}).");
            sourcePageCount = pageCount;

            pdfPlaceholderTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            pdfPlaceholderTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            pdfPlaceholderTexture.Apply(false, false);
            pdfPlaceholder = Sprite.Create(
                pdfPlaceholderTexture,
                new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f),
                100f);
            pdfPlaceholder.name = "PDF placeholder";

            Sprite[] pages = CreateLazyPageArray(rightToLeftReading ? pageCount + 1 : pageCount);
            if (rightToLeftReading) pages[pageCount] = pageFlipBook.background;
            pageFlipBook.bookPages = pages;
            RenderPdfWindow(0);
            return pages;
        }

        private Sprite[] ReadEpubPages(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("The EPUB file was not found.", path);
            epubPages = EpubPageRenderer.ReadAndPaginate(path);
            sourcePageCount = epubPages.Count;
            Sprite[] pages = CreateLazyPageArray(rightToLeftReading ? epubPages.Count + 1 : epubPages.Count);
            if (rightToLeftReading) pages[epubPages.Count] = pageFlipBook.background;
            pageFlipBook.bookPages = pages;
            RenderPdfWindow(0);
            return pages;
        }

        private Sprite[] CreateLazyPageArray(int pageCount)
        {
            if (pdfPlaceholder == null)
            {
                pdfPlaceholderTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                pdfPlaceholderTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                pdfPlaceholderTexture.Apply(false, false);
                pdfPlaceholder = Sprite.Create(
                    pdfPlaceholderTexture,
                    new Rect(0, 0, 2, 2),
                    new Vector2(0.5f, 0.5f),
                    100f);
                pdfPlaceholder.name = "Reader placeholder";
            }
            return Enumerable.Repeat(pdfPlaceholder, pageCount).ToArray();
        }

        private void PrepareNextPdfPages()
        {
            if (pageFlipBook != null) RenderPdfWindow(pageFlipBook.currentPage + 2);
        }

        private void PreparePreviousPdfPages()
        {
            if (pageFlipBook != null) RenderPdfWindow(pageFlipBook.currentPage - 2);
        }

        private void HandlePageFlip()
        {
            if (pageFlipBook == null) return;
            ReadingProgress.Save(activeBookId, pageFlipBook.currentPage, pageFlipBook.bookPages.Length, rightToLeftReading);
            if (pdfBytes != null || epubPages != null)
            {
                RenderPdfWindow(pageFlipBook.currentPage);
                TrimPdfCache(pageFlipBook.currentPage);
            }
        }

        private void RenderPdfWindow(int centerPage)
        {
            if (epubPages != null)
            {
                RenderEpubWindow(centerPage);
                return;
            }
            if (pdfBytes == null || pageFlipBook?.bookPages == null) return;
            int first = Mathf.Max(0, centerPage - 3);
            int last = Mathf.Min(pageFlipBook.bookPages.Length - 1, centerPage + 4);
            var options = new RenderOptions
            {
                Height = 1600,
                WithAspectRatio = true,
                WithAnnotations = true,
                WithFormFill = true
            };

            for (int page = first; page <= last; page++)
            {
                if (pdfPageCache.ContainsKey(page)) continue;
                int sourcePage = MapSourcePage(page, sourcePageCount);
                if (sourcePage < 0)
                {
                    pageFlipBook.bookPages[page] = pageFlipBook.background;
                    continue;
                }
                using SKBitmap bitmap = Conversion.ToImage(pdfBytes, sourcePage, options: options);
                using SKData encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                byte[] png = encoded.ToArray();
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, png, false))
                {
                    Destroy(texture);
                    continue;
                }
                texture.name = $"PDF page {page + 1}";
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = texture.name;
                pdfPageCache[page] = new PdfPageAsset { Texture = texture, Sprite = sprite };
                pageFlipBook.bookPages[page] = sprite;
            }
        }

        private void RenderEpubWindow(int centerPage)
        {
            if (epubPages == null || pageFlipBook?.bookPages == null) return;
            int first = Mathf.Max(0, centerPage - 3);
            int last = Mathf.Min(epubPages.Count - 1, centerPage + 4);
            for (int page = first; page <= last; page++)
            {
                if (pdfPageCache.ContainsKey(page)) continue;
                int sourcePage = MapSourcePage(page, epubPages.Count);
                if (sourcePage < 0)
                {
                    pageFlipBook.bookPages[page] = pageFlipBook.background;
                    continue;
                }
                using SKBitmap bitmap = EpubPageRenderer.Render(epubPages[sourcePage], sourcePage, epubPages.Count);
                StoreRenderedPage(page, bitmap);
            }
        }

        private int MapSourcePage(int readerPage, int sourcePageCount)
        {
            if (!rightToLeftReading) return readerPage;
            if (readerPage == sourcePageCount) return -1;     // unopened blank side on the right
            if (readerPage == sourcePageCount - 1) return 0;  // lone cover on the left
            return sourcePageCount - 1 - readerPage;
        }

        private void StoreRenderedPage(int page, SKBitmap bitmap)
        {
            using SKData encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, encoded.ToArray(), false))
            {
                Destroy(texture);
                return;
            }
            texture.name = $"Reader page {page + 1}";
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = texture.name;
            pdfPageCache[page] = new PdfPageAsset { Texture = texture, Sprite = sprite };
            pageFlipBook.bookPages[page] = sprite;
        }

        private void TrimPdfCache(int centerPage)
        {
            foreach (int page in pdfPageCache.Keys.Where(page => Mathf.Abs(page - centerPage) > 6).ToArray())
            {
                PdfPageAsset asset = pdfPageCache[page];
                pageFlipBook.bookPages[page] = pdfPlaceholder;
                if (asset.Sprite != null) Destroy(asset.Sprite);
                if (asset.Texture != null) Destroy(asset.Texture);
                pdfPageCache.Remove(page);
            }
        }

        /// <summary>
        /// Resizes the two-page spread to the source pages instead of stretching
        /// landscape comics into the portrait dimensions authored in the prefab.
        /// The spread remains inside the current canvas on every aspect ratio.
        /// </summary>
        private void FitReaderToPages(IReadOnlyList<Sprite> pages)
        {
            if (pages == null || pages.Count == 0 || pageFlipBook?.BookPanel == null) return;

            var aspects = pages
                .Where(sprite => sprite != null && sprite != pdfPlaceholder && sprite.rect.height > 0f)
                .Select(sprite => sprite.rect.width / sprite.rect.height)
                .OrderBy(value => value)
                .ToArray();
            if (aspects.Length == 0) return;

            // The median ignores an occasional differently-sized cover or insert.
            float pageAspect = Mathf.Clamp(aspects[aspects.Length / 2], 0.4f, 2.4f);
            RectTransform canvasRect = canvas.transform as RectTransform;
            float availableWidth = canvasRect != null && canvasRect.rect.width > 0f
                ? canvasRect.rect.width * 0.90f
                : 1728f;
            float availableHeight = canvasRect != null && canvasRect.rect.height > 0f
                ? canvasRect.rect.height * 0.80f
                : 864f;
            float spreadAspect = pageAspect * 2f;
            float width = Mathf.Min(availableWidth, availableHeight * spreadAspect);
            float height = width / spreadAspect;

            pageFlipBook.BookPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            pageFlipBook.BookPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(pageFlipBook.BookPanel);
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
            {
                Close();
                return;
            }

            if (!HasFocusedTextInput() && Keyboard.current != null)
            {
                if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
                    previousButton?.onClick.Invoke();
                else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
                    nextButton?.onClick.Invoke();
            }
        }

        private static bool HasFocusedTextInput()
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null) return false;
            InputField unityInput = selected.GetComponent<InputField>() ?? selected.GetComponentInParent<InputField>();
            if (unityInput != null && unityInput.isFocused) return true;
            TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>() ?? selected.GetComponentInParent<TMP_InputField>();
            return tmpInput != null && tmpInput.isFocused;
        }

        public void Close()
        {
            if (pageFlipBook != null) ReadingProgress.Save(activeBookId, pageFlipBook.currentPage, pageFlipBook.bookPages.Length, rightToLeftReading);
            if (canvas != null) canvas.enabled = false;
            IsOpen = false;
            ClearCurrentBook();
            BookInspectUiVisibility.Restore();
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
            foreach (PdfPageAsset asset in pdfPageCache.Values)
            {
                if (asset.Sprite != null) Destroy(asset.Sprite);
                if (asset.Texture != null) Destroy(asset.Texture);
            }
            pdfPageCache.Clear();
            if (pdfPlaceholder != null) Destroy(pdfPlaceholder);
            if (pdfPlaceholderTexture != null) Destroy(pdfPlaceholderTexture);
            pdfPlaceholder = null;
            pdfPlaceholderTexture = null;
            pdfBytes = null;
            epubPages = null;
            activeBookId = null;
            rightToLeftReading = false;
            sourcePageCount = 0;
        }

        private void OnDestroy()
        {
            ClearCurrentBook();
            if (Instance == this) Instance = null;
        }

    }
}

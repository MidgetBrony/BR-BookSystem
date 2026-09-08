using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Boxroom_Books
{
    /// <summary>
    /// Shared, reference-counted cover textures. This follows BOXROOM's native
    /// SteamTextureCache lifecycle so a cover is decoded once and disappears when
    /// no exposed shelf, held, inspected, or placed book is using it.
    /// </summary>
    internal static class BookCoverTextureCache
    {
        internal const int ShelfMaxSize = 512;
        internal const int DetailMaxSize = 1024;

        private static readonly Dictionary<string, Texture2D> textures = new();
        private static readonly Dictionary<string, int> referenceCounts = new();

        internal static Texture2D Acquire(BookData book, int maxSize = DetailMaxSize)
        {
            if (book == null || string.IsNullOrWhiteSpace(book.Id))
                return null;

            string key = GetKey(book.Id, maxSize);
            Texture2D texture = Get(book, maxSize);
            if (texture == null)
                return null;

            referenceCounts.TryGetValue(key, out int count);
            referenceCounts[key] = count + 1;
            return texture;
        }

        internal static Texture2D Get(BookData book, int maxSize = DetailMaxSize)
        {
            if (book == null || string.IsNullOrWhiteSpace(book.Id))
                return null;

            string key = GetKey(book.Id, maxSize);
            if (textures.TryGetValue(key, out Texture2D cached))
                return cached;

            if (string.IsNullOrWhiteSpace(book.CoverArtPath) ||
                !File.Exists(book.CoverArtPath))
            {
                return null;
            }

            Texture2D source = new(2, 2, TextureFormat.RGBA32, mipChain: false);

            try
            {
                byte[] bytes = File.ReadAllBytes(book.CoverArtPath);
                if (!ImageConversion.LoadImage(source, bytes))
                {
                    Object.Destroy(source);
                    return null;
                }

                if (source.height > 0)
                    book.CoverAspectRatio = (float)source.width / source.height;

                Texture2D texture = ResizeForUse(source, maxSize);
                if (texture != source)
                    Object.Destroy(source);

                if (texture.width % 4 == 0 && texture.height % 4 == 0)
                    texture.Compress(highQuality: false);

                texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                textures[key] = texture;
                return texture;
            }
            catch (System.Exception ex)
            {
                Object.Destroy(source);
                MelonLogger.Warning($"Failed loading cover for '{book.Id}': {ex.Message}");
                return null;
            }
        }

        internal static void Release(string bookId, int maxSize = DetailMaxSize)
        {
            string key = GetKey(bookId, maxSize);
            if (string.IsNullOrWhiteSpace(bookId) ||
                !referenceCounts.TryGetValue(key, out int count))
            {
                return;
            }

            if (count > 1)
            {
                referenceCounts[key] = count - 1;
                return;
            }

            referenceCounts.Remove(key);
            if (textures.TryGetValue(key, out Texture2D texture))
            {
                Object.Destroy(texture);
                textures.Remove(key);
            }
        }

        private static string GetKey(string bookId, int maxSize) =>
            $"{bookId}|{Mathf.Max(64, maxSize)}";

        private static Texture2D ResizeForUse(Texture2D source, int maxSize)
        {
            maxSize = Mathf.Max(64, maxSize);
            int largest = Mathf.Max(source.width, source.height);
            if (largest <= maxSize)
                return source;

            float scale = (float)maxSize / largest;
            int width = Mathf.Max(4, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(4, Mathf.RoundToInt(source.height * scale));
            width -= width % 4;
            height -= height % 4;

            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;

            try
            {
                // Preserve the source pixels here. Cover orientation belongs to
                // each prefab's material UVs; baking a flip into this shared texture
                // makes resized and non-resized covers disagree.
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D resized = new(width, height, TextureFormat.RGBA32, mipChain: true);
                resized.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                resized.Apply(updateMipmaps: true, makeNoLongerReadable: false);
                return resized;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        internal static void EvictAll()
        {
            foreach (Texture2D texture in textures.Values)
                if (texture != null) Object.Destroy(texture);

            textures.Clear();
            referenceCounts.Clear();
        }
    }

    internal sealed class BookCoverTextureLease : MonoBehaviour
    {
        private string bookId;
        private int maxSize;

        internal Texture2D Bind(BookData book, int requestedMaxSize = BookCoverTextureCache.DetailMaxSize)
        {
            if (book == null)
            {
                Release();
                return null;
            }

            if (bookId == book.Id && maxSize == requestedMaxSize)
                return BookCoverTextureCache.Get(book, requestedMaxSize);

            Release();
            bookId = book.Id;
            maxSize = requestedMaxSize;
            return BookCoverTextureCache.Acquire(book, requestedMaxSize);
        }

        internal void Unbind() => Release();

        private void OnDestroy() => Release();

        private void Release()
        {
            if (!string.IsNullOrWhiteSpace(bookId))
                BookCoverTextureCache.Release(bookId, maxSize);
            bookId = null;
            maxSize = 0;
        }
    }

    internal static class CoverImageInfo
    {
        internal static float ReadAspectRatio(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                if (TryReadPng(stream, out int width, out int height) ||
                    TryReadJpeg(stream, out width, out height))
                {
                    return height > 0 ? (float)width / height : 0f;
                }
            }
            catch
            {
                // A bad cover remains usable through the fallback book dimensions.
            }

            return 0f;
        }

        private static bool TryReadPng(Stream stream, out int width, out int height)
        {
            width = height = 0;
            stream.Position = 0;
            byte[] header = new byte[24];
            if (stream.Read(header, 0, header.Length) != header.Length ||
                header[0] != 0x89 || header[1] != 0x50 ||
                header[2] != 0x4E || header[3] != 0x47)
            {
                return false;
            }

            width = ReadBigEndianInt32(header, 16);
            height = ReadBigEndianInt32(header, 20);
            return width > 0 && height > 0;
        }

        private static bool TryReadJpeg(Stream stream, out int width, out int height)
        {
            width = height = 0;
            stream.Position = 0;
            if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
                return false;

            while (stream.Position < stream.Length)
            {
                int prefix;
                do prefix = stream.ReadByte(); while (prefix != -1 && prefix != 0xFF);
                if (prefix == -1) return false;

                int marker;
                do marker = stream.ReadByte(); while (marker == 0xFF);
                if (marker == -1 || marker == 0xD9 || marker == 0xDA) return false;
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;

                int length = ReadBigEndianUInt16(stream);
                if (length < 2) return false;
                if (IsStartOfFrame(marker))
                {
                    if (stream.ReadByte() == -1) return false;
                    height = ReadBigEndianUInt16(stream);
                    width = ReadBigEndianUInt16(stream);
                    return width > 0 && height > 0;
                }

                stream.Seek(length - 2, SeekOrigin.Current);
            }

            return false;
        }

        private static bool IsStartOfFrame(int marker) =>
            marker >= 0xC0 && marker <= 0xCF &&
            marker != 0xC4 && marker != 0xC8 && marker != 0xCC;

        private static int ReadBigEndianUInt16(Stream stream)
        {
            int high = stream.ReadByte();
            int low = stream.ReadByte();
            return high < 0 || low < 0 ? -1 : (high << 8) | low;
        }

        private static int ReadBigEndianInt32(byte[] value, int offset) =>
            (value[offset] << 24) |
            (value[offset + 1] << 16) |
            (value[offset + 2] << 8) |
            value[offset + 3];
    }
}

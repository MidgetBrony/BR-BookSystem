using MelonLoader;
using ModsPanel;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace BR_BookSystem
{
    internal static class EpubReaderSettings
    {
        private static readonly IReadOnlyList<string> FontOptions = new[] { "Serif", "Sans Serif", "Monospace" };
        private static MelonPreferences_Entry<float> fontSize;
        private static MelonPreferences_Entry<string> fontType;
        internal static float FontSize => Math.Max(30f, Math.Min(72f, fontSize?.Value ?? 48f));

        internal static void Initialize()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("BRBookSystemEpub", "BR-BookSystem EPUB Reader");
            fontSize = category.CreateEntry("FontSize", 48f, "Font size");
            fontType = category.CreateEntry("FontType", FontOptions[0], "Font type");
            ModsPanelApi.RegisterSection("Rusty.BR-BookSystem", "BR-BookSystem", 100)
                .AddLabel("epub-settings-heading", "EPUB Reader")
                .AddSlider("epub-font-size", "Font Size", () => FontSize, SetFontSize, 30f, 72f, true, "0 px")
                .AddDropdown("epub-font-type", "Font Type", () => FontOptions, GetFontIndex, SetFontIndex)
                .AddLabel("epub-settings-note", "Font changes apply when the next EPUB is opened. EPUB illustrations are shown as full reader pages.");
        }

        internal static SKTypeface CreateTypeface()
        {
            string family = GetFontIndex() switch { 1 => "sans-serif", 2 => "monospace", _ => "serif" };
            return SKTypeface.FromFamilyName(family) ?? SKTypeface.Default;
        }

        private static int GetFontIndex()
        {
            string selected = fontType?.Value ?? FontOptions[0];
            for (int i = 0; i < FontOptions.Count; i++) if (string.Equals(FontOptions[i], selected, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }
        private static void SetFontSize(float value) { fontSize.Value = Math.Max(30f, Math.Min(72f, (float)Math.Round(value))); MelonPreferences.Save(); }
        private static void SetFontIndex(int index) { index = Math.Max(0, Math.Min(FontOptions.Count - 1, index)); fontType.Value = FontOptions[index]; MelonPreferences.Save(); }
    }
}

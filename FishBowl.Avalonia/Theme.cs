using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace EmulatorHub
{
    // FishBowl's themes and accents, matching the WinForms build's ApplyThemeColors.
    public sealed class Palette
    {
        public Color Ink, Top, Bottom, Surface, Subtle, Blue, Pink;
        public Color[] Swatches;
        public static readonly string[] Themes = { "Twilight", "Lavender", "Ember", "Light", "High Contrast", "Midnight", "Forest", "Rosewood", "Mist" };
        public static readonly string[] Accents = { "Sunset", "Amethyst", "Ocean", "Rose", "Lime", "Gold", "Ice" };
        public static Palette Current { get; private set; } = From(null);

        public bool IsDark { get { return Brightness(Bottom) < 0.65f; } }
        public IBrush InkBrush { get { return new SolidColorBrush(Ink); } }
        public IBrush TopBrush { get { return new SolidColorBrush(Top); } }
        public IBrush BottomBrush { get { return new SolidColorBrush(Bottom); } }
        public IBrush SurfaceBrush { get { return new SolidColorBrush(Surface); } }
        public IBrush SubtleBrush { get { return new SolidColorBrush(Subtle); } }
        public IBrush BlueBrush { get { return new SolidColorBrush(Blue); } }
        public IBrush PinkBrush { get { return new SolidColorBrush(Pink); } }

        public static Palette Apply(ThemeSettings theme) { Current = From(theme); return Current; }

        public static Palette From(ThemeSettings settings)
        {
            var p = new Palette();
            var theme = settings == null ? "Twilight" : settings.Name;
            switch (theme)
            {
                case "Lavender": p.Set(Rgb(44, 37, 54), Rgb(242, 237, 248), Rgb(231, 222, 240), Rgb(255, 250, 255), Rgb(105, 91, 119), Rgb(137, 80, 215), Rgb(236, 128, 65)); break;
                case "Light": p.Set(Rgb(42, 43, 50), Rgb(246, 246, 249), Rgb(235, 236, 241), Rgb(255, 255, 255), Rgb(101, 103, 115), Rgb(103, 82, 171), Rgb(203, 113, 61)); break;
                case "High Contrast": p.Set(Colors.White, Colors.Black, Rgb(17, 17, 17), Rgb(30, 30, 30), Rgb(224, 224, 224), Rgb(112, 191, 255), Rgb(255, 220, 77)); break;
                case "Ember": p.Set(Rgb(255, 248, 240), Rgb(47, 28, 25), Rgb(64, 38, 32), Rgb(84, 50, 40), Rgb(229, 191, 174), Rgb(218, 107, 174), Rgb(255, 176, 72)); break;
                case "Midnight": p.Set(Rgb(232, 237, 250), Rgb(16, 20, 33), Rgb(21, 27, 43), Rgb(35, 43, 63), Rgb(159, 173, 201), Rgb(111, 142, 255), Rgb(121, 211, 255)); break;
                case "Forest": p.Set(Rgb(235, 246, 238), Rgb(27, 46, 38), Rgb(34, 58, 47), Rgb(48, 76, 62), Rgb(177, 206, 187), Rgb(111, 202, 150), Rgb(206, 211, 109)); break;
                case "Rosewood": p.Set(Rgb(250, 239, 243), Rgb(52, 30, 40), Rgb(67, 38, 52), Rgb(87, 51, 67), Rgb(225, 184, 199), Rgb(226, 110, 166), Rgb(255, 169, 116)); break;
                case "Mist": p.Set(Rgb(38, 46, 56), Rgb(237, 243, 248), Rgb(222, 231, 239), Rgb(251, 253, 255), Rgb(92, 109, 125), Rgb(69, 135, 191), Rgb(200, 113, 116)); break;
                default: p.Set(Rgb(239, 239, 243), Rgb(31, 32, 37), Rgb(35, 36, 42), Rgb(47, 48, 56), Rgb(174, 176, 186), Rgb(183, 150, 245), Rgb(235, 158, 94)); break;
            }
            var accent = settings == null || settings.AccentColor == null ? "Sunset" : settings.AccentColor;
            switch (accent)
            {
                case "Ocean": p.Blue = Rgb(89, 190, 255); p.Pink = Rgb(67, 220, 187); break;
                case "Rose": p.Blue = Rgb(228, 100, 181); p.Pink = Rgb(255, 157, 101); break;
                case "Lime": p.Blue = Rgb(155, 213, 98); p.Pink = Rgb(246, 210, 92); break;
                case "Amethyst": p.Blue = Rgb(177, 122, 255); p.Pink = Rgb(222, 109, 244); break;
                case "Gold": p.Blue = Rgb(236, 178, 62); p.Pink = Rgb(255, 219, 122); break;
                case "Ice": p.Blue = Rgb(103, 198, 232); p.Pink = Rgb(173, 235, 255); break;
            }
            p.Swatches = new[] { p.Blue, p.Pink, Rgb((byte)((p.Blue.R + p.Pink.R) / 2), (byte)((p.Blue.G + p.Pink.G) / 2), (byte)((p.Blue.B + p.Pink.B) / 2)) };
            return p;
        }
        private void Set(Color ink, Color top, Color bottom, Color surface, Color subtle, Color blue, Color pink)
        { Ink = ink; Top = top; Bottom = bottom; Surface = surface; Subtle = subtle; Blue = blue; Pink = pink; }

        public static bool IsDarkColor(Color c) { return Brightness(c) < 0.65f; }
        public static Color Rgb(byte r, byte g, byte b) { return Color.FromRgb(r, g, b); }
        public static Color Alpha(byte alpha, Color c) { return Color.FromArgb(alpha, c.R, c.G, c.B); }
        // System.Drawing's Color.GetBrightness (HSL lightness), so thresholds match the Windows build.
        public static float Brightness(Color c) { return (Math.Max(c.R, Math.Max(c.G, c.B)) + Math.Min(c.R, Math.Min(c.G, c.B))) / 510f; }
        // A subtle light (on dark) or dark (on light) wash over a background, as FishBowlHighlights.Blend.
        public static Color Blend(Color background, int opacity = 24)
        {
            var light = Brightness(background) < 0.65f ? Colors.White : Colors.Black;
            return Color.FromRgb((byte)((background.R * (255 - opacity) + light.R * opacity) / 255), (byte)((background.G * (255 - opacity) + light.G * opacity) / 255), (byte)((background.B * (255 - opacity) + light.B * opacity) / 255));
        }

        // Font families offered in Settings. Windows names fall back to a Linux font when absent.
        public static readonly string[] FontChoices = { "Bahnschrift", "Inter", "Segoe UI", "Noto Sans", "Cantarell", "DejaVu Sans", "Liberation Sans", "Ubuntu", "Arial", "Verdana", "Georgia", "Consolas", "JetBrains Mono", "Courier New" };
        public static FontFamily Font(ThemeSettings settings)
        {
            var name = settings == null || String.IsNullOrWhiteSpace(settings.FontFamily) ? "Bahnschrift" : settings.FontFamily;
            var installed = FontManager.Current.SystemFonts.Select(f => f.Name).ToList();
            if (name != "Inter" && !installed.Contains(name, StringComparer.OrdinalIgnoreCase)) name = "Inter";
            return name == "Inter" ? new FontFamily("fonts:Inter#Inter") : new FontFamily(name + ", fonts:Inter#Inter");
        }
    }
}

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
        public bool Restrained;
        public Color[] Swatches;
        public static readonly string[] Themes = { "FishBowl Water", "Twilight", "Lavender", "Ember", "Light", "High Contrast", "Midnight", "Forest", "Rosewood", "Mist", "Deep Ocean", "Aurora", "Slate", "Plum", "Sand", "Paper", "Nordic", "Copper", "Obsidian", "Graphite", "Charcoal", "Cobalt", "Indigo", "Ruby", "Burgundy", "Chocolate", "Moss", "Pine", "Lagoon", "Violet", "Ivory", "Peach", "Blush", "Mint", "Sage", "Sky", "Lilac", "Lemon", "Terracotta", "Silver" };
        public static readonly string[] Accents = { "Ocean", "Sunset", "Amethyst", "Rose", "Lime", "Gold", "Ice", "Cobalt", "Indigo", "Violet", "Orchid", "Magenta", "Ruby", "Coral", "Tangerine", "Amber", "Lemon", "Emerald", "Mint", "Teal", "Turquoise", "Sapphire", "Lavender", "Peach", "Copper", "Silver", "Pearl", "Graphite" };
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
            var p = new Palette { Restrained = settings != null && settings.RestrainedAccents };
            var theme = settings == null ? "Twilight" : settings.Name;
            switch (theme)
            {
                case "FishBowl Water": p.Set(Rgb(250, 247, 255), Rgb(5, 31, 78), Rgb(3, 13, 38), Rgb(12, 59, 122), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Twilight": p.Set(Rgb(250, 247, 255), Rgb(48, 36, 71), Rgb(24, 23, 44), Rgb(73, 56, 93), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Lavender": p.Set(Rgb(32, 35, 42), Rgb(230, 219, 250), Rgb(210, 190, 235), Rgb(244, 237, 255), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Ember": p.Set(Rgb(250, 247, 255), Rgb(85, 36, 26), Rgb(42, 18, 16), Rgb(113, 56, 41), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Light": p.Set(Rgb(32, 35, 42), Rgb(241, 241, 245), Rgb(220, 221, 230), Rgb(255, 255, 255), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "High Contrast": p.Set(Rgb(250, 247, 255), Rgb(0, 0, 0), Rgb(0, 0, 0), Rgb(22, 22, 22), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Midnight": p.Set(Rgb(250, 247, 255), Rgb(16, 20, 33), Rgb(8, 11, 20), Rgb(37, 43, 66), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Forest": p.Set(Rgb(250, 247, 255), Rgb(22, 60, 41), Rgb(12, 34, 24), Rgb(40, 86, 59), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Rosewood": p.Set(Rgb(250, 247, 255), Rgb(84, 29, 53), Rgb(46, 16, 34), Rgb(113, 48, 76), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Mist": p.Set(Rgb(32, 35, 42), Rgb(223, 234, 245), Rgb(201, 216, 233), Rgb(241, 247, 253), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Deep Ocean": p.Set(Rgb(250, 247, 255), Rgb(0, 60, 84), Rgb(0, 29, 48), Rgb(7, 90, 112), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Aurora": p.Set(Rgb(250, 247, 255), Rgb(19, 63, 69), Rgb(21, 27, 53), Rgb(36, 89, 88), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Slate": p.Set(Rgb(250, 247, 255), Rgb(53, 68, 79), Rgb(30, 41, 50), Rgb(74, 90, 102), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Plum": p.Set(Rgb(250, 247, 255), Rgb(82, 39, 99), Rgb(45, 22, 58), Rgb(107, 59, 125), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Sand": p.Set(Rgb(32, 35, 42), Rgb(234, 215, 174), Rgb(217, 191, 142), Rgb(255, 240, 211), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Paper": p.Set(Rgb(32, 35, 42), Rgb(245, 240, 228), Rgb(226, 220, 205), Rgb(255, 252, 243), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Nordic": p.Set(Rgb(32, 35, 42), Rgb(208, 236, 234), Rgb(175, 213, 213), Rgb(232, 250, 248), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Copper": p.Set(Rgb(250, 247, 255), Rgb(99, 59, 37), Rgb(53, 31, 23), Rgb(128, 84, 59), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Obsidian": p.Set(Rgb(250, 247, 255), Rgb(22, 22, 22), Rgb(8, 8, 8), Rgb(44, 44, 44), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Graphite": p.Set(Rgb(250, 247, 255), Rgb(72, 72, 72), Rgb(47, 47, 47), Rgb(92, 92, 92), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Charcoal": p.Set(Rgb(250, 247, 255), Rgb(41, 44, 48), Rgb(20, 23, 26), Rgb(62, 67, 73), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Cobalt": p.Set(Rgb(250, 247, 255), Rgb(17, 59, 164), Rgb(9, 27, 82), Rgb(36, 84, 191), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Indigo": p.Set(Rgb(250, 247, 255), Rgb(48, 33, 123), Rgb(23, 18, 62), Rgb(72, 55, 156), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Ruby": p.Set(Rgb(250, 247, 255), Rgb(101, 27, 43), Rgb(51, 13, 26), Rgb(132, 49, 66), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Burgundy": p.Set(Rgb(250, 247, 255), Rgb(73, 35, 55), Rgb(36, 17, 30), Rgb(101, 56, 77), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Chocolate": p.Set(Rgb(250, 247, 255), Rgb(76, 50, 41), Rgb(36, 26, 23), Rgb(103, 75, 62), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Moss": p.Set(Rgb(250, 247, 255), Rgb(55, 72, 32), Rgb(29, 41, 20), Rgb(81, 101, 52), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Pine": p.Set(Rgb(250, 247, 255), Rgb(6, 68, 54), Rgb(3, 37, 31), Rgb(18, 97, 78), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Lagoon": p.Set(Rgb(250, 247, 255), Rgb(0, 93, 106), Rgb(0, 52, 62), Rgb(20, 123, 134), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Violet": p.Set(Rgb(250, 247, 255), Rgb(101, 40, 124), Rgb(53, 19, 71), Rgb(128, 63, 150), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Ivory": p.Set(Rgb(32, 35, 42), Rgb(255, 245, 222), Rgb(239, 223, 186), Rgb(255, 252, 240), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Peach": p.Set(Rgb(32, 35, 42), Rgb(255, 219, 200), Rgb(236, 192, 172), Rgb(255, 240, 230), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Blush": p.Set(Rgb(32, 35, 42), Rgb(247, 213, 227), Rgb(221, 183, 204), Rgb(255, 240, 247), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Mint": p.Set(Rgb(32, 35, 42), Rgb(209, 241, 220), Rgb(180, 218, 196), Rgb(237, 255, 242), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Sage": p.Set(Rgb(32, 35, 42), Rgb(215, 223, 201), Rgb(187, 201, 170), Rgb(241, 245, 232), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Sky": p.Set(Rgb(32, 35, 42), Rgb(206, 232, 255), Rgb(173, 210, 243), Rgb(237, 247, 255), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Lilac": p.Set(Rgb(32, 35, 42), Rgb(235, 214, 250), Rgb(207, 175, 229), Rgb(250, 239, 255), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Lemon": p.Set(Rgb(32, 35, 42), Rgb(250, 235, 174), Rgb(228, 212, 141), Rgb(255, 249, 217), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Terracotta": p.Set(Rgb(250, 247, 255), Rgb(153, 64, 39), Rgb(116, 53, 33), Rgb(171, 76, 49), Rgb(224, 217, 233), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                case "Silver": p.Set(Rgb(32, 35, 42), Rgb(218, 221, 226), Rgb(185, 190, 199), Rgb(240, 241, 244), Rgb(80, 80, 90), Rgb(89, 190, 255), Rgb(67, 220, 187)); break;
                default: return From(new ThemeSettings { Name = "FishBowl Water", AccentColor = settings == null ? "Ocean" : settings.AccentColor, RestrainedAccents = settings != null && settings.RestrainedAccents });
            }
            Color[] primary = { Rgb(89, 190, 255), Rgb(255, 158, 79), Rgb(177, 122, 255), Rgb(228, 100, 181), Rgb(155, 213, 98), Rgb(236, 178, 62), Rgb(103, 198, 232), Rgb(77, 127, 255), Rgb(129, 114, 237), Rgb(193, 86, 237), Rgb(225, 143, 234), Rgb(240, 92, 203), Rgb(240, 91, 112), Rgb(255, 130, 115), Rgb(255, 171, 50), Rgb(240, 196, 70), Rgb(230, 223, 103), Rgb(67, 200, 134), Rgb(131, 226, 188), Rgb(59, 197, 186), Rgb(64, 216, 228), Rgb(72, 158, 221), Rgb(193, 166, 235), Rgb(241, 177, 154), Rgb(207, 145, 101), Rgb(195, 203, 216), Rgb(245, 239, 228), Rgb(135, 149, 166) };
            Color[] secondary = { Rgb(67, 220, 187), Rgb(240, 93, 91), Rgb(222, 109, 244), Rgb(255, 157, 101), Rgb(246, 210, 92), Rgb(255, 219, 122), Rgb(173, 235, 255), Rgb(122, 181, 255), Rgb(178, 158, 255), Rgb(140, 129, 255), Rgb(189, 161, 247), Rgb(247, 151, 222), Rgb(255, 152, 125), Rgb(255, 193, 160), Rgb(255, 221, 124), Rgb(232, 139, 64), Rgb(178, 220, 104), Rgb(146, 229, 176), Rgb(187, 245, 221), Rgb(118, 225, 218), Rgb(136, 241, 238), Rgb(117, 209, 237), Rgb(224, 201, 244), Rgb(255, 219, 192), Rgb(234, 187, 140), Rgb(237, 242, 249), Rgb(207, 197, 184), Rgb(188, 201, 215) };
            int index = Array.FindIndex(Accents, n => String.Equals(n, settings == null ? "Ocean" : settings.AccentColor, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = 0;
            p.Blue = primary[index]; p.Pink = secondary[index];
            p.Swatches = new[] { p.Blue, p.Pink, Rgb((byte)((p.Blue.R + p.Pink.R) / 2), (byte)((p.Blue.G + p.Pink.G) / 2), (byte)((p.Blue.B + p.Pink.B) / 2)) };
            return p;
        }
        private void Set(Color ink, Color top, Color bottom, Color surface, Color subtle, Color blue, Color pink)
        { Ink = ink; Top = top; Bottom = bottom; Surface = surface; Subtle = subtle; Blue = blue; Pink = pink; }

        public static Color ReadableInk(Color c)
        {
            Func<byte, double> channel = v => v / 255.0 <= 0.04045 ? v / 255.0 / 12.92 : Math.Pow((v / 255.0 + 0.055) / 1.055, 2.4);
            double luminance = channel(c.R) * 0.2126 + channel(c.G) * 0.7152 + channel(c.B) * 0.0722;
            return luminance > 0.179 ? Rgb(0, 0, 0) : Colors.White;
        }
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

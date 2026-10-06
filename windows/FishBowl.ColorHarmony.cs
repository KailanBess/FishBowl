using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.CompilerServices;
namespace EmulatorHub
{
    public sealed class HarmonyColors
    {
        public readonly Color Top, Bottom, Surface, Card, Input, Toolbar, Button, Selection, Ink, Muted, SelectionInk, Border, Focus, Primary, Secondary;
        public HarmonyColors(Color top, Color bottom, Color surface, Color card, Color input, Color toolbar, Color button, Color selection, Color ink, Color muted, Color border, Color focus, Color primary, Color secondary)
        {
            Top = top;
            Bottom = bottom;
            Surface = surface;
            Card = card;
            Input = input;
            Toolbar = toolbar;
            Button = button;
            Selection = selection;
            Ink = ink;
            Muted = muted;
            SelectionInk = FishBowlPalette.EnsureReadable(ink, selection);
            Border = border;
            Focus = focus;
            Primary = primary;
            Secondary = secondary;
        }
    }
    public static class ColorHarmony
    {
        sealed class SurfaceRole
        {
            public string Role;
            public Color LastApplied;
        }
        static readonly ConditionalWeakTable<Control, SurfaceRole> textRoles = new ConditionalWeakTable<Control, SurfaceRole>();
        static readonly ConditionalWeakTable<Control, SurfaceRole> controlRoles = new ConditionalWeakTable<Control, SurfaceRole>();
        static bool requested = true, custom;
        static string themeName;
        static Color liveSecondary;
        static HarmonyColors cached;
        static Color lastInk, lastTop, lastBottom, lastSurface, lastMuted, lastPrimary, lastSecondary;
        static bool lastEnabled;
        static readonly HashSet<int> previousCards = new HashSet<int>(), previousInputs = new HashSet<int>(), previousToolbars = new HashSet<int>(), previousButtons = new HashSet<int>(), previousSelections = new HashSet<int>();
        static readonly HashSet<int> catalogTops = new HashSet<int>(), catalogBottoms = new HashSet<int>(), catalogSurfaces = new HashSet<int>();
        static bool indexed;
        static int harmonyRevision;
        public static int Revision
        {
            get
            {
                return harmonyRevision;
            }
        }
        public static bool Enabled
        {
            get
            {
                return requested && !custom && !SystemInformation.HighContrast && !string.Equals(themeName, "High Contrast", StringComparison.OrdinalIgnoreCase);
            }
        }
        public static void Configure(string mode, bool customPalette, string name)
        {
            Configure(mode, customPalette, name, Color.Empty);
        }
        public static void Configure(string mode, bool customPalette, string name, Color secondary)
        {
            bool nextRequested  =  !string.Equals(mode, "Original", StringComparison.OrdinalIgnoreCase);
            bool changed  =  requested != nextRequested || custom != customPalette || !string.Equals(themeName, name, StringComparison.OrdinalIgnoreCase);
            if (changed) harmonyRevision++;
            if (changed || liveSecondary != secondary) cached  =  null;
            liveSecondary  =  secondary;
            requested  =  nextRequested;
            custom  =  customPalette;
            themeName  =  name;
        }
        public static Color Mix(Color first, Color second, int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            int weight = 100-percent;
            return Color.FromArgb((first.R*weight+second.R*percent)/100, (first.G*weight+second.G*percent)/100, (first.B*weight+second.B*percent)/100);
        }
        public static Color EnsureContrast(Color foreground, Color background, double minimum)
        {
            if (FishBowlPalette.Contrast(foreground, background) >= minimum)return foreground;
            Color end = FishBowlPalette.Contrast(Color.Black, background)>FishBowlPalette.Contrast(Color.White, background)?Color.Black:Color.White;
            for (int weight = 2;weight <= 100;weight += 2)
            {
                Color candidate = Mix(foreground, end, weight);
                if (FishBowlPalette.Contrast(candidate, background) >= minimum)return candidate;
            }
            return end;
        }
        public static HarmonyColors Resolve(ThemeColors colors, Color primary, Color secondary, bool harmonized)
        {
            if (SystemInformation.HighContrast) return new HarmonyColors(SystemColors.Window, SystemColors.Window, SystemColors.Window, SystemColors.Window, SystemColors.Window, SystemColors.Control, SystemColors.Control, SystemColors.Highlight, SystemColors.WindowText, SystemColors.WindowText, SystemColors.WindowText, SystemColors.Highlight, primary, secondary);
            bool active = harmonized && !string.Equals(colors.Name, "High Contrast", StringComparison.OrdinalIgnoreCase) && !SystemInformation.HighContrast;
            Color card = active?Mix(colors.Surface, colors.Top, 16):colors.Surface;
            Color input = active?Mix(colors.Surface, colors.Bottom, 12):Mix(colors.Bottom, colors.Surface, 26);
            Color toolbar = active?Mix(colors.Surface, colors.Top, 32):colors.Surface;
            Color button = active?Mix(card, primary, 8):Mix(colors.Surface, primary, 14);
            Color selection = active?Mix(input, primary, 24):Mix(colors.Surface, primary, 14);
            Color ink = FishBowlPalette.EnsureReadable(colors.Ink, card), muted = FishBowlPalette.EnsureReadable(colors.Subtle, card);
            Color border = EnsureContrast(Mix(input, colors.Ink, 32), input, 3);
            Color focus = EnsureContrast(primary, input, 3);
            return new HarmonyColors(colors.Top, colors.Bottom, colors.Surface, card, input, toolbar, button, selection, ink, muted, border, focus, primary, secondary);
        }
        public static HarmonyColors Preview(string theme, string accent, bool restrained, string mode)
        {
            ThemeColors colors = ThemeCatalog.Get(theme);
            Color primary, secondary;
            AccentCatalog.TryGet(accent, out primary, out secondary);
            if (restrained)
            {
                primary = UiPolishTools.RestrainAccent(primary, colors.Surface);
                secondary = UiPolishTools.RestrainAccent(secondary, colors.Surface);
            }
            return Resolve(colors, primary, secondary, !string.Equals(mode, "Original", StringComparison.OrdinalIgnoreCase));
        }
        public static HarmonyColors Current
        {
            get
            {
                Color ink = FishBowlPalette.ThemeInk, top = FishBowlPalette.ThemeTop, bottom = FishBowlPalette.ThemeBottom, surface = FishBowlPalette.ThemeSurface, muted = FishBowlPalette.ThemeSubtle, primary = FishBowlPalette.IconAccent;
                Color secondary = liveSecondary.IsEmpty?primary:liveSecondary;
                bool active = Enabled;
                if (cached == null || lastInk != ink || lastTop != top || lastBottom != bottom || lastSurface != surface || lastMuted != muted || lastPrimary != primary || lastSecondary != secondary || lastEnabled != active)
                {
                    cached = Resolve(new ThemeColors(themeName, ink, top, bottom, surface, muted), primary, secondary, active);
                    lastInk = ink;
                    lastTop = top;
                    lastBottom = bottom;
                    lastSurface = surface;
                    lastMuted = muted;
                    lastPrimary = primary;
                    lastSecondary = secondary;
                    lastEnabled = active;
                }
                return cached;
            }
        }
        public static Color Input
        {
            get
            {
                return Current.Input;
            }
        }
        public static Color Card
        {
            get
            {
                return Current.Card;
            }
        }
        public static Color Toolbar
        {
            get
            {
                return Current.Toolbar;
            }
        }
        public static Color Button
        {
            get
            {
                return Current.Button;
            }
        }
        public static Color Selection
        {
            get
            {
                return Current.Selection;
            }
        }
        public static Color Border
        {
            get
            {
                return Current.Border;
            }
        }
        public static Color Focus
        {
            get
            {
                return Current.Focus;
            }
        }
        static void IndexPalettes()
        {
            if (indexed)return;
            indexed = true;
            foreach (string theme in ThemeCatalog.Names)
            {
                ThemeColors colors = ThemeCatalog.Get(theme);
                catalogTops.Add(colors.Top.ToArgb());
                catalogBottoms.Add(colors.Bottom.ToArgb());
                catalogSurfaces.Add(colors.Surface.ToArgb());
                foreach (string accent in AccentCatalog.Names)
                {
                    Color primary, secondary;
                    AccentCatalog.TryGet(accent, out primary, out secondary);
                    foreach (bool restrained in new[]
                    {
                        false, true
                    }
                    )
                    {
                        Color p = restrained?UiPolishTools.RestrainAccent(primary, colors.Surface):primary;
                        Color q = restrained?UiPolishTools.RestrainAccent(secondary, colors.Surface):secondary;
                        foreach (bool harmony in new[]
                        {
                            false, true
                        }
                        )
                        {
                            HarmonyColors c = Resolve(colors, p, q, harmony);
                            previousCards.Add(c.Card.ToArgb());
                            previousInputs.Add(c.Input.ToArgb());
                            previousToolbars.Add(c.Toolbar.ToArgb());
                            previousButtons.Add(c.Button.ToArgb());
                            previousSelections.Add(c.Selection.ToArgb());
                        }
                    }
                }
            }
        }
        public static Color NormalizeBackground(Color color)
        {
            if (custom || color.A != 255)return color;
            IndexPalettes();
            int rgb = color.ToArgb();
            HarmonyColors live = Current;
            if (color == live.Card || color == live.Input || color == live.Toolbar || color == live.Button || color == live.Selection || color == live.Top || color == live.Bottom)return color;
            // Only known application surfaces are remapped; artwork and deliberate custom fills remain intact.
            if (previousInputs.Contains(rgb) || Legacy(color, 14, 25, 43) || Legacy(color, 20, 32, 52))return Input;
            if (previousButtons.Contains(rgb) || previousSelections.Contains(rgb) || Legacy(color, 45, 83, 137) || Legacy(color, 82, 58, 111) || Legacy(color, 87, 52, 117) || Legacy(color, 154, 128, 211) || Legacy(color, 183, 150, 245) || Legacy(color, 255, 164, 82) || Legacy(color, 235, 158, 94))return Button;
            if (catalogSurfaces.Contains(rgb) || previousCards.Contains(rgb) || Legacy(color, 27, 43, 69) || Legacy(color, 29, 42, 65) || Legacy(color, 41, 60, 90) || Legacy(color, 47, 48, 56) || Legacy(color, 54, 55, 64) || Legacy(color, 62, 56, 69))return Card;
            if (previousToolbars.Contains(rgb))return Toolbar;
            if (catalogTops.Contains(rgb) || Legacy(color, 31, 32, 37))return FishBowlPalette.ThemeTop;
            if (catalogBottoms.Contains(rgb) || Legacy(color, 35, 36, 42))return FishBowlPalette.ThemeBottom;
            if (Legacy(color, 48, 43, 54))return Mix(FishBowlPalette.ThemeTop, FishBowlPalette.ThemeBottom, 55);
            return color;
        }
        public static Color NormalizeControlBackground(Control control, Color color)
        {
            if (control == null || color.A != 255)return color;
            SurfaceRole retained;
            if (controlRoles.TryGetValue(control, out retained))
            {
                if (control.BackColor == retained.LastApplied || color == retained.LastApplied || Known(color))
                {
                    Color applied = RoleColor(retained.Role);
                    retained.LastApplied = applied;
                    return applied;
                }
                controlRoles.Remove(control);
            }
            string role = null;
            if (control is Form)role = "Bottom";
            else if (control is Button)role = "Button";
            else if (control is TextBoxBase || control is ComboBox || control is NumericUpDown || control is ListBox || control is ListView || control is DataGridView)role = "Input";
            else if (control is ToolStrip)role = "Toolbar";
            else if (color == FishBowlPalette.ThemeTop)role = "Top";
            else if (color == FishBowlPalette.ThemeBottom)role = "Bottom";
            else if (color == FishBowlPalette.ThemeSurface)role = "Card";
            else if (Known(color))
            {
                Color normalized = NormalizeBackground(color);
                HarmonyColors c = Current;
                role = normalized == c.Top?"Top":normalized == c.Bottom?"Bottom":normalized == c.Input?"Input":normalized == c.Button?"Button":normalized == c.Toolbar?"Toolbar":"Card";
            }
            if (role == null)return color;
            Color result = RoleColor(role);
            controlRoles.Add(control, new SurfaceRole
            {
                Role = role, LastApplied = result
            }
            );
            return result;
        }
        static bool Known(Color color)
        {
            IndexPalettes();
            int rgb = color.ToArgb();
            return catalogTops.Contains(rgb) || catalogBottoms.Contains(rgb) || catalogSurfaces.Contains(rgb) || previousCards.Contains(rgb) || previousInputs.Contains(rgb) || previousToolbars.Contains(rgb) || previousButtons.Contains(rgb) || previousSelections.Contains(rgb);
        }
        static Color RoleColor(string role)
        {
            HarmonyColors c = Current;
            return role == "Top"?c.Top:role == "Bottom"?c.Bottom:role == "Input"?c.Input:role == "Button"?c.Button:role == "Toolbar"?c.Toolbar:c.Card;
        }
        public static Color Composite(Color overlay, Color background)
        {
            int inverse  =  255 - overlay.A;
            return Color.FromArgb((overlay.R * overlay.A + background.R * inverse) / 255, (overlay.G * overlay.A + background.G * inverse) / 255, (overlay.B * overlay.A + background.B * inverse) / 255);
        }
        public static Color EmulatorRowBackground(bool selected, bool alternate, string contrast, Color bottom, Color surface, Color accent)
        {
            if (SystemInformation.HighContrast) return selected ? SystemColors.Highlight : SystemColors.Window;
            Color background  =  Enabled ? Input : bottom;
            if (selected)
            {
                int alpha  =  contrast == "Soft" ? 58 : contrast == "Strong" ? 132 : 88;
                int strength  =  contrast == "Soft" ? 14 : contrast == "Strong" ? 34 : 24;
                Color fallback  =  Enabled ? Mix(background, accent, strength) : Color.FromArgb(alpha, accent);
                return Composite(CosmeticRuntime.Optional(CosmeticRuntime.Current.SelectionColor, fallback), background);
            }
            return alternate ? (Enabled ? Mix(background, Card, 10) : Composite(Color.FromArgb(20, surface), background)) : background;
        }
        public static Color ReadableText(Control control, Color color, Color background)
        {
            if (control == null)return Readable(color, background);
            SurfaceRole retained;
            string role = null;
            if (textRoles.TryGetValue(control, out retained))
            {
                if (control.ForeColor == retained.LastApplied || color == retained.LastApplied)role = retained.Role;
                else textRoles.Remove(control);
            }
            if (role == null)
            {
                if (color == FishBowlPalette.ThemeInk || color.IsSystemColor && color != SystemColors.GrayText || ThemeCatalog.Names.Any(n =>ThemeCatalog.Get(n).Ink == color))role = "Ink";
                else if (color == FishBowlPalette.ThemeSubtle || color == SystemColors.GrayText || ThemeCatalog.Names.Any(n =>ThemeCatalog.Get(n).Subtle == color || FishBowlPalette.EnsureReadable(ThemeCatalog.Get(n).Subtle, ThemeCatalog.Get(n).Surface) == color))role = "Muted";
            }
            Color desired = role == "Ink"?FishBowlPalette.ThemeInk:role == "Muted"?FishBowlPalette.ThemeSubtle:color;
            Color result = Readable(desired, background);
            if (role != null)
            {
                textRoles.Remove(control);
                textRoles.Add(control, new SurfaceRole
                {
                    Role = role, LastApplied = result
                }
                );
            }
            return result;
        }
        static bool Legacy(Color color, int r, int g, int b)
        {
            return color.R == r && color.G == g && color.B == b;
        }
        public static Color Readable(Color foreground, Color background)
        {
            return FishBowlPalette.EnsureReadable(foreground, background);
        }
    }
}

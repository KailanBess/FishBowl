using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;
class ColorHarmonyTests
{
    static int checks;
    static void Check(bool condition, string label)
    {
        checks++;
        if (!condition)throw new Exception(label);
    }
    static ColorRoleFixture fixture;
    [STAThread]static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Run();
            Console.WriteLine("PASS: "+checks+" color harmony, all40theme/28accent roles, original/custom preservation, preview and live-control contrast checks.");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 1;
        }
    }
    sealed class ColorRoleFixture:NextDialog
    {
        public readonly TextBox Input = new TextBox
        {
            Text = "Game title"
        };
        public readonly Label Label = new Label
        {
            Text = "Game Library", AutoSize = true
        };
        public readonly Button Button = ExperienceUi.Button("Open game", delegate
        {
        }
        );
        public readonly Panel Card = new Panel
        {
            Size = new Size(180, 60)
        };
        public ColorRoleFixture():base("Harmony fixture", 680, 440)
        {
            var table = NextDialog.Fields(Body);
            NextDialog.Field(table, "Name", Input);
            NextDialog.Field(table, "Action", Button);
            Card.Controls.Add(Label);
            NextDialog.Field(table, "Preview", Card, 80);
        }
    }
    static void Apply(ThemeColors colors, Color primary, Color secondary, string mode, bool custom)
    {
        ColorHarmony.Configure(mode, custom, colors.Name, secondary);
        FishBowlPalette.Configure(colors.Ink, colors.Top, colors.Bottom, colors.Surface, colors.Subtle, primary, secondary);
    }
    static void Run()
    {
        var surfaces = new HashSet<int>();
        foreach (string theme in ThemeCatalog.Names)
        {
            ThemeColors colors = ThemeCatalog.Get(theme);
            var buttons = new HashSet<int>();
            foreach (string accent in AccentCatalog.Names)
            {
                Color primary, secondary;
                AccentCatalog.TryGet(accent, out primary, out secondary);
                Apply(colors, primary, secondary, "Harmonized", false);
                HarmonyColors roles = ColorHarmony.Current;
                Check(roles.Top == colors.Top && roles.Bottom == colors.Bottom && roles.Surface == colors.Surface && roles.Primary == primary && roles.Secondary == secondary, "palette and accent identity preserved "+theme+"/"+accent);
                foreach (Color surface in new[]
                {
                    roles.Card, roles.Input, roles.Toolbar, roles.Button, roles.Selection
                }
                )Check(FishBowlPalette.Contrast(ColorHarmony.Readable(colors.Ink, surface), surface) >= 4.5, "readable text across role "+theme+"/"+accent);
                Check(FishBowlPalette.Contrast(roles.SelectionInk, roles.Selection) >= 4.5, "readable selected row "+theme+"/"+accent);
                Check(FishBowlPalette.Contrast(roles.Border, roles.Input) >= 3 && FishBowlPalette.Contrast(roles.Focus, roles.Input) >= 3, "visible input and focus borders "+theme+"/"+accent);
                HarmonyColors preview = ColorHarmony.Preview(theme, accent, false, "Harmonized");
                Check(preview.Card == roles.Card && preview.Input == roles.Input && preview.Button == roles.Button && preview.Selection == roles.Selection, "preview matches effective palette roles "+theme+"/"+accent);
                foreach (string strength in new[]
                {
                    "Soft", "Standard", "Strong"
                }
                )
                {
                    Color row = ColorHarmony.EmulatorRowBackground(true, false, strength, colors.Bottom, colors.Surface, primary);
                    Check(row.A == 255 && FishBowlPalette.Contrast(ColorHarmony.Readable(Color.White, row), row) >= 4.5, "selected emulator row compositing and text contrast "+theme+"/"+accent+"/"+strength);
                    Check(FishBowlPalette.Contrast(ColorHarmony.Readable(Color.FromArgb(118, 211, 161), row), row) >= 4.5, "selected status text contrast "+theme+"/"+accent+"/"+strength);
                }
                if (theme != "High Contrast")buttons.Add(roles.Button.ToArgb());
            }
            Check(surfaces.Add(ColorHarmony.Card.ToArgb()), "theme surfaces remain individual "+theme);
            if (theme != "High Contrast")Check(buttons.Count >= 24, "accent variety remains visible in surfaces "+theme);
        }
        Color p, q;
        AccentCatalog.TryGet("Rose", out p, out q);
        ThemeColors peach = ThemeCatalog.Get("Peach");
        Apply(peach, p, q, "Original", false);
        Check(!ColorHarmony.Enabled && ColorHarmony.Card == peach.Surface && ColorHarmony.Input == ColorHarmony.Mix(peach.Bottom, peach.Surface, 26), "Original restores prior surface roles");
        Apply(peach, p, q, null, false);
        Check(ColorHarmony.Enabled, "default existing libraries use harmonized roles");
        using (fixture = new ColorRoleFixture())
        {
            fixture.Card.BackColor = peach.Surface;
            FishBowlPalette.StyleWindow(fixture);
            Color originalCard = fixture.Card.BackColor;
            Check(fixture.Input.BackColor == ColorHarmony.Input && fixture.Button.BackColor == ColorHarmony.Button, "live dialog inputs and buttons use semantic roles");
            foreach (string name in ThemeCatalog.Names)
            {
                ThemeColors colors = ThemeCatalog.Get(name);
                AccentCatalog.TryGet("Ocean", out p, out q);
                Apply(colors, p, q, "Harmonized", false);
                FishBowlPalette.StyleWindow(fixture);
                Check(fixture.Card.BackColor == ColorHarmony.Card && fixture.Input.BackColor == ColorHarmony.Input && fixture.Button.BackColor == ColorHarmony.Button, "restyled controls replace previous theme fills "+name);
                foreach (Control control in new Control[]
                {
                    fixture.Input, fixture.Button, fixture.Card
                }
                )Check(FishBowlPalette.Contrast(control.ForeColor, control.BackColor) >= 4.5, "live control contrast "+name);
                Color stable = fixture.Card.BackColor;
                FishBowlPalette.StyleWindow(fixture);
                Check(fixture.Card.BackColor == stable, "restyle idempotent "+name);
                using (Bitmap image = new Bitmap(fixture.ClientSize.Width, fixture.ClientSize.Height))
                {
                    fixture.DrawToBitmap(image, fixture.ClientRectangle);
                    Check(image.GetPixel(fixture.Width/2, fixture.Height/2).A == 255, "actual form rendering "+name);
                }
            }
            Apply(peach, p, q, "Original", false);
            FishBowlPalette.StyleWindow(fixture);
            Check(fixture.Card.BackColor == peach.Surface, "Original switch removes old harmonized card fill");
            ThemeColors custom = new ThemeColors("Custom", Color.FromArgb(240, 255, 220), Color.FromArgb(29, 41, 23), Color.FromArgb(16, 29, 17), Color.FromArgb(68, 89, 63), Color.FromArgb(219, 224, 208));
            Apply(custom, Color.FromArgb(246, 195, 97), Color.Red, "Harmonized", true);
            FishBowlPalette.StyleWindow(fixture);
            Check(!ColorHarmony.Enabled && ColorHarmony.Card == custom.Surface && fixture.Card.BackColor == custom.Surface, "custom palette keeps configured colors after switch");
            Color deliberate = Color.FromArgb(113, 29, 84);
            fixture.Card.BackColor = deliberate;
            FishBowlPalette.StyleWindow(fixture);
            Check(fixture.Card.BackColor == deliberate, "unrecognized intentional custom fills are preserved");
        }
        AccentCatalog.TryGet("Ocean", out p, out q);
        Apply(peach, p, q, "Original", false);
        using (var live = new ColorRoleFixture())
        {
            live.ShowInTaskbar = false;
            live.Opacity = 0;
            live.StartPosition = FormStartPosition.Manual;
            live.Location = new Point(-30000, -30000);
            live.Card.BackColor = peach.Surface;
            live.Show();
            Application.DoEvents();
            FishBowlPalette.StyleWindow(live);
            Color oldInput = live.Input.BackColor;
            Apply(peach, p, q, "Harmonized", false);
            FishBowlPalette.StyleOpenWindows();
            Check(live.Input.BackColor == ColorHarmony.Input && live.Input.BackColor != oldInput, "mode-only toggle restyles actually open dialogs");
            int revision = (int)typeof(FishBowlPalette).GetField("paletteRevision", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Apply(peach, p, q, "Harmonized", false);
            FishBowlPalette.StyleOpenWindows();
            Check((int)typeof(FishBowlPalette).GetField("paletteRevision", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null) == revision, "unchanged harmony Configure does not churn palette revisions");
            live.Close();
        }
        CosmeticRuntime.Current.SelectionColor = "#FFFFCC";
        Color customRow = ColorHarmony.EmulatorRowBackground(true, false, "Strong", peach.Bottom, peach.Surface, p);
        Check(customRow == Color.FromArgb(255, 255, 204) && FishBowlPalette.Contrast(ColorHarmony.Readable(Color.White, customRow), customRow) >= 4.5, "custom bright selection backgrounds remain readable");
        CosmeticRuntime.Current.SelectionColor = null;
        ThemeColors hc = ThemeCatalog.Get("High Contrast");
        Apply(hc, p, q, "Harmonized", false);
        Check(!ColorHarmony.Enabled && ColorHarmony.Card == hc.Surface, "High Contrast theme bypasses harmonic tints");
    }
}

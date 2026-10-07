using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;

class ThemeTests
{
    static int checks;
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static double Channel(byte c) { double v = c / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
    static double Luminance(Color c) { return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B); }
    static double Contrast(Color a, Color b) { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
    static Bitmap Render(Control c) { var b = new Bitmap(c.Width, c.Height); c.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); return b; }
    [STAThread] static int Main()
    {
        try { Application.EnableVisualStyles(); Run(); Console.WriteLine("PASS: " + checks + " theme checks covering all theme/accent combinations, contrast, selectors, persistence, branding and the water footer."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run()
    {
        Directory.CreateDirectory("theme-fixture"); Directory.SetCurrentDirectory("theme-fixture");
        var data = Store.Load(); NextData.Ensure(data); LibraryAdditions.Ensure(data); ExperienceData.Ensure(data);
        if (data.Cosmetics == null) data.Cosmetics = new CosmeticSettings(); data.Cosmetics.CustomPalette = false; Store.Save(data);
        var backgrounds = new HashSet<int>(); var accentColors = new HashSet<int>();
        Check(ThemeCatalog.Names.Length >= 40 && AccentCatalog.Names.Length >= 28, "expanded variety");
        Check(new[] { "FishBowl Water", "Twilight", "Lavender", "Ember", "Light", "High Contrast", "Midnight", "Forest", "Rosewood", "Mist", "Deep Ocean", "Aurora", "Slate", "Plum", "Sand", "Paper", "Nordic", "Copper" }.All(n => ThemeCatalog.Names.Contains(n)), "saved theme names remain supported");
        Check(ThemeCatalog.Names.Count(n => Luminance(ThemeCatalog.Get(n).Surface) > .5) >= 12, "light palette variety");
        Check(ThemeCatalog.Names.Count(n => Luminance(ThemeCatalog.Get(n).Surface) < .2) >= 20, "dark palette variety");
        using (var main = new MainForm(true))
        {
            var live = (LibraryData)typeof(MainForm).GetField("library", Flags).GetValue(main);
            var apply = typeof(MainForm).GetMethod("ApplyThemeColors", Flags);
            foreach (string name in ThemeCatalog.Names)
            {
                live.Theme.Name = name;
                foreach (string accent in AccentCatalog.Names)
                {
                    live.Theme.AccentColor = accent; apply.Invoke(main, null);
                    Color primary, secondary; Check(AccentCatalog.TryGet(accent, out primary, out secondary), "known accent " + accent);
                    Check(FishBowlPalette.IconAccent == primary, "applied accent " + name + " / " + accent);
                    Check(Contrast(FishBowlPalette.ThemeInk, FishBowlPalette.ThemeSurface) >= 4.5 && Contrast(FishBowlPalette.ThemeInk, FishBowlPalette.ThemeTop) >= 4.5 && Contrast(FishBowlPalette.ThemeInk, FishBowlPalette.ThemeBottom) >= 4.5, "readable main text " + name + " / " + accent);
                    Check(Contrast(FishBowlPalette.ThemeSubtle, FishBowlPalette.ThemeSurface) >= 4.5, "readable secondary text " + name + " / " + accent);
                    FishBowlBranding.Configure(accent, Color.Black, Color.White); Color light, deep; FishBowlBranding.GetColors(out light, out deep);
                    Check(light == primary && deep == secondary, "fixed icon uses selected accent " + accent);
                    FishBowlBranding.Configure("Match accent", primary, secondary); FishBowlBranding.GetColors(out light, out deep);
                    Check(light == primary && deep == secondary, "matching icon uses live accent " + accent);
                }
                Check(backgrounds.Add(FishBowlPalette.ThemeSurface.ToArgb()), "individual surface " + name);
                using(var choice=NextDialog.Choice(new[]{"Fixture session"},"Fixture session")) {
                    ConsistentInputs.Style(choice); ConsistentInputs.Style(choice);
                    Check(choice.DrawMode==DrawMode.OwnerDrawFixed && Contrast(choice.ForeColor,choice.BackColor)>=4.5,"themed dropdown and readable text "+name);
                    using(var bitmap=new Bitmap(200,40)) using(var graphics=Graphics.FromImage(bitmap)) {
                        var draw=typeof(ComboBox).GetMethod("OnDrawItem",Flags);
                        foreach(var state in new[]{DrawItemState.None,DrawItemState.Selected,DrawItemState.Focus,DrawItemState.Disabled}) {
                            choice.Enabled=state!=DrawItemState.Disabled;
                            draw.Invoke(choice,new object[]{new DrawItemEventArgs(graphics,choice.Font,new Rectangle(0,0,200,40),0,state)});
                            var expected=state==DrawItemState.Selected?CosmeticRuntime.Selection:choice.BackColor;
                            if(!SystemInformation.HighContrast) Check(bitmap.GetPixel(100,35).ToArgb()==expected.ToArgb(),"dropdown state uses theme surface "+name+" / "+state);
                        }
                    }
                }
            }
            foreach (string accent in AccentCatalog.Names) { Color primary, secondary; AccentCatalog.TryGet(accent, out primary, out secondary); Check(accentColors.Add(primary.ToArgb()), "individual primary accent " + accent); }
            live.Theme.Name = "unrecognized"; live.Theme.AccentColor = "unrecognized"; apply.Invoke(main, null);
            Check(FishBowlPalette.ThemeTop == ThemeCatalog.Get("FishBowl Water").Top, "unknown theme safely falls back");
            using (var footer = new AquariumFooter { Size = new Size(500, 90) })
            {
                CosmeticRuntime.Current.ShowFooter = true;
                live.Theme.Name = "FishBowl Water"; apply.Invoke(main, null);
                using (var ocean = Render(footer))
                {
                    Color lower = ocean.GetPixel(250, 86);
                    Check(lower.R < 10 && lower.G < 20 && lower.B < 45, "footer lower edge remains water without sand or pebbles");
                    live.Theme.Name = "Peach"; apply.Invoke(main, null);
                    using (var peach = Render(footer)) Check(peach.GetPixel(250, 86).ToArgb() != lower.ToArgb(), "footer follows live palette changes");
                }
            }
            using (var settings = new SettingsDialog(live.Theme, live.BackupFolder))
            {
                var combos = NextUi.Descendants(settings).OfType<ComboBox>().ToArray();
                Check(combos.Any(c => c.Items.Cast<object>().Select(x => x.ToString()).SequenceEqual(ThemeCatalog.Names)), "settings expose every theme");
                Check(combos.Any(c => c.Items.Cast<object>().Select(x => x.ToString()).SequenceEqual(AccentCatalog.Names)), "settings expose every accent");
                Check(combos.Any(c => c.Items.Cast<object>().Select(x => x.ToString()).SequenceEqual(AccentCatalog.IconNames)), "settings expose every icon color");
            }
            live.Theme.Name = "Peach"; live.Theme.AccentColor = "Turquoise"; live.Theme.AppIconColor = "Pearl"; Store.Save(live);
            var reload = Store.Load(); Check(reload.Theme.Name == "Peach" && reload.Theme.AccentColor == "Turquoise" && reload.Theme.AppIconColor == "Pearl", "new choices survive save and reload");
        }
    }
}


using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;

class UiPolishTests
{
    static int checks;
    static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    [STAThread] static int Main()
    {
        try { Application.EnableVisualStyles(); Run(); Console.WriteLine("PASS: " + checks + " Home layout, customization, palette previews, artwork proportions and accessibility checks."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Run()
    {
        LibraryData data = Store.Load(); ExperienceData.Ensure(data);
        data.Theme.HomeCardOrder = null; data.Theme.HiddenHomeCards = null;
        Check(UiPolishTools.ResolveHomeCards(data).SequenceEqual(data.Experience.HomeCards), "old libraries retain Home order");
        data.Theme.HomeCardOrder = new List<string> { "Quick actions", "Quick actions", "Unknown", "Attention" };
        data.Theme.HiddenHomeCards = new List<string> { "Attention" };
        var visible = UiPolishTools.ResolveHomeCards(data);
        Check(visible[0] == "Quick actions" && !visible.Contains("Attention") && !visible.Contains("Unknown"), "requested order and hide filter are applied");
        Check(visible.Distinct().Count() == visible.Length && visible.Contains("Save review"), "duplicate order values do not duplicate or lose cards");
        data = Json.Deserialize<LibraryData>(Json.Serialize(data));
        Check(UiPolishTools.ResolveHomeCards(data).SequenceEqual(visible), "Home preferences survive serialization");
        using (var list = new CheckedListBox())
        {
            list.Items.Add("A", true); list.Items.Add("B", false); list.SelectedIndex = 0;
            UiPolishTools.Move(list, -1); Check(list.Items[0].ToString() == "A", "moving past first card is safe");
            UiPolishTools.Move(list, 1); Check(list.Items[1].ToString() == "A" && list.GetItemChecked(1) && !list.GetItemChecked(0), "moving preserves checked state");
            UiPolishTools.Move(list, 1); Check(list.SelectedIndex == 1, "moving past last card is safe");
        }
        UiPolishTools.ApplyAccessibility(data, "Larger text"); Check(data.Enhancements.TextPercent == 150, "larger text preset");
        UiPolishTools.ApplyAccessibility(data, "Reduced motion"); Check(data.Enhancements.ReducedMotion && !data.Theme.EnableMotion, "reduced motion preset");
        UiPolishTools.ApplyAccessibility(data, "Controller"); Check(data.UserTools.Controller && Immersion.Ensure(data).Roomier, "controller preset");
        data.Theme.RestrainedAccents = true;
        UiPolishTools.ApplyAccessibility(data, "High contrast"); Check(data.Theme.Name == "High Contrast" && !data.Theme.RestrainedAccents, "contrast preset retains strong selection accents");
        string themeBefore = data.Theme.Name; UiPolishTools.ApplyAccessibility(data, "Current"); Check(data.Theme.Name == themeBefore, "current preset does not reset existing choices");
        Color accent = Color.FromArgb(240, 120, 60), surface = Color.FromArgb(30, 30, 30);
        Color muted = UiPolishTools.RestrainAccent(accent, surface);
        Check(muted.R < accent.R && muted.G < accent.G && muted.A == 255, "restrained accents blend into surfaces without transparency");
        foreach (string name in ThemeCatalog.Names)
            using (var panel = new ThemePreviewPanel { ThemeName = name, AccentName = "Ocean", Size = new Size(240, 130) })
            using (var bitmap = new Bitmap(240, 130))
            {
                panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                Check(bitmap.GetPixel(14, 34).ToArgb() == ThemeCatalog.Get(name).Surface.ToArgb(), "preview paints actual surface: " + name);
            }
        foreach (string name in AccentCatalog.Names)
            using (var panel = new ThemePreviewPanel { ThemeName = "FishBowl Water", AccentName = name, Size = new Size(240, 130) })
            using (var bitmap = new Bitmap(240, 130))
            {
                Color primary, secondary; AccentCatalog.TryGet(name, out primary, out secondary);
                panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                Check(bitmap.GetPixel(24, 106).ToArgb() == primary.ToArgb(), "preview paints actual accent: " + name);
                panel.Restrained = true; panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                Check(bitmap.GetPixel(24, 106).ToArgb() == UiPolishTools.RestrainAccent(primary, ThemeCatalog.Get("FishBowl Water").Surface).ToArgb(), "preview paints restrained accent: " + name);
            }
        string artPath = Path.Combine(Path.GetTempPath(), "FishBowl-Cover-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (Bitmap source = new Bitmap(100, 20)) { using (Graphics g = Graphics.FromImage(source)) g.Clear(Color.Red); source.Save(artPath); }
            foreach (string aspect in new[] { "Square", "Portrait", "Landscape" })
            {
                Size size = UiPolishTools.CoverSize(aspect);
                using (Bitmap cover = UiPolishTools.Cover(artPath, "Game", size))
                {
                    Check(cover.Size == size && cover.GetPixel(size.Width / 2, size.Height / 2).R > 200, "cover retains image: " + aspect);
                    Check(cover.GetPixel(size.Width / 2, 3).ToArgb() == FishBowlPalette.ThemeSurface.ToArgb(), "wide images retain aspect with letterboxing: " + aspect);
                }
                using (Bitmap missing = UiPolishTools.Cover(artPath + "-missing", "Game", size)) Check(missing.Size == size, "missing cover renders in selected dimensions");
            }
        }
        finally { File.Delete(artPath); }
        data.Theme.Name = "FishBowl Water"; data.Theme.CoverAspect = "Square";
        data.Theme.HomeCardOrder = null; data.Theme.HiddenHomeCards = null;
        data.Games.Clear(); data.Emulators.Clear();
        for (int i = 0; i < 12; i++) data.Games.Add(new GameEntry { Id = "polish" + i, Title = "A long library title " + i, Favorite = true, Pinned = true, LastLaunched = DateTime.Now.ToString("o"), ArtworkPath = "missing" });
        data.Experience.HomeTileCount = 12;
        foreach (int textSize in new[] { 100, 150, 200 })
        {
            NextUi.TextPercent = textSize;
            Exception dialogError = null; int phase = 0;
            using (var timer = new Timer { Interval = 25 })
            {
                timer.Tick += delegate
                {
                    try
                    {
                        if (phase == 0)
                        {
                            var dialog = Application.OpenForms.OfType<NextDialog>().FirstOrDefault(f => f.Text == "Appearance and Home");
                            if (dialog == null) return;
                            Check(NextUi.Descendants(dialog).OfType<ComboBox>().Any(c => c.Items.Count == ThemeCatalog.Names.Length), "theme dialog exposes full catalog at " + textSize);
                            Check(NextUi.Descendants(dialog).OfType<Button>().Where(b => b.Text == "Save" || b.Text == "Close").All(b => b.Height >= b.Font.Height), "dialog actions fit enlarged text");
                            phase = 1;
                            NextUi.Descendants(dialog).OfType<Button>().Single(b => b.Text == "Theme samples").PerformClick();
                            dialog.Close();
                        }
                        else
                        {
                            var samples = Application.OpenForms.OfType<NextDialog>().FirstOrDefault(f => f.Text == "Theme and accent samples");
                            if (samples == null) return;
                            Check(NextUi.Descendants(samples).OfType<ThemePreviewPanel>().Count() == ThemeCatalog.Names.Length + AccentCatalog.Names.Length, "all theme and accent samples are available");
                            timer.Stop(); samples.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        dialogError = ex; timer.Stop();
                        foreach (var dialog in Application.OpenForms.OfType<NextDialog>().ToArray()) dialog.Close();
                    }
                };
                timer.Start(); UiPolishTools.Open(null, data, null);
            }
            if (dialogError != null) throw dialogError;
            using (var host = new Form { ClientSize = new Size(800, 600), ShowInTaskbar = false })
            using (var home = new HomeSurface(data, delegate { }))
            {
                host.Controls.Add(home); host.Show(); Application.DoEvents(); home.RefreshLayout(); Application.DoEvents();
                var cards = (FlowLayoutPanel)typeof(HomeSurface).GetField("cards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home);
                Check(cards.AutoScroll, "Home page owns scrolling at " + textSize);
                foreach (Control card in cards.Controls)
                {
                    var content = (FlowLayoutPanel)card.Tag;
                    Check(!content.AutoScroll, "cards do not own nested scrollbars: " + card.AccessibleName);
                    Check(content.Controls.Cast<Control>().All(c => c.Bottom <= content.ClientSize.Height), "all card content fits: " + card.AccessibleName + " / " + textSize + " card=" + card.Height + " content=" + content.Height + " controls=" + string.Join(";", content.Controls.Cast<Control>().Select(c => c.Text + ":" + c.Bounds)));
                }
                var quick = cards.Controls.Cast<Control>().First(c => c.AccessibleName == "Quick actions");
                var quickContent = (FlowLayoutPanel)quick.Tag;
                Check(quickContent.Controls.OfType<Button>().Count() == 2, "one primary utility action and one More menu");
                var more = quickContent.Controls.OfType<Button>().Single(b => b.Text == "More");
                Check(((ContextMenuStrip)more.Tag).Items.Count == 5, "all secondary actions remain available");
                host.Close();
            }
        }
        NextUi.TextPercent = 100;
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EmulatorHub
{
    public static class IntegrationTools
    {
        static CompanionServer companion;
        static void Run(IWin32Window owner, Action work) { try { work(); } catch (Exception ex) { MessageBox.Show(owner, ex.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Information); } }
        static string KeyFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FishBowl", "Credentials", "retroachievements.dat"); } }
        public static string ReadKey() { try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(KeyFile), null, DataProtectionScope.CurrentUser)); } catch { return ""; } }
        static void SaveKey(string key) { Directory.CreateDirectory(Path.GetDirectoryName(KeyFile)); File.WriteAllBytes(KeyFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser)); }
        public static void Open(IWin32Window owner, LibraryData data, Action refresh)
        {
            IntegrationData.Ensure(data);
            using (var dialog = new NextDialog("Integrations", 820, 540))
            {
                var fields = NextDialog.Fields(dialog.Body);
                NextDialog.Field(fields, "Achievement progress", ExperienceUi.Button("RetroAchievements", delegate { Achievements(dialog, data); }));
                NextDialog.Field(fields, "Game information and covers", ExperienceUi.Button("Metadata providers", delegate { Metadata(dialog, data, refresh); }));
                NextDialog.Field(fields, "Catalogs, imports and emulator adapters", ExperienceUi.Button("Extensions", delegate { Extensions(dialog, data, refresh); }));
                NextDialog.Field(fields, "Browse from a paired phone or browser", ExperienceUi.Button("Companion", delegate { Companion(dialog, data); }));
                dialog.Action("Close", dialog.Close); dialog.ShowDialog(owner);
            }
        }
        public static void Achievements(IWin32Window owner, LibraryData data)
        {
            var settings = IntegrationData.Ensure(data);
            using (var d = new NextDialog("RetroAchievements", 900, 670))
            {
                var table = NextDialog.Fields(d.Body); var user = new TextBox { Text = settings.AchievementUser ?? "" }; var key = new TextBox { UseSystemPasswordChar = true, Text = ReadKey() }; var remember = new CheckBox { Text = "Remember API key for this Windows account", Checked = key.TextLength > 0, AutoSize = true };
                NextDialog.Field(table, "Username", user); NextDialog.Field(table, "Web API key", key); NextDialog.Field(table, "Account access", remember);
                var status = new Label { Text = "Progress is refreshed on request. Achievement unlocking is handled by your supported emulator.", AutoSize = false }; NextDialog.Field(table, "Status", status, 72);
                var list = new ListBox { IntegralHeight = false }; NextDialog.Field(table, "Games and completion progress", list, 280);
                Action show = delegate { list.Items.Clear(); foreach (var item in settings.Achievements) list.Items.Add(item.Title + " — " + item.NumAwarded + "/" + item.MaxPossible + " · Hardcore " + item.NumAwardedHardcore + " · " + item.ConsoleName); if (settings.Achievements.Count > 0) status.Text = "Showing " + settings.Achievements.Count + " games for " + settings.CachedAchievementUser + ". Last refreshed: " + settings.AchievementUpdatedAt; };
                show(); d.Action("Refresh progress", delegate { Run(d, delegate {
                    string account = user.Text.Trim(); string apiKey = key.Text; var response = BackgroundWork<AchievementResponse>.Run(d, "Refresh achievement progress", delegate(CancellationToken token, Action<string> progress) { return IntegrationData.FetchAchievements(account, apiKey, token); });
                    settings.AchievementUser = account; settings.CachedAchievementUser = account; settings.Achievements = response.Results; settings.AchievementUpdatedAt = DateTime.Now.ToString("g");
                    if (remember.Checked) SaveKey(key.Text); Store.Save(data); show(); status.Text += ". Showing " + response.Results.Count + " of " + response.Total + " account games.";
                }); });
                d.Action("Open account", delegate { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://retroachievements.org/controlpanel.php") { UseShellExecute = true }); });
                d.Action("Forget API key", delegate { if (File.Exists(KeyFile)) File.Delete(KeyFile); key.Clear(); remember.Checked = false; });
                d.Action("Close", d.Close); d.ShowDialog(owner);
            }
        }
        static void Extensions(IWin32Window owner, LibraryData data, Action refresh)
        {
            var settings = IntegrationData.Ensure(data);
            using (var d = new NextDialog("Extensions", 850, 640))
            {
                var fields = NextDialog.Fields(d.Body); var list = new ListBox { IntegralHeight = false }; var details = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
                NextDialog.Field(fields, "Registered extensions", list, 210); NextDialog.Field(fields, "Details", details, 160);
                Action reload = delegate { list.Items.Clear(); foreach (var item in settings.Extensions) list.Items.Add(item.Name + " — " + item.Kind); }; reload();
                list.SelectedIndexChanged += delegate { if (list.SelectedIndex >= 0) details.Text = Json.Serialize(settings.Extensions[list.SelectedIndex]); };
                d.Action("Add extension", delegate { Run(d, delegate { using (var file = new OpenFileDialog { Filter = "FishBowl extension|*.json" }) if (file.ShowDialog(d) == DialogResult.OK) {
                    var item = IntegrationData.LoadExtension(file.FileName); if (settings.Extensions.Any(e => string.Equals(e.Id, item.Id, StringComparison.OrdinalIgnoreCase))) throw new IOException("An extension with this identifier is already registered.");
                    if (MessageBox.Show(d, item.Name + "\n" + item.Kind + "\n\n" + (item.CatalogUrl ?? item.CatalogPath ?? item.Platform) + "\n\nRegister this extension?", "Review extension", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    settings.Extensions.Add(item); if (item.Kind == "Emulator") { Hub.Ensure(data).Adapters.Add(new EmulatorAdapter { ExtensionId = item.Id, Name = item.Name, Platform = item.Platform, Arguments = item.Arguments, Extensions = item.Extensions, Website = item.Website }); }
                    Store.Save(data); reload(); refresh();
                } }); });
                d.Action("Remove selected", delegate { if (list.SelectedIndex < 0) return; var item = settings.Extensions[list.SelectedIndex]; if (MessageBox.Show(d, "Remove " + item.Name + " from FishBowl?", "Remove extension", MessageBoxButtons.YesNo) != DialogResult.Yes) return; settings.Extensions.Remove(item); if (item.Kind == "Emulator") Hub.Ensure(data).Adapters.RemoveAll(a => a.ExtensionId == item.Id); Store.Save(data); reload(); refresh(); });
                d.Action("Import catalog entries", delegate { Run(d, delegate { if (list.SelectedIndex < 0) throw new IOException("Select an Importer extension."); var item = settings.Extensions[list.SelectedIndex]; if (item.Kind != "Importer") throw new IOException("Select an Importer extension."); ImportCatalog(d, data, item, refresh); }); });
                d.Action("Create example", delegate { using (var save = new SaveFileDialog { Filter = "FishBowl extension|*.json", FileName = "fishbowl-metadata.json" }) if (save.ShowDialog(d) == DialogResult.OK) {
                    string catalog = Path.Combine(Path.GetDirectoryName(save.FileName), Path.GetFileNameWithoutExtension(save.FileName) + ".catalog.json");
                    if (!File.Exists(catalog)) File.WriteAllText(catalog, Json.Serialize(new List<MetadataRecord> { new MetadataRecord { Title = "Example game", Genre = "Adventure", Description = "Replace this entry with your game information." } }), Encoding.UTF8);
                    File.WriteAllText(save.FileName, Json.Serialize(new ExtensionManifest { Id = "local-games", Name = "Local games", Kind = "Metadata", CatalogPath = Path.GetFileName(catalog) }), Encoding.UTF8);
                } });
                d.Action("Close", d.Close); d.ShowDialog(owner);
            }
        }
        static void ImportCatalog(IWin32Window owner, LibraryData data, ExtensionManifest provider, Action refresh)
        {
            if (string.IsNullOrWhiteSpace(provider.CatalogUrl) && new FileInfo(provider.CatalogPath).Length > 4 * 1024 * 1024) throw new IOException("Catalog exceeds 4 MB.");
            // Importer catalogs share the saved GameEntry schema and review every entry before applying.
            string json = !string.IsNullOrWhiteSpace(provider.CatalogUrl) ? BackgroundWork<string>.Run(owner, "Read import catalog", delegate(CancellationToken token, Action<string> progress) { return IntegrationData.FetchJson(provider.CatalogUrl, token); }) : File.ReadAllText(provider.CatalogPath, Encoding.UTF8);
            if (json.Length > 4 * 1024 * 1024) throw new IOException("Catalog exceeds 4 MB."); var incoming = Json.Deserialize<List<GameEntry>>(json) ?? new List<GameEntry>();
            var candidates = IntegrationData.PrepareImport(data, incoming);
            using (var review = new NextDialog("Review catalog import", 850, 630)) { var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true }; review.Body.Controls.Add(list); foreach (var game in candidates) list.Items.Add(game.Title + " — " + game.Path, false); review.Action("Import checked", delegate { foreach (int index in list.CheckedIndices) { var game = candidates[index]; if (data.Games.Any(g => g != null && !string.IsNullOrWhiteSpace(g.Path) && string.Equals(Path.GetFullPath(g.Path), Path.GetFullPath(game.Path), Platform.PathComparison))) continue; game.Id = Guid.NewGuid().ToString(); game.RequiresEmulatorAssignment = !(game.Extras != null && game.Extras.Native); if (!string.IsNullOrWhiteSpace(game.ArtworkPath) && File.Exists(game.ArtworkPath)) game.ArtworkPath = GameLibraryEditing.RetainArtwork(game.ArtworkPath, Store.DataDirectory); data.Games.Add(game); } Store.Save(data); refresh(); review.Close(); }); review.Action("Cancel", review.Close); review.ShowDialog(owner); }
        }
        static void Metadata(IWin32Window owner, LibraryData data, Action refresh)
        {
            using (var d = new NextDialog("Metadata providers", 920, 700))
            {
                var providers = IntegrationData.Ensure(data).Extensions.Where(e => e.Kind == "Metadata").ToList(); var fields = NextDialog.Fields(d.Body); var provider = NextDialog.Choice(providers.Select(p => p.Name), providers.Count == 0 ? "" : providers[0].Name);
                NextDialog.Field(fields, "Provider", provider); NextDialog.Field(fields, "Catalogs", ExperienceUi.Button("Add or manage providers", delegate { Extensions(d, data, refresh); d.Close(); }));
                var list = new CheckedListBox { CheckOnClick = true, IntegralHeight = false }; NextDialog.Field(fields, "Review matching games", list, 310); var details = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical }; NextDialog.Field(fields, "Selected change", details, 130);
                var matches = new List<Tuple<GameEntry, MetadataRecord>>();
                list.SelectedIndexChanged += delegate { if (list.SelectedIndex >= 0) { var item = matches[list.SelectedIndex]; details.Text = item.Item1.Title + "\r\n\r\n" + item.Item2.Description + "\r\nGenre: " + item.Item2.Genre + "\r\nDeveloper: " + item.Item2.Developer + "\r\nYear: " + item.Item2.ReleaseYear + "\r\nSource: " + item.Item2.Source + "\r\nCover: " + item.Item2.CoverUrl; } };
                d.Action("Find matches", delegate { Run(d, delegate { if (provider.SelectedIndex < 0) throw new IOException("Add a metadata provider first."); var selectedProvider = providers[provider.SelectedIndex]; var catalog = BackgroundWork<List<MetadataRecord>>.Run(d, "Read metadata catalog", delegate(CancellationToken token, Action<string> progress) { return IntegrationData.ReadCatalog(selectedProvider, token); }); matches.Clear(); list.Items.Clear(); foreach (var game in data.Games) { var record = IntegrationData.Match(game, catalog); if (record != null) { matches.Add(Tuple.Create(game, record)); list.Items.Add(game.Title, false); } } }); });
                d.Action("Apply checked metadata", delegate { Run(d, delegate { if (list.CheckedIndices.Count == 0) return; Store.CreateRestorePoint(data, "Before metadata provider changes"); foreach (int index in list.CheckedIndices) IntegrationData.ApplyMetadata(matches[index].Item1, matches[index].Item2); Store.Save(data); refresh(); }); });
                d.Action("Apply selected cover", delegate { Run(d, delegate { if (list.SelectedIndex < 0) return; var match = matches[list.SelectedIndex]; if (MessageBox.Show(d, "Download and retain this cover for " + match.Item1.Title + "?\n\n" + match.Item2.CoverUrl, "Review cover", MessageBoxButtons.YesNo) != DialogResult.Yes) return; match.Item1.ArtworkPath = BackgroundWork<string>.Run(d, "Retain game cover", delegate(CancellationToken token, Action<string> progress) { return DownloadCover(match.Item2.CoverUrl, token); }); Store.Save(data); refresh(); }); });
                d.Action("Existing online lookup", delegate { var game = data.Games.FirstOrDefault(); if (game != null) { using (var choice = new NextDialog("Choose game", 700, 550)) { var games = new ListBox { Dock = DockStyle.Fill }; choice.Body.Controls.Add(games); foreach (var g in data.Games) games.Items.Add(g.Title); choice.Action("Open lookup", delegate { if (games.SelectedIndex >= 0) Hub.Metadata(choice, data, data.Games[games.SelectedIndex]); }); choice.Action("Close", choice.Close); choice.ShowDialog(d); } } });
                d.Action("Close", d.Close); d.ShowDialog(owner);
            }
        }
        static string DownloadCover(string url, CancellationToken token)
        {
            Uri uri; if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Choose an HTTPS cover address.");
            var request = (HttpWebRequest)WebRequest.Create(uri); request.Timeout = 20000; request.ReadWriteTimeout = 20000; request.AllowAutoRedirect = false;
            using (token.Register(request.Abort)) using (var response = request.GetResponse()) using (var input = response.GetResponseStream()) using (var memory = new MemoryStream()) {
                var buffer = new byte[8192]; int read; while ((read = input.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); if (memory.Length + read > 4 * 1024 * 1024) throw new IOException("Cover exceeds 4 MB."); memory.Write(buffer, 0, read); }
                memory.Position = 0; using (var image = Image.FromStream(memory)) { if (image.Width > 4096 || image.Height > 4096) throw new IOException("Cover dimensions exceed 4096 pixels."); string folder = Path.Combine(Store.DataDirectory, "Artwork", "GameCovers"); Directory.CreateDirectory(folder); string target = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".png"); using (var copy = new Bitmap(image)) copy.Save(target, ImageFormat.Png); return target; }
            }
        }
        public static void StopCompanion() { if (companion != null) { companion.Dispose(); companion = null; } }
        public static void Companion(IWin32Window owner, LibraryData data)
        {
            using (var d = new NextDialog("Companion", 860, 620))
            {
                var fields = NextDialog.Fields(d.Body); var addresses = new List<string> { "127.0.0.1" }; try { addresses.AddRange(Dns.GetHostAddresses(Dns.GetHostName()).Where(a => CompanionServer.PrivateAddress(a) && !IPAddress.IsLoopback(a)).Select(a => a.ToString())); } catch { }
                var address = NextDialog.Choice(addresses, "127.0.0.1"); var port = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = Math.Max(1024, Math.Min(65535, IntegrationData.Ensure(data).CompanionPort == 0 ? 8765 : data.Integrations.CompanionPort)) };
                var status = new TextBox { Multiline = true, ReadOnly = true }; NextDialog.Field(fields, "Connection", address); NextDialog.Field(fields, "Port", port); NextDialog.Field(fields, "Paired browser address", status, 150);
                NextDialog.Field(fields, "Library access", new Label { Text = "Start on localhost for this computer, or choose your private network address for a phone on the same network. Only paired browsers can read titles and play history. Game files, save contents, paths and credentials are never served.", AutoSize = false }, 110);
                var timer = new System.Windows.Forms.Timer { Interval = 5000 }; timer.Tick += delegate { if (companion != null && companion.Running) companion.Update(IntegrationData.CompanionSnapshot(data)); };
                d.Action("Start companion", delegate { Run(d, delegate { StopCompanion(); companion = new CompanionServer(); companion.Update(IntegrationData.CompanionSnapshot(data)); companion.Start(IPAddress.Parse(address.Text), (int)port.Value); data.Integrations.CompanionPort = (int)port.Value; Store.Save(data); status.Text = "http://" + address.Text + ":" + companion.Port + "/?pair=" + companion.PairCode + "\r\n\r\nKeep this address private. Closing this window stops the companion."; timer.Start(); }); });
                d.Action("Stop companion", delegate { timer.Stop(); StopCompanion(); status.Text = "Companion stopped."; }); d.Action("Close", d.Close);
                try { d.ShowDialog(owner); } finally { timer.Dispose(); StopCompanion(); }
            }
        }
    }
}

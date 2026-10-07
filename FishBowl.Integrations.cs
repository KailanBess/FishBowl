using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace EmulatorHub
{
    public class IntegrationSettings
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif

        public string AchievementUser { get; set; }
        public string CachedAchievementUser { get; set; }
        public string AchievementUpdatedAt { get; set; }
        public List<AchievementProgress> Achievements { get; set; }
        public List<ExtensionManifest> Extensions { get; set; }
        public int CompanionPort { get; set; }
    }
    public class AchievementProgress
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif

        public int GameID { get; set; }
        public string Title { get; set; }
        public string ConsoleName { get; set; }
        public int MaxPossible { get; set; }
        public int NumAwarded { get; set; }
        public int NumAwardedHardcore { get; set; }
        public string MostRecentAwardedDate { get; set; }
        public string HighestAwardKind { get; set; }
    }
    public class AchievementResponse
    {
        public List<AchievementProgress> Results { get; set; }
        public int Total { get; set; }
    }
    public class MetadataRecord
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif

        public string Title { get; set; }
        public string Description { get; set; }
        public string Genre { get; set; }
        public string Developer { get; set; }
        public string ReleaseYear { get; set; }
        public string Source { get; set; }
        public string CoverUrl { get; set; }
    }
    // Declarative extensions add catalogs and adapters without executing downloaded code.
    public class ExtensionManifest
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif

        public string Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string CatalogUrl { get; set; }
        public string CatalogPath { get; set; }
        public string Platform { get; set; }
        public string Arguments { get; set; }
        public List<string> Extensions { get; set; }
        public string Website { get; set; }
    }
    public static class IntegrationData
    {
        public static IntegrationSettings Ensure(LibraryData data)
        {
            if (data.Integrations == null) data.Integrations = new IntegrationSettings();
            if (data.Integrations.Achievements == null) data.Integrations.Achievements = new List<AchievementProgress>();
            if (data.Integrations.Extensions == null) data.Integrations.Extensions = new List<ExtensionManifest>();
            return data.Integrations;
        }
        public static string FetchJson(string url, CancellationToken token)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Choose an HTTPS catalog address.");
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            var request = (HttpWebRequest)WebRequest.Create(uri); request.Timeout = 20000; request.ReadWriteTimeout = 20000; request.UserAgent = "FishBowl/1.28.1"; request.AllowAutoRedirect = false;
            using (token.Register(request.Abort))
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var memory = new MemoryStream())
            {
                byte[] buffer = new byte[8192]; int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); if (memory.Length + read > 4 * 1024 * 1024) throw new IOException("Catalog exceeds 4 MB."); memory.Write(buffer, 0, read); }
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }
        public static AchievementResponse ParseAchievements(string json)
        {
            var result = Json.Deserialize<AchievementResponse>(json);
            if (result == null || result.Results == null) throw new IOException("Achievement service did not return completion progress.");
            result.Results = result.Results.Where(p => p != null && p.GameID > 0).Take(500).ToList();
            foreach (var p in result.Results) { p.MaxPossible = Math.Max(0, p.MaxPossible); p.NumAwarded = Math.Max(0, Math.Min(p.MaxPossible, p.NumAwarded)); p.NumAwardedHardcore = Math.Max(0, Math.Min(p.MaxPossible, p.NumAwardedHardcore)); }
            return result;
        }
        public static AchievementResponse FetchAchievements(string user, string key, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(key)) throw new IOException("Enter your RetroAchievements username and Web API key.");
            string url = "https://retroachievements.org/API/API_GetUserCompletionProgress.php?u=" + Uri.EscapeDataString(user.Trim()) + "&y=" + Uri.EscapeDataString(key.Trim()) + "&c=500";
            return ParseAchievements(FetchJson(url, token));
        }
        public static ExtensionManifest LoadExtension(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) throw new IOException("Choose an extension JSON file smaller than 1 MB.");
            var item = Json.Deserialize<ExtensionManifest>(File.ReadAllText(path, Encoding.UTF8)); ValidateExtension(item);
            if (!string.IsNullOrWhiteSpace(item.CatalogPath)) item.CatalogPath = Path.GetFullPath(Path.IsPathRooted(item.CatalogPath) ? item.CatalogPath : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), item.CatalogPath));
            return item;
        }
        public static void ValidateExtension(ExtensionManifest item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name) || item.Id.Length > 80 || !item.Id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')) throw new IOException("Extension needs a name and a simple unique identifier.");
            if (item.Kind != "Metadata" && item.Kind != "Emulator" && item.Kind != "Importer") throw new IOException("Extension kind must be Metadata, Emulator or Importer.");
            if (item.Kind == "Metadata" || item.Kind == "Importer")
            {
                if (string.IsNullOrWhiteSpace(item.CatalogPath) && string.IsNullOrWhiteSpace(item.CatalogUrl)) throw new IOException("Extension needs a local JSON catalog or HTTPS catalog address.");
                if (!string.IsNullOrWhiteSpace(item.CatalogUrl)) { Uri uri; if (!Uri.TryCreate(item.CatalogUrl, UriKind.Absolute, out uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Catalog address must use HTTPS."); }
            }
            if (item.Kind == "Emulator" && (item.Extensions == null || item.Extensions.Count == 0 || item.Extensions.Count > 128 || item.Extensions.Any(e => string.IsNullOrWhiteSpace(e) || e.Length > 16 || !System.Text.RegularExpressions.Regex.IsMatch(e, @"^\.?[A-Za-z0-9]{1,15}$")))) throw new IOException("Emulator extension needs a list of supported file extensions.");
        }
        public static List<MetadataRecord> ReadCatalog(ExtensionManifest provider, CancellationToken token)
        {
            ValidateExtension(provider); string json;
            if (!string.IsNullOrWhiteSpace(provider.CatalogUrl)) json = FetchJson(provider.CatalogUrl, token);
            else { if (!File.Exists(provider.CatalogPath) || new FileInfo(provider.CatalogPath).Length > 4 * 1024 * 1024) throw new IOException("Choose an available catalog smaller than 4 MB."); json = File.ReadAllText(provider.CatalogPath, Encoding.UTF8); }
            return ParseCatalog(json);
        }
        public static List<MetadataRecord> ParseCatalog(string json)
        {
            if (json == null || json.Length > 4 * 1024 * 1024) throw new IOException("Catalog exceeds 4 MB.");
            var records = Json.Deserialize<List<MetadataRecord>>(json);
            if (records == null) throw new IOException("Catalog must contain a JSON array of game records.");
            return records.Where(r => r != null && !string.IsNullOrWhiteSpace(r.Title)).Take(10000).ToList();
        }
        static string MatchKey(string title) { return new string((title ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()); }
        public static MetadataRecord Match(GameEntry game, IEnumerable<MetadataRecord> records)
        {
            string key = MatchKey(game == null ? null : game.Title); if (key.Length == 0) return null; var matches = (records ?? new List<MetadataRecord>()).Where(r => r != null && MatchKey(r.Title) == key).Take(2).ToList(); return matches.Count == 1 ? matches[0] : null;
        }
        public static void ApplyMetadata(GameEntry game, MetadataRecord record)
        {
            if (!game.TitleIsCustom && !string.IsNullOrWhiteSpace(record.Title)) game.Title = record.Title;
            if (!string.IsNullOrWhiteSpace(record.Description)) game.Description = record.Description;
            if (!string.IsNullOrWhiteSpace(record.Genre)) game.Genre = record.Genre;
            if (!string.IsNullOrWhiteSpace(record.Developer)) game.Developer = record.Developer;
            if (!string.IsNullOrWhiteSpace(record.ReleaseYear)) game.ReleaseYear = record.ReleaseYear;
            if (game.Extras == null) game.Extras = new GameExtras(); game.Extras.MetadataSource = record.Source;
        }
        public static List<GameEntry> PrepareImport(LibraryData data, IEnumerable<GameEntry> incoming)
        {
            var seen = new HashSet<string>(Platform.PathComparer);
            foreach (var game in data.Games ?? new List<GameEntry>()) { try { if (game != null && !string.IsNullOrWhiteSpace(game.Path)) seen.Add(Path.GetFullPath(game.Path)); } catch { } }
            var result = new List<GameEntry>();
            foreach (var item in (incoming ?? new List<GameEntry>()).Take(10000))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Path) || item.Path.StartsWith("\\\\", StringComparison.Ordinal) || !Path.IsPathRooted(item.Path) || !File.Exists(item.Path)) continue;
                string path = Path.GetFullPath(item.Path); if (seen.Contains(path)) continue;
                bool native = item.Extras != null && item.Extras.Native;
                if (native && (Platform.IsWindows ? !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase) : !Platform.IsLaunchFile(path))) continue;
                seen.Add(path);
                // Catalogs describe games; they cannot import restore journals, assignments, saved operations or profile state.
                result.Add(new GameEntry { Id = Guid.NewGuid().ToString(), Title = item.Title, TitleIsCustom = true, Path = path, Notes = item.Notes, Genre = item.Genre, Developer = item.Developer, Description = item.Description, ReleaseYear = item.ReleaseYear, ConsoleLabel = item.ConsoleLabel, Arguments = item.Arguments, Tags = item.Tags == null ? new List<string>() : item.Tags.Where(t => t != null && t.Length <= 80).Take(32).ToList(), RequiresEmulatorAssignment = !native, Extras = new GameExtras { Native = native }, AddedAt = DateTime.Now.ToString("g") });
            }
            return result;
        }
        public static string CompanionSnapshot(LibraryData data)
        {
            return Json.Serialize(new { version = "1.28.1", updated = DateTime.UtcNow.ToString("o"), emulatorCount = (data.Emulators ?? new List<EmulatorProfile>()).Count, games = (data.Games ?? new List<GameEntry>()).Where(g => g != null).Select(g => new { title = g.Title, platform = g.ConsoleLabel, favorite = g.Favorite, status = g.PlayStatus, launches = g.LaunchCount, seconds = g.TotalPlaySeconds }).ToArray(), sessions = (data.PlaySessions ?? new List<PlaySession>()).Count });
        }
    }
    // Paired read-only browser companion. Exact private-interface binding needs no HTTP URL reservation.
    public sealed class CompanionServer : IDisposable
    {
        TcpListener listener; Thread worker; volatile bool running; volatile string snapshot = "{}"; string pair; int clients;
        public string PairCode { get { return pair; } }
        public int Port { get; private set; }
        public bool Running { get { return running; } }
        public void Update(string json) { snapshot = json ?? "{}"; }
        public static bool PrivateAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return true; var b = address.GetAddressBytes(); return b.Length == 4 && (b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] >= 16 && b[1] <= 31);
        }
        public void Start(IPAddress address, int port)
        {
            if (running) throw new IOException("Companion is already running."); if (!PrivateAddress(address) || address.Equals(IPAddress.Any)) throw new IOException("Choose localhost or this computer's private network address.");
            if (port != 0 && (port < 1024 || port > 65535)) throw new IOException("Choose a port between 1024 and 65535.");
            byte[] bytes = new byte[24]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes); pair = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            listener = new TcpListener(address, port); listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port; running = true; worker = new Thread(Listen) { IsBackground = true }; worker.Start();
        }
        void Listen()
        {
            while (running) { try { var client = listener.AcceptTcpClient(); if (Interlocked.Increment(ref clients) > 8) { Interlocked.Decrement(ref clients); client.Close(); continue; } ThreadPool.QueueUserWorkItem(delegate { try { Serve(client); } catch { } finally { client.Close(); Interlocked.Decrement(ref clients); } }); } catch (SocketException) { if (!running) return; } catch (ObjectDisposedException) { return; } }
        }
        static bool Same(string a, string b) { int different = a.Length ^ b.Length; for (int i = 0; i < Math.Min(a.Length, b.Length); i++) different |= a[i] ^ b[i]; return different == 0; }
        void Serve(TcpClient client)
        {
            client.ReceiveTimeout = 2000; client.SendTimeout = 2000;
            using (var stream = client.GetStream())
            {
                var header = new StringBuilder(); int last = 0, current;
                while (header.Length < 8192 && (current = stream.ReadByte()) >= 0) { header.Append((char)current); if (header.Length >= 4 && header.ToString(header.Length - 4, 4) == "\r\n\r\n") break; last = current; }
                string[] request = header.ToString().Split('\n')[0].Trim().Split(' '); string body, type = "text/plain; charset=utf-8", status = "200 OK";
                if (request.Length < 3 || request[0] != "GET") { status = "405 Method Not Allowed"; body = "Read-only companion."; }
                else
                {
                    string[] target = request[1].Split('?'); string token = target.Length == 2 && target[1].StartsWith("pair=", StringComparison.Ordinal) ? target[1].Substring(5) : "";
                    if (!running || string.IsNullOrEmpty(pair) || !Same(token, pair)) { status = "401 Unauthorized"; body = "Pair this browser using the address shown inside FishBowl."; }
                    else if (target[0] == "/library.json") { body = snapshot; type = "application/json; charset=utf-8"; }
                    else if (target[0] == "/") { type = "text/html; charset=utf-8"; body = Page(pair); }
                    else { status = "404 Not Found"; body = "Page not found."; }
                }
                byte[] data = Encoding.UTF8.GetBytes(body); byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + data.Length.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'\r\n\r\n"); stream.Write(headers, 0, headers.Length); stream.Write(data, 0, data.Length);
            }
        }
        static string Page(string token)
        {
            return "<!doctype html><html><meta name='viewport' content='width=device-width,initial-scale=1'><title>FishBowl companion</title><style>body{font:16px system-ui;background:#051f4e;color:#f1f7ff;margin:24px}input{font:inherit;padding:12px;background:#0c3b7a;color:inherit;border:1px solid #59beff;border-radius:6px;width:90%}.game{padding:16px;background:#0c3b7a;margin:12px 0;border-radius:6px}small{color:#bed3f0}h1{font-size:26px}</style><h1>FishBowl companion</h1><p id='status'>Loading library...</p><input id='search' aria-label='Search games' placeholder='Search games'><div id='games'></div><script>let games=[];function render(){let root=document.getElementById('games');root.replaceChildren();let query=document.getElementById('search').value.toLowerCase();games.filter(g=>(g.title||'').toLowerCase().includes(query)).forEach(g=>{let card=document.createElement('div');card.className='game';let title=document.createElement('strong');title.textContent=(g.favorite?'★ ':'')+(g.title||'Untitled');let info=document.createElement('p');info.textContent=[g.platform,g.status,Math.round(g.seconds/60)+' minutes',g.launches+' launches'].filter(Boolean).join(' · ');card.append(title,info);root.append(card)})}async function refresh(){try{let response=await fetch('/library.json?pair=" + token + "',{cache:'no-store'});if(!response.ok)throw Error();let data=await response.json();games=data.games||[];document.getElementById('status').textContent=games.length+' games · '+data.emulatorCount+' emulators · Updated '+new Date(data.updated).toLocaleTimeString();render()}catch(e){document.getElementById('status').textContent='FishBowl is unavailable. Check the companion inside the app.'}}document.getElementById('search').addEventListener('input',render);refresh();setInterval(refresh,15000)</script></html>";
        }
        public void Dispose() { running = false; if (listener != null) listener.Stop(); if (worker != null && worker != Thread.CurrentThread) worker.Join(2500); pair = ""; }
    }
}

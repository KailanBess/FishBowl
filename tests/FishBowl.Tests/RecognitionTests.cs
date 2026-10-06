using System; using System.IO; using System.IO.Compression; using System.Text; using EmulatorHub;
// Shared game identification (FishBowl.GameRecognition.cs) on Linux, with synthetic game files in a temporary folder.
static class RecognitionTests {
  public static void Run(Action<string, bool> check) {
    var root = Path.Combine(Path.GetTempPath(), "fbrec-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    var dataBefore = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
    Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(root, "data"));
    try {
      // Nintendo DS: header code ABCD, banner at 0x200 with a title and a 4-bit palette icon (index 1 = red).
      var nds = new byte[0x200 + 0x840];
      Encoding.ASCII.GetBytes("ABCD").CopyTo(nds, 12); BitConverter.GetBytes(0x200).CopyTo(nds, 0x68);
      nds[0x200 + 0x20] = 0x01; BitConverter.GetBytes((ushort)0x001F).CopyTo(nds, 0x200 + 0x220 + 2);
      Encoding.Unicode.GetBytes("DS Adventure").CopyTo(nds, 0x200 + 0x340);
      var ndsPath = Path.Combine(root, "adventure.nds"); File.WriteAllBytes(ndsPath, nds);
      using (var r = GameRecognition.Inspect(ndsPath)) {
        check("DS banner title", r.Title == "DS Adventure");
        check("DS title ID and platform", r.TitleId == "ABCD" && r.Platform == "Nintendo DS");
        check("DS palette icon decoded", r.Icon != null && r.Icon.Width == 32 && r.Icon.GetPixel(0, 0).R == 255 && r.Icon.GetPixel(1, 0).A == 0);
        var cached = GameRecognition.CacheIcon(r.Icon, ndsPath);
        check("DS icon cached under FishBowl data", cached != null && cached.StartsWith(Path.Combine(root, "data", "FishBowl", "Artwork", "GameIcons")) && File.Exists(cached));
        var png = File.ReadAllBytes(cached);
        check("cached icon is a 32x32 PNG", png.Length > 33 && png[1] == (byte)'P' && Be(png, 16) == 32 && Be(png, 20) == 32);
        var pixels = Inflate(png);
        check("cached PNG holds the decoded pixels", pixels.Length == 32 * (32 * 4 + 1) && pixels[1] == 255 && pixels[2] == 0 && pixels[4] == 255);
      }
      // Nintendo 3DS SMDH: English title, RGB565 tiled icon (first pixel green).
      var smdh = new byte[0x36c0];
      Encoding.ASCII.GetBytes("SMDH").CopyTo(smdh, 0);
      int entry = 8 + 0x200;
      Encoding.Unicode.GetBytes("Short").CopyTo(smdh, entry);
      Encoding.Unicode.GetBytes("Example 3DS Long Title").CopyTo(smdh, entry + 0x80);
      Encoding.Unicode.GetBytes("Example Studio").CopyTo(smdh, entry + 0x180);
      BitConverter.GetBytes((ushort)0x07E0).CopyTo(smdh, 0x24c0);
      var smdhPath = Path.Combine(root, "icon.smdh"); File.WriteAllBytes(smdhPath, smdh);
      using (var r = GameRecognition.Inspect(smdhPath)) {
        check("3DS SMDH title and developer", r.Title == "Example 3DS Long Title" && r.Developer == "Example Studio" && r.Platform == "Nintendo 3DS");
        check("3DS RGB565 icon decoded", r.Icon != null && r.Icon.Width == 48 && r.Icon.GetPixel(0, 0).G == 255 && r.Icon.GetPixel(0, 0).R == 0);
      }
      // A PNG beside the game file is used as its icon and kept byte for byte.
      var gba = Path.Combine(root, "Space Quest.gba"); File.WriteAllBytes(gba, new byte[0x100]);
      var art = Path.Combine(root, "Space Quest.png");
      using (var r = GameRecognition.Inspect(ndsPath)) File.Copy(GameRecognition.CacheIcon(r.Icon, ndsPath), art);
      using (var r = GameRecognition.Inspect(gba)) {
        check("nearby PNG becomes the icon", r.Icon != null && r.Icon.Width == 32);
        check("file name gives a clean title", r.Title == "Space Quest");
        var cached = GameRecognition.CacheIcon(r.Icon, gba);
        check("nearby PNG cached unchanged", Convert.ToBase64String(File.ReadAllBytes(cached)) == Convert.ToBase64String(File.ReadAllBytes(art)));
      }
    } finally {
      Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataBefore);
      Directory.Delete(root, true);
    }
  }
  static int Be(byte[] b, int i) { return (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]; }
  // Decompresses a single-IDAT PNG's image data (zlib: skip the 2-byte header).
  static byte[] Inflate(byte[] png) {
    int length = Be(png, 33);
    using (var input = new MemoryStream(png, 33 + 8 + 2, length - 2)) using (var z = new DeflateStream(input, CompressionMode.Decompress)) using (var output = new MemoryStream()) { z.CopyTo(output); return output.ToArray(); }
  }
}

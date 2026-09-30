using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace EmulatorHub
{
    // FishBowl's line icons, drawn from the same 32-unit coordinates as FishBowlVisuals.DrawIcon in the Windows build.
    public class FishIcon : Control
    {
        public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<FishIcon, string>(nameof(Kind), "arrow");
        public static readonly StyledProperty<Color> InkProperty = AvaloniaProperty.Register<FishIcon, Color>(nameof(Ink), Color.FromRgb(224, 208, 255));
        public static readonly StyledProperty<Color> AccentProperty = AvaloniaProperty.Register<FishIcon, Color>(nameof(Accent), Color.FromRgb(255, 180, 105));
        static FishIcon() { AffectsRender<FishIcon>(KindProperty, InkProperty, AccentProperty); }
        public string Kind { get { return GetValue(KindProperty); } set { SetValue(KindProperty, value); } }
        public Color Ink { get { return GetValue(InkProperty); } set { SetValue(InkProperty, value); } }
        public Color Accent { get { return GetValue(AccentProperty); } set { SetValue(AccentProperty, value); } }

        public FishIcon() { Width = Height = 20; }
        public FishIcon(string kind, double size = 20) : this() { Kind = kind; Width = Height = size; }
        public FishIcon(string kind, double size, Color ink, Color accent) : this(kind, size) { Ink = ink; Accent = accent; }

        public override void Render(DrawingContext context)
        {
            var scale = Math.Min(Bounds.Width, Bounds.Height) / 32.0;
            if (scale <= 0) return;
            using (context.PushTransform(Matrix.CreateScale(scale, scale)))
                Draw(context, Kind ?? "arrow", Ink, Accent);
        }

        public static void Draw(DrawingContext g, string kind, Color inkColor, Color accentColor)
        {
            var pen = new Pen(new SolidColorBrush(inkColor), 2.3, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var mark = new Pen(new SolidColorBrush(accentColor), 2.3, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var wash = new SolidColorBrush(Palette.Alpha(24, inkColor));
            var ink = new SolidColorBrush(inkColor); var accent = new SolidColorBrush(accentColor);
            Action<double, double, double, double, double> box = (x, y, w, h, r) => g.DrawRectangle(wash, pen, new Rect(x, y, w, h), r, r);
            Action<Pen, double, double, double, double> line = (p, x1, y1, x2, y2) => g.DrawLine(p, new Point(x1, y1), new Point(x2, y2));
            Action<IBrush, double, double, double, double> fillEllipse = (b, x, y, w, h) => g.DrawEllipse(b, null, new Rect(x, y, w, h));
            Action<Pen, double, double, double, double> ellipse = (p, x, y, w, h) => g.DrawEllipse(null, p, new Rect(x, y, w, h));
            switch (kind)
            {
                case "add": box(5, 5, 22, 22, 6); line(mark, 16, 10, 16, 22); line(mark, 10, 16, 22, 16); break;
                case "remove": case "close": line(pen, 9, 9, 23, 23); line(mark, 23, 9, 9, 23); break;
                case "edit": Poly(g, pen, null, false, 7, 21, 7, 26, 12, 25, 25, 12, 20, 7, 7, 21); line(mark, 18, 10, 23, 15); break;
                case "settings": line(pen, 5, 8, 27, 8); line(pen, 5, 16, 27, 16); line(pen, 5, 24, 27, 24); fillEllipse(ink, 9, 5, 6, 6); fillEllipse(ink, 19, 13, 6, 6); fillEllipse(accent, 11, 21, 6, 6); break;
                case "folder": Poly(g, pen, wash, true, 5, 10, 5, 7, 12, 7, 16, 11, 27, 11, 27, 25, 5, 25); line(mark, 9, 17, 23, 17); break;
                case "info": ellipse(pen, 5, 5, 22, 22); fillEllipse(accent, 14.5, 8, 3, 3); line(mark, 16, 14, 16, 23); break;
                case "help": ellipse(pen, 5, 5, 22, 22); Arc(g, mark, 11, 9, 10, 8, 185, 270); line(mark, 16, 17, 16, 19); fillEllipse(accent, 14.5, 22, 3, 3); break;
                case "search": ellipse(pen, 5, 5, 17, 17); line(mark, 21, 21, 27, 27); break;
                case "refresh": Arc(g, pen, 6, 6, 20, 20, 40, 280); Poly(g, mark, null, false, 26, 6, 26, 12, 20, 12); break;
                case "restore": Arc(g, pen, 6, 6, 20, 20, 210, 285); Poly(g, mark, null, false, 5, 6, 5, 12, 11, 12); line(pen, 16, 11, 16, 17); line(mark, 16, 17, 21, 20); break;
                case "backup": Poly(g, pen, null, false, 6, 9, 16, 5, 26, 9, 26, 23, 16, 27, 6, 23, 6, 9); Poly(g, pen, null, false, 6, 9, 16, 14, 26, 9); line(mark, 16, 14, 16, 27); line(mark, 12, 8, 21, 12); break;
                case "import": case "download": box(6, 21, 20, 6, 2); line(mark, 16, 5, 16, 18); Poly(g, mark, null, false, 10, 13, 16, 19, 22, 13); break;
                case "export": box(6, 21, 20, 6, 2); line(mark, 16, 6, 16, 19); Poly(g, mark, null, false, 10, 11, 16, 5, 22, 11); break;
                case "check": ellipse(pen, 5, 5, 22, 22); Poly(g, mark, null, false, 10, 16, 14, 20, 22, 12); break;
                case "star": case "star-filled":
                    var points = new double[20];
                    for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; double r = i % 2 == 0 ? 11.5 : 5.5; points[i * 2] = 16 + Math.Cos(a) * r; points[i * 2 + 1] = 16 + Math.Sin(a) * r; }
                    Poly(g, mark, kind == "star-filled" ? new SolidColorBrush(Palette.Alpha(55, accentColor)) : null, true, points); break;
                case "controller": case "game": g.DrawRectangle(wash, pen, new Rect(4, 9, 24, 15), 6, 6); line(pen, 9, 16.5, 15, 16.5); line(pen, 12, 13.5, 12, 19.5); fillEllipse(accent, 20, 13, 3, 3); fillEllipse(accent, 23, 17, 3, 3); break;
                case "globe": ellipse(pen, 5, 5, 22, 22); ellipse(pen, 11, 5, 10, 22); line(mark, 6, 16, 26, 16); break;
                case "book": Poly(g, pen, null, false, 16, 8, 11, 6, 5, 6, 5, 25, 11, 25, 16, 27, 21, 25, 27, 25, 27, 6, 21, 6, 16, 8, 16, 27); line(mark, 9, 12, 12, 12); line(mark, 20, 12, 23, 12); break;
                case "note": box(7, 4, 18, 24, 3); line(mark, 12, 10, 20, 10); line(pen, 12, 16, 20, 16); line(pen, 12, 22, 17, 22); break;
                case "layers": case "layers-add":
                    Poly(g, pen, null, true, 16, 4, 28, 10, 16, 16, 4, 10); Poly(g, pen, null, false, 5, 16, 16, 22, 27, 16);
                    if (kind == "layers") Poly(g, mark, null, false, 5, 22, 16, 28, 27, 22); else { line(mark, 23, 22, 23, 29); line(mark, 19.5, 25.5, 26.5, 25.5); }
                    break;
                case "chip": box(9, 9, 14, 14, 3); box(13, 13, 6, 6, 1); for (int i = 0; i < 3; i++) { double t = 11 + i * 5; line(mark, t, 5, t, 9); line(mark, t, 23, t, 27); line(pen, 5, t, 9, t); line(pen, 23, t, 27, t); } break;
                case "save": box(6, 5, 20, 22, 3); box(10, 5, 12, 8, 1); line(mark, 11, 20, 21, 20); break;
                case "state": box(5, 6, 22, 20, 4); Arc(g, mark, 11, 11, 10, 10, 210, 290); line(mark, 16, 13, 16, 16); line(mark, 16, 16, 20, 18); break;
                case "desktop": box(4, 5, 24, 17, 3); line(pen, 16, 22, 16, 27); line(mark, 10, 27, 22, 27); break;
                case "storage": box(5, 8, 22, 17, 3); line(pen, 5, 14, 27, 14); fillEllipse(accent, 21, 18, 3, 3); break;
                case "repair": line(mark, 8, 25, 20, 13); ellipse(pen, 5, 22, 5, 5); Arc(g, pen, 17, 5, 10, 10, 30, 300); break;
                case "image": box(5, 5, 22, 22, 3); fillEllipse(accent, 19, 9, 4, 4); Poly(g, pen, null, false, 8, 23, 13, 16, 18, 21, 22, 17, 26, 23); break;
                case "power": Arc(g, pen, 6, 6, 20, 20, 40, 280); line(mark, 16, 4, 16, 16); break;
                case "play": Poly(g, mark, null, true, 11, 6, 26, 16, 11, 26); break;
                case "copy": box(5, 5, 16, 18, 3); box(11, 10, 16, 18, 3); break;
                case "library": box(5, 6, 22, 21, 3); line(pen, 11, 6, 11, 27); line(mark, 16, 12, 22, 12); line(mark, 16, 18, 22, 18); break;
                case "tag": Poly(g, pen, null, true, 5, 6, 16, 6, 27, 17, 17, 27, 5, 15); fillEllipse(accent, 9, 10, 3, 3); break;
                default: line(pen, 6, 16, 25, 16); Poly(g, mark, null, false, 19, 10, 25, 16, 19, 22); break;
            }
        }

        private static void Poly(DrawingContext g, Pen pen, IBrush fill, bool closed, params double[] xy)
        {
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                c.BeginFigure(new Point(xy[0], xy[1]), fill != null);
                for (int i = 2; i + 1 < xy.Length; i += 2) c.LineTo(new Point(xy[i], xy[i + 1]));
                c.EndFigure(closed);
            }
            g.DrawGeometry(fill, pen, geometry);
        }
        // GDI+ DrawArc: angles in degrees, clockwise from the positive x-axis, within the bounding box.
        private static void Arc(DrawingContext g, Pen pen, double x, double y, double w, double h, double start, double sweep)
        {
            double rx = w / 2, ry = h / 2, cx = x + rx, cy = y + ry;
            Func<double, Point> at = deg => new Point(cx + rx * Math.Cos(deg * Math.PI / 180), cy + ry * Math.Sin(deg * Math.PI / 180));
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                c.BeginFigure(at(start), false);
                c.ArcTo(at(start + sweep), new Size(rx, ry), 0, sweep > 180, SweepDirection.Clockwise);
                c.EndFigure(false);
            }
            g.DrawGeometry(null, pen, geometry);
        }

        // Maps button text to an icon, as FishBowlVisuals.IconForText.
        public static string ForText(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            if (t.Contains("favorite") || t.Contains("favour")) return t.Contains("★") || t.Contains("favorited") ? "star-filled" : "star";
            if (t.StartsWith("cancel") || t.StartsWith("close")) return "close";
            if (t.StartsWith("remove") || t.StartsWith("forget")) return "remove";
            if (t.Contains("firmware") || t.Contains("bios")) return "chip";
            if (t.Contains("restore")) return "restore";
            if (t.Contains("preview") || t.StartsWith("find") || t.Contains("search")) return "search";
            if (t.StartsWith("create backup")) return "backup";
            if (t.Contains("backup folder")) return "folder";
            if (t.Contains("backup")) return "backup";
            if (t.Contains("repair")) return "repair";
            if (t.Contains("import") || t.Contains("zip") || t.Contains("appimage")) return "import";
            if (t.StartsWith("register")) return "layers-add";
            if (t.Contains("use selected") || t.Contains("compatibility") || t.Contains("checks")) return "check";
            if (t.StartsWith("add")) return "add";
            if (t.Contains("controller")) return "controller";
            if (t.StartsWith("edit") && !t.Contains("information")) return "edit";
            if (t.Contains("information")) return "info";
            if (t.StartsWith("manage") || t.Contains("settings")) return "settings";
            if (t.StartsWith("refresh") || t == "auto" || t.Contains("check for updates")) return "refresh";
            if (t.Contains("changelog") || t.Contains("notes")) return "note";
            if (t.Contains("guide") || t.Contains("documentation")) return "book";
            if (t.Contains("troubleshoot") || t.Contains("help")) return "help";
            if (t.Contains("download") || t.Contains("releases") || t.Contains("found release")) return "download";
            if (t.Contains("project") || t.Contains("open page")) return "globe";
            if (t.StartsWith("save") || t == "ok" || t == "choose") return "check";
            if (t.Contains("browse") || t.Contains("choose") || t.Contains("folder") || t == "open") return "folder";
            if (t.Contains("open emulator")) return "play";
            if (t.Contains("in-game saves")) return "save";
            if (t.Contains("save states")) return "state";
            return "arrow";
        }
    }
}

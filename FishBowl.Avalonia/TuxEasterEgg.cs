using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace EmulatorHub
{
    // Linux easter egg: click the FishBowl logo seven times, or type the Konami code, and Tux dives into the bowl,
    // chases a fish across the banner, then waves goodbye.
    public static class TuxEasterEgg
    {
        private static readonly Key[] Konami = { Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right, Key.B, Key.A };
        private static int konamiIndex, clicks;
        private static DateTime lastClick;

        // Seven clicks, each within a second of the last.
        public static bool LogoClicked()
        {
            var now = DateTime.UtcNow;
            clicks = now - lastClick < TimeSpan.FromSeconds(1) ? clicks + 1 : 1;
            lastClick = now;
            if (clicks < 7) return false;
            clicks = 0; return true;
        }

        public static bool KeyPressed(Key key)
        {
            konamiIndex = key == Konami[konamiIndex] ? konamiIndex + 1 : key == Konami[0] ? 1 : 0;
            if (konamiIndex < Konami.Length) return false;
            konamiIndex = 0; return true;
        }
    }

    // Transparent overlay across the banner that plays the swim. It never takes input.
    public class TuxSwim : Control
    {
        private class Bubble { public double X, Y, Size, Speed, Age; }
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private readonly Random random = new Random();
        private DispatcherTimer timer;
        private DateTime started;
        private bool motion = true;
        private const double Duration = 7.0;

        public TuxSwim() { IsHitTestVisible = false; ClipToBounds = true; }
        public bool Playing { get { return timer != null; } }

        // Without motion (reduced-motion setting) Tux just appears and waves, without swimming.
        public void Play(bool enableMotion)
        {
            if (Playing) return;
            motion = enableMotion; started = DateTime.UtcNow; bubbles.Clear();
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += delegate { Step(); };
            timer.Start();
        }

        // Exposed for tests: render the swim at a given moment.
        public void ShowAt(double seconds) { started = DateTime.UtcNow - TimeSpan.FromSeconds(seconds); InvalidateVisual(); }

        private double Elapsed { get { return (DateTime.UtcNow - started).TotalSeconds; } }

        private void Step()
        {
            double t = Elapsed;
            if (t > Duration) { timer.Stop(); timer = null; bubbles.Clear(); InvalidateVisual(); return; }
            if (motion && random.NextDouble() < 0.18)
                bubbles.Add(new Bubble { X = TuxX(t) + 18, Y = TuxY(t) - 10, Size = 3 + random.NextDouble() * 5, Speed = 25 + random.NextDouble() * 30 });
            foreach (var b in bubbles) { b.Age += 0.016; b.Y -= b.Speed * 0.016; b.X += Math.Sin(b.Age * 6 + b.Size) * 0.4; }
            bubbles.RemoveAll(b => b.Y < -10 || b.Age > 2.5);
            InvalidateVisual();
        }

        // Tux enters from the left at 0.6 s, swims until 5.2 s, then waves at the right until the end.
        private double SwimProgress(double t) { return motion ? Math.Max(0, Math.Min(1, (t - 0.6) / 4.6)) : 1; }
        private double TuxX(double t) { double p = SwimProgress(t); p = p * p * (3 - 2 * p); return -60 + p * (Bounds.Width - 90); }
        private double TuxY(double t) { return Bounds.Height / 2 + (SwimProgress(t) < 1 ? Math.Sin(t * 5) * 7 : 0); }

        public override void Render(DrawingContext g)
        {
            if (!Playing && Elapsed > Duration) return;
            double t = Elapsed;
            double fade = t > Duration - 0.8 ? Math.Max(0, (Duration - t) / 0.8) : t < 0.3 ? t / 0.3 : 1;
            using (g.PushOpacity(fade))
            {
                foreach (var b in bubbles)
                    g.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Max(0, 170 - b.Age * 60), 210, 235, 255)), 1.2), new Point(b.X, b.Y), b.Size, b.Size);
                double progress = SwimProgress(t);
                // The fish stays a little ahead, then escapes off the right edge while Tux waves.
                double fishX = progress < 1 ? TuxX(t) + 70 + Math.Sin(t * 3) * 6 : TuxX(t) + 70 + (t - 5.2) * 160;
                DrawFish(g, fishX, TuxY(t) - 4 + Math.Sin(t * 7) * 4);
                double wave = progress >= 1 ? Math.Sin((t - 5.2) * 9) * 35 : Math.Sin(t * 10) * 18;
                DrawTux(g, TuxX(t), TuxY(t), wave, progress >= 1);
            }
        }

        private static void DrawFish(DrawingContext g, double x, double y)
        {
            var orange = new SolidColorBrush(Color.FromRgb(255, 152, 56));
            var tail = new StreamGeometry();
            using (var c = tail.Open()) { c.BeginFigure(new Point(x - 9, y), true); c.LineTo(new Point(x - 19, y - 8)); c.LineTo(new Point(x - 19, y + 8)); c.EndFigure(true); }
            g.DrawGeometry(orange, null, tail);
            g.DrawEllipse(orange, new Pen(new SolidColorBrush(Color.FromRgb(214, 104, 30)), 1), new Point(x, y), 12, 7.5);
            g.DrawEllipse(Brushes.White, null, new Point(x + 6, y - 2), 2.4, 2.4);
            g.DrawEllipse(Brushes.Black, null, new Point(x + 6.6, y - 2), 1.2, 1.2);
        }

        // Tux, about 44 px tall, centred on (x, y). wave rotates the right flipper; waving faces the viewer.
        private static void DrawTux(DrawingContext g, double x, double y, double wave, bool waving)
        {
            var black = new SolidColorBrush(Color.FromRgb(24, 24, 28)); var white = new SolidColorBrush(Color.FromRgb(246, 246, 240));
            var yellow = new SolidColorBrush(Color.FromRgb(246, 190, 40)); var yellowDark = new SolidColorBrush(Color.FromRgb(214, 150, 20));
            using (g.PushTransform(Matrix.CreateTranslation(x, y)))
            {
                // Feet
                g.DrawEllipse(yellow, new Pen(yellowDark, 1), new Point(-8, 20), 8, 3.5);
                g.DrawEllipse(yellow, new Pen(yellowDark, 1), new Point(8, 20), 8, 3.5);
                // Left flipper, body, belly
                using (g.PushTransform(Matrix.CreateRotation(Math.PI / 180 * 20) * Matrix.CreateTranslation(-15, 4))) g.DrawEllipse(black, null, new Point(0, 0), 5, 12);
                g.DrawEllipse(black, null, new Point(0, 0), 16, 21);
                g.DrawEllipse(white, null, new Point(0, 6), 10.5, 14);
                // Right flipper (waves)
                using (g.PushTransform(Matrix.CreateTranslation(0, -9) * Matrix.CreateRotation(Math.PI / 180 * (-20 - wave)) * Matrix.CreateTranslation(15, 13))) g.DrawEllipse(black, null, new Point(0, 9), 5, 12);
                // Face
                g.DrawEllipse(white, null, new Point(-5, -11), 4, 5);
                g.DrawEllipse(white, null, new Point(5, -11), 4, 5);
                g.DrawEllipse(black, null, new Point(waving ? -4.5 : -3.8, -10.5), 1.9, 2.4);
                g.DrawEllipse(black, null, new Point(waving ? 4.5 : 6.2, -10.5), 1.9, 2.4);
                var beak = new StreamGeometry();
                using (var c = beak.Open()) { c.BeginFigure(new Point(-6, -5), true); c.QuadraticBezierTo(new Point(0, -8), new Point(6, -5)); c.QuadraticBezierTo(new Point(waving ? 0 : 3, 1), new Point(-6, -5)); c.EndFigure(true); }
                g.DrawGeometry(yellow, new Pen(yellowDark, 1), beak);
            }
        }
    }
}

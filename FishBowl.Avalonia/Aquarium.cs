using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EmulatorHub
{
    // Windows BubbleRail: a thin strip of theme bubbles drifting up beside the workspace. Decorative only.
    public sealed class BubbleRail : Control
    {
        private const int Count = 8;
        private readonly double[] positions = new double[Count], speeds = new double[Count], phases = new double[Count];
        private readonly double[] sizes = new double[Count];
        private readonly Random random;
        private readonly bool right;
        private readonly Palette p;

        public BubbleRail(bool rightSide, Palette palette)
        {
            right = rightSide; p = palette; random = new Random(rightSide ? 9173 : 4289);
            Width = 24; Focusable = false; IsHitTestVisible = false;
            AutomationProperties.SetName(this, "Decorative theme bubbles");
            for (int i = 0; i < Count; i++) { positions[i] = (i + random.NextDouble() * 0.85) / Count; ResetBubble(i); }
        }

        private void ResetBubble(int i)
        {
            sizes[i] = 6 + random.NextDouble() * 9; speeds[i] = 13 + random.NextDouble() * 22; phases[i] = random.NextDouble() * Math.PI * 2;
        }

        public void Advance(double seconds)
        {
            double height = Bounds.Height; if (height < 1) return;
            for (int i = 0; i < Count; i++)
            {
                positions[i] -= Math.Max(0, Math.Min(0.1, seconds)) * speeds[i] / height;
                if (positions[i] < -0.03) { positions[i] = 1.03 + random.NextDouble() * 0.08; ResetBubble(i); }
            }
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(new SolidColorBrush(p.Bottom), new Rect(Bounds.Size));
            var rim = new Pen(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Palette.Alpha(210, p.Blue), 0), new GradientStop(Palette.Alpha(170, p.Pink), 1) } }, 1.2);
            var fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Palette.Alpha(65, p.Blue), 0), new GradientStop(Palette.Alpha(32, p.Pink), 1) } };
            var shine = new Pen(new SolidColorBrush(Color.FromArgb(175, 255, 255, 255)), 1);
            for (int i = 0; i < Count; i++)
            {
                double size = Math.Min(sizes[i], Bounds.Width - 4);
                double x = (Bounds.Width - size) / 2 + Math.Sin(positions[i] * 9 + phases[i]) * (right ? -2 : 2);
                double y = positions[i] * Bounds.Height;
                var rect = new Rect(x, y, size, size);
                context.DrawEllipse(fill, rim, rect.Center, size / 2, size / 2);
                // A small highlight arc in the upper left, like the Windows rail.
                var arc = new StreamGeometry();
                using (var g = arc.Open())
                {
                    double r = size * 0.25, cx = x + size * 0.45, cy = y + size * 0.43;
                    g.BeginFigure(new Point(cx + r * Math.Cos(Math.PI * 200 / 180), cy + r * Math.Sin(Math.PI * 200 / 180)), false);
                    g.ArcTo(new Point(cx + r * Math.Cos(Math.PI * 265 / 180), cy + r * Math.Sin(Math.PI * 265 / 180)), new Size(r, r), 0, false, SweepDirection.Clockwise);
                }
                context.DrawGeometry(null, shine, arc);
            }
        }
    }

    // Windows AmbientWindow: a full-screen artwork slideshow after idle time; any key, click or real mouse movement closes it.
    public sealed class AmbientWindow : Window
    {
        private readonly List<GameEntry> games;
        private readonly Image image = new Image { Stretch = Stretch.Uniform };
        private readonly TextBlock caption = new TextBlock { FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 12, 24, 28) };
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        private Bitmap current;
        private Point? origin;
        private int index;

        public AmbientWindow(LibraryData data, Palette p)
        {
            Title = "Ambient artwork — move, click or press a key to exit";
            WindowState = WindowState.FullScreen; SystemDecorations = SystemDecorations.None; Background = new SolidColorBrush(p.Bottom); Foreground = p.InkBrush;
            games = Immersion.AmbientGames(data);
            AutomationProperties.SetName(image, "Ambient game artwork");
            var layout = new DockPanel(); DockPanel.SetDock(caption, Dock.Bottom); layout.Children.Add(caption); layout.Children.Add(image);
            Content = layout;
            timer.Tick += delegate { Advance(); };
            Opened += delegate { Advance(); timer.Start(); };
            Closed += delegate { timer.Stop(); if (current != null) current.Dispose(); };
            KeyDown += (sender, e) => { e.Handled = true; Close(); };
            PointerPressed += (sender, e) => { e.Handled = true; Close(); };
            // Ignore the first position and tiny jitter so a resting mouse doesn't close it straight away.
            PointerMoved += (sender, e) =>
            {
                var at = e.GetPosition(this);
                if (origin == null) { origin = at; return; }
                if (Math.Abs(at.X - origin.Value.X) + Math.Abs(at.Y - origin.Value.Y) > 5) Close();
            };
        }

        private void Advance()
        {
            if (games.Count == 0) { caption.Text = "No game artwork linked. Move, click or press a key to exit."; return; }
            var game = games[index++ % games.Count];
            Bitmap next = null;
            try { using (var stream = File.OpenRead(game.ArtworkPath)) next = Bitmap.DecodeToWidth(stream, 1600); }
            catch (Exception error) { Store.Log("Ambient artwork skipped for " + game.Title + ": " + error.Message); }
            image.Source = next; if (current != null) current.Dispose(); current = next;
            caption.Text = game.Title + (String.IsNullOrWhiteSpace(game.ConsoleLabel) ? "" : "  •  " + game.ConsoleLabel);
        }
    }

    public partial class MainWindow
    {
        private BubbleRail leftRail, rightRail;
        private DispatcherTimer bubbleClock, idleClock;
        private DateTime lastInputUtc = DateTime.UtcNow, lastBubbleTick = DateTime.UtcNow;
        private bool ambientOpen;

        // Puts bubble rails beside the workspace and starts the idle check for ambient artwork.
        private Control WithAquarium(Control workspace)
        {
            leftRail = new BubbleRail(false, p); rightRail = new BubbleRail(true, p);
            var frame = new DockPanel();
            DockPanel.SetDock(leftRail, Dock.Left); DockPanel.SetDock(rightRail, Dock.Right);
            frame.Children.Add(leftRail); frame.Children.Add(rightRail); frame.Children.Add(workspace);
            bubbleClock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            bubbleClock.Tick += delegate
            {
                var now = DateTime.UtcNow; double seconds = (now - lastBubbleTick).TotalSeconds; lastBubbleTick = now;
                if (!BubblesAnimate()) { UpdateAquarium(); return; }
                leftRail.Advance(seconds); rightRail.Advance(seconds);
            };
            idleClock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            idleClock.Tick += async delegate
            {
                if (ambientOpen || !IsActive || sessionTrackers.Values.Any(t => !t.Finished) || !Immersion.AmbientDue(library, lastInputUtc, DateTime.UtcNow)) return;
                ambientOpen = true;
                try
                {
                    var closed = new TaskCompletionSource<bool>(); var ambient = new AmbientWindow(library, p);
                    ambient.Closed += delegate { closed.TrySetResult(true); }; ambient.Show(this); await closed.Task;
                }
                catch (Exception error) { Store.Log("Ambient artwork failed: " + error.Message); }
                finally { ambientOpen = false; lastInputUtc = DateTime.UtcNow; }
            };
            EventHandler<RoutedEventArgs> input = (sender, e) => lastInputUtc = DateTime.UtcNow;
            foreach (var routed in new RoutedEvent[] { PointerMovedEvent, PointerPressedEvent, PointerWheelChangedEvent, KeyDownEvent })
                AddHandler(routed, input, RoutingStrategies.Tunnel, true);
            Activated += delegate { UpdateAquarium(); }; Deactivated += delegate { UpdateAquarium(); };
            PropertyChanged += (sender, e) => { if (e.Property == WindowStateProperty) UpdateAquarium(); };
            Opened += delegate { idleClock.Start(); UpdateAquarium(); };
            Closed += delegate { idleClock.Stop(); bubbleClock.Stop(); };
            return frame;
        }

        private bool BubblesShown() { return Immersion.Ensure(library).Bubbles; }

        // Animate only while it can be seen and nothing else wants the CPU: the window is active, a game isn't running,
        // and motion is allowed.
        private bool BubblesAnimate()
        {
            return BubblesShown() && library.Theme.EnableMotion && !(library.Enhancements != null && library.Enhancements.ReducedMotion)
                && IsActive && WindowState != WindowState.Minimized && !sessionTrackers.Values.Any(t => !t.Finished);
        }

        private void UpdateAquarium()
        {
            if (leftRail == null) return;
            bool shown = BubblesShown(); leftRail.IsVisible = shown; rightRail.IsVisible = shown;
            bool animate = BubblesAnimate();
            if (animate != bubbleClock.IsEnabled) { lastBubbleTick = DateTime.UtcNow; bubbleClock.IsEnabled = animate; }
        }
    }
}

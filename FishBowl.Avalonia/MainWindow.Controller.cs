using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private readonly LinuxJoystick controllerDevice = new LinuxJoystick();
        private readonly DispatcherTimer controllerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        private void ConfigureControllerNavigation()
        {
            controllerTimer.Tick += delegate
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var active = desktop?.Windows.LastOrDefault(w => w.IsActive && (w == this || w is FishDialog));
                var actions = controllerDevice.Poll(library.UserTools?.Controller == true, active != null, Environment.TickCount64);
                if (active != null) foreach (var action in actions) RouteController(active, action);
            };
            controllerTimer.Start();
        }
        private void RouteController(Window window, ControllerAction action)
        {
            if (!window.IsActive) return;
            var focused = window.FocusManager?.GetFocusedElement() as Control;
            var ancestry = focused == null ? Array.Empty<Control>() : new[] { focused }.Concat(focused.GetVisualAncestors().OfType<Control>()).ToArray();
            var combo = ancestry.OfType<ComboBox>().FirstOrDefault();
            var menu = ancestry.OfType<MenuItem>().FirstOrDefault();
            if (action == ControllerAction.Back)
            {
                if (combo?.IsDropDownOpen == true) { combo.IsDropDownOpen = false; return; }
                var openMenus = window.GetVisualDescendants().OfType<MenuItem>().Where(m => m.IsSubMenuOpen).ToList();
                if (openMenus.Count > 0) { foreach (var item in openMenus) item.IsSubMenuOpen = false; return; }
                if (window is FishDialog) { window.Close(); return; }
                RaiseControllerKey(focused ?? window, Key.Escape); return;
            }
            if (action == ControllerAction.PreviousTab || action == ControllerAction.NextTab)
            {
                var tabs = ancestry.OfType<TabControl>().FirstOrDefault() ?? (window == this ? libraryPages : window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault());
                if (tabs != null && tabs.Items.Count > 0) tabs.SelectedIndex = (Math.Max(0, tabs.SelectedIndex) + (action == ControllerAction.NextTab ? 1 : tabs.Items.Count - 1)) % tabs.Items.Count;
                return;
            }
            if (focused == null) { FocusControllerFirst(window); return; }
            if (action == ControllerAction.Activate)
            {
                if (combo != null) { combo.IsDropDownOpen = !combo.IsDropDownOpen; return; }
                if (menu != null) { if (menu.Items.Count > 0) menu.IsSubMenuOpen = !menu.IsSubMenuOpen; else menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); return; }
                var toggle = ancestry.OfType<ToggleButton>().FirstOrDefault(); if (toggle != null && toggle.IsEffectivelyEnabled) { toggle.IsChecked = toggle.IsChecked != true; return; }
                var button = ancestry.OfType<Button>().FirstOrDefault(); if (button != null && button.IsEffectivelyEnabled) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return; }
                RaiseControllerKey(focused, Key.Enter); return;
            }
            var key = action == ControllerAction.Up ? Key.Up : action == ControllerAction.Down ? Key.Down : action == ControllerAction.Left ? Key.Left : Key.Right;
            if (menu != null) { RaiseControllerKey(menu, key); return; }
            if (combo?.IsDropDownOpen == true && (key == Key.Up || key == Key.Down)) { combo.SelectedIndex = Math.Max(0, Math.Min(combo.Items.Count - 1, combo.SelectedIndex + (key == Key.Down ? 1 : -1))); return; }
            var list = ancestry.OfType<ListBox>().FirstOrDefault();
            if (list != null && (key == Key.Up || key == Key.Down)) { list.SelectedIndex = Math.Max(0, Math.Min(list.Items.Count - 1, list.SelectedIndex + (key == Key.Down ? 1 : -1))); if (list.SelectedIndex >= 0) list.ScrollIntoView(list.SelectedIndex); return; }
            FocusControllerDirection(window, focused, key);
        }
        private static void RaiseControllerKey(Control target, Key key)
        {
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = KeyModifiers.None });
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key, KeyModifiers = KeyModifiers.None });
        }
        private static Control[] ControllerTargets(Window window)
        {
            return window.GetVisualDescendants().OfType<Control>().Where(c => c.Focusable && c.IsEffectivelyEnabled && c.IsEffectivelyVisible && c.Bounds.Width > 0 && c.Bounds.Height > 0 && (c is Button || c is TextBox || c is ComboBox || c is ListBox || c is TabItem || c is MenuItem)).ToArray();
        }
        private static void FocusControllerFirst(Window window)
        {
            var first = ControllerTargets(window).FirstOrDefault(c => !(c is MenuItem)) ?? ControllerTargets(window).FirstOrDefault(); first?.Focus(NavigationMethod.Tab);
        }
        private static void FocusControllerDirection(Window window, Control current, Key key)
        {
            var origin = current.TranslatePoint(new Point(current.Bounds.Width / 2, current.Bounds.Height / 2), window);
            if (!origin.HasValue) { FocusControllerFirst(window); return; }
            var next = ControllerTargets(window).Where(c => c != current && !current.GetVisualAncestors().Contains(c)).Select(c => new { Control = c, Point = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window) }).Where(x => x.Point.HasValue)
                .Select(x => new { x.Control, X = x.Point.Value.X - origin.Value.X, Y = x.Point.Value.Y - origin.Value.Y })
                .Where(x => key == Key.Up ? x.Y < -2 : key == Key.Down ? x.Y > 2 : key == Key.Left ? x.X < -2 : x.X > 2)
                .OrderBy(x => key == Key.Up || key == Key.Down ? Math.Abs(x.Y) + Math.Abs(x.X) * 2 : Math.Abs(x.X) + Math.Abs(x.Y) * 2).FirstOrDefault();
            next?.Control.Focus(NavigationMethod.Tab);
        }

        private async Task ShowAppearanceHub()
        {
            var dialog = new FishDialog("Appearance", 640); var body = new StackPanel { Spacing = 12 };
            body.Children.Add(Ui.Text("Appearance", 22, true));
            body.Children.Add(Ui.Hint("Choose themes, text, Home cards, and controller navigation using the current FishBowl settings."));
            body.Children.Add(Ui.Actions(Ui.Action("Themes and text", () => ShowSettings(dialog), true), Ui.Action("Home cards", () => CustomizeLibraryHome(dialog))));
            var enabled = Ui.Check("Enable controller navigation", library.UserTools?.Controller == true);
            enabled.IsCheckedChanged += delegate { LibraryProfiles.Ensure(library).Controller = enabled.IsChecked == true; Store.Save(library); };
            var state = Ui.Hint(controllerDevice.Status); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; timer.Tick += delegate { state.Text = controllerDevice.Status; };
            body.Children.Add(enabled); body.Children.Add(state);
            body.Children.Add(Ui.Hint("Use the left stick or directional pad to move, A to select, B to go back, and shoulders to switch tabs. Navigation pauses while another app is active. Linux requires a readable /dev/input/js device; no packages or permissions are changed."));
            body.Children.Add(Ui.Action("Close", dialog.Close)); dialog.Body = body; timer.Start(); try { await dialog.Present(this); } finally { timer.Stop(); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace EmulatorHub
{
    // Building blocks shared by FishBowl's windows: themed buttons, labels, message boxes and file pickers.
    public static class Ui
    {
        public static Palette P { get { return Palette.Current; } }
        public static FontFamily Font = FontFamily.Default;

        public static Button Action(string text, Func<Task> click, bool primary = false)
        {
            var button = new Button { Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 8, 0), MinHeight = 34, CornerRadius = new CornerRadius(6), VerticalAlignment = VerticalAlignment.Center };
            SetAction(button, text, primary);
            if (click != null) button.Click += async delegate { await Run(TopLevel.GetTopLevel(button) as Window, click); };
            return button;
        }
        public static Button Action(string text, Action click, bool primary = false)
        { return Action(text, click == null ? (Func<Task>)null : () => { click(); return Task.CompletedTask; }, primary); }

        // Sets a button's label, icon and colours; primary actions use the accent colour like "Add emulator".
        public static void SetAction(Button button, string text, bool primary = false)
        {
            var background = primary && !P.Restrained ? P.Blue : P.Surface; var foreground = primary && !P.Restrained ? Palette.ReadableInk(P.Blue) : P.Ink;
            bool darkInk = Palette.Brightness(foreground) < 0.55f;
            var icon = new FishIcon(FishIcon.ForText(text), 20, darkInk ? foreground : Palette.Rgb(224, 208, 255), darkInk ? foreground : Palette.Rgb(255, 180, 105));
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, FontWeight = primary ? FontWeight.Bold : FontWeight.Normal };
            button.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { icon, label } };
            button.Tag = text;
            Tint(button, background, foreground);
        }
        public static string ActionText(Button button) { return button.Tag as string; }

        // Fluent reads these resources for hover/pressed/disabled states; setting them per control gives FishBowl's soft blend.
        public static void Tint(TemplatedControl control, Color background, Color foreground)
        {
            control.Background = new SolidColorBrush(background); control.Foreground = new SolidColorBrush(foreground);
            control.BorderBrush = new SolidColorBrush(Palette.Blend(background, 22)); control.BorderThickness = new Thickness(1);
            var r = control.Resources;
            r["ButtonBackgroundPointerOver"] = new SolidColorBrush(Palette.Blend(background, 24));
            r["ButtonBackgroundPressed"] = new SolidColorBrush(Palette.Blend(background, 36));
            r["ButtonForegroundPointerOver"] = r["ButtonForegroundPressed"] = new SolidColorBrush(foreground);
            r["ButtonBorderBrushPointerOver"] = r["ButtonBorderBrushPressed"] = new SolidColorBrush(Palette.Blend(background, 40));
            r["ButtonBackgroundDisabled"] = new SolidColorBrush(background);
            r["ButtonForegroundDisabled"] = new SolidColorBrush(Palette.IsDarkColor(background) ? Palette.Rgb(127, 129, 144) : Palette.Rgb(130, 132, 146));
            r["ButtonBorderBrushDisabled"] = new SolidColorBrush(Palette.Blend(background, 12));
        }

        public static TextBlock Text(string text, double size = 13, bool bold = false, IBrush foreground = null)
        { return new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Foreground = foreground ?? P.InkBrush, TextWrapping = TextWrapping.Wrap }; }
        public static TextBlock Hint(string text) { var hint = Text(text, 12, false, P.SubtleBrush); hint.Margin = new Thickness(0, 4, 0, 6); return hint; }
        public static TextBlock Caption(string text) { var caption = Text(text, 12, true); caption.Margin = new Thickness(0, 8, 0, 3); return caption; }

        public static TextBox Field(string text = "", bool readOnly = false)
        { return new TextBox { Text = text ?? "", IsReadOnly = readOnly, Background = P.SurfaceBrush, Foreground = P.InkBrush, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center }; }
        public static TextBox Paragraphs(string text = "", bool readOnly = true)
        {
            var box = new TextBox { Text = text ?? "", IsReadOnly = readOnly, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Background = P.BottomBrush, Foreground = P.InkBrush, BorderThickness = new Thickness(0), FontSize = 13.5 };
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            return box;
        }
        public static ComboBox Combo(IEnumerable<string> items, string selected)
        {
            var list = items.ToList();
            var combo = new ComboBox { ItemsSource = list, SelectedItem = list.Contains(selected) ? selected : list.FirstOrDefault(), MinHeight = 32, HorizontalAlignment = HorizontalAlignment.Stretch };
            return combo;
        }
        public static CheckBox Check(string text, bool value) { return new CheckBox { Content = text, IsChecked = value, Foreground = P.InkBrush }; }
        public static WrapPanel Actions(params Control[] items) { var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4) }; foreach (var item in items) { item.Margin = new Thickness(0, 3, 8, 3); panel.Children.Add(item); } return panel; }
        // A field with a trailing action button, as the WinForms Input/AddRow helpers.
        public static Control WithButton(Control field, Button button)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(field); button.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(button, 1); grid.Children.Add(button); return grid;
        }

        public static Bitmap Logo()
        {
            using (var stream = typeof(Ui).Assembly.GetManifestResourceStream("FishBowl.png")) return stream == null ? null : new Bitmap(stream);
        }
        public static WindowIcon AppIcon()
        {
            using (var stream = typeof(Ui).Assembly.GetManifestResourceStream("FishBowl.png")) return stream == null ? null : new WindowIcon(stream);
        }

        // Runs a UI action, logging and reporting any failure (RunUiAction in the Windows build).
        public static async Task Run(Window owner, Func<Task> action)
        {
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Store.Log("Action failed: " + error);
                await Message(owner, "FishBowl could not complete this action.\n\n" + error.Message);
            }
        }

        public static Task Message(Window owner, string text, string title = "FishBowl") { return Ask(owner, text, title, "OK"); }
        public static async Task<bool> Confirm(Window owner, string text, string title = "FishBowl", string yes = "Yes", string no = "No")
        { return await Ask(owner, text, title, yes, no) == yes; }
        public static async Task<string> Ask(Window owner, string text, string title, params string[] buttons)
        {
            var dialog = new FishDialog(title, 520);
            var body = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = P.InkBrush, FontSize = 13.5, MaxWidth = 480 };
            var scroll = new ScrollViewer { Content = body, MaxHeight = 520 };
            string result = null;
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            for (int i = 0; i < buttons.Length; i++)
            {
                var label = buttons[i]; var button = Action(label, () => { result = label; dialog.Close(); }, i == 0);
                if (i == 0) button.IsDefault = true; if (i == buttons.Length - 1 && buttons.Length > 1) button.IsCancel = true;
                row.Children.Add(button);
            }
            dialog.Body = new StackPanel { Children = { scroll, row } };
            await dialog.Present(owner);
            return result;
        }

        // ----- File pickers ----------------------------------------------------------------------------------------

        // filter uses "Label|*.a;*.b|Label2|*" pairs like the WinForms dialogs.
        private static List<FilePickerFileType> Types(string filter)
        {
            var types = new List<FilePickerFileType>(); if (String.IsNullOrWhiteSpace(filter)) return types;
            var parts = filter.Split('|');
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                var patterns = parts[i + 1].Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                types.Add(new FilePickerFileType(parts[i]) { Patterns = patterns.Contains("*.*") ? new[] { "*" } : patterns });
            }
            return types;
        }
        private static async Task<IStorageFolder> Folder(Window owner, string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return null;
            try { return await owner.StorageProvider.TryGetFolderFromPathAsync(path); } catch (Exception) { return null; }
        }
        public static async Task<string> PickFile(Window owner, string title, string filter = null, string initialDirectory = null)
        {
            var files = await PickFiles(owner, title, filter, initialDirectory, false);
            return files.FirstOrDefault();
        }
        public static async Task<List<string>> PickFiles(Window owner, string title, string filter, string initialDirectory, bool multiple)
        {
            var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = multiple, FileTypeFilter = Types(filter), SuggestedStartLocation = await Folder(owner, initialDirectory) });
            return result.Select(f => f.TryGetLocalPath()).Where(p => p != null).ToList();
        }
        public static async Task<string> PickFolder(Window owner, string title, string initialDirectory = null)
        {
            var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, SuggestedStartLocation = await Folder(owner, initialDirectory) });
            return result.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
        }
        public static async Task<string> SaveFile(Window owner, string title, string filter, string suggestedName)
        {
            var result = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title, FileTypeChoices = Types(filter), SuggestedFileName = suggestedName, ShowOverwritePrompt = true });
            return result == null ? null : result.TryGetLocalPath();
        }

        public static void Post(Action action) { Dispatcher.UIThread.Post(action); }
    }

    // A themed modal window. Set Body, then await Present(owner).
    public class FishDialog : Window
    {
        public FishDialog(string title, double width, double height = double.NaN)
        {
            Title = title; Width = width; Icon = Ui.AppIcon(); ShowInTaskbar = false;
            if (Double.IsNaN(height)) SizeToContent = SizeToContent.Height; else Height = height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Ui.P.TopBrush; Foreground = Ui.P.InkBrush; FontFamily = Ui.Font;
            KeyDown += (sender, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        }
        public Control Body { set { Content = new Border { Padding = new Thickness(18), Child = value }; } }
        // Not ShowDialog: on X11 a modal dialog takes focus back whenever its disabled owner is activated, and on
        // focus-follows-mouse desktops (Hyprland, Sway, i3) that warps the pointer back into the dialog, trapping it.
        // Instead the dialog stays above its owner, and the owner ignores input until the dialog closes.
        public async Task Present(Window owner)
        {
            var closed = new TaskCompletionSource<bool>(); Closed += delegate { closed.TrySetResult(true); };
            if (owner == null || !owner.IsVisible) { Show(); await closed.Task; return; }
            EventHandler<RoutedEventArgs> block = (sender, e) => e.Handled = true;
            var events = new RoutedEvent[] { InputElement.PointerPressedEvent, InputElement.PointerReleasedEvent, InputElement.PointerWheelChangedEvent, InputElement.KeyDownEvent, InputElement.KeyUpEvent, InputElement.TextInputEvent, DragDrop.DragOverEvent, DragDrop.DropEvent };
            foreach (var routed in events) owner.AddHandler(routed, block, RoutingStrategies.Tunnel, true);
            try { Show(owner); await closed.Task; }
            finally
            {
                foreach (var routed in events) owner.RemoveHandler(routed, block);
                owner.Activate();
            }
        }
        // Standard right-aligned Save/Cancel row; save returns false to keep the dialog open.
        public Control Footer(string confirm, Func<Task<bool>> save, params Control[] extra)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            foreach (var item in extra) row.Children.Add(item);
            var ok = Ui.Action(confirm, async () => { if (await save()) { Confirmed = true; Close(); } }, true); ok.IsDefault = true;
            var cancel = Ui.Action("Cancel", () => Close()); cancel.IsCancel = true; cancel.Margin = new Thickness(0);
            row.Children.Add(ok); row.Children.Add(cancel); return row;
        }
        public bool Confirmed { get; protected set; }
    }
}

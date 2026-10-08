using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace EmulatorHub
{
    public class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            // Fluent's tab headers are sized for page navigation; FishBowl uses compact information tabs.
            Styles.Add(new Style(x => x.OfType<TabItem>())
            {
                Setters = { new Setter(TemplatedControl.FontSizeProperty, 14.0), new Setter(Layoutable.MinHeightProperty, 36.0), new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 4)) }
            });
            Name = "FishBowl";
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Like Application.ThreadException in the Windows build: log, tell the user, keep running.
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (sender, e) =>
            {
                e.Handled = true;
                Store.Log("Interface action failed: " + e.Exception);
                var owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => await Ui.Message(owner, "FishBowl could not complete this action.\n\n" + e.Exception.Message));
            };
            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                var window = new MainWindow();
                desktop.MainWindow = window;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            base.OnFrameworkInitializationCompleted();
        }

        // Matches Fluent's own colours to the active FishBowl palette so text boxes, lists and menus blend in.
        public void ApplyPalette(Palette p)
        {
            RequestedThemeVariant = p.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            var r = Resources;
            r["SystemAccentColor"] = p.Blue;
            r["SystemAccentColorLight1"] = r["SystemAccentColorLight2"] = r["SystemAccentColorLight3"] = p.Blue;
            r["SystemAccentColorDark1"] = r["SystemAccentColorDark2"] = r["SystemAccentColorDark3"] = p.Blue;
            r["TextControlBackground"] = r["TextControlBackgroundPointerOver"] = new SolidColorBrush(p.Surface);
            r["TextControlBackgroundFocused"] = new SolidColorBrush(Palette.Blend(p.Surface, 12));
            r["TextControlForeground"] = r["TextControlForegroundPointerOver"] = r["TextControlForegroundFocused"] = new SolidColorBrush(p.Ink);
            r["TextControlBorderBrush"] = r["TextControlBorderBrushPointerOver"] = new SolidColorBrush(Palette.Blend(p.Surface, 30));
            r["TextControlBorderBrushFocused"] = new SolidColorBrush(p.Blue);
            r["TextControlSelectionHighlightColor"] = new SolidColorBrush(Palette.Alpha(140, p.Blue));
            r["ComboBoxBackground"] = r["ComboBoxBackgroundPointerOver"] = new SolidColorBrush(p.Surface);
            r["ComboBoxDropDownBackground"] = new SolidColorBrush(p.Surface);
            r["ComboBoxForeground"] = new SolidColorBrush(p.Ink);
            r["MenuFlyoutPresenterBackground"] = new SolidColorBrush(p.Surface);
            r["MenuFlyoutItemBackgroundPointerOver"] = new SolidColorBrush(Palette.Blend(p.Surface, 24));
            r["ListBoxItemBackgroundPointerOver"] = Brushes.Transparent;
            r["ListBoxItemBackgroundSelected"] = r["ListBoxItemBackgroundSelectedPointerOver"] = r["ListBoxItemBackgroundPressed"] = Brushes.Transparent;
            r["TabItemHeaderBackgroundSelected"] = new SolidColorBrush(Palette.Blend(p.Top, 24));
            r["TabItemHeaderSelectedPipeFill"] = new SolidColorBrush(p.Blue);
            // Fluent's unselected tab text is too faint on the light themes; use the palette's secondary text colour.
            r["TabItemHeaderForegroundUnselected"] = r["TabItemHeaderForegroundUnselectedPointerOver"] = r["TabItemHeaderForegroundUnselectedPressed"] = new SolidColorBrush(p.Subtle);
            r["CheckBoxCheckBackgroundFillChecked"] = r["CheckBoxCheckBackgroundFillCheckedPointerOver"] = new SolidColorBrush(p.Blue);
            r["CheckBoxCheckGlyphForegroundChecked"] = new SolidColorBrush(p.Top);
        }
    }

    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => Store.Log("Unhandled failure: " + e.ExceptionObject);
            // One FishBowl per library: a second launch hands its arguments to the running copy and exits.
            SingleInstance instance = null;
            try
            {
                var socket = SingleInstance.SocketPath(Store.DataDirectory);
                if (SingleInstance.TryForward(socket, args)) return 0;
                instance = SingleInstance.Listen(socket, MainWindow.ReceiveForwardedArguments);
                if (instance == null && SingleInstance.TryForward(socket, args)) return 0;
            }
            catch (Exception error) { Store.Log("Single-instance check skipped: " + error.Message); }
            try
            {
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception error)
            {
                Store.Log("Startup failed: " + error);
                Console.Error.WriteLine("FishBowl could not start.\n\n" + error);
                return 1;
            }
            finally { if (instance != null) instance.Dispose(); }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont()
                .With(new X11PlatformOptions { WmClass = "FishBowl" }).LogToTrace();
        }
    }
}

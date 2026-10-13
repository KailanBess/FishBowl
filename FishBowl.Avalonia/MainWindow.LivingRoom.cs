using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private LivingRoomWindow livingRoom;

        // True while the Living-room Library is open: controller input then works even if navigation is off elsewhere.
        private bool LivingRoomOpen { get { return livingRoom != null; } }

        // Opens (or brings back) the full-screen Living-room Library. Menu: View > Living-room Library; Ctrl+L;
        // command line: fishbowl --living-room.
        public void OpenLivingRoom()
        {
            if (livingRoom != null) { livingRoom.Activate(); return; }
            livingRoom = new LivingRoomWindow(library, LaunchGame,
                game => sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished),
                () => RefreshGameLibrary());
            livingRoom.Closed += delegate { livingRoom = null; RefreshGameLibrary(); if (IsVisible) Activate(); };
            livingRoom.Show();
        }

        private Control LivingRoomPage()
        {
            var panel = new StackPanel { Spacing = 14, Margin = new Avalonia.Thickness(24), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(Ui.Text("Living-room Library", 24, true));
            panel.Children.Add(Ui.Hint("Full-screen, large cards with controller, keyboard and mouse navigation. Personal shelves (Continue playing, Recently added, Favorites), a focus view, artwork backdrops, launch presentation, session recaps and ambient artwork are set in Immersion settings."));
            panel.Children.Add(Ui.Actions(Ui.Action("Open Living-room Library (Ctrl+L)", () => OpenLivingRoom(), true), Ui.Action("Immersion settings", () => ImmersionSettingsDialog.Show(this, library, () => RefreshGameLibrary()))));
            panel.Children.Add(Ui.Hint("Controller: A plays, B goes back, X marks a favorite, Y opens the focus view, LB and RB switch shelves, Start opens the menu. Keyboard: arrows, Enter, F, I, Tab, M and Esc."));
            return panel;
        }

        // Offers the session recap (Immersion setting) when a verified play session ends and nothing else is running.
        private void OfferSessionRecap(SessionTracker tracker)
        {
            var session = (library.PlaySessions ?? new System.Collections.Generic.List<PlaySession>()).FirstOrDefault(s => s.Id == tracker.SessionId);
            var game = library.Games.FirstOrDefault(g => g.Id == tracker.GameId);
            if (session == null || game == null || UserTools.Guest || !Immersion.OfferRecap(library, session)) return;
            Ui.Post(async () =>
            {
                if (UserTools.ActiveLaunches > 0) return;
                Window owner = livingRoom != null && livingRoom.IsVisible ? (Window)livingRoom : this;
                await Ui.Run(owner, () => SessionRecapDialog.Show(owner, library, game, session));
                if (livingRoom != null) livingRoom.RefreshGames();
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace EmulatorHub
{
    // Arguments a second FishBowl launch handed over (see SingleInstance): raise this window and act on them.
    public partial class MainWindow
    {
        private static MainWindow running;
        private static readonly List<string[]> forwardedArguments = new List<string[]>();

        // Called on the socket thread.
        internal static void ReceiveForwardedArguments(string[] args)
        {
            Ui.Post(async () =>
            {
                var window = running;
                if (window == null || !window.IsVisible) { forwardedArguments.Add(args); return; }
                await window.HandleForwardedArguments(args);
            });
        }

        private async Task HandleForwardedArguments(string[] args)
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            if (args.Contains("--living-room")) OpenLivingRoom();
            await HandleExternalLaunch(args);
        }

        // Runs once the window is open: launches that arrived during startup.
        private async Task HandlePendingForwardedArguments()
        {
            running = this;
            var pending = forwardedArguments.ToArray(); forwardedArguments.Clear();
            foreach (var args in pending) await HandleForwardedArguments(args);
        }
    }
}

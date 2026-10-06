using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private async Task ShowRemoteCouchPlay()
        {
            var dialog = new FishDialog("Remote couch play", 640);
            var body = new StackPanel { Spacing = 10 };
            var service = Ui.Field(library.Multiplayer?.RelayGatewayUrl ?? "");
            body.Children.Add(Ui.Hint("Join a session in the browser; hosting shared controls currently requires Windows FishBowl."));
            body.Children.Add(Ui.Caption("Token service URL")); body.Children.Add(service);
            body.Children.Add(Ui.Hint("Use the host's HTTPS service address. The browser lets you enter the session code and shows video and shared controls after the host approves."));
            body.Children.Add(Ui.Action("Open session browser", () =>
            {
                Uri address;
                if (!Uri.TryCreate((service.Text ?? "").Trim(), UriKind.Absolute, out address) || !String.IsNullOrEmpty(address.UserInfo) || !(address.Scheme == "https" || address.Scheme == "http" && address.IsLoopback))
                    throw new IOException("Use an HTTPS token service, or localhost for development.");
                if (!String.IsNullOrEmpty(address.Query) || !String.IsNullOrEmpty(address.Fragment)) throw new IOException("Enter the service address without a session code or token.");
                if (library.Multiplayer == null) library.Multiplayer = new MultiplayerSettings();
                library.Multiplayer.RelayGatewayUrl = address.AbsoluteUri; Store.Save(library);
                Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
            }, true));
            body.Children.Add(Ui.Action("Close", dialog.Close)); dialog.Body = body; await dialog.Present(this);
        }
    }
}

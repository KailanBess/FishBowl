using System;
using System.Diagnostics;
using System.IO;

namespace EmulatorHub
{
    public static class SystemWebLinks
    {
        public static ProcessStartInfo StartInfo(string address)
        {
            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidDataException("Use an http or https address without embedded sign-in details.");
            return new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
        }

        public static void Open(string address)
        {
            using (var process = Process.Start(StartInfo(address))) { }
        }
    }
}

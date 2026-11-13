using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace YunoSetup
{
    public static class WelcomeGuide
    {
        public static string Render(string userName)
        {
            using (var stream = typeof(WelcomeGuide).Assembly.GetManifestResourceStream("Yuno.Welcome.html"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd().Replace("{{USER}}", WebUtility.HtmlEncode(
                    string.IsNullOrWhiteSpace(userName) ? "usuário YUNO" : userName));
        }

        // Offline guide: the Windows account name never leaves this computer.
        public static void Open(CorelInstall corel)
        {
            var path = Path.Combine(corel.AddonDir, "Bem-vindo.html");
            File.WriteAllText(path, Render(Environment.UserName), new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}

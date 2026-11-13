using System;
using System.Linq;
using System.Windows;

namespace YunoSetup
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            bool desinstalar = e.Args.Any(a => a.Equals("/desinstalar", StringComparison.OrdinalIgnoreCase) || a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
            bool silencioso = e.Args.Any(a => a.Equals("/silencioso", StringComparison.OrdinalIgnoreCase) || a.Equals("/quiet", StringComparison.OrdinalIgnoreCase));

            var targetArg = e.Args.FirstOrDefault(a => a.StartsWith("/corel:", StringComparison.OrdinalIgnoreCase));
            var target = targetArg?.Substring(7);
            if (silencioso)
            {
                int code = 0;
                try
                {
                    // Do not expose the serial in process command-line arguments or logs.
                    var serial = Environment.GetEnvironmentVariable("YUNO_INSTALL_SERIAL");
                    Environment.SetEnvironmentVariable("YUNO_INSTALL_SERIAL", null);
                    if (!desinstalar) SerialAccess.Require(serial);
                    var installs = Installer.EncontrarCorel().Where(c =>
                        (target == null || string.Equals(c.Programs64, target, StringComparison.OrdinalIgnoreCase)) &&
                        (desinstalar ? System.IO.Directory.Exists(c.AddonDir) : c.Suportado)).ToList();
                    if (installs.Count == 0) throw new InvalidOperationException("Nenhuma instalação compatível encontrada.");
                    foreach (var c in installs)
                    {
                        if (desinstalar) Installer.Desinstalar(c, _ => { });
                        else Installer.Instalar(c, _ => { }, serial);
                    }
                }
                catch { code = 1; }
                Shutdown(code);
                return;
            }

            var w = new MainWindow(desinstalar, target);
            MainWindow = w;
            w.Show();
        }
    }
}

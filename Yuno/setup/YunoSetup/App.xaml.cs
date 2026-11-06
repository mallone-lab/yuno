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

            if (silencioso)
            {
                int code = 0;
                try
                {
                    foreach (var c in Installer.EncontrarCorel().Where(c => c.Suportado))
                    {
                        if (desinstalar) Installer.Desinstalar(c, _ => { });
                        else Installer.Instalar(c, _ => { });
                    }
                }
                catch { code = 1; }
                Shutdown(code);
                return;
            }

            var w = new MainWindow(desinstalar);
            MainWindow = w;
            w.Show();
        }
    }
}

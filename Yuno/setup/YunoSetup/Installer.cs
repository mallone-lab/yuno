using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Xml;

namespace YunoSetup
{
    /// <summary>Toda a lógica de instalação/remoção, sem interface.</summary>
    public static class Installer
    {
        public const string Produto = "YUNO";
        public const string ChaveUninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Yuno";
        public static string Versao => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public static List<CorelInstall> EncontrarCorel() => CorelCompatibility.Find();
        public const string PackagePrefix = "payload/universal/";
        public static bool TemPacoteUniversal()
        {
            var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            return new[] { "Yuno.dll", "YunoRes.dll", "AppUI.xslt", "UserUI.xslt", "config.xml", "CorelDrw.addon" }
                .All(file => names.Contains(PackagePrefix + file));
        }
        public static string ChavePara(CorelInstall c)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return ChaveUninstall + "-" + BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(Path.GetFullPath(c.Programs64).TrimEnd('\\').ToUpperInvariant())))
                    .Replace("-", "").Substring(0, 16);
        }

        public static List<Process> CorelAberto(bool soComJanela)
            => Process.GetProcessesByName("CorelDRW").Where(p => { try { return !soComJanela || p.MainWindowHandle != IntPtr.Zero; } catch { return false; } }).ToList();

        static IEnumerable<string> Payload(CorelInstall corel)
            => Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(n => n.StartsWith(PackagePrefix, StringComparison.Ordinal));
        public static string AvisoCorelAberto()
        {
            var currentSession = Process.GetCurrentProcess().SessionId;
            var details = new List<string>();
            foreach (var process in CorelAberto(false))
            {
                using (process)
                {
                    try
                    {
                        process.Refresh();
                        if (process.HasExited) continue;
                        var state = process.SessionId != currentSession ? "outra sessão do Windows"
                            : process.MainWindowHandle == IntPtr.Zero ? "em segundo plano, sem janela principal"
                            : "com janela aberta";
                        details.Add($"CorelDRW.exe — PID {process.Id} — {state}");
                    }
                    catch (InvalidOperationException) { /* Process exited during inspection. */ }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        details.Add($"CorelDRW.exe — PID {process.Id} — não foi possível consultar a janela");
                    }
                }
            }
            if (details.Count == 0) return null;
            return "O Windows ainda informa estes processos do CorelDRAW:\n\n" + string.Join("\n", details)
                + "\n\nMesmo depois de fechar a janela, o Corel pode continuar em segundo plano. "
                + "Aguarde alguns segundos e tente instalar novamente. Se continuar, confira os PIDs "
                + "no Gerenciador de Tarefas > Detalhes. Salve qualquer trabalho antes de encerrar um processo.";
        }

        static void ExigirCorelFechado()
        {
            var aviso = AvisoCorelAberto();
            if (aviso != null) throw new InvalidOperationException(aviso);
        }

        /// <summary>Copia um arquivo; se estiver em uso (DLL carregada pelo Corel), renomeia o antigo e copia o novo.</summary>
        static void Gravar(string destino, Func<Stream> origem)
        {
            try
            {
                using (var s = origem()) using (var f = File.Create(destino)) s.CopyTo(f);
            }
            catch (IOException)
            {
                var velho = destino + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".old";
                File.Move(destino, velho);
                using (var s = origem()) using (var f = File.Create(destino)) s.CopyTo(f);
            }
        }

        static void ApagarPasta(string dir, Action<string> log)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir))
            {
                try { File.Delete(f); }
                catch { try { File.Move(f, f + "." + DateTime.Now.Ticks + ".old"); } catch { } }
            }
            try { Directory.Delete(dir, true); } catch { log?.Invoke("Alguns arquivos antigos estavam em uso; serão removidos na próxima atualização."); }
        }

        public static void Instalar(CorelInstall corel, Action<string> log, string serial = null)
        {
            // Enforce at the installation boundary, including silent and direct callers.
            SerialAccess.Require(serial);
            if (!corel.Suportado) throw new InvalidOperationException("O YUNO exige CorelDRAW versão 20 (2018) ou superior, de 64 bits, e um instalador completo.");
            ExigirCorelFechado();

            log("Removendo versão anterior (AHC Moldes)...");
            ApagarPasta(corel.AddonAntigo, log);

            // Remove o servidor web da instalação anterior enquanto o YUNO volta
            // ao modo simples de importar pedidos por planilha.
            PararServidorWebAntigo(Path.Combine(corel.AddonDir, "server", "Yuno.Server.dll"), log);
            ApagarPasta(Path.Combine(corel.AddonDir, "server"), log);
            try { File.Delete(Path.Combine(corel.AddonDir, "iniciar-servidor-rede.ps1")); } catch { }

            log("Copiando arquivos do YUNO...");
            Directory.CreateDirectory(corel.AddonDir);
            foreach (var f in Directory.GetFiles(corel.AddonDir, "*.old")) { try { File.Delete(f); } catch { } }
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in Payload(corel))
            {
                var nome = res.Substring(PackagePrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                var destino = Path.Combine(corel.AddonDir, nome);
                Directory.CreateDirectory(Path.GetDirectoryName(destino));
                Gravar(destino, () => asm.GetManifestResourceStream(res));
            }

            // o próprio instalador fica junto, para desinstalar/reparar
            var self = asm.Location;
            var copia = Path.Combine(corel.AddonDir, "YunoSetup.exe");
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(copia), StringComparison.OrdinalIgnoreCase))
                Gravar(copia, () => File.OpenRead(self));

            log("Ativando a barra e o menu no CorelDRAW...");
            int n = LimparRegistroAreaDeTrabalho(corel);
            if (n < 0) log("Atenção: o CorelDRAW estava aberto; a barra aparece depois de fechar e abrir de novo.");

            log("Registrando em Aplicativos do Windows...");
            Registrar(corel, copia);
            log($"YUNO {Versao} instalado no {corel.Nome}.");
        }

        private static void PararServidorWebAntigo(string dll, Action<string> log)
        {
            if (!File.Exists(dll)) return;
            try
            {
                var alvo = Path.GetFullPath(dll).Replace("'", "''");
                var comando = "$alvo='" + alvo + "'; Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | " +
                    "Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($alvo,[StringComparison]::OrdinalIgnoreCase) -ge 0 } | " +
                    "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }";
                var start = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " +
                    Convert.ToBase64String(Encoding.Unicode.GetBytes(comando)))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var process = Process.Start(start))
                {
                    if (process != null && process.WaitForExit(5000) && process.ExitCode == 0)
                        log("Servidor web antigo do YUNO encerrado.");
                }
            }
            catch { log("Não consegui encerrar automaticamente um servidor web antigo; ele não será usado pelo YUNO novo."); }
        }

        public static void Desinstalar(CorelInstall corel, Action<string> log)
        {
            ExigirCorelFechado();
            log("Removendo arquivos...");
            ApagarPasta(corel.AddonDir, log);
            ApagarPasta(corel.AddonAntigo, log);
            LimparRegistroAreaDeTrabalho(corel);
            try { Registry.LocalMachine.DeleteSubKeyTree(ChavePara(corel), false); } catch { }
            log("YUNO removido. As configurações em %APPDATA%\\Yuno foram mantidas.");
        }

        /// <summary>
        /// O Corel anota na área de trabalho que já aplicou o UserUI.xslt de cada add-on e não reaplica.
        /// Removendo a anotação, ele insere a barra/menu novamente na próxima abertura.
        /// Retorna quantas áreas de trabalho foram ajustadas, ou -1 se o Corel estava aberto.
        /// </summary>
        public static int LimparRegistroAreaDeTrabalho(CorelInstall corel)
        {
            if (CorelAberto(true).Count > 0) return -1;
            var ano = corel.Nome.Substring("CorelDRAW ".Length);
            var ws = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                  "Corel", "CorelDRAW Graphics Suite " + ano, "Draw", "Workspace");
            if (!Directory.Exists(ws)) return 0;

            var backup = ws + "-backup-yuno-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            if (!Directory.Exists(backup))
            {
                Directory.CreateDirectory(backup);
                foreach (var f in Directory.GetFiles(ws)) File.Copy(f, Path.Combine(backup, Path.GetFileName(f)), true);
            }

            var rx = new Regex(@"<new_element installFile=""[^""]*\\addons\\(yuno|ahcmoldes)\\userui\.xslt""\s*(/>|></new_element>)", RegexOptions.IgnoreCase);
            int n = 0;
            foreach (var f in Directory.GetFiles(ws, "*.cdws"))
            {
                try
                {
                    using (var z = ZipFile.Open(f, ZipArchiveMode.Update))
                    {
                        var e = z.GetEntry("content/workspace.xml");
                        if (e == null) continue;
                        string txt;
                        using (var r = new StreamReader(e.Open(), Encoding.UTF8, true)) txt = r.ReadToEnd();
                        var novo = MigrarNomeYuno(rx.Replace(txt, ""));
                        if (novo == txt) continue;
                        e.Delete();
                        var ne = z.CreateEntry("content/workspace.xml", CompressionLevel.Optimal);
                        using (var s = ne.Open())
                        {
                            var b = new UTF8Encoding(false).GetBytes(novo);
                            s.Write(b, 0, b.Length);
                        }
                        n++;
                    }
                }
                catch { /* área de trabalho corrompida ou em uso: ignora */ }
            }
            return n;
        }

        public static string MigrarNomeYuno(string xml)
        {
            var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
            bool changed = false;
            // These are the existing YUNO toolbar/menu IDs, also used by the old AHC Moldes add-on.
            foreach (XmlElement bar in document.SelectNodes("//*[local-name()='commandBarData']"))
            {
                var id = bar.GetAttribute("guid");
                if (id != "9992b30a-3266-5e32-88f5-5ca096b24bbd" && id != "09ba2112-3fa1-4178-96e7-d230c33692f3") continue;
                foreach (var attribute in new[] { "userCaption", "nonLocalizableName" })
                    if (string.Equals(bar.GetAttribute(attribute), "AHC Moldes", StringComparison.OrdinalIgnoreCase))
                    {
                        bar.SetAttribute(attribute, "YUNO");
                        changed = true;
                    }
            }
            return changed ? document.OuterXml : xml;
        }

        static void Registrar(CorelInstall corel, string uninstaller)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(ChavePara(corel)))
            {
                k.SetValue("DisplayName", $"YUNO para {corel.Nome}");
                k.SetValue("DisplayVersion", Versao);
                k.SetValue("Publisher", "YUNO");
                k.SetValue("DisplayIcon", uninstaller);
                k.SetValue("InstallLocation", corel.AddonDir);
                k.SetValue("UninstallString", $"\"{uninstaller}\" /desinstalar \"/corel:{corel.Programs64}\"");
                k.SetValue("QuietUninstallString", $"\"{uninstaller}\" /desinstalar /silencioso \"/corel:{corel.Programs64}\"");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                long kb = Directory.GetFiles(corel.AddonDir).Sum(f => new FileInfo(f).Length) / 1024;
                k.SetValue("EstimatedSize", (int)kb, RegistryValueKind.DWord);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            }
        }
    }
}

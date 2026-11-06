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

namespace YunoSetup
{
    public class CorelInstall
    {
        public string Versao;      // "27"
        public string Nome;        // "CorelDRAW 2026"
        public string Programs64;  // ...\27\Programs64
        public bool Suportado => Versao == "27";
        public string AddonDir => Path.Combine(Programs64, "Addons", "Yuno");
        public string AddonAntigo => Path.Combine(Programs64, "Addons", "AhcMoldes");
        public override string ToString() => Nome;
    }

    /// <summary>Toda a lógica de instalação/remoção, sem interface.</summary>
    public static class Installer
    {
        public const string Produto = "YUNO";
        public const string ChaveUninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Yuno";
        public static string Versao => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        static readonly Dictionary<string, string> Anos = new Dictionary<string, string>
        { { "27", "2026" }, { "26", "2025" }, { "25", "2024" }, { "24", "2023" } };

        public static List<CorelInstall> EncontrarCorel()
        {
            var achados = new List<CorelInstall>();
            var bases = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"C:\Program Files"
            }.Distinct();
            foreach (var b in bases)
            {
                var suite = Path.Combine(b, "Corel", "CorelDRAW Graphics Suite");
                if (!Directory.Exists(suite)) continue;
                foreach (var d in Directory.GetDirectories(suite))
                {
                    var v = Path.GetFileName(d);
                    var p64 = Path.Combine(d, "Programs64");
                    if (!File.Exists(Path.Combine(p64, "CorelDRW.exe"))) continue;
                    if (achados.Any(a => string.Equals(a.Programs64, p64, StringComparison.OrdinalIgnoreCase))) continue;
                    achados.Add(new CorelInstall
                    {
                        Versao = v,
                        Programs64 = p64,
                        Nome = "CorelDRAW " + (Anos.TryGetValue(v, out var ano) ? ano : "versão " + v)
                    });
                }
            }
            return achados.OrderByDescending(a => a.Versao).ToList();
        }

        public static List<Process> CorelAberto(bool soComJanela)
            => Process.GetProcessesByName("CorelDRW").Where(p => { try { return !soComJanela || p.MainWindowHandle != IntPtr.Zero; } catch { return false; } }).ToList();

        public static void FecharCorel()
        {
            foreach (var p in Process.GetProcessesByName("CorelDRW"))
            {
                try { p.Kill(); p.WaitForExit(10000); } catch { }
            }
        }

        static IEnumerable<string> Payload()
            => Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.StartsWith("payload/"));

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

        public static void Instalar(CorelInstall corel, Action<string> log)
        {
            if (!corel.Suportado) throw new InvalidOperationException($"{corel.Nome} ainda não é suportado. O YUNO funciona no CorelDRAW 2026.");

            log("Removendo versão anterior (AHC Moldes)...");
            ApagarPasta(corel.AddonAntigo, log);

            log("Copiando arquivos do YUNO...");
            Directory.CreateDirectory(corel.AddonDir);
            foreach (var f in Directory.GetFiles(corel.AddonDir, "*.old")) { try { File.Delete(f); } catch { } }
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in Payload())
            {
                var nome = res.Substring("payload/".Length);
                Gravar(Path.Combine(corel.AddonDir, nome), () => asm.GetManifestResourceStream(res));
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

        public static void Desinstalar(CorelInstall corel, Action<string> log)
        {
            log("Removendo arquivos...");
            ApagarPasta(corel.AddonDir, log);
            ApagarPasta(corel.AddonAntigo, log);
            LimparRegistroAreaDeTrabalho(corel);
            try { Registry.LocalMachine.DeleteSubKeyTree(ChaveUninstall, false); } catch { }
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
            var ano = Anos.TryGetValue(corel.Versao, out var a) ? a : corel.Versao;
            var ws = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                  "Corel", "CorelDRAW Graphics Suite " + ano, "Draw", "Workspace");
            if (!Directory.Exists(ws)) return 0;

            var backup = ws + "-backup-yuno";
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
                        var novo = rx.Replace(txt, "");
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

        static void Registrar(CorelInstall corel, string uninstaller)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(ChaveUninstall))
            {
                k.SetValue("DisplayName", $"YUNO para {corel.Nome}");
                k.SetValue("DisplayVersion", Versao);
                k.SetValue("Publisher", "YUNO");
                k.SetValue("DisplayIcon", uninstaller);
                k.SetValue("InstallLocation", corel.AddonDir);
                k.SetValue("UninstallString", $"\"{uninstaller}\" /desinstalar");
                k.SetValue("QuietUninstallString", $"\"{uninstaller}\" /desinstalar /silencioso");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                long kb = Directory.GetFiles(corel.AddonDir).Sum(f => new FileInfo(f).Length) / 1024;
                k.SetValue("EstimatedSize", (int)kb, RegistryValueKind.DWord);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            }
        }
    }
}

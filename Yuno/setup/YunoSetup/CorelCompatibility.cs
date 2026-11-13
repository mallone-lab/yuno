using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace YunoSetup
{
    public class CorelInstall
    {
        public string Versao;
        public string Nome;
        public string Programs64;
        public string InteropVersion;
        public int Major;
        public bool Suportado => Major >= 20 && Installer.TemPacoteUniversal();
        public string AddonDir => Path.Combine(Programs64, "Addons", "Yuno");
        public string AddonAntigo => Path.Combine(Programs64, "Addons", "AhcMoldes");
        public override string ToString() => Nome + " · " + InteropVersion;
    }

    public static class CorelCompatibility
    {
        public static string Nome(int major, int minor)
        {
            int year = major <= 23 ? major + 1998 : major == 24 ? (minor >= 3 ? 2023 : 2022) : major + 1999;
            return "CorelDRAW " + year;
        }

        public static CorelInstall Inspect(string programs)
        {
            try
            {
                var exe = Path.Combine(programs, "CorelDRW.exe");
                if (!File.Exists(exe)) return null;
                // The plugin is x64. Reject x86 hosts, even in custom installation directories.
                using (var r = new BinaryReader(File.OpenRead(exe)))
                {
                    if (r.ReadUInt16() != 0x5a4d) return null;
                    r.BaseStream.Position = 0x3c;
                    int offset = r.ReadInt32();
                    if (offset < 0 || offset > r.BaseStream.Length - 6) return null;
                    r.BaseStream.Position = offset;
                    if (r.ReadUInt32() != 0x4550 || r.ReadUInt16() != 0x8664) return null;
                }
                var v = FileVersionInfo.GetVersionInfo(exe);
                return new CorelInstall {
                    Major = v.FileMajorPart, Versao = v.FileMajorPart.ToString(),
                    Nome = Nome(v.FileMajorPart, v.FileMinorPart),
                    Programs64 = Path.GetFullPath(programs),
                    // Display the executable version. No client interop DLL is required.
                    InteropVersion = new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart, v.FilePrivatePart).ToString()
                };
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (BadImageFormatException) { return null; }
        }

        public static List<CorelInstall> Find()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // COM registration finds installations on other drives and custom paths.
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64))
            {
                foreach (var name in root.GetSubKeyNames().Where(n => n.StartsWith("CorelDRAW.Application.", StringComparison.OrdinalIgnoreCase)))
                {
                    using (var prog = root.OpenSubKey(name + "\\CLSID"))
                    using (var server = root.OpenSubKey("CLSID\\" + prog?.GetValue(null) + "\\LocalServer32"))
                    {
                        var command = server?.GetValue(null) as string;
                        if (string.IsNullOrWhiteSpace(command)) continue;
                        command = Environment.ExpandEnvironmentVariables(command.Trim());
                        int end = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                        if (end < 0) continue;
                        var exe = command.Substring(0, end + 4).Trim('"');
                        if (File.Exists(exe)) paths.Add(Path.GetDirectoryName(exe));
                    }
                }
            }
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Corel");
            if (Directory.Exists(baseDir))
                foreach (var suite in Directory.GetDirectories(baseDir, "CorelDRAW*"))
                {
                    paths.Add(Path.Combine(suite, "Programs64"));
                    foreach (var version in Directory.GetDirectories(suite))
                        paths.Add(Path.Combine(version, "Programs64"));
                }
            return paths.Select(Inspect).Where(c => c != null).OrderByDescending(c => c.Major)
                .ThenByDescending(c => new Version(c.InteropVersion)).ToList();
        }
    }
}

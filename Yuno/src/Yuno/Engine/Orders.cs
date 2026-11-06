using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Yuno.Engine
{
    public class OrderLine
    {
        public string Tipo { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Numero { get; set; } = "";
        public string Adicional { get; set; } = "";
        public string TamanhoCamiseta { get; set; } = "";
        public string TamanhoCalcao { get; set; } = "";
        public int Quantidade { get; set; } = 1;
        /// <summary>Tamanho usado na montagem (camiseta ou calção, conforme a escolha).</summary>
        public string Tamanho { get; set; } = "";

        public override string ToString() => $"{Tamanho} {Nome} {Numero} x{Quantidade}";
    }

    /// <summary>Lê pedidos de texto colado (Excel/TSV), CSV ou planilha Excel.</summary>
    public static class OrderImport
    {
        public static int ColumnIndex(string letters)
        {
            letters = (letters ?? "").Trim().ToUpperInvariant();
            if (letters.Length == 0) return -1;
            if (int.TryParse(letters, out var n)) return n - 1;
            int idx = 0;
            foreach (char c in letters)
            {
                if (c < 'A' || c > 'Z') return -1;
                idx = idx * 26 + (c - 'A' + 1);
            }
            return idx - 1;
        }

        public static List<OrderLine> FromRows(IEnumerable<string[]> rows, Config cfg, List<string> warnings = null)
        {
            int cTipo = ColumnIndex(cfg.ColunaTipo), cN = ColumnIndex(cfg.ColunaNome),
                cNum = ColumnIndex(cfg.ColunaNumero), cA = ColumnIndex(cfg.ColunaAdicional),
                cCam = ColumnIndex(cfg.ColunaTamanho), cCal = ColumnIndex(cfg.ColunaTamanhoCalcao),
                cQ = ColumnIndex(cfg.ColunaQuantidade);
            string Get(string[] r, int i) => i >= 0 && i < r.Length ? (r[i] ?? "").Trim() : "";

            var result = new List<OrderLine>();
            bool first = true;
            int lineNo = 0;
            foreach (var r in rows)
            {
                lineNo++;
                if (first && cfg.PrimeiraLinhaCabecalho)
                {
                    first = false;
                    // cabeçalho reconhecido pelo texto: Tipo, Nome, Número, Adicional, Tam. Camiseta, Tam. Calção, Quantidade
                    int Find(params string[] keys)
                    {
                        for (int i = 0; i < r.Length; i++)
                        {
                            var h = Plain(r[i]);
                            if (keys.Any(k => h.Contains(k))) return i;
                        }
                        return -1;
                    }
                    int f;
                    if ((f = Find("tipo")) >= 0) cTipo = f;
                    if ((f = Find("nome")) >= 0) cN = f;
                    if ((f = Find("numero", "num", "nº", "n°")) >= 0) cNum = f;
                    if ((f = Find("adic")) >= 0) cA = f;
                    if ((f = Find("camis")) >= 0) cCam = f;
                    else if ((f = Find("tamanho", "tam")) >= 0) cCam = f;
                    if ((f = Find("calc")) >= 0) cCal = f;
                    if ((f = Find("quant", "qtd", "qtde")) >= 0) cQ = f;
                    continue;
                }
                first = false;
                if (r.All(string.IsNullOrWhiteSpace)) continue;

                var cam = NormalizeSize(Get(r, cCam));
                var cal = NormalizeSize(Get(r, cCal));
                if (cam.Length == 0 && cal.Length == 0)
                {
                    warnings?.Add($"Linha {lineNo}: sem tamanho, ignorada.");
                    continue;
                }
                int qty = 1;
                var q = Get(r, cQ);
                if (q.Length > 0)
                {
                    if (double.TryParse(q.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var dq) && dq >= 1)
                        qty = (int)Math.Round(dq);
                    else if (cQ >= 0) warnings?.Add($"Linha {lineNo}: quantidade \"{q}\" inválida, usando 1.");
                }
                var l = new OrderLine
                {
                    Tipo = Get(r, cTipo),
                    Nome = Get(r, cN),
                    Numero = CleanNumber(Get(r, cNum)),
                    Adicional = Get(r, cA),
                    TamanhoCamiseta = cam,
                    TamanhoCalcao = cal,
                    Quantidade = qty
                };
                l.Tamanho = cfg.UsarTamanhoCalcao ? cal : cam;
                result.Add(l);
            }
            return result;
        }

        /// <summary>Escolhe qual tamanho (camiseta ou calção) vale na montagem.</summary>
        public static void UsarTamanhos(IEnumerable<OrderLine> linhas, bool calcao)
        {
            foreach (var l in linhas)
            {
                var t = calcao ? l.TamanhoCalcao : l.TamanhoCamiseta;
                if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(l.TamanhoCamiseta) && string.IsNullOrEmpty(l.TamanhoCalcao)) t = l.Tamanho;
                l.Tamanho = NormalizeSize(t);
            }
        }

        static string Plain(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            var norm = s.Normalize(System.Text.NormalizationForm.FormD);
            return new string(norm.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        }

        /// <summary>Grava um modelo de planilha (CSV com ;) no mesmo formato do formulário de pedido.</summary>
        public static void SalvarModelo(string path)
        {
            var txt = string.Join("\r\n", "Tipo;Nome;Número;Adicional;Tam. Camiseta;Tam. Calção;Quantidade", "1 - Linha;JOÃO;10;;M;M;1", "");
            File.WriteAllText(path, txt, new UTF8Encoding(true));
        }

        public static string NormalizeSize(string s)
            => new string((s ?? "").Where(c => !char.IsWhiteSpace(c) && c != '/' && c != '\\').ToArray()).ToUpperInvariant();

        static string CleanNumber(string s)
        {
            // Excel devolve "10" como 10.0 / "10,00" em formato contábil
            if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && Math.Abs(d - Math.Round(d)) < 1e-9)
                return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            return s;
        }

        /// <summary>Texto copiado do Excel: linhas separadas por quebra, colunas por TAB.</summary>
        public static IEnumerable<string[]> ParseDelimited(string text, char? sep = null)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (sep == null)
            {
                var sample = string.Join("\n", lines.Take(5));
                sep = sample.Contains('\t') ? '\t' : sample.Contains(';') ? ';' : ',';
            }
            foreach (var l in lines)
            {
                if (l.Length == 0) continue;
                yield return SplitLine(l, sep.Value);
            }
        }

        static string[] SplitLine(string line, char sep)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == sep && !quoted) { cells.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells.ToArray();
        }

        /// <summary>Lê .csv/.txt diretamente ou .xlsx/.xls via Excel instalado.</summary>
        public static List<string[]> ReadFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".csv" || ext == ".txt" || ext == ".tsv")
                return ParseDelimited(File.ReadAllText(path, Encoding.Default)).ToList();
            return ReadExcel(path);
        }

        static List<string[]> ReadExcel(string path)
        {
            var type = Type.GetTypeFromProgID("Excel.Application");
            if (type == null) throw new UserException("Excel não encontrado. Salve a planilha como CSV ou copie e cole as linhas.");
            dynamic excel = Activator.CreateInstance(type);
            dynamic wb = null;
            try
            {
                excel.DisplayAlerts = false;
                wb = excel.Workbooks.Open(path, 0, true);
                dynamic ws = wb.Worksheets[1];
                object v = ws.UsedRange.Value2;
                var rows = new List<string[]>();
                if (v is object[,] arr)
                {
                    int r0 = arr.GetLowerBound(0), r1 = arr.GetUpperBound(0);
                    int c0 = arr.GetLowerBound(1), c1 = arr.GetUpperBound(1);
                    // UsedRange pode não começar na coluna A
                    int firstCol = (int)ws.UsedRange.Column - 1;
                    for (int r = r0; r <= r1; r++)
                    {
                        var row = new string[firstCol + (c1 - c0 + 1)];
                        for (int c = c0; c <= c1; c++)
                            row[firstCol + c - c0] = Cell(arr[r, c]);
                        rows.Add(row);
                    }
                }
                else if (v != null) rows.Add(new[] { Cell(v) });
                return rows;
            }
            finally
            {
                try { wb?.Close(false); } catch { }
                try { excel.Quit(); } catch { }
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(excel);
            }
        }

        static string Cell(object o)
        {
            if (o == null) return "";
            if (o is double d) return d.ToString(Math.Abs(d - Math.Round(d)) < 1e-9 ? "0" : "0.##", CultureInfo.InvariantCulture);
            return o.ToString().Trim();
        }
    }
}

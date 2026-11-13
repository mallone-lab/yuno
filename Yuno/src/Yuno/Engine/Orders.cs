using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Yuno.Engine
{
    [System.Runtime.Serialization.DataContract]
    public class OrderLine
    {
        [System.Runtime.Serialization.DataMember(Order = 1)] public string Tipo { get; set; } = "";
        [System.Runtime.Serialization.DataMember(Order = 2)] public string Nome { get; set; } = "";
        [System.Runtime.Serialization.DataMember(Order = 3)] public string Numero { get; set; } = "";
        [System.Runtime.Serialization.DataMember(Order = 4)] public string Adicional { get; set; } = "";
        [System.Runtime.Serialization.DataMember(Order = 5)] public string TamanhoCamiseta { get; set; } = "";
        [System.Runtime.Serialization.DataMember(Order = 6)] public string TamanhoCalcao { get; set; } = "";
        [System.Runtime.Serialization.DataMember(Order = 7)] public int Quantidade { get; set; } = 1;
        /// <summary>Tamanho usado na montagem (camiseta ou calção, conforme a escolha).</summary>
        [System.Runtime.Serialization.DataMember(Order = 8)] public string Tamanho { get; set; } = "";

        public override string ToString() => $"{Tamanho} {Nome} {Numero} x{Quantidade}";
    }

    /// <summary>Lê dados do pedido colados do Excel (TSV), CSV ou planilha Excel.</summary>
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

        /// <summary>Cria um modelo Excel com listas suspensas para tipos e tamanhos configurados no YUNO.</summary>
        public static void SalvarModelo(string path, Config cfg)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Escolha onde salvar o modelo.", nameof(path));
            cfg = cfg ?? new Config();
            var tipos = (cfg.TiposJogador ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var tamanhos = (cfg.Tamanhos ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (tipos.Count == 0) tipos.AddRange(new Config().TiposJogador);
            if (tamanhos.Count == 0) tamanhos.AddRange(new Config().Tamanhos);

            XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            XNamespace xml = XNamespace.Xml;
            string[] headers = { "Tipo", "Nome", "Número", "Adicional", "Tam. Camiseta", "Tam. Calção", "Quantidade" };
            string Col(int n)
            {
                string result = "";
                while (n > 0) { n--; result = (char)('A' + n % 26) + result; n /= 26; }
                return result;
            }
            XElement Cell(int column, int row, string value, int style = 0)
            {
                return new XElement(s + "c", new XAttribute("r", Col(column) + row),
                    style > 0 ? new XAttribute("s", style) : null, new XAttribute("t", "inlineStr"),
                    new XElement(s + "is", new XElement(s + "t", new XAttribute(xml + "space", "preserve"), value ?? "")));
            }
            XElement Row(int row, IEnumerable<XElement> cells) => new XElement(s + "row", new XAttribute("r", row), cells);
            XElement Validation(string range, string formula, string title, string prompt) => new XElement(s + "dataValidation",
                new XAttribute("type", "list"), new XAttribute("allowBlank", "1"), new XAttribute("showErrorMessage", "1"),
                new XAttribute("showInputMessage", "1"), new XAttribute("errorTitle", "Escolha uma opção da lista"),
                new XAttribute("error", "Use uma das opções cadastradas nas configurações do YUNO."),
                new XAttribute("promptTitle", title), new XAttribute("prompt", prompt), new XAttribute("sqref", range),
                new XElement(s + "formula1", "=" + formula));

            var mainRoot = new XElement(s + "worksheet");
            mainRoot.Add(new XElement(s + "dimension", new XAttribute("ref", "A1:G501")));
            mainRoot.Add(new XElement(s + "sheetViews", new XElement(s + "sheetView", new XAttribute("workbookViewId", "0"),
                new XElement(s + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))));
            mainRoot.Add(new XElement(s + "sheetFormatPr", new XAttribute("defaultRowHeight", "20")));
            mainRoot.Add(new XElement(s + "cols", new[] { 20, 28, 14, 26, 20, 20, 14 }.Select((w, i) =>
                new XElement(s + "col", new XAttribute("min", i + 1), new XAttribute("max", i + 1), new XAttribute("width", w), new XAttribute("customWidth", "1")))));
            mainRoot.Add(new XElement(s + "sheetData", Row(1, headers.Select((h, i) => Cell(i + 1, 1, h, 1)))));
            mainRoot.Add(new XElement(s + "autoFilter", new XAttribute("ref", "A1:G501")));
            mainRoot.Add(new XElement(s + "dataValidations", new XAttribute("count", "3"),
                Validation("A2:A501", "TiposJogador", "Tipo de jogador", "Selecione uma opção cadastrada."),
                Validation("E2:E501", "Tamanhos", "Tamanho da camiseta", "Selecione um tamanho cadastrado nas configurações do YUNO."),
                Validation("F2:F501", "Tamanhos", "Tamanho do calção", "Selecione um tamanho cadastrado nas configurações do YUNO.")));
            mainRoot.Add(new XElement(s + "pageMargins", new XAttribute("left", "0.25"), new XAttribute("right", "0.25"), new XAttribute("top", "0.5"), new XAttribute("bottom", "0.5"), new XAttribute("header", "0.2"), new XAttribute("footer", "0.2")));
            var mainSheet = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), mainRoot);

            var listaRows = new List<XElement> { Row(1, new[] { Cell(1, 1, "Tipos de jogador", 1), Cell(2, 1, "Tamanhos", 1) }) };
            int max = Math.Max(tipos.Count, tamanhos.Count);
            for (int i = 0; i < max; i++)
            {
                int row = i + 2;
                var cells = new List<XElement>();
                if (i < tipos.Count) cells.Add(Cell(1, row, tipos[i]));
                if (i < tamanhos.Count) cells.Add(Cell(2, row, tamanhos[i]));
                listaRows.Add(Row(row, cells));
            }
            var listaRoot = new XElement(s + "worksheet");
            listaRoot.Add(new XElement(s + "sheetViews", new XElement(s + "sheetView", new XAttribute("workbookViewId", "0"))));
            listaRoot.Add(new XElement(s + "sheetFormatPr", new XAttribute("defaultRowHeight", "18")));
            listaRoot.Add(new XElement(s + "cols", new XElement(s + "col", new XAttribute("min", "1"), new XAttribute("max", "1"), new XAttribute("width", "28"), new XAttribute("customWidth", "1")),
                new XElement(s + "col", new XAttribute("min", "2"), new XAttribute("max", "2"), new XAttribute("width", "22"), new XAttribute("customWidth", "1"))));
            listaRoot.Add(new XElement(s + "sheetData", listaRows));
            var listaSheet = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), listaRoot);

            var workbookRoot = new XElement(s + "workbook", new XAttribute(XNamespace.Xmlns + "r", r));
            workbookRoot.Add(new XElement(s + "sheets", new XElement(s + "sheet", new XAttribute("name", "Pedido"), new XAttribute("sheetId", "1"), new XAttribute(r + "id", "rId1")),
                new XElement(s + "sheet", new XAttribute("name", "Listas"), new XAttribute("sheetId", "2"), new XAttribute(r + "id", "rId2"), new XAttribute("state", "hidden"))));
            workbookRoot.Add(new XElement(s + "definedNames", new XElement(s + "definedName", new XAttribute("name", "TiposJogador"), $"'Listas'!$A$2:$A${tipos.Count + 1}"),
                new XElement(s + "definedName", new XAttribute("name", "Tamanhos"), $"'Listas'!$B$2:$B${tamanhos.Count + 1}")));
            var workbook = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), workbookRoot);

            var relationships = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(rel + "Relationships",
                new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(rel + "Relationship", new XAttribute("Id", "rId2"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet2.xml")),
                new XElement(rel + "Relationship", new XAttribute("Id", "rId3"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"), new XAttribute("Target", "styles.xml"))));
            var packageRels = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(rel + "Relationships",
                new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
            var contentTypes = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(ct + "Types",
                new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/worksheets/sheet2.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))));
            var stylesRoot = new XElement(s + "styleSheet");
            stylesRoot.Add(new XElement(s + "fonts", new XAttribute("count", "2"),
                new XElement(s + "font", new XElement(s + "sz", new XAttribute("val", "11")), new XElement(s + "name", new XAttribute("val", "Aptos"))),
                new XElement(s + "font", new XElement(s + "b"), new XElement(s + "sz", new XAttribute("val", "11")), new XElement(s + "color", new XAttribute("rgb", "FFFFFFFF")), new XElement(s + "name", new XAttribute("val", "Aptos")))));
            stylesRoot.Add(new XElement(s + "fills", new XAttribute("count", "3"),
                new XElement(s + "fill", new XElement(s + "patternFill", new XAttribute("patternType", "none"))),
                new XElement(s + "fill", new XElement(s + "patternFill", new XAttribute("patternType", "gray125"))),
                new XElement(s + "fill", new XElement(s + "patternFill", new XAttribute("patternType", "solid"), new XElement(s + "fgColor", new XAttribute("rgb", "FF13865A")), new XElement(s + "bgColor", new XAttribute("indexed", "64"))))));
            stylesRoot.Add(new XElement(s + "borders", new XAttribute("count", "2"),
                new XElement(s + "border", new XElement(s + "left"), new XElement(s + "right"), new XElement(s + "top"), new XElement(s + "bottom"), new XElement(s + "diagonal")),
                new XElement(s + "border", new XElement(s + "left", new XAttribute("style", "thin"), new XElement(s + "color", new XAttribute("rgb", "FFD9E2DC"))),
                    new XElement(s + "right", new XAttribute("style", "thin"), new XElement(s + "color", new XAttribute("rgb", "FFD9E2DC"))),
                    new XElement(s + "top", new XAttribute("style", "thin"), new XElement(s + "color", new XAttribute("rgb", "FFD9E2DC"))),
                    new XElement(s + "bottom", new XAttribute("style", "thin"), new XElement(s + "color", new XAttribute("rgb", "FFD9E2DC"))), new XElement(s + "diagonal"))));
            stylesRoot.Add(new XElement(s + "cellStyleXfs", new XAttribute("count", "1"),
                new XElement(s + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"))));
            stylesRoot.Add(new XElement(s + "cellXfs", new XAttribute("count", "2"),
                new XElement(s + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"), new XAttribute("xfId", "0")),
                new XElement(s + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "1"), new XAttribute("fillId", "2"), new XAttribute("borderId", "1"), new XAttribute("xfId", "0"),
                    new XAttribute("applyFont", "1"), new XAttribute("applyFill", "1"), new XAttribute("applyBorder", "1"), new XElement(s + "alignment", new XAttribute("vertical", "center")))));
            stylesRoot.Add(new XElement(s + "cellStyles", new XAttribute("count", "1"),
                new XElement(s + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", "0"), new XAttribute("builtinId", "0"))));
            var styles = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), stylesRoot);

            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var file = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                void Write(string name, XDocument doc)
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using (var stream = entry.Open())
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true })) doc.Save(writer);
                }
                Write("[Content_Types].xml", contentTypes);
                Write("_rels/.rels", packageRels);
                Write("xl/workbook.xml", workbook);
                Write("xl/_rels/workbook.xml.rels", relationships);
                Write("xl/worksheets/sheet1.xml", mainSheet);
                Write("xl/worksheets/sheet2.xml", listaSheet);
                Write("xl/styles.xml", styles);
            }
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

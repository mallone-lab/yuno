using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Corel.Interop.VGCore;

namespace AhcMoldes.Engine
{
    public class ExportResult
    {
        public List<string> Arquivos = new List<string>();
        public List<string> Avisos = new List<string>();
        public string Relatorio;
        public bool Cancelado;
    }

    public static class Exporter
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        public static bool EscPressionado() => (GetAsyncKeyState(0x1B) & 0x8000) != 0;

        /// <param name="umPorObjeto">true: cada objeto selecionado vira um arquivo; false: a seleção inteira vira um arquivo.</param>
        public static ExportResult Exportar(Application app, IList<Shape> itens, string nomeBase, Tecido tecido, bool umPorObjeto,
                                            Config cfg, Action<int, int> progresso = null)
        {
            var doc = app.ActiveDocument;
            var res = new ExportResult();
            if (itens.Count == 0) throw new UserException("Selecione o que deseja exportar.");

            var pasta = cfg.PastaExportacao;
            if (string.IsNullOrWhiteSpace(pasta)) throw new UserException("Defina a pasta de exportação.");
            try { Directory.CreateDirectory(pasta); }
            catch (Exception ex) { throw new UserException($"Não foi possível acessar a pasta \"{pasta}\": {ex.Message}"); }

            var fmt = (cfg.Formato ?? "JPG").ToUpperInvariant();
            string ext = fmt == "PDF" ? ".pdf" : fmt == "PNG" ? ".png" : fmt == "TIF" ? ".tif" : ".jpg";
            var grupos = umPorObjeto ? itens.Select(s => new List<Shape> { s }).ToList() : new List<List<Shape>> { itens.ToList() };

            var original = doc.SelectionRange;
            var relatorio = new StringBuilder();
            relatorio.AppendLine($"Exportação {DateTime.Now:dd/MM/yyyy HH:mm}  —  {doc.Name}");
            if (tecido != null) relatorio.AppendLine($"Tecido: {tecido}");
            relatorio.AppendLine();

            try
            {
                for (int i = 0; i < grupos.Count; i++)
                {
                    if (EscPressionado()) { res.Cancelado = true; break; }
                    progresso?.Invoke(i + 1, grupos.Count);

                    var g = grupos[i];
                    var range = Cdr.Range(app, g);
                    var box = Cdr.BoxOf(range);
                    if (tecido != null && tecido.LarguraCm > 0 && Math.Min(box.W, box.H) > tecido.LarguraCm + 0.01)
                        res.Avisos.Add($"Item {i + 1}: {box.W:0.0} x {box.H:0.0} cm não cabe na largura do {tecido.Nome} ({tecido.LarguraCm:0} cm).");

                    var nome = MontarNome(nomeBase, tecido, grupos.Count > 1 ? i + 1 : 0, box) + ext;
                    var caminho = Path.Combine(pasta, nome);

                    doc.ClearSelection();
                    range.CreateSelection();
                    if (fmt == "PDF") ExportarPdf(doc, caminho, cfg);
                    else ExportarBitmap(app, doc, caminho, fmt, cfg);
                    res.Arquivos.Add(caminho);

                    var cont = Tools.Contar(g);
                    relatorio.AppendLine($"{nome}   ({box.W:0.0} x {box.H:0.0} cm)");
                    foreach (var l in cont) relatorio.AppendLine($"    {l.Tamanho}\t{l.Parte}\t{l.Qtd}");
                }
            }
            finally
            {
                try { doc.ClearSelection(); original?.CreateSelection(); } catch { }
            }

            if (cfg.GerarRelatorio && res.Arquivos.Count > 0)
            {
                var todas = Tools.Contar(itens);
                relatorio.AppendLine();
                relatorio.AppendLine(Tools.RelatorioTexto(todas, cfg.Tamanhos));
                var rp = string.IsNullOrWhiteSpace(cfg.PastaRelatorio) ? pasta : cfg.PastaRelatorio;
                Directory.CreateDirectory(rp);
                res.Relatorio = Path.Combine(rp, MontarNome(nomeBase, tecido, 0, null) + "_relatorio.txt");
                File.WriteAllText(res.Relatorio, relatorio.ToString(), Encoding.UTF8);
            }
            return res;
        }

        static string MontarNome(string nomeBase, Tecido tecido, int idx, Box? box)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(nomeBase)) parts.Add(nomeBase.Trim());
            if (tecido != null && !string.IsNullOrWhiteSpace(tecido.Nome)) parts.Add(tecido.Nome.Trim());
            if (idx > 0) parts.Add(idx.ToString("000"));
            if (box.HasValue) parts.Add($"{box.Value.W:0}x{box.Value.H:0}cm");
            var s = string.Join("_", parts);
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return s.Length == 0 ? "exportacao" : s;
        }

        static void ExportarBitmap(Application app, Document doc, string caminho, string fmt, Config cfg)
        {
            var opt = app.CreateStructExportOptions();
            opt.ResolutionX = cfg.Dpi;
            opt.ResolutionY = cfg.Dpi;
            opt.AntiAliasingType = cdrAntiAliasingType.cdrNormalAntiAliasing;
            opt.Overwrite = true;
            opt.MaintainAspect = true;
            bool cmyk = string.Equals(cfg.Cor, "CMYK", StringComparison.OrdinalIgnoreCase) && fmt != "PNG";
            opt.ImageType = cmyk ? cdrImageType.cdrCMYKColorImage : cdrImageType.cdrRGBColorImage;
            if (fmt == "PNG") opt.Transparent = true;
            if (fmt == "TIF") opt.Compression = cdrCompressionType.cdrCompressionLZW;

            var filtro = fmt == "PNG" ? cdrFilter.cdrPNG : fmt == "TIF" ? cdrFilter.cdrTIFF : cdrFilter.cdrJPEG;
            var f = doc.ExportEx(caminho, filtro, cdrExportRange.cdrSelection, opt, null);
            f.Finish();
        }

        static void ExportarPdf(Document doc, string caminho, Config cfg)
        {
            var p = doc.PDFSettings;
            p.Reset();
            p.PublishRange = pdfExportRange.pdfSelection;
            p.ColorMode = string.Equals(cfg.Cor, "RGB", StringComparison.OrdinalIgnoreCase) ? pdfColorMode.pdfRGB : pdfColorMode.pdfCMYK;
            p.BitmapCompression = string.Equals(cfg.CompressaoPdf, "LZW", StringComparison.OrdinalIgnoreCase)
                ? pdfBitmapCompressionType.pdfLZW : pdfBitmapCompressionType.pdfJPEG;
            p.JPEGQualityFactor = 10;
            p.EmbedFonts = true;
            p.TextAsCurves = true;
            p.Overprints = true;
            if (File.Exists(caminho)) File.Delete(caminho);
            doc.PublishToPDF(caminho);
        }
    }
}

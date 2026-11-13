using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Corel.Interop.VGCore;

namespace Yuno.Engine
{
    public class ExportResult
    {
        public List<string> Arquivos = new List<string>();
        public List<string> Avisos = new List<string>();
        public string Relatorio;
        public string PastaPedido;
        public bool Cancelado;
    }

    public static class Exporter
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        public static bool EscPressionado() => (GetAsyncKeyState(0x1B) & 0x8000) != 0;

        /// <summary>Exporta cada página de tamanho em PDF a 50%, processando uma cópia temporária do documento.</summary>
        public static ExportResult ExportarMontagemPdf50(Application app, string pastaRaiz, string referenciaPedido, Config cfg)
        {
            var original = app?.ActiveDocument;
            if (original == null) throw new UserException("Nenhum documento CorelDRAW está aberto.");
            if (string.IsNullOrWhiteSpace(pastaRaiz)) throw new UserException("Escolha a pasta-base na ferramenta Exportar.");

            var tamanhos = new HashSet<string>((cfg.Tamanhos ?? new List<string>()).Select(OrderImport.NormalizeSize), StringComparer.OrdinalIgnoreCase);
            var paginas = new List<Tuple<int, string, List<string>>>();
            var pedidosEncontrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= original.Pages.Count; i++)
            {
                var page = original.Pages[i];
                if (!ExtrairTamanhoDaPagina(page.Name, tamanhos, out var tamanho)) continue;
                var nomePagina = NomeExportacaoPagina(page.Name);
                var pedidosDaPagina = Cdr.Walk(Cdr.Items(page.Shapes))
                    .Select(s => Tag.Parse(s.Name)).Where(t => t != null && t.Kind == Tag.Peca)
                    .Select(t => t["PED"]).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var p in pedidosDaPagina) pedidosEncontrados.Add(p);
                paginas.Add(Tuple.Create(i, nomePagina, pedidosDaPagina));
            }
            if (paginas.Count == 0) throw new UserException("Não encontrei páginas nomeadas com os tamanhos configurados (M, G, P...). Ative a separação por tamanho ao montar o pedido.");

            var pedido = (referenciaPedido ?? "").Trim();
            if (pedido.Length == 0 && pedidosEncontrados.Count == 1) pedido = pedidosEncontrados.First();
            if (pedido.Length == 0 && pedidosEncontrados.Count > 1)
                throw new UserException("Encontrei mais de um pedido nas páginas. Informe o nome do pedido no campo Pedido antes de exportar.");
            if (pedido.Length == 0) pedido = Path.GetFileNameWithoutExtension(original.FileName ?? "");
            if (string.IsNullOrWhiteSpace(pedido)) throw new UserException("Informe o nome do pedido para criar a pasta e nomear os PDFs.");

            string pastaPedido = pedido.StartsWith("PEDIDO ", StringComparison.OrdinalIgnoreCase) ? pedido : "PEDIDO " + pedido;
            pastaPedido = NomeSeguro(pastaPedido);
            string destino = Path.Combine(pastaRaiz, pastaPedido);
            try { Directory.CreateDirectory(destino); }
            catch (Exception ex) { throw new UserException("Não foi possível criar a pasta do pedido: " + ex.Message); }

            var res = new ExportResult { PastaPedido = pastaPedido };
            Document copia = null;
            try
            {
                copia = original.Duplicate();
                var repeticoes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var info in paginas)
                {
                    var page = copia.Pages[info.Item1];
                    page.Activate();
                    var formas = Cdr.Items(page.Shapes).Where(s => !s.Locked).ToList();
                    if (formas.Count == 0)
                    {
                        res.Avisos.Add($"Página {info.Item2}: não há objetos desbloqueados para exportar.");
                        continue;
                    }
                    foreach (var forma in formas) forma.MoveToLayer(page.ActiveLayer);
                    var grupo = Cdr.Range(app, formas).Group();
                    var box = Cdr.BoxOf(grupo);
                    grupo.SetSizeEx(box.Cx, box.Cy, box.W * 0.5, box.H * 0.5);
                    copia.ClearSelection();
                    grupo.AddToSelection();

                    int repeticao = repeticoes.TryGetValue(info.Item2, out var atual) ? atual + 1 : 1;
                    repeticoes[info.Item2] = repeticao;
                    string sufixo = repeticao > 1 ? " (" + repeticao + ")" : "";
                    string nome = NomeSeguro(pastaPedido + " - " + info.Item2 + sufixo) + ".pdf";
                    string caminho = Path.Combine(destino, nome);
                    ExportarPdf(copia, caminho, cfg);
                    res.Arquivos.Add(caminho);
                }
                if (res.Arquivos.Count == 0) throw new UserException("Não consegui exportar nenhum dos tamanhos encontrados.");
                return res;
            }
            catch (UserException) { throw; }
            catch (Exception ex) { throw new UserException("Falha ao gerar os PDFs do pedido: " + ex.Message); }
            finally
            {
                if (copia != null)
                {
                    try { copia.Dirty = false; } catch { }
                    try { copia.Close(); } catch { }
                }
            }
        }

        private static string NomeSeguro(string valor)
        {
            var s = (valor ?? "").Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return s.Trim().TrimEnd('.');
        }

        private static bool ExtrairTamanhoDaPagina(string nomePagina, HashSet<string> tamanhos, out string tamanho)
        {
            tamanho = null;
            var nome = NomeExportacaoPagina(nomePagina);
            var normalizado = OrderImport.NormalizeSize(nome);
            foreach (var candidato in tamanhos.OrderByDescending(x => x.Length))
            {
                if (string.Equals(normalizado, candidato, StringComparison.OrdinalIgnoreCase) ||
                    (normalizado.StartsWith(candidato + "-", StringComparison.OrdinalIgnoreCase) && normalizado.Length > candidato.Length + 1))
                {
                    tamanho = candidato;
                    return true;
                }
            }
            return false;
        }

        private static string NomeExportacaoPagina(string nomePagina)
        {
            var nome = (nomePagina ?? "").Trim();
            int doisPontos = nome.IndexOf(':');
            if (doisPontos > 0 && nome.Substring(0, doisPontos).All(char.IsDigit))
                nome = nome.Substring(doisPontos + 1).Trim();
            return nome;
        }

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

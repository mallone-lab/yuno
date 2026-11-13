using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Corel.Interop.VGCore;

namespace Yuno.Engine
{
    public sealed class OrderServiceResult
    {
        public string PdfPath;
        public string OrderFolder;
        public string Aviso;
        public int Uniformes;
        public int VariacoesCor;
    }

    internal sealed class OsUniforme
    {
        public Tag Tag;
        public List<Shape> Pecas;
        public string Pagina;
        public string Tipo;
        public string Categoria;
        public string TamanhoCamiseta;
        public string TamanhoCalcao;
        public string Paleta;
    }

    internal sealed class OsPrevia
    {
        public string Tipo;
        public List<Shape> Camiseta = new List<Shape>();
        public List<Shape> Calcao = new List<Shape>();
        public string CaminhoCamiseta;
        public string CaminhoCalcao;
        public double? MangaEsquerdaY;
        public double? MangaDireitaY;
    }

    internal sealed class OsItem
    {
        public string Tipo, Nome, Numero, Adicional, Camiseta, Calcao;
        public int Quantidade;
    }

    public static class OrderServiceReport
    {
        public static string LogoFolder => Path.Combine(Config.Folder, "OS", "LOGO");

        public static OrderServiceResult Gerar(Application app, Document origem, Config cfg, string nomePedido,
                                               string tecido, DateTime? dataEntrega, string tipoGola, string acabamentoGola, string punhoManga,
                                               string modeloCalcao, string bolsoCalcao, string detalhesCalcao,
                                               string tipoEtiqueta, string observacoes, IList<OrderLine> linhasPedido)
        {
            if (app == null || origem == null) throw new UserException("Abra o documento da montagem antes de gerar a OS.");
            if (string.IsNullOrWhiteSpace(cfg.PastaExportacao)) throw new UserException("Defina a pasta de saída na ferramenta Exportar.");

            var pecas = new List<Tuple<Shape, Tag, string>>();
            var pecasImagens = new List<Tuple<Shape, Tag, string>>();
            var pedidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int p = 1; p <= origem.Pages.Count; p++)
            {
                var pagina = origem.Pages[p];
                int antes = pecasImagens.Count;
                foreach (var raiz in Cdr.Items(pagina.Shapes))
                    ColetarPecasMarcadas(raiz, pagina.Name, pecas, pecasImagens, pedidos);
                int pecasMarcadas = pecasImagens.Count - antes;

                // Documentos antigos podem ter a página nomeada por peça/tipo,
                // mas sem as etiquetas YUNO nos objetos. Nesse caso, usa as formas
                // da própria página como uma única amostra do modelo.
                if (pecasMarcadas == 0 && CategoriaPagina(pagina.Name).Length > 0)
                {
                    var formasVisuais = Cdr.Items(pagina.Shapes).Where(s => s.Type != cdrShapeType.cdrTextShape).ToList();
                    var tamanhoPagina = TamanhoNoNomeDaPagina(pagina.Name);
                    var tipoPagina = Tipo(new Tag(Tag.Peca), pagina.Name);
                    for (int i = 0; i < formasVisuais.Count; i++)
                    {
                        var tagFallback = new Tag(Tag.Peca);
                        tagFallback["T"] = tamanhoPagina;
                        tagFallback["SEQ"] = "1";
                        tagFallback["TIPO"] = tipoPagina;
                        pecasImagens.Add(Tuple.Create(formasVisuais[i], tagFallback, PaginaDaForma(formasVisuais[i], pagina.Name)));
                    }
                }
            }
            if (pecas.Count == 0) throw new UserException("Não encontrei peças YUNO montadas neste documento.");

            var pedido = (nomePedido ?? "").Trim();
            if (pedido.Length == 0 && pedidos.Count == 1) pedido = pedidos.First();
            var referenciaLimpa = pedido.StartsWith("PEDIDO ", StringComparison.OrdinalIgnoreCase) ? pedido.Substring(7).Trim() : pedido;
            var selecionadas = pecas.Where(x => string.Equals(x.Item2["PED"].Trim(), pedido, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(x.Item2["PED"].Trim(), referenciaLimpa, StringComparison.OrdinalIgnoreCase)).ToList();
            string avisoFonte = null;
            if (selecionadas.Count == 0)
            {
                var correspondentes = EncontrarPorLinhas(pecas, linhasPedido);
                if (correspondentes.Count > 0)
                {
                    selecionadas = correspondentes;
                    avisoFonte = "As peças foram localizadas pelo tipo, nome, número e tamanhos do pedido.";
                }
            }
            if (selecionadas.Count == 0)
            {
                var selectionIds = new HashSet<int>();
                try
                {
                    var selection = app.ActiveSelectionRange;
                    if (selection != null && selection.Count > 0)
                        foreach (var s in Cdr.Walk(Cdr.Items(selection))) selectionIds.Add(s.StaticID);
                }
                catch { }
                var pecasSelecionadas = pecas.Where(x => selectionIds.Contains(x.Item1.StaticID)).ToList();
                var tamanhosSelecionados = new HashSet<string>(pecasSelecionadas.Select(x => x.Item2["T"]), StringComparer.OrdinalIgnoreCase);
                var paginaAtiva = origem.ActivePage;
                var pecasDaPagina = pecas.Where(x => string.Equals(x.Item3, paginaAtiva.Name, StringComparison.OrdinalIgnoreCase) &&
                    (tamanhosSelecionados.Count == 0 || tamanhosSelecionados.Contains(x.Item2["T"]))).ToList();
                selecionadas = pecasDaPagina.Count > 0 ? pecasDaPagina : pecasSelecionadas;
                if (selecionadas.Count > 0)
                    avisoFonte = pecasDaPagina.Count > 0
                        ? "A OS foi montada com as peças do tamanho selecionado na página ativa, pois a referência digitada não correspondeu à referência das peças."
                        : "A OS foi montada a partir dos moldes YUNO selecionados, pois a referência digitada não correspondeu à referência gravada nas peças.";
            }
            if (selecionadas.Count == 0)
            {
                var ativa = origem.ActivePage;
                selecionadas = pecas.Where(x => string.Equals(x.Item3, ativa.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (selecionadas.Count > 0)
                    avisoFonte = "A OS foi montada a partir das peças YUNO da página ativa, pois a referência digitada não correspondeu às peças.";
            }
            if (selecionadas.Count == 0 && pedidos.Count == 1)
            {
                selecionadas = pecas;
                pedido = pedidos.First();
                avisoFonte = "A OS usou o único pedido encontrado no documento.";
            }
            if (selecionadas.Count == 0)
                throw new UserException("Não encontrei peças YUNO compatíveis. Selecione o bloco montado na página e gere a OS novamente.");
            if (string.IsNullOrWhiteSpace(pedido))
            {
                var pedidosSelecionados = selecionadas.Select(x => x.Item2["PED"].Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (pedidosSelecionados.Count == 1) pedido = pedidosSelecionados[0];
            }
            if (string.IsNullOrWhiteSpace(pedido)) throw new UserException("Informe o nome do pedido na aba OS para nomear o PDF.");
            pedido = pedido.ToUpperInvariant();

            var uniformes = CriarUniformes(selecionadas, cfg);
            // A galeria da OS representa os modelos existentes no documento inteiro.
            // Não filtrar aqui pelo pedido gravado em cada peça: documentos já montados
            // podem ter metadados PED inconsistentes entre páginas, embora todas elas
            // pertençam visualmente ao mesmo trabalho. CriarPreviews elimina tamanho e
            // sufixo de cópia, mantendo uma amostra por nome/tipo de peça.
            var modelos = CriarPreviews(CriarUniformes(pecasImagens, cfg));
            if (!TemCalcaoPedido(linhasPedido, cfg))
                foreach (var modelo in modelos) modelo.Calcao.Clear();
            modelos = modelos.Where(m => m.Camiseta.Count > 0 || m.Calcao.Count > 0).ToList();
            if (modelos.Count == 0) throw new UserException("Não foi possível identificar camisetas e calções para ilustrar a OS.");

            string pastaPedido = NomeSeguro(pedido.StartsWith("PEDIDO ", StringComparison.OrdinalIgnoreCase) ? pedido : "PEDIDO " + pedido);
            string destino = Path.Combine(cfg.PastaExportacao, pastaPedido);
            try { Directory.CreateDirectory(destino); Directory.CreateDirectory(LogoFolder); }
            catch (Exception ex) { throw new UserException("Não foi possível criar a pasta da OS: " + ex.Message); }

            string logo = Directory.GetFiles(LogoFolder)
                .Where(f => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            string temp = Path.Combine(Path.GetTempPath(), "YunoOS-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            Document relatorio = null;
            string etapa = "preparar imagens de referência";
            try
            {
                var paginaOriginal = origem.ActivePage;
                var selecaoOriginal = origem.SelectionRange;
                try
                {
                    for (int i = 0; i < modelos.Count; i++)
                    {
                        var camisetaPng = Path.Combine(temp, "camiseta-" + (i + 1).ToString("00") + ".png");
                        var calcaoPng = Path.Combine(temp, "calcao-" + (i + 1).ToString("00") + ".png");
                        if (modelos[i].Camiseta.Count > 0)
                        {
                            etapa = "capturar CAMISETA - " + modelos[i].Tipo;
                            ExportarPrevia(app, origem, modelos[i].Camiseta, camisetaPng, cfg.Dpi);
                        }
                        if (modelos[i].Calcao.Count > 0)
                        {
                            etapa = "capturar CALÇÃO - " + modelos[i].Tipo;
                            ExportarPrevia(app, origem, modelos[i].Calcao, calcaoPng, cfg.Dpi);
                        }
                        modelos[i].CaminhoCamiseta = modelos[i].Camiseta.Count > 0 ? camisetaPng : null;
                        modelos[i].CaminhoCalcao = modelos[i].Calcao.Count > 0 ? calcaoPng : null;
                    }
                }
                finally
                {
                    try { paginaOriginal.Activate(); } catch { }
                    try { origem.ClearSelection(); selecaoOriginal?.CreateSelection(); } catch { }
                }

                etapa = "criar o documento da OS";
                relatorio = app.CreateDocument();
                relatorio.Unit = cdrUnit.cdrCentimeter;
                var titulo = pedido.StartsWith("PEDIDO ", StringComparison.OrdinalIgnoreCase) ? pedido : "PEDIDO " + pedido;
                if (titulo.Length > 76) titulo = titulo.Substring(0, 73) + "...";

                var itens = CriarItens(linhasPedido, uniformes, cfg);
                if (itens.Count == 0) throw new UserException("Não encontrei itens da planilha nem peças montadas para detalhar a OS.");

                etapa = "montar as informações da OS";
                var capa = relatorio.ActivePage;
                capa.SetSize(21, 29.7);
                capa.Name = "OS";
                var icones = CriarIconesOS(temp);
                bool resumoNaCapa;
                double topoFotosCapa;
                int primeiraTabela = Capa(app, capa, logo, titulo, tecido, dataEntrega, tipoGola, acabamentoGola, punhoManga,
                    modeloCalcao, bolsoCalcao, detalhesCalcao, tipoEtiqueta, observacoes, itens, icones,
                    out resumoNaCapa, out topoFotosCapa);

                // As fotos começam sempre na segunda página e ficam sozinhas nela.
                etapa = "montar a página de fotos";
                AdicionarPaginasFotos(app, relatorio, modelos);
                const int linhasPagina = 17;
                int paginaDetalhe = 0;
                for (int inicio = primeiraTabela; inicio < itens.Count; inicio += linhasPagina)
                {
                    var page = NovaPagina(relatorio, "Itens " + (++paginaDetalhe));
                    TituloPagina(app, page, "DETALHAMENTO DO PEDIDO", logo);
                    TextoCor(app, page.ActiveLayer, titulo, 2, 25.7, 17, 22, "Arial", true, 100, 70, 0, 10);
                    DesenharTabela(app, page, itens, inicio, Math.Min(inicio + linhasPagina, itens.Count), 24.1,
                        itens.Any(x => !string.IsNullOrWhiteSpace(x.Calcao)));
                }

                if (!resumoNaCapa)
                {
                    var resumo = NovaPagina(relatorio, "Resumo e assinatura");
                    TituloPagina(app, resumo, "RESUMO DO PEDIDO", logo);
                    TextoCor(app, resumo.ActiveLayer, titulo, 2, 25.4, 17, 24, "Arial", true, 100, 70, 0, 10);
                    DesenharResumoAssinatura(app, resumo, itens, 22.8);
                }

                string nomePdf = NomeSeguro("OS - " + pastaPedido) + ".pdf";
                string pdfPath = Path.Combine(destino, nomePdf);
                etapa = "publicar o PDF";
                PublicarPDF(relatorio, pdfPath, cfg);
                relatorio.Dirty = false;
                relatorio.Close();
                relatorio = null;
                return new OrderServiceResult { PdfPath = pdfPath, OrderFolder = destino, Aviso = avisoFonte, Uniformes = uniformes.Count, VariacoesCor = modelos.Count };
            }
            catch (UserException) { throw; }
            catch (Exception ex) { throw new UserException($"Falha ao {etapa}: {ex.Message}"); }
            finally
            {
                if (relatorio != null) { try { relatorio.Dirty = false; relatorio.Close(); } catch { } }
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static void ColetarPecasMarcadas(Shape shape, string pagina,
            List<Tuple<Shape, Tag, string>> pecas,
            List<Tuple<Shape, Tag, string>> pecasImagens,
            HashSet<string> pedidos)
        {
            if (shape == null || shape.Type == cdrShapeType.cdrGuidelineShape) return;
            var tag = Tag.Parse(shape.Name);
            if (tag?.Kind == Tag.Peca)
            {
            var paginaForma = PaginaDaForma(shape, pagina);
            var item = Tuple.Create(shape, tag, paginaForma);
                pecas.Add(item);
                pecasImagens.Add(item);
                var pedidoTag = tag["PED"].Trim();
                if (pedidoTag.Length > 0) pedidos.Add(pedidoTag);
                // Se o grupo já representa uma peça, não envie também os filhos
                // marcados: selecionar pai e descendentes provoca E_FAIL no Corel.
                return;
            }
            if (shape.Type != cdrShapeType.cdrGroupShape) return;
            foreach (var filho in Cdr.Items(shape.Shapes))
                ColetarPecasMarcadas(filho, pagina, pecas, pecasImagens, pedidos);
        }

        private static string PaginaDaForma(Shape shape, string alternativa)
        {
            try { return shape.Page?.Name ?? alternativa ?? ""; }
            catch { return alternativa ?? ""; }
        }

        private static int Capa(Application app, Page page, string logo, string titulo,
                                string tecido, DateTime? dataEntrega, string tipoGola, string acabamentoGola, string punhoManga,
                                string modeloCalcao, string bolsoCalcao, string detalhesCalcao, string tipoEtiqueta,
                                string observacoes, List<OsItem> itens, IDictionary<string, string> icones,
                                out bool resumoNaCapa, out double topoFotos)
        {
            if (!string.IsNullOrWhiteSpace(logo)) ImportarImagem(app, page, logo, 5.0, 2.4, 16.3, 26.8);
            TextoCor(app, page.ActiveLayer, "INFORMAÇÕES DO PEDIDO", 2, 27.5, 11.2, 15, "Arial", true, 100, 70, 0, 0);
            TextoCor(app, page.ActiveLayer, titulo, 2, 25.8, 11.4, 24, "Arial", true, 100, 70, 0, 0, 1.55);
            InserirIcone(app, page, icones, "entrega", 2.2, 23.65);
            Texto(app, page.ActiveLayer, "ENTREGA: " + FormatarData(dataEntrega), 2.55, 23.9, 7.6, 11.5f, "Arial", .55);
            InserirIcone(app, page, icones, "tecido", 10.7, 23.65);
            Texto(app, page.ActiveLayer, "TECIDO: " + ValorOu(tecido, "NÃO INFORMADO").ToUpperInvariant(), 11.05, 23.9, 7.8, 11.5f, "Arial", .55);

            TextoCor(app, page.ActiveLayer, "CAMISETA", 2, 22.75, 17, 11.5f, "Arial", true, 100, 70, 0, 5);
            InserirIcone(app, page, icones, "gola", 2.2, 22.05);
            Texto(app, page.ActiveLayer, "GOLA: " + ValorOu(tipoGola, "NÃO INFORMADA").ToUpperInvariant(), 2.55, 22.3, 7.6, 11.5f, "Arial", .55);
            InserirIcone(app, page, icones, "punho", 10.7, 22.05);
            Texto(app, page.ActiveLayer, "MANGA: " + ValorOu(punhoManga, "NÃO INFORMADO").ToUpperInvariant(), 11.05, 22.3, 7.8, 11.5f, "Arial", .55);
            InserirIcone(app, page, icones, "gola", 2.2, 21.05);
            Texto(app, page.ActiveLayer, "ACABAMENTO DA GOLA: " + ValorOu(acabamentoGola, "NÃO INFORMADO").ToUpperInvariant(), 2.55, 21.3, 7.6, 11.5f, "Arial", .55);
            InserirIcone(app, page, icones, "etiqueta", 10.7, 21.05);
            Texto(app, page.ActiveLayer, ValorOu(tipoEtiqueta, "SEM ETIQUETA").ToUpperInvariant(), 11.05, 21.3, 7.8, 11.5f, "Arial", .55);

            bool temCalcao = itens.Any(x => !string.IsNullOrWhiteSpace(x.Calcao));
            double cursorInfo = 20.45;
            if (temCalcao)
            {
                const int verdeC = 88, verdeM = 35, verdeY = 90, verdeK = 35;
                TextoCor(app, page.ActiveLayer, "CALÇÃO", 2, cursorInfo, 17, 11.5f, "Arial", true, verdeC, verdeM, verdeY, verdeK);
                cursorInfo -= .52;
                InserirIcone(app, page, icones, "calcao", 2.2, cursorInfo - .25);
                TextoCor(app, page.ActiveLayer, "CONSTRUÇÃO: " + ValorOu(modeloCalcao, "NÃO INFORMADA").ToUpperInvariant(), 2.55, cursorInfo, 7.6, 11.2f, "Arial", true, verdeC, verdeM, verdeY, verdeK, .55);
                InserirIcone(app, page, icones, "bolso", 10.7, cursorInfo - .25);
                TextoCor(app, page.ActiveLayer, "BOLSO: " + ValorOu(bolsoCalcao, "NÃO INFORMADO").ToUpperInvariant(), 11.05, cursorInfo, 7.8, 11.2f, "Arial", true, verdeC, verdeM, verdeY, verdeK, .55);
                cursorInfo -= .78;
                if (!string.IsNullOrWhiteSpace(detalhesCalcao))
                {
                    InserirIcone(app, page, icones, "detalhes", 2.2, cursorInfo - .25);
                    TextoCor(app, page.ActiveLayer, "DETALHES: " + detalhesCalcao.Trim().ToUpperInvariant(), 2.55, cursorInfo, 16.3, 10.5f, "Arial", true, verdeC, verdeM, verdeY, verdeK, .65);
                    cursorInfo -= .82;
                }
            }
            if (!string.IsNullOrWhiteSpace(observacoes))
            {
                InserirIcone(app, page, icones, "observacoes", 2.2, cursorInfo - .25);
                Texto(app, page.ActiveLayer, "OBSERVAÇÕES: " + observacoes.Trim().ToUpperInvariant(), 2.55, cursorInfo, 16.3, 10.5f, "Arial", .65);
                cursorInfo -= .85;
            }
            double topoTabela = cursorInfo - .45;
            InserirIcone(app, page, icones, "camiseta", 2.2, topoTabela + .35);
            TextoCor(app, page.ActiveLayer, "ITENS DO PEDIDO", 2.55, topoTabela + .6, 16.4, 12.5f, "Arial", true, 100, 70, 0, 0);
            int capacidadeCapa = Math.Max(1, (int)Math.Floor((topoTabela - .55 - 2.0) / .72));
            int end = Math.Min(capacidadeCapa, itens.Count);
            DesenharTabela(app, page, itens, 0, end, topoTabela, temCalcao);
            DesenharCheckboxImpresso(app, page);
            double topoResumo = topoTabela - .55 - end * .72 - .5;
            resumoNaCapa = end == itens.Count && topoResumo - AlturaResumo(itens) >= 5.8;
            if (resumoNaCapa) DesenharResumoAssinatura(app, page, itens, topoResumo);
            topoFotos = resumoNaCapa ? topoResumo - AlturaResumo(itens) - .45 : 0;
            return end;
        }

        private static void DesenharCheckboxImpresso(Application app, Page page)
        {
            const double y = 1.18, lado = .32;
            var layer = page.ActiveLayer;
            double centroY = y + lado / 2;
            TextoCentroCor(app, layer, "PEDIDO IMPRESSO?", 8.675, centroY, 2.65, 9, "Arial", true, 0, 0, 0, 100);
            DesenharCaixa(layer, 10.18, y, lado);
            TextoCentro(app, layer, "SIM", 10.895, centroY, .55, 8.5f, "Arial");
            DesenharCaixa(layer, 11.55, y, lado);
            TextoCentro(app, layer, "NÃO", 12.315, centroY, .65, 8.5f, "Arial");
        }
        private static void DesenharCaixa(Layer layer, double x, double y, double lado)
        {
            layer.CreateLineSegment(x, y, x + lado, y);
            layer.CreateLineSegment(x + lado, y, x + lado, y + lado);
            layer.CreateLineSegment(x + lado, y + lado, x, y + lado);
            layer.CreateLineSegment(x, y + lado, x, y);
        }

        private static void AdicionarPaginasFotos(Application app, Document doc, IList<OsPrevia> modelos)
        {
            if (modelos == null || modelos.Count == 0) return;
            const int maximoPorPagina = 4;
            const double margem = .8, topoGrade = 27.5, baseGrade = .8, vaoX = .65, vaoY = .45;
            int numeroPaginas = (int)Math.Ceiling(modelos.Count / (double)maximoPorPagina);
            for (int paginaIndex = 0; paginaIndex < numeroPaginas; paginaIndex++)
            {
                var page = NovaPagina(doc, paginaIndex == 0 ? "Modelos" : "Modelos " + (paginaIndex + 1));
                var modelosPagina = modelos.Skip(paginaIndex * maximoPorPagina).Take(maximoPorPagina).ToList();
                int colunas = Math.Min(2, (int)Math.Ceiling(modelosPagina.Count / 2.0));
                int linhas = Math.Min(2, (int)Math.Ceiling(modelosPagina.Count / (double)colunas));
                double larguraCelula = (21.0 - 2 * margem - (colunas - 1) * vaoX) / colunas;
                double alturaCelula = (topoGrade - baseGrade - (linhas - 1) * vaoY) / linhas;
                for (int i = 0; i < modelosPagina.Count; i++)
                {
                    // Primeiro preenche dois moldes na coluna esquerda; os próximos dois vão à direita.
                    int coluna = i / 2;
                    int linha = i % 2;
                    double centroX = margem + coluna * (larguraCelula + vaoX) + larguraCelula / 2;
                    double topoCelula = topoGrade - linha * (alturaCelula + vaoY);
                    double baseCelula = topoCelula - alturaCelula;
                    var modelo = modelosPagina[i];
                    bool temCamiseta = !string.IsNullOrWhiteSpace(modelo.CaminhoCamiseta);
                    bool temCalcao = !string.IsNullOrWhiteSpace(modelo.CaminhoCalcao);
                    double larguraImagem = larguraCelula - (colunas == 1 ? .5 : .8);
                    double centroCamiseta = centroX - (colunas == 1 ? 0 : .2);
                    if (temCamiseta && temCalcao)
                    {
                        double metade = alturaCelula / 2;
                        double alturaFoto = Math.Max(2.0, metade - .72);
                        DesenharTituloModelo(app, page, "CAMISETA", modelo.Tipo, centroX, topoCelula - .08, larguraCelula);
                        var fotoCamiseta = InserirFotoModelo(app, page, modelo.CaminhoCamiseta, larguraImagem, alturaFoto,
                            centroCamiseta, topoCelula - metade / 2 - .28);
                        AdicionarLadosManga(app, page, fotoCamiseta, modelo);
                        DesenharTituloModelo(app, page, "CALÇÃO", modelo.Tipo, centroX, topoCelula - metade - .08, larguraCelula);
                        InserirFotoModelo(app, page, modelo.CaminhoCalcao, larguraImagem, alturaFoto, centroX,
                            baseCelula + metade / 2 - .26);
                    }
                    else
                    {
                        string caminho = temCamiseta ? modelo.CaminhoCamiseta : modelo.CaminhoCalcao;
                        DesenharTituloModelo(app, page, temCamiseta ? "CAMISETA" : "CALÇÃO", modelo.Tipo, centroX, topoCelula - .08, larguraCelula);
                        var imagem = InserirFotoModelo(app, page, caminho, larguraImagem,
                            Math.Max(2.2, alturaCelula - .8), temCamiseta ? centroCamiseta : centroX,
                            (topoCelula + baseCelula) / 2 - .18);
                        if (temCamiseta) AdicionarLadosManga(app, page, imagem, modelo);
                    }
                }
            }
        }

        private static Shape InserirFotoModelo(Application app, Page page, string caminho, double largura, double altura,
                                              double centroX, double centroY)
        {
            var imagem = ImportarImagem(app, page, caminho, largura, altura, centroX, centroY);
            if (imagem == null) throw new UserException("Não foi possível inserir uma imagem de molde na OS.");
            return imagem;
        }

        private static double? PosicaoRelativaDaManga(IList<Shape> camisa, string lado)
        {
            try
            {
                var partes = camisa.Select(s => new { Shape = s, Tag = Tag.Parse(s.Name) })
                    .Where(x => x.Tag?.Kind == Tag.Peca && LadoManga(x.Tag["A"]) == lado)
                    .Select(x => x.Shape).ToList();
                if (partes.Count == 0) return null;
                var total = CaixaUniao(camisa);
                var manga = CaixaUniao(partes);
                if (total.H <= 0) return null;
                return Math.Max(0, Math.Min(1, (manga.Cy - total.B) / total.H));
            }
            catch { return null; }
        }

        private static Box CaixaUniao(IList<Shape> shapes)
        {
            var caixas = shapes.Select(Cdr.BoxOf).ToList();
            double esquerda = caixas.Min(b => b.L), baixo = caixas.Min(b => b.B);
            double direita = caixas.Max(b => b.R), topo = caixas.Max(b => b.T);
            return new Box { L = esquerda, B = baixo, W = direita - esquerda, H = topo - baixo };
        }

        private static string LadoManga(string apelido)
        {
            var lado = new string((apelido ?? "").Where(char.IsLetter).ToArray()).ToUpperInvariant().Replace("MANGA", "");
            if (lado == "ESQUERDA" || lado == "ESQ") return "ESQUERDA";
            if (lado == "DIREITA" || lado == "DIR") return "DIREITA";
            return "";
        }

        private static void AdicionarLadosManga(Application app, Page page, Shape imagem, OsPrevia modelo)
        {
            if (imagem == null) return;
            var esquerda = modelo.MangaEsquerdaY;
            var direita = modelo.MangaDireitaY;
            if (!esquerda.HasValue && !direita.HasValue) return;
            var b = Cdr.BoxOf(imagem);
            double yEsquerda = esquerda.HasValue ? b.B + esquerda.Value * b.H : 0;
            double yDireita = direita.HasValue ? b.B + direita.Value * b.H : 0;
            if (esquerda.HasValue && direita.HasValue && Math.Abs(yEsquerda - yDireita) < 1.15)
            {
                yEsquerda = Math.Min(b.T - .45, yEsquerda + .55);
                yDireita = Math.Max(b.B + .45, yDireita - .55);
            }
            double x = Math.Min(page.SizeWidth - .35, b.R + .38);
            if (esquerda.HasValue) InserirRotuloManga(app, page, "ESQUERDA", x, yEsquerda);
            if (direita.HasValue) InserirRotuloManga(app, page, "DIREITA", x, yDireita);
        }

        private static void InserirRotuloManga(Application app, Page page, string texto, double x, double y)
        {
            var rotulo = Cdr.CreateText(app, page.ActiveLayer, texto, "Arial", 10);
            rotulo.Text.Story.Bold = true;
            var box = Cdr.BoxOf(rotulo);
            rotulo.RotateEx(90, box.Cx, box.Cy);
            rotulo.SetPositionEx(cdrReferencePoint.cdrCenter, x, y);
        }

        private static double AlturaResumo(List<OsItem> itens)
        {
            double altura = itens.Any(x => !string.IsNullOrWhiteSpace(x.Calcao)) ? 3.6 : 2.9;
            foreach (var g in itens.GroupBy(x => x.Tipo))
            {
                altura += .75;
                if (g.Any(x => !string.IsNullOrWhiteSpace(x.Camiseta))) altura += .65;
                if (g.Any(x => !string.IsNullOrWhiteSpace(x.Calcao))) altura += .65;
                altura += .15;
            }
            return altura;
        }

        private static void DesenharResumoAssinatura(Application app, Page page, List<OsItem> itens, double topo)
        {
            var layer = page.ActiveLayer;
            TextoCor(app, layer, "TOTAL DE UNIFORMES: " + itens.Sum(x => x.Quantidade), 2, topo, 17, 14, "Arial", true, 100, 70, 0, 10);
            topo -= .75;
            TextoCor(app, layer, "TOTAIS POR PEÇA E TAMANHO", 2, topo, 17, 11.5f, "Arial", true, 100, 70, 0, 0);
            topo -= .7;
            var camisetasTotal = itens.Where(x => !string.IsNullOrWhiteSpace(x.Camiseta)).Sum(x => x.Quantidade);
            var camisetasTamanho = ResumoTamanhos(itens.Where(x => !string.IsNullOrWhiteSpace(x.Camiseta)), x => x.Camiseta);
            Texto(app, layer, $"CAMISETAS: {camisetasTotal}   |   TAMANHOS: {camisetasTamanho}", 2.1, topo, 17, 10.5f, "Arial", .5); topo -= .58;
            var calcoesTotal = itens.Where(x => !string.IsNullOrWhiteSpace(x.Calcao)).Sum(x => x.Quantidade);
            if (calcoesTotal > 0)
            {
                var calcoesTamanho = ResumoTamanhos(itens.Where(x => !string.IsNullOrWhiteSpace(x.Calcao)), x => x.Calcao);
                Texto(app, layer, $"CALÇÕES: {calcoesTotal}   |   TAMANHOS: {calcoesTamanho}", 2.1, topo, 17, 10.5f, "Arial", .5); topo -= .72;
            }
            TextoCor(app, layer, "RESUMO POR TIPO", 2, topo, 17, 11.5f, "Arial", true, 100, 70, 0, 0);
            topo -= .68;
            foreach (var g in itens.GroupBy(x => x.Tipo).OrderBy(g => g.Key))
            {
                TextoCor(app, layer, (string.IsNullOrWhiteSpace(g.Key) ? "UNIFORMES" : g.Key.ToUpperInvariant()) + " — " + g.Sum(x => x.Quantidade) + " PEÇA(S)", 2, topo, 17, 11.5f, "Arial", true, 100, 70, 0, 10);
                topo -= .58;
                var camisetas = g.Where(x => !string.IsNullOrWhiteSpace(x.Camiseta)).GroupBy(x => x.Camiseta).OrderBy(x => x.Key)
                    .Select(x => x.Key + "=" + x.Sum(y => y.Quantidade)).ToList();
                var calcoes = g.Where(x => !string.IsNullOrWhiteSpace(x.Calcao)).GroupBy(x => x.Calcao).OrderBy(x => x.Key)
                    .Select(x => x.Key + "=" + x.Sum(y => y.Quantidade)).ToList();
                if (camisetas.Count > 0) { Texto(app, layer, "Camisetas: " + string.Join("   ", camisetas), 2.4, topo, 16, 10, "Arial", .45); topo -= .55; }
                if (calcoes.Count > 0) { Texto(app, layer, "Calções: " + string.Join("   ", calcoes), 2.4, topo, 16, 10, "Arial", .45); topo -= .55; }
                topo -= .12;
            }
            Texto(app, layer, "Assinatura do responsável", 2, 3.35, 8.5, 10, "Arial");
            layer.CreateLineSegment(2, 2.55, 10.5, 2.55);
            Texto(app, layer, "Data: ____ / ____ / ______", 12, 3.35, 7, 10, "Arial");
            layer.CreateLineSegment(12, 2.55, 19, 2.55);
        }

        private static string ResumoTamanhos(IEnumerable<OsItem> itens, Func<OsItem, string> obterTamanho)
            => string.Join("   ", itens.GroupBy(x => OrderImport.NormalizeSize(obterTamanho(x)), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Key.Length > 0)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Key + "=" + g.Sum(x => x.Quantidade)));

        private static void DesenharTabela(Application app, Page page, List<OsItem> itens, int inicio, int fim, double topo, bool mostrarCalcao = true)
        {
            var layer = page.ActiveLayer;
            var indices = mostrarCalcao ? new[] { 0, 1, 2, 3, 4, 5, 6 } : new[] { 0, 1, 2, 3, 4, 6 };
            double[] larguraBase = mostrarCalcao
                ? new[] { 2.1, 3.3, 0.8, 3.8, 2.2, 2.2, 2.6 }
                : new[] { 2.1, 5.5, 0.8, 3.8, 2.2, 2.6 };
            string[] cabecalhosBase = { "TIPO", "NOME", "Nº", "ADICIONAL", "TAM. CAM.", "TAM. CAL.", "QTDE." };
            double[] xs = new double[indices.Length + 1]; xs[0] = 2;
            for (int i = 0; i < larguraBase.Length; i++) xs[i + 1] = xs[i] + larguraBase[i];
            layer.CreateLineSegment(xs[0], topo, xs[indices.Length], topo);
            double y = topo - .55;
            for (int i = 0; i < indices.Length; i++) TextoCor(app, layer, cabecalhosBase[indices[i]], xs[i] + .05, topo - .08, larguraBase[i] - .1, 9.5f, "Arial", true, 100, 70, 0, 10);
            layer.CreateLineSegment(xs[0], y, xs[indices.Length], y);
            for (int r = inicio; r < fim; r++)
            {
                var item = itens[r];
                string[] values = { item.Tipo, item.Nome, item.Numero, item.Adicional, item.Camiseta, item.Calcao, item.Quantidade.ToString() };
                for (int c = 0; c < indices.Length; c++) Texto(app, layer, values[indices[c]], xs[c] + .05, y - .08, larguraBase[c] - .1, 9.2f, "Arial", .38);
                y -= .72;
                layer.CreateLineSegment(xs[0], y, xs[indices.Length], y);
            }
            for (int i = 0; i < xs.Length; i++) layer.CreateLineSegment(xs[i], topo, xs[i], y);
        }

        private static List<Tuple<Shape, Tag, string>> EncontrarPorLinhas(
            List<Tuple<Shape, Tag, string>> pecas, IList<OrderLine> linhas)
        {
            var result = new Dictionary<int, Tuple<Shape, Tag, string>>();
            foreach (var linha in linhas ?? new List<OrderLine>())
            {
                if (linha == null) continue;
                var tipo = NomeTipo(linha.Tipo);
                foreach (var peca in pecas)
                {
                    var tag = peca.Item2;
                    if (tipo.Length > 0 && !string.Equals(tipo, NomeTipo(Tipo(tag, peca.Item3)), StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrWhiteSpace(linha.Nome) && !string.Equals(linha.Nome.Trim(), tag["N"].Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrWhiteSpace(linha.Numero) && !string.Equals(linha.Numero.Trim(), tag["NUM"].Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrWhiteSpace(linha.Adicional) && !string.Equals(linha.Adicional.Trim(), tag["AD"].Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    if (linha.TamanhoCamiseta.Length > 0 && tag["TCAM"].Length > 0 && !MesmoTamanho(linha.TamanhoCamiseta, tag["TCAM"])) continue;
                    if (linha.TamanhoCalcao.Length > 0 && tag["TCAL"].Length > 0 && !MesmoTamanho(linha.TamanhoCalcao, tag["TCAL"])) continue;
                    result[peca.Item1.StaticID] = peca;
                }
            }
            return result.Values.ToList();
        }

        private static List<OsPrevia> CriarPreviews(List<OsUniforme> uniformes)
        {
            var result = new List<OsPrevia>();
            // O nome comercial identifica o modelo. Tamanho e nome de página não criam outra cópia.
            foreach (var combo in uniformes.GroupBy(u => ChaveModelo(u.Tipo),
                         StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var camiseta = combo.Where(u => u.Categoria == "CAMISETA")
                    .OrderBy(u => u.Tag["T"], StringComparer.OrdinalIgnoreCase).ThenBy(u => u.Tag.Int("SEQ")).FirstOrDefault();
                var calcao = combo.Where(u => u.Categoria == "CALÇÃO")
                    .OrderBy(u => u.Tag["T"], StringComparer.OrdinalIgnoreCase).ThenBy(u => u.Tag.Int("SEQ")).FirstOrDefault();
                var nome = combo.Select(u => u.Tipo).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? "UNIFORME";
                AdicionarPrevia(nome, camiseta, calcao, result);
            }
            return result;
        }

        private static List<OsUniforme> CriarUniformes(IEnumerable<Tuple<Shape, Tag, string>> pecas, Config cfg)
            => (pecas ?? Enumerable.Empty<Tuple<Shape, Tag, string>>())
                .GroupBy(x => x.Item3 + "|" + x.Item2["PED"] + "|" + x.Item2["SEQ"] + "|" + x.Item2["T"] + "|" +
                    NomeTipo(Tipo(x.Item2, x.Item3)) + "|" + Categoria(x.Item3, x.Item2, cfg), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var primeira = g.First();
                    var categoria = Categoria(primeira.Item3, primeira.Item2, cfg);
                    return new OsUniforme
                    {
                        Tag = primeira.Item2,
                        Pecas = g.Select(x => x.Item1).ToList(),
                        Pagina = primeira.Item3,
                        Tipo = NomeTipo(Tipo(primeira.Item2, primeira.Item3)),
                        Categoria = categoria,
                        TamanhoCamiseta = TamanhoCamiseta(primeira.Item2, categoria),
                        TamanhoCalcao = TamanhoCalcao(primeira.Item2, categoria),
                        Paleta = PaletteKey(g.Select(x => x.Item1))
                    };
                })
                .OrderBy(u => u.Tag["T"], StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.Tag.Int("SEQ"))
                .ToList();

        private static string ChaveModelo(string nome)
        {
            // Compara o nome comercial como ele é lido: ignora acentos, espaços,
            // hífens, sufixos de cópia e pontuação para ignorar tamanho/página duplicada.
            var semAcentos = SemAcentos(LimparSufixoDuplicado(NomeTipo(nome))).ToUpperInvariant();
            var chave = new string(semAcentos.Where(char.IsLetterOrDigit).ToArray());
            // Alguns documentos antigos gravaram a categoria junto com o tipo (ex.:
            // "CAMISETA COMISSÃO"). A categoria já é renderizada separadamente no título.
            foreach (var categoria in new[] { "CAMISETA", "CAMISA", "CALCAO", "SHORT", "BERMUDA" })
                chave = chave.Replace(categoria, "");
            return chave;
        }
        private static void AdicionarPrevia(string tipo, OsUniforme camiseta, OsUniforme calcao, List<OsPrevia> destino)
        {
            if (camiseta == null && calcao == null) return;
            // Em documentos de montagem antigos, mais de uma unidade pode ter sido
            // gravada com o mesmo SEQ. A OS precisa mostrar uma amostra do modelo,
            // não o bloco com todas as unidades. P identifica a parte do molde e A
            // diferencia apelidos como manga esquerda/direita; assim mantemos todas
            // as partes distintas do uniforme e descartamos as repetições.
            var pecasCamiseta = PecasUnicasDaUnidade(camiseta);
            var pecasCalcao = PecasUnicasDaUnidade(calcao);
            destino.Add(new OsPrevia
            {
                Tipo = tipo,
                Camiseta = pecasCamiseta,
                Calcao = pecasCalcao,
                MangaEsquerdaY = camiseta == null ? null : PosicaoRelativaDaManga(pecasCamiseta, "ESQUERDA"),
                MangaDireitaY = camiseta == null ? null : PosicaoRelativaDaManga(pecasCamiseta, "DIREITA")
            });
        }

        private static List<Shape> PecasUnicasDaUnidade(OsUniforme uniforme)
        {
            if (uniforme?.Pecas == null || uniforme.Pecas.Count == 0) return new List<Shape>();
            var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resultado = new List<Shape>();
            foreach (var shape in uniforme.Pecas)
            {
                var tag = Tag.Parse(shape?.Name);
                if (shape == null) continue;

                // PECA é gerada com P (parte do molde). O apelido separa partes
                // com a mesma matriz, por exemplo MANGA-ESQUERDA e MANGA-DIREITA.
                // Sem P, não deduplicamos às cegas para não apagar moldes legítimos
                // de arquivos antigos que não guardavam essa informação.
                string parte = tag?["P"]?.Trim() ?? "";
                string apelido = tag?["A"]?.Trim() ?? "";
                if (parte.Length == 0)
                {
                    resultado.Add(shape);
                    continue;
                }
                string chave = parte + "|" + apelido;
                if (vistas.Add(chave)) resultado.Add(shape);
            }
            return resultado;
        }

        private static List<OsUniforme> VariantesPorCor(IEnumerable<OsUniforme> uniformes)
            => uniformes.GroupBy(u => string.IsNullOrWhiteSpace(u.Paleta) ? "sem-paleta" : u.Paleta,
                    StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(u => u.Tag["T"], StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.Tag.Int("SEQ")).ToList();

        private static string Categoria(string pagina, Tag tag, Config cfg)
        {
            var categoria = CategoriaPagina(pagina);
            if (categoria.Length > 0) return categoria;
            var tamanho = tag["T"].Trim();
            if (tag["TCAM"].Length > 0 && tag["TCAL"].Length > 0)
            {
                if (MesmoTamanho(tamanho, tag["TCAM"]) && !MesmoTamanho(tamanho, tag["TCAL"])) return "CAMISETA";
                if (MesmoTamanho(tamanho, tag["TCAL"]) && !MesmoTamanho(tamanho, tag["TCAM"])) return "CALÇÃO";
            }
            return cfg.UsarTamanhoCalcao ? "CALÇÃO" : "CAMISETA";
        }

        private static string CategoriaPagina(string pagina)
        {
            var nome = SemAcentos(pagina).ToUpperInvariant();
            if (nome.Contains("CAMISETA") || nome.Contains("CAMISA")) return "CAMISETA";
            if (nome.Contains("CALCAO") || nome.Contains("SHORT") || nome.Contains("BERMUDA")) return "CALÇÃO";
            return "";
        }

        private static string TamanhoNoNomeDaPagina(string pagina)
        {
            var marcador = new[] { "CAMISETA", "CAMISA", "CALÇÃO", "CALCAO", "SHORT", "BERMUDA" }
                .Select(m => new { Marker = m, Index = (pagina ?? "").ToUpperInvariant().LastIndexOf(m, StringComparison.OrdinalIgnoreCase) })
                .Where(x => x.Index >= 0).OrderBy(x => x.Index).FirstOrDefault();
            if (marcador == null) return "";
            var prefixo = pagina.Substring(0, marcador.Index).Trim().TrimEnd('-', '\u2013', '\u2014');
            int colon = prefixo.LastIndexOf(':');
            if (colon >= 0) prefixo = prefixo.Substring(colon + 1);
            return OrderImport.NormalizeSize(prefixo.Trim());
        }

        private static string SemAcentos(string valor)
        {
            var normalizado = (valor ?? "").Normalize(System.Text.NormalizationForm.FormD);
            return new string(normalizado.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        }

        private static string TamanhoCamiseta(Tag tag, string categoria)
            => tag["TCAM"].Length > 0 ? tag["TCAM"] : categoria == "CAMISETA" ? tag["T"] : "";

        private static string TamanhoCalcao(Tag tag, string categoria)
            => tag["TCAL"].Length > 0 ? tag["TCAL"] : categoria == "CALÇÃO" ? tag["T"] : "";

        private static bool MesmoTamanho(string a, string b)
            => string.Equals(OrderImport.NormalizeSize(a), OrderImport.NormalizeSize(b), StringComparison.OrdinalIgnoreCase);

        private static string NomeTipo(string tipo)
        {
            tipo = (tipo ?? "").Trim();
            // Aceita os diferentes traços usados em nomes de páginas e remove
            // o número/prefixo da página antes de comparar ou imprimir o modelo.
            int i = 0;
            while (i < tipo.Length && char.IsWhiteSpace(tipo[i])) i++;
            int inicioNumero = i;
            while (i < tipo.Length && char.IsDigit(tipo[i])) i++;
            if (i > inicioNumero)
            {
                while (i < tipo.Length && char.IsWhiteSpace(tipo[i])) i++;
                if (i < tipo.Length && (tipo[i] == '-' || tipo[i] == '\u2013' || tipo[i] == '\u2014' || tipo[i] == '\u2212'))
                    tipo = tipo.Substring(i + 1).Trim();
            }
            tipo = tipo.Trim(' ', '-', '\u2013', '\u2014', '\u2212');
            foreach (var categoria in new[] { "CAMISETA", "CAMISA", "CALÇÃO", "CALCAO", "SHORT", "BERMUDA" })
            {
                if (tipo.StartsWith(categoria + " - ", StringComparison.OrdinalIgnoreCase)) tipo = tipo.Substring(categoria.Length + 3).Trim();
                else if (tipo.StartsWith(categoria + " ", StringComparison.OrdinalIgnoreCase)) tipo = tipo.Substring(categoria.Length + 1).Trim();
                else if (tipo.Equals(categoria, StringComparison.OrdinalIgnoreCase)) tipo = "";
            }
            var resultado = string.Join(" ", tipo.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
            return resultado.Length == 0 ? "UNIFORME" : resultado;
        }

        private static string FormatarData(DateTime? data) => data.HasValue ? data.Value.ToString("dd/MM/yyyy") : "Não informada";
        private static string ValorOu(string valor, string fallback) => string.IsNullOrWhiteSpace(valor) ? fallback : valor.Trim();

        private static List<OsItem> CriarItens(IList<OrderLine> linhas, List<OsUniforme> uniformes, Config cfg)
        {
            var itens = linhas?.Where(l => l != null).Select(l => new OsItem
            {
                Tipo = NomeTipo(l.Tipo), Nome = l.Nome ?? "", Numero = l.Numero ?? "", Adicional = l.Adicional ?? "",
                Camiseta = l.TamanhoCamiseta ?? "",
                Calcao = !string.IsNullOrWhiteSpace(l.TamanhoCalcao) ? l.TamanhoCalcao : (cfg.UsarTamanhoCalcao ? l.Tamanho ?? "" : ""),
                Quantidade = Math.Max(1, l.Quantidade)
            }).ToList() ?? new List<OsItem>();
            if (itens.Count > 0) return itens;
            return uniformes.GroupBy(u => new { Tipo = u.Tipo, Nome = u.Tag["N"], Num = u.Tag["NUM"], Ad = u.Tag["AD"], T = u.Tag["T"] })
                .Select(g => new OsItem { Tipo = NomeTipo(g.Key.Tipo), Nome = g.Key.Nome, Numero = g.Key.Num, Adicional = g.Key.Ad,
                    Camiseta = cfg.UsarTamanhoCalcao ? "" : g.Key.T, Calcao = cfg.UsarTamanhoCalcao ? g.Key.T : "", Quantidade = g.Count() })
                .OrderBy(x => x.Tipo).ThenBy(x => x.Camiseta).ThenBy(x => x.Calcao).ThenBy(x => x.Nome).ToList();
        }

        private static bool TemCalcaoPedido(IList<OrderLine> linhas, Config cfg)
            => (linhas ?? new List<OrderLine>()).Any(l => l != null &&
                (!string.IsNullOrWhiteSpace(l.TamanhoCalcao) || (cfg.UsarTamanhoCalcao && !string.IsNullOrWhiteSpace(l.Tamanho))));

        private static Page NovaPagina(Document doc, string nome)
        {
            var page = doc.AddPages(1);
            page.SetSize(21, 29.7);
            page.Name = nome;
            page.Activate();
            return page;
        }

        private static void TituloPagina(Application app, Page page, string titulo, string logo)
        {
            TextoCor(app, page.ActiveLayer, titulo.ToUpperInvariant(), 2, 27.3, 14.2, 18, "Arial", true, 100, 70, 0, 10);
            if (!string.IsNullOrWhiteSpace(logo)) ImportarImagem(app, page, logo, 5.0, 2.4, 16.3, 26.8);
        }

        private static Shape Texto(Application app, Layer layer, string texto, double x, double top, double largura, float fonte,
                                   string familia, double alturaMax = 0)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var shape = Cdr.CreateText(app, layer, texto, familia, fonte);
            Cdr.Fit(shape, largura, alturaMax > 0 ? alturaMax : 50, false);
            shape.SetPositionEx(cdrReferencePoint.cdrTopLeft, x, top);
            return shape;
        }

        private static void DesenharTituloModelo(Application app, Page page, string categoria, string tipo, double centroX, double topo, double largura)
        {
            // Escreve o nome completo em uma linha só, como o usuário identifica a página do Corel.
            string nomeCompleto = categoria.ToUpperInvariant() + " - " + NomeTipo(tipo);
            TextoCentroCor(app, page.ActiveLayer, nomeCompleto, centroX, topo - .25, largura - .35, 8.5f, "Arial", true, 100, 70, 0, 0);
        }

        private static Shape TextoCentro(Application app, Layer layer, string texto, double centroX, double centroY, double largura,
                                         float fonte, string familia)
        {
            var shape = Texto(app, layer, texto, centroX - largura / 2, centroY, largura, fonte, familia, .55);
            if (shape != null) shape.SetPositionEx(cdrReferencePoint.cdrCenter, centroX, centroY);
            return shape;
        }

        private static Shape TextoCentroCor(Application app, Layer layer, string texto, double centroX, double centroY, double largura,
                                            float fonte, string familia, bool negrito, int c, int m, int y, int k)
        {
            var shape = TextoCor(app, layer, texto, centroX - largura / 2, centroY, largura, fonte, familia, negrito, c, m, y, k, .55);
            if (shape != null) shape.SetPositionEx(cdrReferencePoint.cdrCenter, centroX, centroY);
            return shape;
        }
        private static Shape TextoCor(Application app, Layer layer, string texto, double x, double top, double largura, float fonte,
                                      string familia, bool negrito, int c, int m, int y, int k, double alturaMax = 0)
        {
            var shape = Texto(app, layer, texto, x, top, largura, fonte, familia, alturaMax);
            if (shape == null) return null;
            shape.Text.Story.Bold = negrito;
            Cdr.Fit(shape, largura, alturaMax > 0 ? alturaMax : 50, false);
            shape.Fill.ApplyUniformFill(app.CreateCMYKColor(c, m, y, k));
            return shape;
        }

        private static IDictionary<string, string> CriarIconesOS(string pasta)
        {
            var arquivos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] nomes = { "entrega", "tecido", "gola", "punho", "etiqueta", "observacoes", "camiseta", "calcao", "bolso", "detalhes" };
            foreach (var nome in nomes)
            {
                string caminho = Path.Combine(pasta, "icone-" + nome + ".png");
                using (var bitmap = new System.Drawing.Bitmap(128, 128, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(25, 72, 132), 7))
                using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(25, 72, 132)))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    switch (nome)
                    {
                        case "entrega":
                            g.DrawRectangle(pen, 22, 30, 84, 76); g.DrawLine(pen, 22, 50, 106, 50);
                            g.DrawLine(pen, 43, 20, 43, 39); g.DrawLine(pen, 85, 20, 85, 39);
                            g.DrawLine(pen, 42, 68, 53, 79); g.DrawLine(pen, 53, 79, 82, 60); break;
                        case "tecido":
                            g.DrawEllipse(pen, 19, 34, 30, 62); g.DrawEllipse(pen, 78, 34, 30, 62);
                            g.DrawLine(pen, 34, 34, 93, 34); g.DrawLine(pen, 34, 96, 93, 96);
                            g.DrawLine(pen, 49, 48, 78, 48); g.DrawLine(pen, 49, 82, 78, 82); break;
                        case "gola":
                            g.DrawLine(pen, 23, 38, 45, 22); g.DrawLine(pen, 45, 22, 64, 42);
                            g.DrawLine(pen, 64, 42, 84, 22); g.DrawLine(pen, 84, 22, 105, 38);
                            g.DrawLine(pen, 23, 38, 33, 103); g.DrawLine(pen, 105, 38, 95, 103);
                            g.DrawLine(pen, 33, 103, 95, 103); g.DrawLine(pen, 45, 22, 64, 48); g.DrawLine(pen, 84, 22, 64, 48); break;
                        case "punho":
                            g.DrawLine(pen, 25, 37, 102, 37); g.DrawLine(pen, 25, 91, 102, 91);
                            g.DrawLine(pen, 31, 40, 31, 89); g.DrawLine(pen, 96, 40, 96, 89);
                            g.DrawLine(pen, 42, 53, 85, 53); g.DrawLine(pen, 42, 75, 85, 75); break;
                        case "etiqueta":
                            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                            {
                                path.AddPolygon(new[] { new System.Drawing.Point(20, 36), new System.Drawing.Point(72, 24),
                                    new System.Drawing.Point(108, 59), new System.Drawing.Point(59, 108), new System.Drawing.Point(20, 69) });
                                g.DrawPath(pen, path);
                            }
                            g.DrawEllipse(pen, 38, 48, 13, 13); break;
                        case "observacoes":
                            g.DrawRectangle(pen, 28, 20, 72, 88); g.DrawLine(pen, 43, 46, 85, 46);
                            g.DrawLine(pen, 43, 64, 85, 64); g.DrawLine(pen, 43, 82, 74, 82); break;
                        case "calcao":
                            g.DrawLine(pen, 25, 27, 103, 27); g.DrawLine(pen, 28, 31, 34, 101);
                            g.DrawLine(pen, 100, 31, 94, 101); g.DrawLine(pen, 34, 101, 58, 101);
                            g.DrawLine(pen, 70, 101, 94, 101); g.DrawLine(pen, 58, 30, 58, 70);
                            g.DrawLine(pen, 70, 30, 70, 70); g.DrawLine(pen, 58, 70, 48, 88);
                            g.DrawLine(pen, 70, 70, 80, 88); break;
                        case "bolso":
                            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                            {
                                path.AddPolygon(new[] { new System.Drawing.Point(31, 25), new System.Drawing.Point(97, 25),
                                    new System.Drawing.Point(88, 100), new System.Drawing.Point(40, 100) });
                                g.DrawPath(pen, path);
                            }
                            g.DrawLine(pen, 42, 45, 86, 45); g.DrawLine(pen, 64, 25, 64, 44); break;
                        case "detalhes":
                            g.DrawRectangle(pen, 28, 20, 72, 88); g.DrawLine(pen, 43, 42, 85, 42);
                            g.DrawLine(pen, 43, 60, 85, 60); g.DrawLine(pen, 43, 78, 72, 78);
                            g.DrawLine(pen, 43, 94, 85, 94); break;
                        default:
                            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                            {
                                path.AddPolygon(new[] { new System.Drawing.Point(43, 18), new System.Drawing.Point(55, 31),
                                    new System.Drawing.Point(73, 31), new System.Drawing.Point(85, 18), new System.Drawing.Point(111, 35),
                                    new System.Drawing.Point(99, 58), new System.Drawing.Point(89, 52), new System.Drawing.Point(89, 108),
                                    new System.Drawing.Point(39, 108), new System.Drawing.Point(39, 52), new System.Drawing.Point(29, 58),
                                    new System.Drawing.Point(17, 35) });
                                g.DrawPath(pen, path);
                            }
                            g.DrawLine(pen, 48, 22, 64, 42); g.DrawLine(pen, 80, 22, 64, 42); break;
                    }
                    bitmap.Save(caminho, System.Drawing.Imaging.ImageFormat.Png);
                }
                arquivos[nome] = caminho;
            }
            return arquivos;
        }

        private static void InserirIcone(Application app, Page page, IDictionary<string, string> icones, string nome, double x, double y)
        {
            if (icones != null && icones.TryGetValue(nome, out var caminho))
                ImportarImagem(app, page, caminho, .42, .42, x, y);
        }

        private static Shape ImportarImagem(Application app, Page page, string caminho, double larguraMax, double alturaMax, double centroX, double centroY)
        {
            var layer = page.ActiveLayer;
            var antes = new HashSet<int>(Cdr.Items(page.Shapes).Select(s => s.StaticID));
            var ext = Path.GetExtension(caminho).ToLowerInvariant();
            var filtro = ext == ".png" ? cdrFilter.cdrPNG : ext == ".bmp" ? cdrFilter.cdrBMP : ext == ".tif" || ext == ".tiff" ? cdrFilter.cdrTIFF : cdrFilter.cdrJPEG;
            layer.ImportEx(caminho, filtro, null).Finish();
            var novas = Cdr.Items(page.Shapes).Where(s => !antes.Contains(s.StaticID)).ToList();
            if (novas.Count == 0) return null;
            var shape = novas.Count == 1 ? novas[0] : Cdr.Range(app, novas).Group();
            var b = Cdr.BoxOf(shape);
            if (b.W <= 0 || b.H <= 0) return null;
            double escala = Math.Min(larguraMax / b.W, alturaMax / b.H);
            shape.SetSizeEx(b.Cx, b.Cy, b.W * escala, b.H * escala);
            shape.SetPositionEx(cdrReferencePoint.cdrCenter, centroX, centroY);
            return shape;
        }

        private static void ExportarPrevia(Application app, Document doc, IList<Shape> shapes, string caminho, int dpi)
        {
            if (shapes == null || shapes.Count == 0) throw new UserException("Um molde de referência está vazio.");
            var page = shapes[0].Page;
            string nomePagina = page.Name;
            var ids = new HashSet<int>();
            var formasDaPagina = shapes.Where(s => string.Equals(PaginaDaForma(s, ""), nomePagina, StringComparison.OrdinalIgnoreCase)
                                                   && ids.Add(s.StaticID)).ToList();
            if (formasDaPagina.Count == 0) throw new UserException("Não encontrei formas válidas na página usada como amostra.");
            var original = doc.SelectionRange;
            string etapa = "ativar a página";
            try
            {
                page.Activate();
                etapa = "selecionar as formas da amostra";
                doc.ClearSelection();
                foreach (var shape in formasDaPagina) shape.AddToSelection();
                if (doc.SelectionRange == null || doc.SelectionRange.Count == 0)
                    throw new UserException("O CorelDRAW não selecionou as formas da amostra.");
                etapa = "exportar a imagem PNG";
                var opt = app.CreateStructExportOptions();
                opt.ResolutionX = Math.Max(96, Math.Min(300, dpi));
                opt.ResolutionY = opt.ResolutionX;
                opt.AntiAliasingType = cdrAntiAliasingType.cdrNormalAntiAliasing;
                opt.Overwrite = true;
                opt.MaintainAspect = true;
                opt.Transparent = true;
                opt.ImageType = cdrImageType.cdrRGBColorImage;
                var filter = doc.ExportEx(caminho, cdrFilter.cdrPNG, cdrExportRange.cdrSelection, opt, null);
                if (filter == null) throw new UserException("O CorelDRAW não iniciou a exportação PNG.");
                filter.Finish();
            }
            catch (UserException) { throw; }
            catch (Exception ex)
            {
                var com = ex as System.Runtime.InteropServices.COMException;
                var hresult = com == null ? "" : $" (HRESULT 0x{com.ErrorCode:X8})";
                throw new UserException($"Falha ao {etapa} de '{page.Name}'{hresult}: {ex.Message}");
            }
            finally { try { doc.ClearSelection(); original?.CreateSelection(); } catch { } }
        }

        private static string PaletteKey(IEnumerable<Shape> shapes)
        {
            var cores = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var shape in shapes)
            {
                try
                {
                    foreach (var c in shape.GetColors(128) ?? new Color[0])
                    {
                        if (c == null) continue;
                        cores.Add(c.IsCMYK
                            ? $"C{c.CMYKCyan}M{c.CMYKMagenta}Y{c.CMYKYellow}K{c.CMYKBlack}"
                            : $"R{c.RGBRed}G{c.RGBGreen}B{c.RGBBlue}");
                    }
                }
                catch { }
            }
            return cores.Count == 0 ? "sem-paleta" : string.Join(";", cores);
        }

        private static string Tipo(Tag tag, string pagina)
        {
            var nome = pagina ?? "";
            var normalizado = nome.ToUpperInvariant();
            foreach (var marcador in new[] { "CAMISETA", "CAMISA", "CALÇÃO", "CALCAO", "SHORT", "BERMUDA" })
            {
                int indice = normalizado.LastIndexOf(marcador, StringComparison.OrdinalIgnoreCase);
                if (indice < 0) continue;
                var partes = nome.Substring(indice + marcador.Length).Split('-')
                    .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                if (partes.Count > 0) return LimparSufixoDuplicado(string.Join(" - ", partes));
            }
            // Quando a página já foi organizada/renomeada no Corel, o nome dela
            // identifica o tipo de peça efetivamente exibido. Só use o metadado da
            // peça se a página não trouxer um tipo legível; isso evita que uma
            // etiqueta antiga (ex.: Comissão) esconda páginas Linha ou Goleiro.
            return LimparSufixoDuplicado(!string.IsNullOrWhiteSpace(tag["TIPO"]) ? tag["TIPO"] : "Uniforme");
        }

        private static string LimparSufixoDuplicado(string nome)
        {
            nome = (nome ?? "").Trim();
            int limite = nome.EndsWith(")", StringComparison.Ordinal) ? nome.Length - 1 : nome.Length;
            int inicioDigitos = limite;
            while (inicioDigitos > 0 && char.IsDigit(nome[inicioDigitos - 1])) inicioDigitos--;
            if (inicioDigitos == limite || inicioDigitos == 0) return nome;
            // O Corel acrescenta -2, _2, (2) etc. ao duplicar uma página.
            // Esse contador não faz parte do nome do modelo da OS.
            char separador = nome[inicioDigitos - 1];
            if (char.IsWhiteSpace(separador) || separador == '-' || separador == '_' ||
                separador == '\u2013' || separador == '\u2014' || separador == '(')
            {
                nome = nome.Substring(0, inicioDigitos - 1).TrimEnd(' ', '-', '_', '\u2013', '\u2014', '(');
            }
            return nome.Trim();
        }

        private static string NomeSeguro(string nome)
        {
            var s = (nome ?? "").Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return s.Trim().TrimEnd('.');
        }

        private static void PublicarPDF(Document doc, string caminho, Config cfg)
        {
            var p = doc.PDFSettings;
            p.Reset();
            p.PublishRange = pdfExportRange.pdfWholeDocument;
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

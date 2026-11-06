using System;
using System.Collections.Generic;
using System.Linq;
using Corel.Interop.VGCore;

namespace Yuno.Engine
{
    public class AssemblyResult
    {
        public int Pecas;
        public int Linhas;
        public List<string> Avisos = new List<string>();
        public List<Shape> Criadas = new List<Shape>();
    }

    /// <summary>Montagem de peças: copia a arte da matriz para o molde de cada tamanho e preenche nome/número/ID.</summary>
    public static class Assembler
    {
        // ------------------------------------------------------------------ montagem

        /// <param name="pedido">número/código do pedido (vai no ID)</param>
        /// <param name="preencherBranco">null = usar a configuração; true/false força</param>
        public static AssemblyResult Montar(Application app, IList<OrderLine> linhas, string pedido, Config cfg, bool? preencherBranco = null)
        {
            var doc = app.ActiveDocument;
            var res = new AssemblyResult();
            if (linhas == null || linhas.Count == 0) throw new UserException("A lista do pedido está vazia.");

            var moldes = Moldes.FindAll(doc);
            var dups = Moldes.Duplicates(moldes);
            if (dups.Count > 0)
            {
                Cdr.Select(doc, dups);
                throw new UserException($"Há {dups.Count} moldes repetidos (mesmo tamanho e parte). Eles foram selecionados para você conferir.");
            }

            var matrizes = moldes.Where(m => m.IsMatriz).OrderBy(m => m.Parte).ToList();
            if (matrizes.Count == 0) throw new UserException("Nenhuma MATRIZ definida. Use \"Definir tamanhos\" para marcar a matriz de cada parte.");
            var porTam = moldes.Where(m => !m.IsMatriz).ToLookup(m => m.Tamanho);

            foreach (var tam in linhas.Select(l => l.Tamanho).Distinct())
                if (!porTam.Contains(tam))
                    throw new UserException($"O tamanho \"{tam}\" não tem nenhum molde definido no documento.");

            // campos soltos (fora do PowerClip) sobre cada matriz
            var soltos = new Dictionary<int, List<Shape>>();
            var todas = Cdr.PageShapes(doc).ToList();
            foreach (var m in matrizes) soltos[m.Parte] = LooseFieldsOver(m.Shape, todas);

            bool branco = preencherBranco ?? cfg.PreencherBrancoSemCor;
            int seq = NextSeq(doc);

            // Layout: cada tamanho vira uma coluna EMBAIXO dos moldes daquele tamanho.
            // Cada uniforme repete a mesma disposição das partes do molde (frente, costas, mangas...),
            // descendo uma "linha" por uniforme. Se já houver peças montadas na coluna, continua abaixo delas.
            var layer = doc.ActiveLayer;
            var caixaTam = new Dictionary<string, Box>();
            var proxTopo = new Dictionary<string, double>();
            var pecasExistentes = Cdr.PageShapes(doc).Where(s => Tag.KindOf(s.Name) == Tag.Peca).Select(s => new { t = Tag.Parse(s.Name)["T"], b = Cdr.BoxOf(s) }).ToList();
            foreach (var g in porTam)
            {
                var box = Cdr.BoxOf(Cdr.Range(app, g.Select(m => m.Shape)));
                caixaTam[g.Key] = box;
                double topo = box.B - cfg.Espacamento * 2;
                foreach (var pe in pecasExistentes.Where(x => x.t == g.Key && x.b.Cx >= box.L && x.b.Cx <= box.R))
                    topo = Math.Min(topo, pe.b.B - cfg.Espacamento * 2);
                proxTopo[g.Key] = topo;
            }

            foreach (var linha in linhas)
            {
                var box = caixaTam[linha.Tamanho];
                for (int k = 0; k < Math.Max(1, linha.Quantidade); k++)
                {
                    var rowShapes = new List<Shape>();
                    double rowTop = proxTopo[linha.Tamanho];
                    double alturaInd = 0;
                    if (cfg.GerarIndicador)
                    {
                        var ind = Cdr.CreateText(app, layer, IndicatorText(linha, seq), "Arial", cfg.FonteIndicador);
                        ind.SetPositionEx(cdrReferencePoint.cdrTopLeft, box.L, rowTop);
                        var it = new Tag(Tag.Indicador); it["SEQ"] = seq.ToString();
                        ind.Name = it.ToString();
                        alturaInd = Cdr.BoxOf(ind).H + 0.5;
                        rowShapes.Add(ind);
                    }
                    // deslocamento vertical: o topo do conjunto de moldes vai para baixo do indicador
                    double dy = (rowTop - alturaInd) - box.T;

                    foreach (var mtz in matrizes)
                    {
                        var tpl = porTam[linha.Tamanho].FirstOrDefault(m => m.Parte == mtz.Parte);
                        if (tpl == null)
                        {
                            Warn(res, $"Tamanho {linha.Tamanho} não tem a parte {mtz.Parte}; ignorada.");
                            continue;
                        }
                        var tag = new Tag(Tag.Peca);
                        tag["T"] = linha.Tamanho; tag["P"] = mtz.Parte.ToString(); tag["A"] = mtz.Apelido;
                        tag["PED"] = pedido; tag["SEQ"] = seq.ToString();
                        tag["N"] = linha.Nome; tag["NUM"] = linha.Numero; tag["AD"] = linha.Adicional;

                        var tb = Cdr.BoxOf(tpl.Shape);
                        var peca = BuildPiece(app, mtz.Shape, soltos[mtz.Parte], tpl.Shape, linha, tag, cfg, branco, res, tb.L, tb.T + dy);
                        rowShapes.Add(peca);
                        res.Criadas.Add(peca);
                        res.Pecas++;
                    }

                    if (cfg.AgruparLinha && rowShapes.Count > 1)
                        Cdr.Range(app, rowShapes).Group().Name = "YUNO:LINHA;SEQ=" + seq;

                    proxTopo[linha.Tamanho] = box.B + dy - cfg.Espacamento * 2;
                    res.Linhas++;
                    seq++;
                }
            }            return res;
        }

        static void Warn(AssemblyResult r, string msg) { if (!r.Avisos.Contains(msg)) r.Avisos.Add(msg); }

        static string IndicatorText(OrderLine l, int seq)
        {
            var parts = new List<string> { seq.ToString("00"), l.Tamanho };
            if (!string.IsNullOrWhiteSpace(l.Nome)) parts.Add(l.Nome);
            if (!string.IsNullOrWhiteSpace(l.Numero)) parts.Add(l.Numero);
            if (!string.IsNullOrWhiteSpace(l.Adicional)) parts.Add(l.Adicional);
            return string.Join("  |  ", parts);
        }

        public static int NextSeq(Document doc)
        {
            int max = 0;
            foreach (var s in Cdr.PageShapes(doc))
            {
                var t = Tag.Parse(s.Name);
                if (t != null && (t.Kind == Tag.Peca || t.Kind == Tag.Indicador)) max = Math.Max(max, t.Int("SEQ"));
            }
            return max + 1;
        }

        public static string FormatId(Tag t, Config cfg)
        {
            var p = new List<string>();
            if (cfg.IdPedido && t["PED"].Length > 0) p.Add(t["PED"]);
            if (cfg.IdSequencial && t["SEQ"].Length > 0) p.Add(t["SEQ"]);
            if (cfg.IdTamanho && t["T"].Length > 0) p.Add(t["T"]);
            if (cfg.IdParte && t["P"].Length > 0) p.Add(t["P"]);
            if (cfg.IdApelido && t["A"].Length > 0) p.Add(t["A"]);
            return string.Join("-", p);
        }

        static bool Empty(string v) => string.IsNullOrWhiteSpace(v) || v.Trim().Equals("N/T", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Objetos soltos (fora do PowerClip) que estão por cima da matriz: textos, campos, gabaritos, logos.
        /// Considera os objetos do nível principal da página, mais campos marcados dentro de grupos.
        /// </summary>
        internal static List<Shape> LooseFieldsOver(Shape matriz, List<Shape> todas)
        {
            var b = Cdr.BoxOf(matriz);
            int mid = matriz.StaticID;
            var topo = new HashSet<int>(Cdr.Items(matriz.Page.Shapes).Select(s => s.StaticID));
            var r = new List<Shape>();
            foreach (var s in todas)
            {
                if (s.StaticID == mid) continue;
                var k = Tag.KindOf(s.Name);
                bool marcado = k != null && (Tag.IsField(k) || Tag.IsGab(k) || k == Tag.Fixo);
                if (!marcado)
                {
                    if (k != null || !topo.Contains(s.StaticID)) continue;   // peças, IDs, rótulos, filhos de grupo
                    if (Moldes.Info(s) != null) continue;                   // outro molde
                }
                var sb = Cdr.BoxOf(s);
                if (sb.W > b.W * 1.2 || sb.H > b.H * 1.2) continue;
                if (sb.Cx < b.L || sb.Cx > b.R || sb.Cy < b.B || sb.Cy > b.T) continue;
                if (!Cdr.Inside(matriz, sb.Cx, sb.Cy)) continue;
                r.Add(s);
            }
            return r;
        }

        // ------------------------------------------------------------------ aplicar matriz nos tamanhos

        public class AplicarResult { public int Moldes; public int Partes; public List<string> Avisos = new List<string>(); }

        /// <summary>
        /// Copia a arte de cada MATRIZ para todos os moldes da mesma parte (nos outros tamanhos),
        /// no próprio lugar, ajustando à graduação. Textos e objetos FIXO mantêm o tamanho.
        /// </summary>
        public static AplicarResult AplicarMatrizes(Application app, Config cfg, int? soParte = null, IEnumerable<Shape> soEstes = null)
        {
            var doc = app.ActiveDocument;
            var res = new AplicarResult();
            var moldes = Moldes.FindAll(doc);
            var dups = Moldes.Duplicates(moldes);
            if (dups.Count > 0)
            {
                Cdr.Select(doc, dups);
                throw new UserException($"Há {dups.Count} moldes com o mesmo nome (tamanho e parte). Eles foram selecionados para você conferir.");
            }
            var matrizes = moldes.Where(m => m.IsMatriz && (soParte == null || m.Parte == soParte)).OrderBy(m => m.Parte).ToList();
            if (matrizes.Count == 0)
                throw new UserException(soParte == null ? "Nenhuma MATRIZ definida. Clique na matriz de cada parte com o tamanho MATRIZ escolhido."
                                                        : $"Não existe MATRIZ - {soParte}.");

            HashSet<int> filtro = soEstes == null ? null : new HashSet<int>(soEstes.Select(s => s.StaticID));
            var todas = Cdr.PageShapes(doc).ToList();

            foreach (var mtz in matrizes)
            {
                var alvos = moldes.Where(m => !m.IsMatriz && m.Parte == mtz.Parte && (filtro == null || filtro.Contains(m.Shape.StaticID))).ToList();
                if (alvos.Count == 0) { res.Avisos.Add($"MATRIZ - {mtz.Parte}: nenhum tamanho definido para esta parte."); continue; }
                if (!Cdr.HasContents(mtz.Shape) && mtz.Shape.Fill.Type == cdrFillType.cdrNoFill)
                    res.Avisos.Add($"MATRIZ - {mtz.Parte} está vazia (sem arte e sem cor).");

                var soltos = LooseFieldsOver(mtz.Shape, todas);
                var src = Cdr.BoxOf(mtz.Shape);
                foreach (var alvo in alvos)
                {
                    if (cfg.SubstituirAoAplicar) Cdr.ClearPowerClip(alvo.Shape);
                    if (mtz.Shape.Fill.Type != cdrFillType.cdrNoFill) alvo.Shape.Fill.CopyAssign(mtz.Shape.Fill);
                    var dst = Cdr.BoxOf(alvo.Shape);
                    var art = CloneArt(app, mtz.Shape, soltos);
                    MapArt(art, src, dst, cfg.EscalaArte);
                    foreach (var g in art.Where(s => Tag.IsGab(Tag.KindOf(s.Name))).ToList()) { art.Remove(g); g.Delete(); }
                    if (art.Count > 0) Cdr.Range(app, art).AddToPowerClip(alvo.Shape, cdrTriState.cdrFalse);
                    res.Moldes++;
                }
                res.Partes++;
            }
            return res;
        }

        static Shape BuildPiece(Application app, Shape matriz, List<Shape> soltos, Shape tpl, OrderLine linha, Tag tag,
                                Config cfg, bool branco, AssemblyResult res, double left, double top)
        {
            var peca = tpl.Duplicate(0, 0);
            Cdr.ClearPowerClip(peca);
            peca.SetPositionEx(cdrReferencePoint.cdrTopLeft, left, top);

            if (matriz.Fill.Type != cdrFillType.cdrNoFill) peca.Fill.CopyAssign(matriz.Fill);
            else if (branco) peca.Fill.ApplyUniformFill(app.CreateCMYKColor(0, 0, 0, 0));

            var src = Cdr.BoxOf(matriz);
            var dst = Cdr.BoxOf(peca);
            var art = CloneArt(app, matriz, soltos);
            MapArt(art, src, dst, cfg.EscalaArte);

            // preenche campos
            // campos: marcados pelo nome OU reconhecidos pelo texto ("NOME", "10", "ADICIONAL"), inclusive dentro de grupos
            var byKind = Cdr.Walk(art).GroupBy(s => FieldKind(s) ?? Tag.KindOf(s.Name) ?? "").ToDictionary(g => g.Key, g => g.ToList());
            Shape Gab(string k) => byKind.TryGetValue(k, out var l) ? l[0] : null;

            FillField(byKind, Tag.Nome, cfg.NomeMaiusculo ? linha.Nome?.ToUpperInvariant() : linha.Nome, Gab(Tag.GabNome), peca, dst, cfg.LarguraNomePct, cfg, art);
            FillField(byKind, Tag.Numero, linha.Numero, Gab(Tag.GabNumero), peca, dst, cfg.LarguraNumeroPct, cfg, art);
            FillField(byKind, Tag.Adicional, cfg.AdicionalMaiusculo ? linha.Adicional?.ToUpperInvariant() : linha.Adicional, Gab(Tag.GabAdicional), peca, dst, cfg.LarguraAdicionalPct, cfg, art);

            if (cfg.GerarId)
            {
                var idText = FormatId(tag, cfg);
                if (idText.Length > 0) art.Add(CreateId(app, peca, idText, Gab(Tag.GabId), cfg));
            }

            // gabaritos são só referência: removem-se
            foreach (var g in art.Where(s => Tag.IsGab(Tag.KindOf(s.Name))).ToList()) { art.Remove(g); g.Delete(); }

            if (art.Count > 0) Cdr.Range(app, art).AddToPowerClip(peca, cdrTriState.cdrFalse);
            peca.Name = tag.ToString();
            return peca;
        }

        /// <summary>Duplica o conteúdo do PowerClip da origem (+ formas soltas indicadas).</summary>
        internal static List<Shape> CloneArt(Application app, Shape origem, IEnumerable<Shape> soltos)
        {
            var result = new List<Shape>();
            if (Cdr.HasContents(origem))
            {
                var dup = origem.Duplicate(0, 0);
                var ex = dup.PowerClip.ExtractShapes();
                result.AddRange(Cdr.Items(ex));
                dup.Delete();
            }
            if (soltos != null)
                foreach (var s in soltos) result.Add(s.Duplicate(0, 0));
            return result;
        }

        /// <summary>Leva a arte da caixa da origem para a caixa do destino, respeitando FIXO e campos.</summary>
        internal static void MapArt(IEnumerable<Shape> art, Box src, Box dst, ArtScale mode)
        {
            if (src.W <= 0 || src.H <= 0) return;
            double sx = dst.W / src.W, sy = dst.H / src.H;
            double ax = sx, ay = sy;
            if (mode == ArtScale.UniformeCobrir) ax = ay = Math.Max(sx, sy);
            else if (mode == ArtScale.UniformeLargura) ax = ay = sx;

            foreach (var s in art)
            {
                var k = Tag.KindOf(s.Name);
                var b = Cdr.BoxOf(s);
                double ncx = dst.Cx + (b.Cx - src.Cx) * sx;
                double ncy = dst.Cy + (b.Cy - src.Cy) * sy;
                if (k == Tag.Fixo || k == Tag.Id)
                {
                    s.SetPositionEx(cdrReferencePoint.cdrCenter, ncx, ncy);
                }
                else if (Tag.IsField(k) || s.Type == cdrShapeType.cdrTextShape)
                {
                    // nome, número e textos: crescem/diminuem junto com o tamanho, sem deformar
                    double u = Math.Min(sx, sy);
                    if (Math.Abs(u - 1) > 1e-6) s.SetSizeEx(b.Cx, b.Cy, b.W * u, b.H * u);
                    s.SetPositionEx(cdrReferencePoint.cdrCenter, ncx, ncy);
                }
                else if (Tag.IsGab(k))
                {
                    // área de referência: acompanha a graduação nos dois eixos
                    s.SetSizeEx(b.Cx, b.Cy, b.W * sx, b.H * sy);
                    s.SetPositionEx(cdrReferencePoint.cdrCenter, ncx, ncy);
                }
                else
                {
                    s.StretchEx(src.Cx, src.Cy, ax, ay, true);
                    s.Move(dst.Cx - src.Cx, dst.Cy - src.Cy);
                }
            }
        }

        static readonly System.Text.RegularExpressions.Regex RxNumeroModelo = new System.Text.RegularExpressions.Regex(@"^\d{1,2}$");

        /// <summary>
        /// Reconhece campos sem precisar marcar: o texto escrito "NOME" é o nome, um texto só com
        /// número (ex.: "10") é o número, "ADICIONAL" é o adicional. Marcação manual tem prioridade.
        /// </summary>
        public static string FieldKind(Shape s)
        {
            var k = Tag.KindOf(s.Name);
            if (Tag.IsField(k)) return k;
            if (k != null || s.Type != cdrShapeType.cdrTextShape) return null;
            string t;
            try { t = (s.Text.Story.Text ?? "").Trim(); } catch { return null; }
            if (t.Equals("NOME", StringComparison.OrdinalIgnoreCase)) return Tag.Nome;
            if (t.Equals("ADICIONAL", StringComparison.OrdinalIgnoreCase)) return Tag.Adicional;
            if (RxNumeroModelo.IsMatch(t)) return Tag.Numero;
            return null;
        }

        static void FillField(Dictionary<string, List<Shape>> byKind, string kind, string value, Shape gab, Shape peca, Box dst,
                              double pct, Config cfg, List<Shape> art)
        {
            if (!byKind.TryGetValue(kind, out var fields)) return;
            foreach (var f in fields)
            {
                if (Empty(value) || f.Type != cdrShapeType.cdrTextShape)
                {
                    if (Empty(value)) { art.Remove(f); f.Delete(); }
                    continue;
                }
                Cdr.SetText(f, value.Trim());
                if (gab != null)
                {
                    var gb = Cdr.BoxOf(gab);
                    f.SetPositionEx(cdrReferencePoint.cdrCenter, gb.Cx, gb.Cy);
                    Cdr.Fit(f, gb.W, gb.H, cfg.AjustarAoGabarito);
                }
                else
                {
                    // não pode passar da largura máxima (% da peça) nem da borda real da peça naquela altura
                    Cdr.FitInsideShape(f, peca, cfg.MargemTextoPct / 100.0, pct > 0 ? dst.W * pct / 100.0 : 0);
                }
            }
        }

        static Shape CreateId(Application app, Shape peca, string text, Shape gab, Config cfg)
        {
            var t = Cdr.CreateText(app, peca.Layer, text, cfg.FonteIdNome, cfg.FonteId);
            t.Name = new Tag(Tag.Id).ToString();
            if (gab != null)
            {
                var gb = Cdr.BoxOf(gab);
                double ang = 0;
                try { ang = gab.RotationAngle; } catch { }
                t.SetPositionEx(cdrReferencePoint.cdrCenter, gb.Cx, gb.Cy);
                if (Math.Abs(ang) > 0.01) t.RotateEx(ang, gb.Cx, gb.Cy);
                return t;
            }
            // sem gabarito: embaixo, centralizado, no primeiro ponto em que o texto cabe dentro do molde
            var pb = Cdr.BoxOf(peca);
            var tb = Cdr.BoxOf(t);
            double step = Math.Max(0.2, pb.H / 100);
            double y = pb.B + 0.3;
            while (y + tb.H < pb.T)
            {
                double yc = y + tb.H / 2;
                if (Cdr.Inside(peca, pb.Cx - tb.W / 2, y) && Cdr.Inside(peca, pb.Cx + tb.W / 2, y) &&
                    Cdr.Inside(peca, pb.Cx - tb.W / 2, y + tb.H) && Cdr.Inside(peca, pb.Cx + tb.W / 2, y + tb.H))
                    break;
                y += step;
            }
            t.SetPositionEx(cdrReferencePoint.cdrBottomMiddle, pb.Cx, y + 0.2);
            return t;
        }

        // ------------------------------------------------------------------ transferir

        /// <summary>
        /// Transfere a arte da origem para cada destino, ajustando à graduação.
        /// tudo=true leva também os objetos soltos que estão por cima da origem.
        /// </summary>
        public static int Transferir(Application app, Shape origem, IEnumerable<Shape> destinos, bool tudo, bool substituir, Config cfg)
        {
            if (origem == null) throw new UserException("Defina primeiro o molde de origem.");
            var doc = app.ActiveDocument;
            var src = Cdr.BoxOf(origem);
            List<Shape> soltos = new List<Shape>();
            if (tudo)
            {
                foreach (var s in Cdr.Items(origem.Layer.Shapes))
                {
                    if (ReferenceEquals(s, origem) || s.StaticID == origem.StaticID) continue;
                    var k = Tag.KindOf(s.Name);
                    if (k == Tag.Molde || k == Tag.Peca || k == Tag.Rotulo || k == Tag.Indicador || Moldes.Info(s) != null) continue;
                    var b = Cdr.BoxOf(s);
                    if (b.W > src.W * 1.5 || b.H > src.H * 1.5) continue;
                    if (Cdr.Inside(origem, b.Cx, b.Cy)) soltos.Add(s);
                }
            }

            int n = 0;
            foreach (var d in destinos.ToList())
            {
                if (d.StaticID == origem.StaticID || !Cdr.IsClosed(d)) continue;
                if (substituir) Cdr.ClearPowerClip(d);
                var dst = Cdr.BoxOf(d);
                var art = CloneArt(app, origem, null);
                MapArt(art, src, dst, cfg.EscalaArte);
                if (art.Count > 0) Cdr.Range(app, art).AddToPowerClip(d, cdrTriState.cdrFalse);
                if (origem.Fill.Type != cdrFillType.cdrNoFill) d.Fill.CopyAssign(origem.Fill);

                if (soltos.Count > 0)
                {
                    var loose = soltos.Select(s => s.Duplicate(0, 0)).ToList();
                    MapArt(loose, src, dst, cfg.EscalaArte);
                }
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ cópias

        /// <summary>Cria cópias das peças montadas, com novo sequencial e ID atualizado.</summary>
        public static List<Shape> CriarCopias(Application app, IEnumerable<Shape> pecas, int copias, Config cfg)
        {
            var doc = app.ActiveDocument;
            var list = pecas.Where(p => Tag.KindOf(p.Name) == Tag.Peca).ToList();
            if (list.Count == 0) throw new UserException("Selecione peças montadas.");
            if (copias < 1) throw new UserException("Informe a quantidade de cópias.");

            var box = Cdr.BoxOf(Cdr.Range(app, list));
            int seq = NextSeq(doc);
            var created = new List<Shape>();
            // peças da mesma "linha" (mesmo SEQ original) recebem o mesmo novo SEQ
            var groups = list.GroupBy(p => Tag.Parse(p.Name)["SEQ"]).ToList();
            for (int c = 1; c <= copias; c++)
            {
                foreach (var g in groups)
                {
                    foreach (var p in g)
                    {
                        var dup = p.Duplicate((box.W + cfg.Espacamento * 2) * c, 0);
                        var t = Tag.Parse(p.Name);
                        t["SEQ"] = seq.ToString();
                        dup.Name = t.ToString();
                        UpdateId(dup, t, cfg);
                        created.Add(dup);
                    }
                    seq++;
                }
            }
            return created;
        }

        static void UpdateId(Shape peca, Tag t, Config cfg)
        {
            var pc = Cdr.PowerClipOf(peca);
            if (pc == null) return;
            foreach (var s in Cdr.Walk(Cdr.Items(pc.Shapes)))
            {
                if (Tag.KindOf(s.Name) == Tag.Id && s.Type == cdrShapeType.cdrTextShape)
                    Cdr.SetText(s, FormatId(t, cfg));
            }
        }

        // ------------------------------------------------------------------ limpar

        public static int Limpar(Application app, IEnumerable<Shape> shapes, bool removerPreenchimento)
        {
            int n = 0;
            foreach (var s in shapes.ToList())
            {
                bool did = false;
                if (Cdr.HasContents(s)) { Cdr.ClearPowerClip(s); did = true; }
                if (removerPreenchimento && s.Fill.Type != cdrFillType.cdrNoFill) { s.Fill.ApplyNoFill(); did = true; }
                var t = Tag.Parse(s.Name);
                if (t?.Kind == Tag.Peca) { s.Name = ""; did = true; }
                if (did) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ seleção

        public static List<Shape> FindPecas(Document doc, Func<Tag, bool> filter)
            => Cdr.PageShapes(doc).Where(s => { var t = Tag.Parse(s.Name); return t?.Kind == Tag.Peca && filter(t); }).ToList();
    }
}

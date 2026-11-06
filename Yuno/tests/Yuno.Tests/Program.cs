using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Yuno.Engine;
using Corel.Interop.VGCore;

namespace Yuno.Tests
{
    /// <summary>
    /// Teste de ponta a ponta: abre o CorelDRAW via COM, cria um documento novo com moldes de exemplo
    /// e exercita definição, montagem, cópias, ferramentas, encaixe e exportação.
    /// Uso: Yuno.Tests.exe [pasta-de-saída]
    /// </summary>
    internal static class Program
    {
        static int _fail, _ok;
        static Application app;

        static void Check(bool cond, string what)
        {
            if (cond) { _ok++; Console.WriteLine("  ok   " + what); }
            else { _fail++; Console.WriteLine("  FAIL " + what); }
        }

        static void Near(double a, double b, double tol, string what) => Check(Math.Abs(a - b) <= tol, $"{what} ({a:0.###} ≈ {b:0.###})");

        [STAThread]
        static int Main(string[] args)
        {
            Console.WriteLine("Yuno.Tests iniciando...");
            try { return Run(args); }
            catch (Exception ex) { Console.WriteLine("FALHA FATAL: " + ex); return 2; }
        }

        static int Run(string[] args)
        {
            var outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "YunoTest");
            Directory.CreateDirectory(outDir);
            bool wasRunning = Process.GetProcessesByName("CorelDRW").Length > 0;

            Console.WriteLine("Conectando ao CorelDRAW...");
            app = (Application)Activator.CreateInstance(Type.GetTypeFromProgID("CorelDRAW.Application.27"));
            app.Visible = true;
            Document doc = null;
            var sw = Stopwatch.StartNew();
            try
            {
                doc = app.CreateDocument();
                doc.Unit = cdrUnit.cdrCentimeter;
                var cfg = new Config { PastaExportacao = Path.Combine(outDir, "export"), PastaRelatorio = Path.Combine(outDir, "export") };

                // -------------------------------------------------- desenho de exemplo
                Console.WriteLine("[1] Desenhando moldes de exemplo");
                var layer = doc.ActiveLayer;
                // parte 1 FRENTE: pentágono assimétrico ~ 50 x 70
                var frente = new[] { (0.0, 0.0), (50.0, 0.0), (50.0, 55.0), (35.0, 70.0), (5.0, 66.0) };
                // parte 2 MANGA: trapézio assimétrico ~ 30 x 22
                var manga = new[] { (0.0, 0.0), (30.0, 4.0), (26.0, 22.0), (6.0, 20.0) };

                Shape Poly((double, double)[] pts, double scale, double ox, double oy)
                {
                    var c = doc.CreateCurve();
                    var sp = c.CreateSubPath(ox + pts[0].Item1 * scale, oy + pts[0].Item2 * scale);
                    foreach (var p in pts.Skip(1)) sp.AppendLineSegment(ox + p.Item1 * scale, oy + p.Item2 * scale, false);
                    sp.Closed = true;
                    var s = layer.CreateCurve(c);
                    s.Fill.ApplyNoFill();
                    return s;
                }

                var mtzF = Poly(frente, 1.0, 0, 0);
                var mtzM = Poly(manga, 1.0, 0, -40);
                // tamanhos em ordem embaralhada de posição (G, P, M)
                var scales = new Dictionary<string, double> { { "P", 0.9 }, { "M", 1.0 }, { "G", 1.1 } };
                var fShapes = new List<Shape>(); var mShapes = new List<Shape>();
                double x = 70;
                foreach (var t in new[] { "G", "P", "M" })
                {
                    fShapes.Add(Poly(frente, scales[t], x, 0));
                    mShapes.Add(Poly(manga, scales[t], x, -40));
                    x += 65;
                }

                // arte da matriz: fundo + logo fixo dentro do PowerClip
                var fundo = layer.CreateRectangle2(-5, -5, 60, 80, 0, 0, 0, 0);
                fundo.Fill.ApplyUniformFill(app.CreateCMYKColor(100, 0, 0, 0));
                var faixa = layer.CreateRectangle2(-5, 30, 60, 8, 0, 0, 0, 0);
                faixa.Fill.ApplyUniformFill(app.CreateCMYKColor(0, 100, 100, 0));
                var logo = layer.CreateEllipse2(38, 56, 3, 3, 0, 0, false);
                logo.Fill.ApplyUniformFill(app.CreateCMYKColor(0, 0, 100, 0));
                logo.Name = "YUNO:FIXO";
                var logoW = Cdr.BoxOf(logo).W;
                Cdr.Range(app, new[] { fundo, faixa, logo }).AddToPowerClip(mtzF, cdrTriState.cdrFalse);

                var fundoM = layer.CreateRectangle2(-2, -42, 34, 28, 0, 0, 0, 0);
                fundoM.Fill.ApplyUniformFill(app.CreateCMYKColor(0, 60, 0, 0));
                Cdr.Range(app, new[] { fundoM }).AddToPowerClip(mtzM, cdrTriState.cdrFalse);

                // -------------------------------------------------- definir tamanhos
                Console.WriteLine("[2] Definir tamanhos / matriz / campos");
                using (new CorelScope(app, "t"))
                {
                    Moldes.Define(app, new[] { mtzF }, Tag.Matriz, 1, "frente 1", cfg);
                    Moldes.Define(app, new[] { mtzM }, Tag.Matriz, 2, "manga", cfg);
                    Moldes.AutoDefine(app, fShapes, new[] { "P", "M", "G" }, 1, "FRENTE", cfg);
                    Moldes.AutoDefine(app, mShapes, new[] { "P", "M", "G" }, 2, "MANGA", cfg);
                    Tools.InserirCamposPadrao(app, mtzF);
                    // gabarito para o nome
                    var gab = layer.CreateRectangle2(10, 52, 30, 6, 0, 0, 0, 0);
                    Tools.Marcar(app, new[] { gab }, Tag.GabNome);
                    // gabarito do ID rotacionado 90° na manga
                    var gid = layer.CreateRectangle2(2, -36, 6, 1, 0, 0, 0, 0);
                    gid.RotateEx(90, Cdr.BoxOf(gid).Cx, Cdr.BoxOf(gid).Cy);
                    Tools.Marcar(app, new[] { gid }, Tag.GabId);
                    Cdr.Range(app, new[] { gid }).AddToPowerClip(mtzM, cdrTriState.cdrFalse);
                }
                var ms = Moldes.FindAll(doc);
                Check(ms.Count == 8, $"8 moldes definidos (achou {ms.Count})");
                var fP = ms.FirstOrDefault(m => m.Tamanho == "P" && m.Parte == 1);
                Check(fP != null && Math.Abs(Cdr.BoxOf(fP.Shape).W - 45) < 0.1, "auto por área: menor frente = P");
                Check(ms.First(m => m.IsMatriz && m.Parte == 1).Apelido == "FRENTE", "apelido limpo (só letras)");
                Check(Cdr.PageShapes(doc).Count(s => Tag.KindOf(s.Name) == Tag.Rotulo) == 8, "rótulos criados");
                Check(Moldes.Duplicates(ms).Count == 0, "sem duplicados");

                // -------------------------------------------------- pedido
                Console.WriteLine("[3] Importação de pedido (texto colado)");
                var colado = "Nome\tTam\tQtd\tNum\tAdic\njoão silva\tM\t1\t10\tcapitão\nMaria\tg\t2\t7,00\t\nSEM NOME\tP\t\t\tN/T\n";
                var avisos = new List<string>();
                var linhas = OrderImport.FromRows(OrderImport.ParseDelimited(colado), cfg, avisos);
                Check(linhas.Count == 3, "3 linhas lidas");
                Check(linhas[1].Tamanho == "G" && linhas[1].Quantidade == 2 && linhas[1].Numero == "7", "normaliza tamanho/qtd/número");
                linhas[2].Nome = "";

                // -------------------------------------------------- montar
                Console.WriteLine("[4] Montagem");
                AssemblyResult res;
                using (new CorelScope(app, "t")) res = Assembler.Montar(app, linhas, "1234", cfg, true);
                foreach (var a in res.Avisos) Console.WriteLine("     aviso: " + a);
                Check(res.Pecas == 8 && res.Linhas == 4, $"8 peças em 4 linhas (={res.Pecas}/{res.Linhas})");

                var pecas = Assembler.FindPecas(doc, t => true);
                var joaoF = pecas.First(p => { var t = Tag.Parse(p.Name); return t["N"] == "joão silva" && t["P"] == "1"; });
                var contF = Cdr.Items(joaoF.PowerClip.Shapes).ToList();
                var nome = contF.FirstOrDefault(s => Tag.KindOf(s.Name) == Tag.Nome);
                Check(nome != null && nome.Text.Story.Text == "JOÃO SILVA", "nome em maiúsculas dentro da peça");
                var num = contF.FirstOrDefault(s => Tag.KindOf(s.Name) == Tag.Numero);
                Check(num != null && num.Text.Story.Text == "10", "número preenchido");
                var id = contF.FirstOrDefault(s => Tag.KindOf(s.Name) == Tag.Id);
                Check(id != null && id.Text.Story.Text.StartsWith("1234-") && id.Text.Story.Text.Contains("-M-FRENTE"), $"ID = {id?.Text.Story.Text}");
                Check(!contF.Any(s => Tag.IsGab(Tag.KindOf(s.Name))), "gabaritos removidos da peça");
                var pb = Cdr.BoxOf(joaoF);
                Check(nome != null && Cdr.BoxOf(nome).W <= 30 * 1.0 + 0.05, "nome cabe no gabarito");
                Check(id != null && Cdr.Inside(joaoF, Cdr.BoxOf(id).Cx, Cdr.BoxOf(id).Cy), "ID dentro do molde");
                var lg = contF.First(s => Tag.KindOf(s.Name) == Tag.Fixo);
                Near(Cdr.BoxOf(lg).W, logoW, 0.01, "logo FIXO mantém tamanho");
                Check(joaoF.Fill.Type == cdrFillType.cdrUniformFill, "matriz sem cor -> peça branca");

                var gF = pecas.First(p => { var t = Tag.Parse(p.Name); return t["T"] == "G" && t["P"] == "1"; });
                var fundoG = Cdr.Items(gF.PowerClip.Shapes).OrderByDescending(s => Cdr.BoxOf(s).Area).First();
                Near(Cdr.BoxOf(fundoG).W, 60 * 1.1, 0.05, "fundo graduado para G (esticado)");

                var semNome = pecas.First(p => Tag.Parse(p.Name)["T"] == "P" && Tag.Parse(p.Name)["P"] == "1");
                Check(!Cdr.Items(semNome.PowerClip.Shapes).Any(s => Tag.IsField(Tag.KindOf(s.Name))), "campos vazios / N/T removidos");

                var mangaM = pecas.First(p => { var t = Tag.Parse(p.Name); return t["T"] == "M" && t["P"] == "2"; });
                var idM = Cdr.Items(mangaM.PowerClip.Shapes).First(s => Tag.KindOf(s.Name) == Tag.Id);
                Near(Math.Abs(idM.RotationAngle % 180), 90, 0.5, "ID segue rotação do gabarito");

                Check(Cdr.PageShapes(doc).Count(s => Tag.KindOf(s.Name) == Tag.Indicador) == 4, "indicadores criados");
                Check(Cdr.Items(mtzF.PowerClip.Shapes).Count() == 3, "matriz não foi alterada");

                // -------------------------------------------------- erros previstos
                Console.WriteLine("[5] Validações");
                try { using (new CorelScope(app, "t")) Assembler.Montar(app, new[] { new OrderLine { Tamanho = "XG" } }, "", cfg); Check(false, "tamanho inexistente gera erro"); }
                catch (UserException e) { Check(e.Message.Contains("XG"), "tamanho inexistente gera erro claro"); }

                // -------------------------------------------------- cópias
                Console.WriteLine("[6] Criar cópias");
                List<Shape> copias;
                var linhaJoao = pecas.Where(p => Tag.Parse(p.Name)["N"] == "joão silva").ToList();
                using (new CorelScope(app, "t")) copias = Assembler.CriarCopias(app, linhaJoao, 1, cfg);
                Check(copias.Count == 2, "2 cópias");
                var seqs = copias.Select(c => Tag.Parse(c.Name)["SEQ"]).Distinct().ToList();
                Check(seqs.Count == 1 && seqs[0] == "5", $"cópias com novo sequencial 5 (={string.Join(",", seqs)})");
                var idCopia = Cdr.Items(copias[0].PowerClip.Shapes).First(s => Tag.KindOf(s.Name) == Tag.Id).Text.Story.Text;
                Check(idCopia.StartsWith("1234-5-"), $"ID da cópia atualizado ({idCopia})");

                // -------------------------------------------------- transferir
                Console.WriteLine("[7] Transferir");
                var alvo = Poly(frente, 1.2, 0, -120);
                using (new CorelScope(app, "t")) Assembler.Transferir(app, mtzF, new[] { alvo }, false, true, cfg);
                Check(Cdr.HasContents(alvo) && Cdr.Items(alvo.PowerClip.Shapes).Count() == 3, "arte transferida para molde maior");
                var fundoAlvo = Cdr.Items(alvo.PowerClip.Shapes).OrderByDescending(s => Cdr.BoxOf(s).Area).First();
                Near(Cdr.BoxOf(fundoAlvo).W, 60 * 1.2, 0.05, "transferência ajusta escala");

                // -------------------------------------------------- ferramentas
                Console.WriteLine("[8] Ferramentas");
                using (new CorelScope(app, "t"))
                {
                    var amarelo = app.CreateCMYKColor(0, 0, 100, 0);
                    var verde = app.CreateCMYKColor(100, 0, 100, 0);
                    int n = Tools.SubstituirCor(app, new[] { alvo }, amarelo, verde, true, true, true);
                    Check(n == 1, $"substituir cor dentro de PowerClip ({n})");
                    var t1 = Cdr.CreateText(app, layer, "TESTE", "Arial", 20);
                    t1.Move(0, -160);
                    int c = Tools.TextoParaCurvas(new[] { t1 }, true);
                    Check(c == 1, "texto convertido em curvas");
                    var r1 = layer.CreateRectangle2(100, -160, 10, 5, 0, 0, 0, 0);
                    var r2 = layer.CreateRectangle2(130, -170, 4, 4, 0, 0, 0, 0);
                    Tools.EmpilharCentro(new[] { r1, r2 });
                    Near(Cdr.BoxOf(r2).Cx, Cdr.BoxOf(r1).Cx, 0.001, "empilhar ao centro");
                    Tools.AplicarContorno(app, new[] { r1 }, 0.5, 2);
                    Near(r1.Outline.Width, 0.05, 0.001, "contorno 0,5 mm");
                    r1.Delete(); r2.Delete();
                }
                var rel = Tools.RelatorioTexto(Tools.Contar(Cdr.Items(doc.ActivePage.Shapes)), cfg.Tamanhos);
                Check(rel.Contains("TOTAL: 10 partes"), "relatório conta 10 partes");

                // -------------------------------------------------- encaixe sobre outro
                Console.WriteLine("[9] Encaixe sobre outro");
                // formas-base simulando o retorno do software de nesting: rotações e espelhamento
                var esperado = new List<(Shape peca, double cx, double cy)>();
                var bases = new List<Shape>();
                var alvoPecas = pecas.Where(p => Tag.Parse(p.Name)["T"] != "P").Take(4).ToList();
                var transf = new[] { (0, false), (90, false), (180, true), (270, true) };
                double bx = 0;
                for (int i = 0; i < alvoPecas.Count; i++)
                {
                    var b = alvoPecas[i].Duplicate(0, 0);
                    Cdr.ClearPowerClip(b);
                    b.Name = "";
                    b.Fill.ApplyNoFill();
                    if (transf[i].Item2) b.Flip(cdrFlipAxes.cdrFlipHorizontal);
                    var bb0 = Cdr.BoxOf(b);
                    if (transf[i].Item1 != 0) b.RotateEx(transf[i].Item1, bb0.Cx, bb0.Cy);
                    b.SetPositionEx(cdrReferencePoint.cdrBottomLeft, 400 + bx, -300);
                    var bb = Cdr.BoxOf(b);
                    bx += bb.W + 1;
                    bases.Add(b);
                    esperado.Add((alvoPecas[i], bb.Cx, bb.Cy));
                }
                // uma base extra igual à 1ª -> deve gerar cópia
                var extra = bases[0].Duplicate(0, -100);
                bases.Add(extra);
                // uma base sem par
                var semPar = layer.CreateRectangle2(400, -500, 13, 17, 0, 0, 0, 0);
                bases.Add(semPar);

                NestResult nr;
                cfg.EncaixeApagarBase = true;
                using (new CorelScope(app, "t")) nr = Nesting.Encaixar(app, alvoPecas.Concat(bases).ToList(), cfg);
                Check(nr.Encaixadas == 5, $"5 encaixadas ({nr.Encaixadas})");
                Check(nr.Copiadas == 1, $"1 cópia criada ({nr.Copiadas})");
                Check(nr.SemPar.Count == 1, $"1 base sem par ({nr.SemPar.Count})");
                foreach (var (p, cx, cy) in esperado)
                {
                    var b = Cdr.BoxOf(p);
                    Check(Math.Abs(b.Cx - cx) < 0.05 && Math.Abs(b.Cy - cy) < 0.05, $"peça {Tag.Parse(p.Name)["T"]}/{Tag.Parse(p.Name)["A"]} sobre a base");
                }
                Check(Math.Abs(esperado[1].peca.RotationAngle - 90) < 0.5, $"rotação 90° detectada ({esperado[1].peca.RotationAngle:0})");

                // -------------------------------------------------- exportar
                Console.WriteLine("[10] Exportar");
                var exp = new List<Shape> { esperado[0].peca, esperado[2].peca };
                cfg.Formato = "JPG"; cfg.Dpi = 72;
                ExportResult er;
                using (new CorelScope(app, "t")) er = Exporter.Exportar(app, exp, "teste", cfg.Tecidos[0], true, cfg);
                Check(er.Arquivos.Count == 2 && er.Arquivos.All(f => File.Exists(f) && new FileInfo(f).Length > 1000), "2 JPGs exportados");
                Check(er.Relatorio != null && File.Exists(er.Relatorio), "relatório gravado");
                cfg.Formato = "PDF";
                using (new CorelScope(app, "t")) er = Exporter.Exportar(app, exp, "teste", null, false, cfg);
                Check(er.Arquivos.Count == 1 && File.Exists(er.Arquivos[0]) && new FileInfo(er.Arquivos[0]).Length > 1000, "PDF exportado");

                var cdr = Path.Combine(outDir, "exemplo_teste.cdr");
                if (File.Exists(cdr)) File.Delete(cdr);
                doc.SaveAs(cdr);
                Console.WriteLine("Documento de teste salvo em " + cdr);
            }
            catch (Exception ex)
            {
                _fail++;
                Console.WriteLine("EXCEÇÃO: " + ex);
            }
            finally
            {
                try { if (doc != null) { doc.Dirty = false; doc.Close(); } } catch { }
                if (!wasRunning) { try { app.Quit(); } catch { } }
            }
            Console.WriteLine($"\n{_ok} ok, {_fail} falha(s) em {sw.Elapsed.TotalSeconds:0}s");
            return _fail == 0 ? 0 : 1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Corel.Interop.VGCore;

namespace AhcMoldes.Engine
{
    public class NestResult
    {
        public int Encaixadas;
        public int Copiadas;
        public List<Shape> SemPar = new List<Shape>();
    }

    /// <summary>
    /// "Encaixe sobre outro": recebe na seleção as peças montadas + as formas-base de um encaixe
    /// (curvas sem marcação, vindas do software de nesting) e coloca cada peça montada exatamente
    /// sobre a forma-base equivalente, descobrindo rotação (0/90/180/270) e espelhamento.
    /// </summary>
    public static class Nesting
    {
        const int Amostras = 36;

        class Modelo
        {
            public string Chave;
            public List<Shape> Livres = new List<Shape>();
            public Shape Reserva;               // cópia intacta para gerar cópias extras
            public Box Caixa;
            public int Nos;
            public List<(double x, double y)> Pontos; // contorno relativo ao centro
        }

        public static NestResult Encaixar(Application app, IEnumerable<Shape> selecao, Config cfg, Action<int, int> progresso = null, Func<bool> cancelar = null)
        {
            var res = new NestResult();
            var todas = Cdr.Walk(selecao).ToList();
            var pecas = todas.Where(s => Tag.KindOf(s.Name) == Tag.Peca).ToList();
            var bases = todas.Where(s => Tag.Parse(s.Name) == null && Moldes.Info(s) == null && !Cdr.IsGroup(s) && Cdr.IsClosed(s)
                                         && s.Type != cdrShapeType.cdrTextShape && s.Type != cdrShapeType.cdrBitmapShape).ToList();
            if (pecas.Count == 0) throw new UserException("A seleção não tem peças montadas.");
            if (bases.Count == 0) throw new UserException("A seleção não tem formas-base do encaixe (curvas fechadas sem marcação).");

            double tol = Math.Max(0.01, cfg.EncaixeTolerancia);
            var modelos = new List<Modelo>();
            foreach (var g in pecas.GroupBy(p => KeyOf(p)))
            {
                var rep = g.First();
                var m = new Modelo { Chave = g.Key, Caixa = Cdr.BoxOf(rep) };
                m.Livres.AddRange(g);
                (m.Pontos, m.Nos) = Outline(rep, m.Caixa);
                modelos.Add(m);
            }

            int i = 0;
            foreach (var b in bases)
            {
                if (cancelar?.Invoke() == true) break;
                progresso?.Invoke(++i, bases.Count);

                var bb = Cdr.BoxOf(b);
                int nosBase = NodeCount(b);
                double hot = tol * 2 + 0.05;

                Modelo melhor = null; int melhorRot = 0; bool melhorEsp = false; double melhorScore = 0;
                foreach (var m in modelos)
                {
                    if (cfg.EncaixeSimilaridadeNos > 0 && nosBase > 0 && m.Nos > 0)
                    {
                        double sim = Math.Min(nosBase, m.Nos) / (double)Math.Max(nosBase, m.Nos);
                        if (sim < cfg.EncaixeSimilaridadeNos / 100.0) continue;
                    }
                    bool mesma = Near(bb.W, m.Caixa.W, tol) && Near(bb.H, m.Caixa.H, tol);
                    bool girada = Near(bb.W, m.Caixa.H, tol) && Near(bb.H, m.Caixa.W, tol);
                    if (!mesma && !girada) continue;

                    foreach (int rot in new[] { 0, 180, 90, 270 })
                    {
                        if ((rot == 0 || rot == 180) && !mesma) continue;
                        if ((rot == 90 || rot == 270) && !girada) continue;
                        foreach (bool esp in cfg.EncaixeEspelhar ? new[] { false, true } : new[] { false })
                        {
                            double sc = Score(b, bb, m.Pontos, rot, esp, hot, melhorScore);
                            if (sc > melhorScore) { melhorScore = sc; melhor = m; melhorRot = rot; melhorEsp = esp; }
                            if (melhorScore > 0.99) break;
                        }
                        if (melhorScore > 0.99) break;
                    }
                    if (melhorScore > 0.99) break;
                }

                if (melhor == null || melhorScore < 0.85)
                {
                    res.SemPar.Add(b);
                    continue;
                }

                Shape peca;
                if (melhor.Livres.Count > 0)
                {
                    peca = melhor.Livres[0];
                    if (melhor.Reserva == null && cfg.EncaixeCopiarFaltantes) melhor.Reserva = peca.Duplicate(0, 0);
                    melhor.Livres.RemoveAt(0);
                }
                else if (cfg.EncaixeCopiarFaltantes && melhor.Reserva != null)
                {
                    peca = melhor.Reserva.Duplicate(0, 0);
                    res.Copiadas++;
                }
                else { res.SemPar.Add(b); continue; }

                Aplicar(peca, melhorRot, melhorEsp, bb.Cx, bb.Cy);
                peca.OrderToFront();
                res.Encaixadas++;
                if (cfg.EncaixeApagarBase) b.Delete();
            }

            foreach (var m in modelos) m.Reserva?.Delete();

            // marca em vermelho as bases que não acharam par
            var vermelho = app.CreateCMYKColor(0, 100, 100, 0);
            foreach (var b in res.SemPar)
            {
                try { b.Outline.Width = 0.1; b.Outline.Color.CopyAssign(vermelho); } catch { }
            }
            return res;
        }

        static string KeyOf(Shape p)
        {
            var t = Tag.Parse(p.Name);
            var b = Cdr.BoxOf(p);
            return $"{t["T"]}|{t["P"]}|{Math.Round(b.W, 1)}|{Math.Round(b.H, 1)}";
        }

        static bool Near(double a, double b, double tol) => Math.Abs(a - b) <= tol + Math.Max(a, b) * 0.003;

        static void Aplicar(Shape peca, int rot, bool esp, double cx, double cy)
        {
            if (esp) peca.Flip(cdrFlipAxes.cdrFlipHorizontal);
            if (rot != 0)
            {
                var b = Cdr.BoxOf(peca);
                peca.RotateEx(rot, b.Cx, b.Cy);
            }
            peca.SetPositionEx(cdrReferencePoint.cdrCenter, cx, cy);
        }

        /// <summary>Fração dos pontos do contorno da peça (transformada) que caem na borda da base.</summary>
        static double Score(Shape baseShape, Box bb, List<(double x, double y)> pts, int rot, bool esp, double hot, double atual)
        {
            double rad = rot * Math.PI / 180, cos = Math.Round(Math.Cos(rad)), sin = Math.Round(Math.Sin(rad));
            int ok = 0, n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                double x = esp ? -pts[i].x : pts[i].x, y = pts[i].y;
                double px = bb.Cx + x * cos - y * sin;
                double py = bb.Cy + x * sin + y * cos;
                if (baseShape.IsOnShape(px, py, hot) == cdrPositionOfPointOverShape.cdrOnMarginOfShape) ok++;
                // não alcança mais o melhor atual: para cedo
                if ((ok + (n - i - 1)) / (double)n < atual) return 0;
            }
            return ok / (double)n;
        }

        static (List<(double, double)>, int) Outline(Shape peca, Box caixa)
        {
            var dup = peca.Duplicate(0, 0);
            try
            {
                Cdr.ClearPowerClip(dup);
                if (dup.Type != cdrShapeType.cdrCurveShape) dup.ConvertToCurves();
                var curve = dup.Curve;
                SubPath sp = null; double maxLen = 0;
                for (int i = 1; i <= curve.SubPaths.Count; i++)
                {
                    var s = curve.SubPaths[i];
                    if (s.Length > maxLen) { maxLen = s.Length; sp = s; }
                }
                var pts = new List<(double, double)>();
                if (sp != null)
                {
                    for (int i = 0; i < Amostras; i++)
                    {
                        double x = 0, y = 0;
                        sp.GetPointPositionAt(out x, out y, maxLen * i / Amostras, cdrSegmentOffsetType.cdrAbsoluteSegmentOffset);
                        pts.Add((x - caixa.Cx, y - caixa.Cy));
                    }
                }
                return (pts, curve.Nodes.Count);
            }
            finally { dup.Delete(); }
        }

        static int NodeCount(Shape s)
        {
            try { return s.Type == cdrShapeType.cdrCurveShape ? s.Curve.Nodes.Count : 0; } catch { return 0; }
        }
    }
}

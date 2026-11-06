using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Corel.Interop.VGCore;

namespace Yuno.Engine
{
    /// <summary>Ferramentas de apoio: gabaritos, alinhamento, contorno, cores, relatório.</summary>
    public static class Tools
    {
        // ------------------------------------------------------------------ gabaritos / campos

        /// <summary>Marca a seleção como campo (NOME/NUMERO/ADICIONAL), gabarito (GAB_*) ou FIXO.</summary>
        public static int Marcar(Application app, IEnumerable<Shape> shapes, string kind)
        {
            int n = 0;
            foreach (var s in shapes.ToList())
            {
                if (Tag.IsField(kind) && s.Type != cdrShapeType.cdrTextShape)
                    throw new UserException("Campos NOME, NÚMERO e ADICIONAL precisam ser objetos de texto artístico.");
                s.Name = new Tag(kind).ToString();
                if (Tag.IsGab(kind))
                {
                    s.Fill.ApplyNoFill();
                    var cor = kind == Tag.GabId ? app.CreateCMYKColor(100, 0, 0, 0) : app.CreateCMYKColor(0, 100, 100, 0);
                    s.Outline.Width = 0.02;
                    s.Outline.Color.CopyAssign(cor);
                }
                n++;
            }
            return n;
        }

        public static int Desmarcar(IEnumerable<Shape> shapes)
        {
            int n = 0;
            foreach (var s in shapes.ToList())
            {
                var k = Tag.KindOf(s.Name);
                if (k == null || k == Tag.Molde || k == Tag.Peca) continue;
                s.Name = ""; n++;
            }
            return n;
        }

        /// <summary>Cria textos NOME / 10 / ADICIONAL por cima da matriz selecionada, já marcados.</summary>
        public static void InserirCamposPadrao(Application app, Shape matriz)
        {
            var info = Moldes.Info(matriz);
            if (info == null || !info.IsMatriz) throw new UserException("Selecione uma MATRIZ (defina-a primeiro em Moldes).");
            var b = Cdr.BoxOf(matriz);
            var layer = matriz.Layer;

            var nome = Cdr.CreateText(app, layer, "NOME", "Arial", 48);
            nome.Text.Story.Bold = true;
            nome.SetPositionEx(cdrReferencePoint.cdrCenter, b.Cx, b.B + b.H * 0.80);
            nome.Name = new Tag(Tag.Nome).ToString();

            var num = Cdr.CreateText(app, layer, "10", "Arial", 200);
            num.Text.Story.Bold = true;
            num.SetPositionEx(cdrReferencePoint.cdrCenter, b.Cx, b.B + b.H * 0.50);
            num.Name = new Tag(Tag.Numero).ToString();

            var ad = Cdr.CreateText(app, layer, "ADICIONAL", "Arial", 28);
            ad.SetPositionEx(cdrReferencePoint.cdrCenter, b.Cx, b.B + b.H * 0.22);
            ad.Name = new Tag(Tag.Adicional).ToString();
        }

        // ------------------------------------------------------------------ alinhamento e rotação

        public static void Espelhar(IEnumerable<Shape> shapes, bool horizontal)
        {
            foreach (var s in shapes.ToList())
                s.Flip(horizontal ? cdrFlipAxes.cdrFlipHorizontal : cdrFlipAxes.cdrFlipVertical);
        }

        public static void Girar(IEnumerable<Shape> shapes, double angulo)
        {
            foreach (var s in shapes.ToList())
            {
                var b = Cdr.BoxOf(s);
                s.RotateEx(angulo, b.Cx, b.Cy);
            }
        }

        /// <summary>Sobrepõe tudo no centro do maior; o menor fica por cima.</summary>
        public static void EmpilharCentro(IEnumerable<Shape> shapes)
        {
            var list = shapes.Select(s => new { s, b = Cdr.BoxOf(s) }).OrderByDescending(x => x.b.Area).ToList();
            if (list.Count < 2) return;
            double cx = list[0].b.Cx, cy = list[0].b.Cy;
            foreach (var x in list)
            {
                x.s.SetPositionEx(cdrReferencePoint.cdrCenter, cx, cy);
                x.s.OrderToFront();
            }
        }

        /// <summary>Coloca lado a lado (eixo X), do menor para o maior, alinhados pela base.</summary>
        public static void EmpilharHorizontal(IEnumerable<Shape> shapes, double espaco)
        {
            var list = shapes.Select(s => new { s, b = Cdr.BoxOf(s) }).OrderBy(x => x.b.Area).ToList();
            if (list.Count < 2) return;
            double x0 = list.Min(v => v.b.L), y0 = list.Min(v => v.b.B);
            foreach (var v in list)
            {
                v.s.SetPositionEx(cdrReferencePoint.cdrBottomLeft, x0, y0);
                x0 += v.b.W + espaco;
            }
        }

        // ------------------------------------------------------------------ texto em curvas

        public static int TextoParaCurvas(IEnumerable<Shape> shapes, bool dentroDePowerClips)
        {
            var textos = Cdr.Walk(shapes, dentroDePowerClips)
                .Where(s => s.Type == cdrShapeType.cdrTextShape && Tag.KindOf(s.Name) != Tag.Id)
                .ToList();
            foreach (var t in textos) t.ConvertToCurves();
            return textos.Count;
        }

        // ------------------------------------------------------------------ contorno

        public static Color CorContorno(Application app, int idx)
        {
            switch (idx)
            {
                case 1: return app.CreateCMYKColor(0, 0, 0, 70);
                case 2: return app.CreateCMYKColor(0, 100, 100, 0);
                case 3: return app.CreateCMYKColor(0, 0, 0, 0);
                default: return app.CreateCMYKColor(0, 0, 0, 100);
            }
        }

        public static int AplicarContorno(Application app, IEnumerable<Shape> shapes, double mm, int corIdx)
        {
            var cor = CorContorno(app, corIdx);
            int n = 0;
            foreach (var s in shapes.ToList())
            {
                if (mm <= 0) s.Outline.SetNoOutline();
                else
                {
                    s.Outline.Width = mm / 10.0; // documento está em cm
                    s.Outline.Color.CopyAssign(cor);
                }
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ substituir cor

        public static int SubstituirCor(Application app, IEnumerable<Shape> shapes, Color original, Color nova,
                                        bool preenchimentos, bool contornos, bool dentroDePowerClips)
        {
            int n = 0;
            foreach (var s in Cdr.Walk(shapes, dentroDePowerClips).ToList())
            {
                if (Cdr.IsGroup(s)) continue;
                try
                {
                    if (preenchimentos && s.Fill.Type == cdrFillType.cdrUniformFill && s.Fill.UniformColor.IsSame(original))
                    {
                        s.Fill.ApplyUniformFill(nova);
                        n++;
                    }
                }
                catch { }
                try
                {
                    if (contornos && s.Outline.Type != cdrOutlineType.cdrNoOutline && s.Outline.Color.IsSame(original))
                    {
                        s.Outline.Color.CopyAssign(nova);
                        n++;
                    }
                }
                catch { }
            }
            return n;
        }

        public static string DescreverCor(Color c)
        {
            try
            {
                if (c.Type == cdrColorType.cdrColorCMYK)
                    return $"C{c.CMYKCyan} M{c.CMYKMagenta} Y{c.CMYKYellow} K{c.CMYKBlack}";
                return c.get_Name(true);
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------------ relatório

        public class LinhaRelatorio { public string Tamanho; public string Parte; public int Qtd; }

        public static List<LinhaRelatorio> Contar(IEnumerable<Shape> shapes)
        {
            return Cdr.Walk(shapes)
                .Select(s => Tag.Parse(s.Name))
                .Where(t => t?.Kind == Tag.Peca)
                .GroupBy(t => new { T = t["T"], P = t["P"] + (t["A"].Length > 0 ? "-" + t["A"] : "") })
                .Select(g => new LinhaRelatorio { Tamanho = g.Key.T, Parte = g.Key.P, Qtd = g.Count() })
                .OrderBy(l => l.Tamanho).ThenBy(l => l.Parte)
                .ToList();
        }

        public static string RelatorioTexto(List<LinhaRelatorio> linhas, IList<string> ordemTamanhos = null)
        {
            if (linhas.Count == 0) return "Nenhuma peça montada encontrada.";
            int Ord(string t) { int i = ordemTamanhos?.IndexOf(t) ?? -1; return i < 0 ? 999 : i; }
            var sb = new StringBuilder();
            sb.AppendLine("Tamanho\tParte\tQtd");
            foreach (var l in linhas.OrderBy(l => Ord(l.Tamanho)).ThenBy(l => l.Tamanho).ThenBy(l => l.Parte))
                sb.AppendLine($"{l.Tamanho}\t{l.Parte}\t{l.Qtd}");
            sb.AppendLine();
            foreach (var g in linhas.GroupBy(l => l.Tamanho).OrderBy(g => Ord(g.Key)))
                sb.AppendLine($"{g.Key}: {g.Sum(x => x.Qtd)} partes");
            sb.AppendLine($"TOTAL: {linhas.Sum(l => l.Qtd)} partes");
            return sb.ToString();
        }
    }
}

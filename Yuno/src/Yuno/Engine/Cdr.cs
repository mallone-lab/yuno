using System;
using System.Collections.Generic;
using Corel.Interop.VGCore;

namespace Yuno.Engine
{
    /// <summary>Erro previsto, com mensagem pronta para mostrar ao usuário.</summary>
    public class UserException : Exception
    {
        public UserException(string message) : base(message) { }
    }

    /// <summary>Retângulo delimitador em unidades do documento (cm durante as operações).</summary>
    public struct Box
    {
        public double L, B, W, H;
        public double R => L + W;
        public double T => B + H;
        public double Cx => L + W / 2;
        public double Cy => B + H / 2;
        public double Area => W * H;
        public override string ToString() => $"[{L:0.##},{B:0.##} {W:0.##}x{H:0.##}]";
    }

    /// <summary>
    /// Abre um bloco de operação no Corel: unidade em cm, um único "desfazer",
    /// otimização ligada. Restaura tudo no Dispose.
    /// </summary>
    public sealed class CorelScope : IDisposable
    {
        private readonly Application _app;
        private readonly cdrUnit _oldUnit;
        private readonly bool _oldOpt;
        private readonly bool _oldEvents;
        public Document Doc { get; }

        public CorelScope(Application app, string undoName)
        {
            _app = app ?? throw new UserException("CorelDRAW não disponível.");
            Doc = app.ActiveDocument ?? throw new UserException("Nenhum documento aberto.");
            _oldUnit = Doc.Unit;
            Doc.Unit = cdrUnit.cdrCentimeter;
            Doc.BeginCommandGroup(undoName);
            _oldOpt = app.Optimization;
            _oldEvents = app.EventsEnabled;
            app.Optimization = true;
            app.EventsEnabled = false;
        }

        public void Dispose()
        {
            try { _app.EventsEnabled = _oldEvents; } catch { }
            try { _app.Optimization = _oldOpt; } catch { }
            try { Doc.EndCommandGroup(); } catch { }
            try { Doc.Unit = _oldUnit; } catch { }
            try { _app.Refresh(); } catch { }
        }
    }

    public static class Cdr
    {
        public static Box BoxOf(Shape s)
        {
            double x = 0, y = 0, w = 0, h = 0;
            s.GetBoundingBox(out x, out y, out w, out h, false);
            return new Box { L = x, B = y, W = w, H = h };
        }

        public static Box BoxOf(ShapeRange r)
        {
            double x = 0, y = 0, w = 0, h = 0;
            r.GetBoundingBox(out x, out y, out w, out h, false);
            return new Box { L = x, B = y, W = w, H = h };
        }

        public static IEnumerable<Shape> Items(Shapes shapes)
        {
            int n = shapes.Count;
            for (int i = 1; i <= n; i++) yield return shapes[i];
        }

        public static IEnumerable<Shape> Items(ShapeRange range)
        {
            int n = range.Count;
            for (int i = 1; i <= n; i++) yield return range[i];
        }

        public static bool IsGroup(Shape s) => s.Type == cdrShapeType.cdrGroupShape;

        /// <summary>Percorre formas entrando em grupos (e opcionalmente em PowerClips).</summary>
        public static IEnumerable<Shape> Walk(IEnumerable<Shape> roots, bool intoPowerClips = false)
        {
            var stack = new Stack<Shape>();
            var list = new List<Shape>(roots);
            for (int i = list.Count - 1; i >= 0; i--) stack.Push(list[i]);
            while (stack.Count > 0)
            {
                var s = stack.Pop();
                if (s.Type == cdrShapeType.cdrGuidelineShape) continue;
                yield return s;
                if (IsGroup(s))
                {
                    var kids = new List<Shape>(Items(s.Shapes));
                    for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
                }
                if (intoPowerClips)
                {
                    var pc = PowerClipOf(s);
                    if (pc != null)
                    {
                        var kids = new List<Shape>(Items(pc.Shapes));
                        for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
                    }
                }
            }
        }

        public static IEnumerable<Shape> PageShapes(Document doc, bool intoPowerClips = false)
            => Walk(Items(doc.ActivePage.Shapes), intoPowerClips);

        public static PowerClip PowerClipOf(Shape s)
        {
            try { return s.PowerClip; } catch { return null; }
        }

        public static bool HasContents(Shape s)
        {
            var pc = PowerClipOf(s);
            return pc != null && pc.Shapes.Count > 0;
        }

        public static void ClearPowerClip(Shape s)
        {
            var pc = PowerClipOf(s);
            if (pc != null && pc.Shapes.Count > 0) pc.ExtractShapes().Delete();
        }

        public static bool IsClosed(Shape s)
        {
            switch (s.Type)
            {
                case cdrShapeType.cdrRectangleShape:
                case cdrShapeType.cdrEllipseShape:
                case cdrShapeType.cdrPolygonShape:
                case cdrShapeType.cdrPerfectShape:
                    return true;
                case cdrShapeType.cdrCurveShape:
                    try { return s.Curve.Closed; } catch { return false; }
                default:
                    return false;
            }
        }

        public static double AreaOf(Shape s)
        {
            if (s.Type == cdrShapeType.cdrCurveShape)
            {
                try { return Math.Abs(s.Curve.Area); } catch { }
            }
            var b = BoxOf(s);
            return b.Area;
        }

        public static bool Inside(Shape s, double x, double y)
            => s.IsOnShape(x, y, -1) != cdrPositionOfPointOverShape.cdrOutsideShape;

        public static ShapeRange Range(Application app, IEnumerable<Shape> shapes)
        {
            var r = app.CreateShapeRange();
            foreach (var s in shapes) r.Add(s);
            return r;
        }

        public static void Select(Document doc, IEnumerable<Shape> shapes)
        {
            doc.ClearSelection();
            foreach (var s in shapes) s.AddToSelection();
        }

        public static Shape CreateText(Application app, Layer layer, string text, string font, float sizePt)
        {
            var t = layer.CreateArtisticText(0, 0, text);
            var story = t.Text.Story;
            story.Font = font;
            story.Size = sizePt;
            t.Fill.ApplyUniformFill(app.CreateCMYKColor(0, 0, 0, 100));
            t.Outline.SetNoOutline();
            return t;
        }

        public static void SetText(Shape t, string value)
        {
            var b = BoxOf(t);
            t.Text.Story.Text = value;
            t.SetPositionEx(cdrReferencePoint.cdrCenter, b.Cx, b.Cy);
        }

        /// <summary>
        /// Meia-largura livre da forma na altura y, a partir de cx (menor distância até a borda esquerda ou direita).
        /// Retorna -1 se o ponto (cx, y) já está fora da forma.
        /// </summary>
        public static double HalfSpan(Shape molde, Box mb, double cx, double y)
        {
            if (!Inside(molde, cx, y)) return -1;
            double Edge(int dir)
            {
                double step = Math.Max(mb.W / 60, 0.05), d = 0;
                while (d < mb.W && Inside(molde, cx + dir * (d + step), y)) d += step;
                double lo = d, hi = d + step;                 // refina a borda por bisseção
                for (int i = 0; i < 8; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (Inside(molde, cx + dir * mid, y)) lo = mid; else hi = mid;
                }
                return lo;
            }
            return Math.Min(Edge(-1), Edge(+1));
        }

        /// <summary>
        /// Reduz o texto (proporcionalmente, mantendo o centro) até caber dentro da forma real da peça
        /// na altura em que ele está, deixando uma margem; e até no máximo maxW (0 = sem limite).
        /// </summary>
        public static void FitInsideShape(Shape t, Shape molde, double margem, double maxW)
        {
            var mb = BoxOf(molde);
            for (int iter = 0; iter < 4; iter++)
            {
                var b = BoxOf(t);
                if (b.W <= 0 || b.H <= 0) return;
                double livre = double.MaxValue;
                foreach (var y in new[] { b.B, b.B + b.H * 0.25, b.Cy, b.B + b.H * 0.75, b.T })
                {
                    double h = HalfSpan(molde, mb, b.Cx, y);
                    if (h >= 0) livre = Math.Min(livre, h * 2);
                }
                double limite = livre == double.MaxValue ? double.MaxValue : livre * (1 - margem);
                if (maxW > 0) limite = Math.Min(limite, maxW);
                if (limite == double.MaxValue || b.W <= limite + 1e-4) return;
                double k = limite / b.W;
                t.SetSizeEx(b.Cx, b.Cy, b.W * k, b.H * k);
            }
        }

        /// <summary>Reduz (ou ajusta) a forma proporcionalmente para caber em maxW x maxH, mantendo o centro.</summary>
        public static void Fit(Shape s, double maxW, double maxH, bool allowGrow)
        {
            var b = BoxOf(s);
            if (b.W <= 0 || b.H <= 0) return;
            double k = double.MaxValue;
            if (maxW > 0) k = Math.Min(k, maxW / b.W);
            if (maxH > 0) k = Math.Min(k, maxH / b.H);
            if (k == double.MaxValue) return;
            if (!allowGrow && k >= 1) return;
            s.SetSizeEx(b.Cx, b.Cy, b.W * k, b.H * k);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Corel.Interop.VGCore;

namespace AhcMoldes.Engine
{
    public class MoldeInfo
    {
        public Shape Shape;
        public string Tamanho;
        public int Parte;
        public string Apelido;
        public bool IsMatriz => Tamanho == Tag.Matriz;
        public string ParteLabel => string.IsNullOrEmpty(Apelido) ? $"Parte {Parte}" : $"{Parte}-{Apelido}";
        public string Nome => Moldes.NomeDe(Tamanho, Parte, Apelido);
    }

    /// <summary>
    /// Definição de tamanhos/matriz e localização dos moldes no documento.
    /// O molde fica identificado pelo próprio nome do objeto, legível no Gerenciador de Objetos:
    ///   "MATRIZ 1", "M 1", "M 2 MANGA"   (tamanho, parte, apelido opcional)
    /// Também aceita "G - 1", "G_1" etc. e o formato antigo "AHC:MOLDE;T=..;P=..".
    /// </summary>
    public static class Moldes
    {
        static readonly Regex RxMatriz = new Regex(@"^\s*MATRIZ[\s_\-]*(?<p>\d+)(?:[\s_\-]+(?<a>[A-Za-zÀ-ÿ]+))?\s*$", RegexOptions.IgnoreCase);
        static readonly Regex RxTam = new Regex(@"^\s*(?<t>[A-Za-z0-9]+?)[\s_]*-[\s_]*(?<p>\d+)(?:[\s_]*-[\s_]*(?<a>[A-Za-zÀ-ÿ]+))?\s*$|^\s*(?<t>[A-Za-z0-9]+)[\s_]+(?<p>\d+)(?:[\s_]+(?<a>[A-Za-zÀ-ÿ]+))?\s*$", RegexOptions.IgnoreCase);

        public static string NomeDe(string tamanho, int parte, string apelido)
            => $"{tamanho} {parte}" + (string.IsNullOrEmpty(apelido) ? "" : " " + apelido);

        public static MoldeInfo Info(Shape s)
        {
            string name;
            try { name = s.Name; } catch { return null; }
            if (string.IsNullOrWhiteSpace(name)) return null;

            var t = Tag.Parse(name);
            if (t != null)
                return t.Kind == Tag.Molde ? new MoldeInfo { Shape = s, Tamanho = t["T"], Parte = t.Int("P", 1), Apelido = t["A"] } : null;

            var m = RxMatriz.Match(name);
            if (m.Success)
                return new MoldeInfo { Shape = s, Tamanho = Tag.Matriz, Parte = int.Parse(m.Groups["p"].Value), Apelido = CleanApelido(m.Groups["a"].Value) };

            m = RxTam.Match(name);
            if (m.Success && Cdr.IsClosed(s))
            {
                var tam = m.Groups["t"].Value.ToUpperInvariant();
                if (tam == Tag.Matriz) return null;
                return new MoldeInfo { Shape = s, Tamanho = tam, Parte = int.Parse(m.Groups["p"].Value), Apelido = CleanApelido(m.Groups["a"].Value) };
            }
            return null;
        }

        public static List<MoldeInfo> FindAll(Document doc)
            => Cdr.PageShapes(doc).Where(s => s.Type != cdrShapeType.cdrTextShape).Select(Info).Where(i => i != null).ToList();

        public static string CleanApelido(string a)
        {
            a = new string((a ?? "").Where(char.IsLetter).ToArray()).ToUpperInvariant();
            return a.Length > 8 ? a.Substring(0, 8) : a;
        }

        /// <summary>Marca as formas como um tamanho (ou MATRIZ) de uma parte.</summary>
        public static int Define(Application app, IEnumerable<Shape> shapes, string tamanho, int parte, string apelido, Config cfg)
        {
            tamanho = (tamanho ?? "").Trim().ToUpperInvariant() == Tag.Matriz ? Tag.Matriz : OrderImport.NormalizeSize(tamanho);
            if (tamanho.Length == 0) throw new UserException("Escolha um tamanho.");
            if (parte < 1) throw new UserException("O número da parte deve ser 1 ou maior.");
            apelido = CleanApelido(apelido);

            var list = shapes.ToList();
            if (list.Count == 0) throw new UserException("Selecione os moldes.");
            var open = list.Where(s => !Cdr.IsClosed(s)).ToList();
            if (open.Count > 0)
                throw new UserException($"{open.Count} objeto(s) não são curvas fechadas. Feche as curvas (ou desagrupe) antes de definir.");
            if (list.Count > 1 && tamanho == Tag.Matriz)
                throw new UserException("Selecione apenas UM molde para ser a matriz desta parte.");

            var doc = app.ActiveDocument;
            if (list.Count == 1)
            {
                // cada tamanho+parte só existe uma vez: quem tinha esse nome perde a definição
                foreach (var old in FindAll(doc).Where(m => m.Tamanho == tamanho && m.Parte == parte && m.Shape.StaticID != list[0].StaticID))
                {
                    RemoveLabel(doc, old.Shape);
                    old.Shape.Name = "";
                }
            }

            foreach (var s in list)
            {
                s.Name = NomeDe(tamanho, parte, apelido);
                if (cfg.CriarRotulo) MakeLabel(app, s, tamanho, parte, apelido);
                else RemoveLabel(doc, s);
            }
            return list.Count;
        }

        /// <summary>
        /// Definição automática: as formas de UMA parte em todos os tamanhos são ordenadas
        /// pela área (menor → maior) e recebem os tamanhos na ordem da lista.
        /// </summary>
        public static int AutoDefine(Application app, IEnumerable<Shape> shapes, IList<string> tamanhosEmOrdem, int parte, string apelido, Config cfg)
        {
            var list = shapes.Where(s => Info(s)?.IsMatriz != true).ToList();
            if (list.Count == 0) throw new UserException("Selecione os moldes da parte em todos os tamanhos.");
            if (list.Count != tamanhosEmOrdem.Count)
                throw new UserException($"Foram selecionados {list.Count} moldes, mas há {tamanhosEmOrdem.Count} tamanhos marcados. As quantidades precisam ser iguais.");
            var open = list.Where(s => !Cdr.IsClosed(s)).ToList();
            if (open.Count > 0) throw new UserException($"{open.Count} objeto(s) não são curvas fechadas.");

            var ordered = list.Select(s => new { s, a = Cdr.AreaOf(s) }).OrderBy(x => x.a).ToList();
            for (int i = 0; i < ordered.Count; i++)
                Define(app, new[] { ordered[i].s }, tamanhosEmOrdem[i], parte, apelido, cfg);
            return ordered.Count;
        }

        public static int Undefine(Application app, IEnumerable<Shape> shapes)
        {
            int n = 0;
            foreach (var s in shapes.ToList())
            {
                if (Info(s) == null) continue;
                RemoveLabel(app.ActiveDocument, s);
                s.Name = "";
                n++;
            }
            return n;
        }

        static string LabelRef(Shape s) => s.StaticID.ToString();

        static void RemoveLabel(Document doc, Shape molde)
        {
            var r = LabelRef(molde);
            foreach (var l in Cdr.PageShapes(doc).Where(x => { var t = Tag.Parse(x.Name); return t?.Kind == Tag.Rotulo && t["REF"] == r; }).ToList())
                l.Delete();
        }

        static void MakeLabel(Application app, Shape molde, string tam, int parte, string apelido)
        {
            RemoveLabel(app.ActiveDocument, molde);
            var b = Cdr.BoxOf(molde);
            var t = Cdr.CreateText(app, molde.Layer, NomeDe(tam, parte, apelido), "Arial", 12);
            t.Fill.ApplyUniformFill(app.CreateCMYKColor(0, 100, 100, 0));
            t.SetPositionEx(cdrReferencePoint.cdrTopMiddle, b.Cx, b.B - 0.3);
            var tag = new Tag(Tag.Rotulo); tag["REF"] = LabelRef(molde);
            t.Name = tag.ToString();
        }

        /// <summary>Verifica duplicidades (dois moldes do mesmo tamanho+parte). Retorna as formas repetidas.</summary>
        public static List<Shape> Duplicates(IEnumerable<MoldeInfo> moldes)
            => moldes.GroupBy(m => m.Tamanho + "|" + m.Parte).Where(g => g.Count() > 1).SelectMany(g => g.Select(m => m.Shape)).ToList();
    }
}

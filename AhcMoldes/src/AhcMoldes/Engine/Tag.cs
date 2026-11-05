using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AhcMoldes.Engine
{
    /// <summary>
    /// Metadados guardados no nome do objeto (visível no Gerenciador de Objetos).
    /// Formato: AHC:TIPO;CHAVE=valor;CHAVE=valor
    /// </summary>
    public sealed class Tag
    {
        public const string Prefix = "AHC:";

        // Tipos
        public const string Molde = "MOLDE";       // molde vazio de um tamanho, ou a MATRIZ
        public const string Peca = "PECA";         // peça montada
        public const string Nome = "NOME";         // campo de texto na matriz
        public const string Numero = "NUMERO";
        public const string Adicional = "ADICIONAL";
        public const string GabNome = "GAB_NOME";  // área (gabarito) para o campo
        public const string GabNumero = "GAB_NUMERO";
        public const string GabAdicional = "GAB_ADICIONAL";
        public const string GabId = "GAB_ID";
        public const string Fixo = "FIXO";         // arte que não muda de tamanho na graduação
        public const string Id = "ID";             // identificador impresso na peça
        public const string Rotulo = "ROTULO";     // etiqueta de tamanho ao lado do molde
        public const string Indicador = "INDICADOR"; // texto acima da linha montada

        public const string Matriz = "MATRIZ";

        public string Kind { get; set; }
        public Dictionary<string, string> Props { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Tag(string kind) { Kind = kind; }

        public string this[string key]
        {
            get => Props.TryGetValue(key, out var v) ? v : "";
            set => Props[key] = Clean(value);
        }

        public int Int(string key, int def = 0) => int.TryParse(this[key], out var v) ? v : def;

        public static string Clean(string v) => (v ?? "").Replace(";", ",").Replace("=", "-").Trim();

        public static Tag Parse(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
            var parts = name.Substring(Prefix.Length).Split(';');
            var t = new Tag(parts[0].Trim().ToUpperInvariant());
            foreach (var p in parts.Skip(1))
            {
                int i = p.IndexOf('=');
                if (i > 0) t.Props[p.Substring(0, i).Trim()] = p.Substring(i + 1);
            }
            return t;
        }

        public static string KindOf(string name) => Parse(name)?.Kind;

        public override string ToString()
        {
            var sb = new StringBuilder(Prefix).Append(Kind);
            foreach (var kv in Props) sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value);
            return sb.ToString();
        }

        public static bool IsField(string kind) => kind == Nome || kind == Numero || kind == Adicional;
        public static bool IsGab(string kind) => kind != null && kind.StartsWith("GAB_");
        public static string GabFor(string field) => "GAB_" + field;
    }
}

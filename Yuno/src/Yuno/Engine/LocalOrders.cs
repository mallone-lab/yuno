using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Yuno.Engine
{
    [DataContract]
    public sealed class LocalOrder
    {
        [DataMember(Order = 1)] public string Id { get; set; } = "";
        [DataMember(Order = 2)] public string Nome { get; set; } = "";
        [DataMember(Order = 3)] public DateTime CriadoEm { get; set; }
        [DataMember(Order = 4)] public DateTime AtualizadoEm { get; set; }
        [DataMember(Order = 5)] public string PlanilhaOrigem { get; set; } = "";
        [DataMember(Order = 6)] public string DocumentoCorel { get; set; } = "";
        [DataMember(Order = 7)] public string PdfOs { get; set; } = "";
        [DataMember(Order = 8)] public List<OrderLine> Linhas { get; set; } = new List<OrderLine>();
        [DataMember(Order = 9)] public string Tecido { get; set; } = "";
        [DataMember(Order = 10)] public DateTime? DataEntrega { get; set; }
        [DataMember(Order = 11)] public string TipoGola { get; set; } = "";
        [DataMember(Order = 12)] public string AcabamentoGola { get; set; } = "";
        [DataMember(Order = 13)] public string Manga { get; set; } = "";
        [DataMember(Order = 14)] public string ModeloCalcao { get; set; } = "";
        [DataMember(Order = 15)] public string BolsoCalcao { get; set; } = "";
        [DataMember(Order = 16)] public string DetalhesCalcao { get; set; } = "";
        [DataMember(Order = 17)] public bool Etiqueta { get; set; }
        [DataMember(Order = 18)] public string Observacoes { get; set; } = "";
        [DataMember(Order = 19)] public bool UsarTamanhoCalcao { get; set; }
        [DataMember(Order = 20)] public string TipoEtiqueta { get; set; } = "";

        public string Resumo => $"{Linhas?.Count ?? 0} linha(s) · atualizado {AtualizadoEm.ToString("dd/MM/yyyy HH:mm")}";
    }

    /// <summary>Armazena cada pedido em um arquivo separado para manter a pasta leve e fácil de copiar/compartilhar.</summary>
    public static class LocalOrderStore
    {
        public static List<LocalOrder> Listar(string pasta)
        {
            GarantirPasta(pasta);
            var pedidos = new List<LocalOrder>();
            foreach (var arquivo in Directory.GetFiles(pasta, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using (var stream = File.OpenRead(arquivo))
                    {
                        var pedido = (LocalOrder)new DataContractJsonSerializer(typeof(LocalOrder)).ReadObject(stream);
                        if (pedido != null && !string.IsNullOrWhiteSpace(pedido.Id))
                        {
                            pedido.Linhas = pedido.Linhas ?? new List<OrderLine>();
                            pedidos.Add(pedido);
                        }
                    }
                }
                catch { /* ignora arquivo parcial/antigo; os demais pedidos continuam disponíveis */ }
            }
            return pedidos.OrderByDescending(p => p.AtualizadoEm).ToList();
        }

        public static LocalOrder Ler(string pasta, string id)
        {
            var arquivo = Arquivo(pasta, id);
            if (!File.Exists(arquivo)) return null;
            using (var stream = File.OpenRead(arquivo))
                return (LocalOrder)new DataContractJsonSerializer(typeof(LocalOrder)).ReadObject(stream);
        }

        public static string Salvar(string pasta, LocalOrder pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));
            GarantirPasta(pasta);
            bool novo = string.IsNullOrWhiteSpace(pedido.Id);
            if (novo) pedido.Id = Guid.NewGuid().ToString("N");
            if (pedido.CriadoEm == default(DateTime)) pedido.CriadoEm = DateTime.Now;
            pedido.AtualizadoEm = DateTime.Now;
            pedido.Linhas = pedido.Linhas ?? new List<OrderLine>();

            var destino = Arquivo(pasta, pedido.Id);
            var temporario = destino + ".tmp";
            try
            {
                using (var stream = File.Create(temporario))
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true))
                {
                    new DataContractJsonSerializer(typeof(LocalOrder)).WriteObject(writer, pedido);
                    writer.Flush();
                }
                if (File.Exists(destino)) File.Copy(temporario, destino, true);
                else File.Move(temporario, destino);
            }
            finally { try { if (File.Exists(temporario)) File.Delete(temporario); } catch { } }
            return destino;
        }

        private static string Arquivo(string pasta, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("Identificador local de pedido inválido.", nameof(id));
            return Path.Combine(pasta, id + ".json");
        }

        private static void GarantirPasta(string pasta)
        {
            if (string.IsNullOrWhiteSpace(pasta)) throw new InvalidOperationException("Defina a pasta dos pedidos locais nas configurações.");
            Directory.CreateDirectory(pasta);
        }
    }
}

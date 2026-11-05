using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;

namespace AhcMoldes.Engine
{
    public class PedidoOnline
    {
        public string Id { get; set; }
        public string Referencia { get; set; }
        public string Status { get; set; }
        public string Cliente { get; set; }
        public DateTime? CriadoEm { get; set; }
        public DateTime? EnviadoEm { get; set; }
        public int Pecas { get; set; }
        public string Data => (EnviadoEm ?? CriadoEm)?.ToLocalTime().ToString("dd/MM/yy HH:mm") ?? "";
    }

    /// <summary>
    /// Acesso aos pedidos online (Supabase REST). Usa a chave SECRETA, guardada só na configuração local.
    /// </summary>
    public class Cloud
    {
        private readonly string _url, _key;
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        static Cloud()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public Cloud(Config cfg)
        {
            _url = (cfg.SupabaseUrl ?? "").Trim().TrimEnd('/');
            _key = (cfg.SupabaseChave ?? "").Trim();
            if (_url.Length == 0 || _key.Length == 0)
                throw new UserException("Configure os pedidos online em Config > Pedidos online (endereço e chave do Supabase).");
        }

        private string Send(HttpMethod method, string path, object body = null, bool representation = false)
        {
            var req = new HttpRequestMessage(method, _url + "/rest/v1/" + path);
            req.Headers.Add("apikey", _key);
            if (!_key.StartsWith("sb_")) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _key);
            if (representation) req.Headers.Add("Prefer", "return=representation");
            if (body != null) req.Content = new StringContent(Json.Serialize(body), Encoding.UTF8, "application/json");
            HttpResponseMessage resp;
            try { resp = Http.SendAsync(req).GetAwaiter().GetResult(); }
            catch (Exception ex) { throw new UserException("Sem conexão com o servidor de pedidos: " + (ex.InnerException?.Message ?? ex.Message)); }
            var txt = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                string m = txt;
                try { var d = Json.Deserialize<Dictionary<string, object>>(txt); if (d != null && d.TryGetValue("message", out var mm)) m = mm?.ToString(); } catch { }
                throw new UserException($"Servidor de pedidos respondeu {(int)resp.StatusCode}: {m}");
            }
            return txt;
        }

        static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? v.ToString() : "";
        static DateTime? D(Dictionary<string, object> d, string k)
            => DateTime.TryParse(S(d, k), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
               ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : (DateTime?)null;

        public PedidoOnline Criar(string referencia)
        {
            if (string.IsNullOrWhiteSpace(referencia)) throw new UserException("Informe a referência do pedido.");
            var txt = Send(HttpMethod.Post, "pedidos", new Dictionary<string, object> { { "referencia", referencia.Trim() } }, true);
            var arr = Json.Deserialize<List<Dictionary<string, object>>>(txt);
            var d = arr.First();
            return new PedidoOnline { Id = S(d, "id"), Referencia = S(d, "referencia"), Status = S(d, "status"), CriadoEm = D(d, "criado_em") };
        }

        public List<PedidoOnline> Listar(bool incluirEncerrados)
        {
            var filtro = incluirEncerrados ? "" : "&status=neq.encerrado";
            var txt = Send(HttpMethod.Get, "pedidos?select=id,referencia,status,cliente_nome,criado_em,enviado_em,pedido_itens(quantidade)&order=criado_em.desc&limit=200" + filtro);
            var arr = Json.Deserialize<List<Dictionary<string, object>>>(txt) ?? new List<Dictionary<string, object>>();
            return arr.Select(d =>
            {
                int pecas = 0;
                if (d.TryGetValue("pedido_itens", out var it) && it is System.Collections.ArrayList al)
                    foreach (var o in al) if (o is Dictionary<string, object> x && int.TryParse(S(x, "quantidade"), out var q)) pecas += q;
                return new PedidoOnline
                {
                    Id = S(d, "id"), Referencia = S(d, "referencia"), Status = S(d, "status"),
                    Cliente = S(d, "cliente_nome"), CriadoEm = D(d, "criado_em"), EnviadoEm = D(d, "enviado_em"), Pecas = pecas
                };
            }).ToList();
        }

        public List<OrderLine> Itens(string pedidoId, bool usarCalcao)
        {
            var txt = Send(HttpMethod.Get, "pedido_itens?pedido_id=eq." + Uri.EscapeDataString(pedidoId) + "&order=ordem.asc");
            var arr = Json.Deserialize<List<Dictionary<string, object>>>(txt) ?? new List<Dictionary<string, object>>();
            return arr.Select(d =>
            {
                var l = new OrderLine
                {
                    Tipo = S(d, "tipo"), Nome = S(d, "nome"), Numero = S(d, "numero"), Adicional = S(d, "adicional"),
                    TamanhoCamiseta = OrderImport.NormalizeSize(S(d, "tam_camiseta")),
                    TamanhoCalcao = OrderImport.NormalizeSize(S(d, "tam_calcao")),
                    Quantidade = int.TryParse(S(d, "quantidade"), out var q) && q > 0 ? q : 1
                };
                l.Tamanho = usarCalcao ? l.TamanhoCalcao : l.TamanhoCamiseta;
                return l;
            }).ToList();
        }

        public void MudarStatus(string pedidoId, string status)
            => Send(new HttpMethod("PATCH"), "pedidos?id=eq." + Uri.EscapeDataString(pedidoId), new Dictionary<string, object> { { "status", status } });

        public static string Link(Config cfg, string pedidoId)
        {
            var b = (cfg.LinkFormulario ?? "").Trim();
            if (b.Length == 0) return "?p=" + pedidoId;
            return b + (b.Contains("?") ? "&" : "?") + "p=" + pedidoId;
        }
    }
}

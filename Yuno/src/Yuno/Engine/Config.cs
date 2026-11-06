using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Yuno.Engine
{
    public enum ArtScale { Esticar = 0, UniformeCobrir = 1, UniformeLargura = 2 }

    [DataContract]
    public class Tecido
    {
        [DataMember] public string Nome { get; set; }
        [DataMember] public double LarguraCm { get; set; }
        public override string ToString() => LarguraCm > 0 ? $"{Nome} ({LarguraCm:0} cm)" : Nome;
    }

    [DataContract]
    public class Config
    {
        // ---- Tamanhos
        [DataMember] public List<string> Tamanhos { get; set; } = new List<string>
        {
            "PP","P","M","G","GG","XG","XGG","BLPP","BLP","BLM","BLG","BLGG","BLXG",
            "2A","4A","6A","8A","10A","12A","14A","G1","G2","G3","G4",
            "ESP5","ESP6","ESP7","ESP8","ESP9","ESP10"
        };
        [DataMember] public bool CriarRotulo { get; set; } = false;
        [DataMember] public bool SubstituirAoAplicar { get; set; } = true;

        [DataMember] public int PartesPorPeca { get; set; } = 5;

        // ---- Pedidos online (Supabase)
        [DataMember] public string SupabaseUrl { get; set; } = "";
        [DataMember] public string SupabaseChave { get; set; } = "";   // chave secreta (service_role / sb_secret_...)
        [DataMember] public string LinkFormulario { get; set; } = "";  // endereço público do formulário (index.html)

        // ---- Planilha (letras de coluna) — mesmo modelo do formulário de pedido:
        //      A Tipo | B Nome | C Número | D Adicional | E Tam. Camiseta | F Tam. Calção | G Quantidade
        [DataMember] public int ConfigVersao { get; set; } = 3;
        [DataMember] public string ColunaTipo { get; set; } = "A";
        [DataMember] public string ColunaNome { get; set; } = "B";
        [DataMember] public string ColunaNumero { get; set; } = "C";
        [DataMember] public string ColunaAdicional { get; set; } = "D";
        [DataMember] public string ColunaTamanho { get; set; } = "E";
        [DataMember] public string ColunaTamanhoCalcao { get; set; } = "F";
        [DataMember] public string ColunaQuantidade { get; set; } = "G";
        [DataMember] public bool PrimeiraLinhaCabecalho { get; set; } = true;
        [DataMember] public bool UsarTamanhoCalcao { get; set; } = false;

        // ---- Montagem
        [DataMember] public bool NomeMaiusculo { get; set; } = true;
        [DataMember] public bool AdicionalMaiusculo { get; set; } = true;
        [DataMember] public double LarguraNomePct { get; set; } = 90;
        [DataMember] public double LarguraNumeroPct { get; set; } = 90;
        [DataMember] public double LarguraAdicionalPct { get; set; } = 90;
        [DataMember] public bool AjustarAoGabarito { get; set; } = false;
        [DataMember] public double MargemTextoPct { get; set; } = 12; // folga total (esq.+dir.) entre o texto e a borda da peça // true: aumenta/reduz para preencher a área
        [DataMember] public ArtScale EscalaArte { get; set; } = ArtScale.Esticar;
        [DataMember] public double Espacamento { get; set; } = 1.5;
        [DataMember] public bool GerarIndicador { get; set; } = true;
        [DataMember] public float FonteIndicador { get; set; } = 36;
        [DataMember] public bool AgruparLinha { get; set; } = false;
        [DataMember] public bool PreencherBrancoSemCor { get; set; } = true;

        // ---- ID impresso na peça
        [DataMember] public bool GerarId { get; set; } = true;
        [DataMember] public bool IdPedido { get; set; } = true;
        [DataMember] public bool IdSequencial { get; set; } = true;
        [DataMember] public bool IdTamanho { get; set; } = true;
        [DataMember] public bool IdParte { get; set; } = false;
        [DataMember] public bool IdApelido { get; set; } = true;
        [DataMember] public float FonteId { get; set; } = 14;
        [DataMember] public string FonteIdNome { get; set; } = "Arial";

        // ---- Contorno
        [DataMember] public double ContornoMm { get; set; } = 0.1;
        [DataMember] public int ContornoCor { get; set; } = 0; // 0 preto, 1 cinza 70, 2 vermelho, 3 branco

        // ---- Encaixe sobre outro
        [DataMember] public double EncaixeTolerancia { get; set; } = 0.1;
        [DataMember] public double EncaixeSimilaridadeNos { get; set; } = 0;
        [DataMember] public bool EncaixeEspelhar { get; set; } = true;
        [DataMember] public bool EncaixeCopiarFaltantes { get; set; } = true;
        [DataMember] public bool EncaixeApagarBase { get; set; } = true;

        // ---- Exportação
        [DataMember] public string PastaExportacao { get; set; } = @"C:\Export";
        [DataMember] public string PastaRelatorio { get; set; } = @"C:\Export";
        [DataMember] public string Formato { get; set; } = "JPG"; // JPG, PNG, TIF, PDF
        [DataMember] public string Cor { get; set; } = "CMYK";
        [DataMember] public int Dpi { get; set; } = 150;
        [DataMember] public string CompressaoPdf { get; set; } = "JPEG"; // JPEG ou LZW
        [DataMember] public bool GerarRelatorio { get; set; } = true;
        [DataMember] public bool NomeDoArquivoCdr { get; set; } = true;
        [DataMember] public int TecidoSelecionado { get; set; } = 0;
        [DataMember] public List<Tecido> Tecidos { get; set; } = new List<Tecido>
        {
            new Tecido { Nome = "Tecido 1", LarguraCm = 160 },
            new Tecido { Nome = "Tecido 2", LarguraCm = 180 },
            new Tecido { Nome = "Tecido 3", LarguraCm = 90 },
        };

        // ---------------------------------------------------------------

        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Yuno");
        public static string FilePath => Path.Combine(Folder, "config.json");

        public static Config Load(string path = null)
        {
            path = path ?? FilePath;
            try
            {
                var antigo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AhcMoldes", "config.json");
                if (!File.Exists(path) && File.Exists(antigo)) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.Copy(antigo, path); }
                if (File.Exists(path))
                {
                    using (var fs = File.OpenRead(path))
                    {
                        var c = (Config)new DataContractJsonSerializer(typeof(Config)).ReadObject(fs);
                        c.FillDefaults();
                        return c;
                    }
                }
            }
            catch { /* arquivo corrompido: volta ao padrão */ }
            return new Config();
        }

        public void Save(string path = null)
        {
            path = path ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var ms = new MemoryStream())
            {
                using (var w = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, false, true))
                    new DataContractJsonSerializer(typeof(Config)).WriteObject(w, this);
                File.WriteAllBytes(path, ms.ToArray());
            }
        }

        private void FillDefaults()
        {
            var d = new Config();
            if (ConfigVersao < 2)
            {
                ColunaTipo = d.ColunaTipo; ColunaNome = d.ColunaNome; ColunaNumero = d.ColunaNumero;
                ColunaAdicional = d.ColunaAdicional; ColunaTamanho = d.ColunaTamanho;
                ColunaTamanhoCalcao = d.ColunaTamanhoCalcao; ColunaQuantidade = d.ColunaQuantidade;
                PrimeiraLinhaCabecalho = true; CriarRotulo = false; PartesPorPeca = 5; SubstituirAoAplicar = true;
                ConfigVersao = 2;
            }
            if (ConfigVersao < 3)
            {
                // nome e número mantêm o tamanho da matriz; só reduzem se passarem de 90% da largura da peça
                LarguraNomePct = 90; LarguraNumeroPct = 90; LarguraAdicionalPct = 90;
                ConfigVersao = 3;
            }
            if (PartesPorPeca < 1) PartesPorPeca = 5;
            if (MargemTextoPct <= 0 || MargemTextoPct >= 80) MargemTextoPct = 12;
            if (Tamanhos == null || Tamanhos.Count == 0) Tamanhos = d.Tamanhos;
            if (Tecidos == null || Tecidos.Count == 0) Tecidos = d.Tecidos;
            if (string.IsNullOrWhiteSpace(FonteIdNome)) FonteIdNome = d.FonteIdNome;
            if (FonteId <= 0) FonteId = d.FonteId;
            if (FonteIndicador <= 0) FonteIndicador = d.FonteIndicador;
            if (Dpi <= 0) Dpi = d.Dpi;
            if (string.IsNullOrWhiteSpace(Formato)) Formato = d.Formato;
            if (string.IsNullOrWhiteSpace(Cor)) Cor = d.Cor;
            if (string.IsNullOrWhiteSpace(CompressaoPdf)) CompressaoPdf = d.CompressaoPdf;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Yuno.Engine;
using Corel.Interop.VGCore;
using CorelApp = Corel.Interop.VGCore.Application;
using CorelColor = Corel.Interop.VGCore.Color;

namespace Yuno.UI
{
    /// <summary>Docker principal do YUNO (hospedado pelo CorelDRAW via AppUI.xslt).</summary>
    public partial class MainDocker : UserControl
    {
        private static int _dockerActivationRequest;
        private readonly CorelApp _app;
        private static Config _shared;
        private Config _cfg = new Config();
        private readonly ObservableCollection<OrderLine> _linhas = new ObservableCollection<OrderLine>();
        private List<LocalOrder> _pedidosLocais = new List<LocalOrder>();
        private string _pedidoLocalId;
        private string _planilhaOrigem;
        private string _pdfOsAtual;
        private bool _suspenderResumo;
        private Shape _origem;
        private CorelColor _corOriginal;

        public MainDocker()
        {
            InitializeComponent();
            OSDataEntrega.SelectedDate = DateTime.Today;
            OrdenarAbas();
        }

        private void OrdenarAbas()
        {
            var porNome = Tabs.Items.Cast<TabItem>().ToDictionary(t => Convert.ToString(t.Header), t => t);
            var ordem = new[] { "Pedido", "Montagem", "OS", "Exportar" };
            Tabs.SelectionChanged -= Tabs_SelectionChanged;
            Tabs.Items.Clear();
            foreach (var nome in ordem) Tabs.Items.Add(porNome[nome]);
            Tabs.SelectedItem = porNome["Pedido"];
            Tabs.SelectionChanged += Tabs_SelectionChanged;
        }

        /// <summary>Construtor chamado pelo CorelDRAW (recebe o objeto Application).</summary>
        public MainDocker(object app) : this()
        {
            _app = app as CorelApp;
            if (_shared == null) { try { _shared = Config.Load(); } catch { _shared = new Config(); } }
            _cfg = _shared;
            IsVisibleChanged += (s, e) => { if (IsVisible) { CarregarConfigNaTela(); AtualizarResumo(); } };
            Grade.ItemsSource = _linhas;
            System.Windows.Data.CollectionViewSource.GetDefaultView(_linhas).Filter = FiltrarLinhaDaGrade;
            _linhas.CollectionChanged += (s, e) => { if (!_suspenderResumo) AtualizarResumo(); };
            try { Directory.CreateDirectory(OrderServiceReport.LogoFolder); } catch { }
            OSLogoCaminho.Text = "Pasta da logo: " + OrderServiceReport.LogoFolder;
            OSDestino.Text = "A OS será salva dentro da pasta do pedido, em " + _cfg.PastaExportacao;
            CarregarConfigNaTela();
            AtualizarPedidosLocais();
            AtualizarResumo();
        }

        // ================================================================ PEDIDOS LOCAIS

        private void AtualizarPedidosLocais()
        {
            try
            {
                _pedidosLocais = LocalOrderStore.Listar(_cfg.PastaPedidosLocais);
                AplicarFiltroPedidosLocais();
            }
            catch (Exception ex)
            {
                _pedidosLocais = new List<LocalOrder>();
                PedidosLocais.ItemsSource = null;
                PedidoLocalCaminhos.Text = "Não foi possível acessar a pasta de pedidos: " + ex.Message;
            }
        }

        private void AplicarFiltroPedidosLocais()
        {
            if (PedidosLocais == null) return;
            string busca = (PedidoLocalBusca?.Text ?? "").Trim();
            var filtrados = _pedidosLocais.Where(p => busca.Length == 0 || (p.Nome ?? "").IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            PedidosLocais.ItemsSource = filtrados;
            PedidosLocais.SelectedItem = filtrados.FirstOrDefault(p => p.Id == _pedidoLocalId);
            if (PedidosLocais.SelectedItem == null && filtrados.Count == 0)
                PedidoLocalCaminhos.Text = "Nenhum pedido salvo. Importe/preencha um pedido e clique em Salvar.";
        }

        private void PedidoLocalBusca_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltroPedidosLocais();

        private void PedidosLocais_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pedido = PedidosLocais?.SelectedItem as LocalOrder;
            if (pedido == null) return;
            var arquivo = Path.Combine(_cfg.PastaPedidosLocais, pedido.Id + ".json");
            PedidoLocalCaminhos.Text = "Dados: " + arquivo +
                "\nCorel: " + ValorCaminho(pedido.DocumentoCorel) +
                "\nPlanilha: " + ValorCaminho(pedido.PlanilhaOrigem) +
                (string.IsNullOrWhiteSpace(pedido.PdfOs) ? "" : "\nOS: " + pedido.PdfOs);
        }

        private static string ValorCaminho(string caminho) => string.IsNullOrWhiteSpace(caminho) ? "não registrado" : caminho;

        private void PedidosLocais_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => AbrirPedidoLocal();
        private void AbrirPedidoLocal_Click(object sender, RoutedEventArgs e) => AbrirPedidoLocal();

        private void AbrirPedidoLocal()
        {
            var selecionado = PedidosLocais?.SelectedItem as LocalOrder;
            if (selecionado == null) { Aviso("Selecione um pedido salvo para abrir e editar."); return; }
            try
            {
                var pedido = LocalOrderStore.Ler(_cfg.PastaPedidosLocais, selecionado.Id);
                if (pedido == null) throw new UserException("O pedido salvo não foi encontrado na pasta local.");
                _suspenderResumo = true;
                try
                {
                    _linhas.Clear();
                    foreach (var linha in pedido.Linhas ?? new List<OrderLine>())
                        _linhas.Add(new OrderLine
                        {
                            Tipo = linha.Tipo, Nome = linha.Nome, Numero = linha.Numero, Adicional = linha.Adicional,
                            TamanhoCamiseta = linha.TamanhoCamiseta, TamanhoCalcao = linha.TamanhoCalcao,
                            Quantidade = linha.Quantidade, Tamanho = linha.Tamanho
                        });
                }
                finally { _suspenderResumo = false; }

                _pedidoLocalId = pedido.Id;
                _planilhaOrigem = pedido.PlanilhaOrigem;
                _pdfOsAtual = pedido.PdfOs;
                Pedido.Text = pedido.Nome ?? "";
                OSNomePedido.Text = pedido.Nome ?? "";
                UsarCalcao.IsChecked = pedido.UsarTamanhoCalcao;
                UsarCamiseta.IsChecked = !pedido.UsarTamanhoCalcao;
                OSTecido.Text = pedido.Tecido ?? "";
                OSDataEntrega.SelectedDate = pedido.DataEntrega;
                DefinirTextoCombo(OSGola, pedido.TipoGola);
                DefinirTextoCombo(OSAcabamentoGola, pedido.AcabamentoGola);
                DefinirTextoCombo(OSPunho, pedido.Manga);
                OSModeloCalcao.Text = pedido.ModeloCalcao ?? "";
                OSBolsoCalcao.Text = pedido.BolsoCalcao ?? "";
                OSDetalhesCalcao.Text = pedido.DetalhesCalcao ?? "";
                DefinirTextoCombo(OSEtiqueta, string.IsNullOrWhiteSpace(pedido.TipoEtiqueta)
                    ? (pedido.Etiqueta ? "Etiqueta AHC" : "Sem etiqueta") : pedido.TipoEtiqueta);
                OSObservacoes.Text = pedido.Observacoes ?? "";
                OSDestino.Text = string.IsNullOrWhiteSpace(_pdfOsAtual) ? "A OS ainda não foi gerada para este pedido." : "Última OS: " + _pdfOsAtual;
                AtualizarTipos();
                AtualizarResumo();
                AtualizarResumoOS();
                PedidoLocalCaminhos.Text = "Pedido carregado para edição.\nCorel: " + ValorCaminho(pedido.DocumentoCorel) +
                    "\nPlanilha: " + ValorCaminho(pedido.PlanilhaOrigem);
                SetStatus("Pedido local aberto: " + pedido.Nome);
            }
            catch (Exception ex) { Aviso("Não foi possível abrir o pedido salvo: " + ex.Message); }
        }

        private static void DefinirTextoCombo(ComboBox combo, string valor)
        {
            valor = valor ?? "";
            foreach (var item in combo.Items)
            {
                var texto = item is ComboBoxItem cbi ? Convert.ToString(cbi.Content) : Convert.ToString(item);
                if (string.Equals(texto, valor, StringComparison.OrdinalIgnoreCase)) { combo.SelectedItem = item; return; }
            }
            combo.SelectedIndex = -1;
            combo.Text = valor;
        }

        private void NovoPedidoLocal_Click(object sender, RoutedEventArgs e)
        {
            if ((_linhas.Count > 0 || !string.IsNullOrWhiteSpace(Pedido.Text)) && !Pergunta("Começar um pedido novo? As alterações atuais precisam ser salvas antes.")) return;
            _pedidoLocalId = null; _planilhaOrigem = null; _pdfOsAtual = null;
            Pedido.Text = ""; OSNomePedido.Text = ""; OSTecido.SelectedIndex = -1; OSTecido.Text = ""; OSDataEntrega.SelectedDate = DateTime.Today;
            OSDetalhesCalcao.Text = ""; OSObservacoes.Text = ""; OSEtiqueta.SelectedIndex = 1;
            foreach (var combo in new[] { OSGola, OSAcabamentoGola, OSPunho, OSModeloCalcao, OSBolsoCalcao })
            { combo.SelectedIndex = -1; if (combo.IsEditable) combo.Text = ""; }
            _linhas.Clear();
            AtualizarTipos(); AtualizarResumo(); AtualizarResumoOS();
            PedidosLocais.SelectedItem = null;
            PedidoLocalCaminhos.Text = "Novo rascunho. Salve para registrar este pedido na Nuvem Local.";
            OSDestino.Text = "A OS será salva dentro da pasta do pedido, em " + _cfg.PastaExportacao;
            SetStatus("Novo pedido local.");
        }

        private void SalvarPedidoLocal_Click(object sender, RoutedEventArgs e) => SalvarPedidoLocal();

        private void SalvarPedidoLocal()
        {
            Grade.CommitEdit(DataGridEditingUnit.Cell, true);
            Grade.CommitEdit(DataGridEditingUnit.Row, true);
            string nome = (Pedido.Text ?? "").Trim();
            if (nome.Length == 0) { Aviso("Informe o nome do pedido antes de salvar."); return; }
            try
            {
                Pedido.Text = nome.ToUpperInvariant();
                OSNomePedido.Text = Pedido.Text;
                string documento = "";
                try { documento = _app?.ActiveDocument?.FileName ?? ""; } catch { }
                var pedido = new LocalOrder
                {
                    Id = _pedidoLocalId ?? "", Nome = Pedido.Text,
                    PlanilhaOrigem = _planilhaOrigem ?? "", DocumentoCorel = documento, PdfOs = _pdfOsAtual ?? "",
                    Linhas = _linhas.Select(l => new OrderLine
                    {
                        Tipo = l.Tipo ?? "", Nome = l.Nome ?? "", Numero = l.Numero ?? "", Adicional = l.Adicional ?? "",
                        TamanhoCamiseta = l.TamanhoCamiseta ?? "", TamanhoCalcao = l.TamanhoCalcao ?? "",
                        Quantidade = l.Quantidade, Tamanho = l.Tamanho ?? ""
                    }).ToList(),
                    Tecido = TecidoSelecionado(), DataEntrega = OSDataEntrega.SelectedDate,
                    TipoGola = TextoCombo(OSGola), AcabamentoGola = TextoCombo(OSAcabamentoGola), Manga = TextoCombo(OSPunho),
                    ModeloCalcao = OSModeloCalcao.Text ?? "", BolsoCalcao = OSBolsoCalcao.Text ?? "",
                    DetalhesCalcao = OSDetalhesCalcao.Text ?? "", Etiqueta = string.Equals(TextoCombo(OSEtiqueta), "Etiqueta AHC", StringComparison.OrdinalIgnoreCase),
                    TipoEtiqueta = TextoCombo(OSEtiqueta),
                    Observacoes = OSObservacoes.Text ?? "", UsarTamanhoCalcao = UsandoCalcao
                };
                _pedidoLocalId = pedido.Id;
                string arquivo = LocalOrderStore.Salvar(_cfg.PastaPedidosLocais, pedido);
                _pedidoLocalId = pedido.Id;
                AtualizarPedidosLocais();
                PedidoLocalCaminhos.Text = "Salvo em: " + arquivo + "\nCorel: " + ValorCaminho(documento) + "\nPlanilha: " + ValorCaminho(_planilhaOrigem);
                SetStatus("Pedido salvo localmente: " + pedido.Nome);
            }
            catch (Exception ex) { Aviso("Não foi possível salvar o pedido local: " + ex.Message); }
        }

        private string TecidoSelecionado() => OSTecido.SelectedItem is Tecido tecido ? tecido.Nome : (OSTecido.Text ?? "");
        private static string TextoCombo(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Content as string ?? combo.Text ?? "";

        private void AbrirArquivoPedidoLocal_Click(object sender, RoutedEventArgs e)
            => AbrirCaminhoPedido((PedidosLocais.SelectedItem as LocalOrder)?.DocumentoCorel ?? _pedidosLocais.FirstOrDefault(p => p.Id == _pedidoLocalId)?.DocumentoCorel, "arquivo Corel");

        private void AbrirPlanilhaPedidoLocal_Click(object sender, RoutedEventArgs e)
            => AbrirCaminhoPedido((PedidosLocais.SelectedItem as LocalOrder)?.PlanilhaOrigem ?? _planilhaOrigem, "planilha");

        private void AbrirCaminhoPedido(string caminho, string tipo)
        {
            if (string.IsNullOrWhiteSpace(caminho)) { Aviso("Este pedido não tem caminho de " + tipo + " registrado."); return; }
            if (!File.Exists(caminho)) { Aviso("O arquivo não está acessível neste computador:\n" + caminho); return; }
            try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
            catch (Exception ex) { Aviso("Não consegui abrir " + tipo + ": " + ex.Message); }
        }

        private void EscolherPastaPedidos_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Escolha uma pasta local ou compartilhada na rede para os pedidos" })
            {
                if (Directory.Exists(CfgPastaPedidos.Text)) dialog.SelectedPath = CfgPastaPedidos.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) CfgPastaPedidos.Text = dialog.SelectedPath;
            }
        }

        // ================================================================ infraestrutura

        private void SetStatus(string msg) => Status.Text = msg;

        private static void Info(string msg) => MessageBox.Show(msg, "YUNO", MessageBoxButton.OK, MessageBoxImage.Information);
        private static void Aviso(string msg) => MessageBox.Show(msg, "YUNO", MessageBoxButton.OK, MessageBoxImage.Warning);
        private static bool Pergunta(string msg) => MessageBox.Show(msg, "YUNO", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        /// <summary>Executa uma ação em cm com tratamento de erro; operações longas podem usar grupos menores de desfazer.</summary>
        private void Run(string nome, Action<Document> acao, bool agruparDesfazer = true, bool atualizarDefinicoes = false)
        {
            if (_app == null) { Aviso("CorelDRAW não disponível."); return; }
            var cursor = Cursor;
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                using (var sc = new CorelScope(_app, "YUNO: " + nome, agruparDesfazer)) acao(sc.Doc);
            }
            catch (UserException ex) { SetStatus(ex.Message); Aviso(ex.Message); }
            catch (Exception ex)
            {
                SetStatus("Erro: " + ex.Message);
                MessageBox.Show($"{nome}: ocorreu um erro inesperado.\n\n{ex.Message}\n\n{ex.StackTrace}", "YUNO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = cursor;
            }
        }

        private List<Shape> Selecao()
        {
            var r = _app?.ActiveSelectionRange;
            if (r == null || r.Count == 0) throw new UserException("Nada selecionado.");
            return Cdr.Items(r).ToList();
        }

        private static int ParseInt(string s, int def) => int.TryParse((s ?? "").Trim(), out var v) ? v : def;
        private static double ParseDbl(string s, double def)
            => double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        private static string Fmt(double d) => d.ToString("0.##", CultureInfo.CurrentCulture);

        // ================================================================ MOLDES

        private void DefinirManga_Click(object sender, RoutedEventArgs e)
        {
            var lado = (LadoManga.SelectedItem as ComboBoxItem)?.Content as string ?? "";
            Run("Definir lado da manga", doc =>
            {
                int n = Moldes.DefinirLadoManga(_app, Selecao(), lado, _cfg);
                SetStatus($"{n} conjunto(s) de manga definido(s) como {lado}; tamanhos, matriz e parte atualizados.");
            }, atualizarDefinicoes: true);
        }

        private void VerDefinicoes_Click(object sender, RoutedEventArgs e) => AtualizarDefinicoes(selecionarRepetidos: true);

        /// <summary>Mostra no cartão "Moldes definidos" quais partes têm matriz e quais tamanhos.</summary>
        private void AtualizarDefinicoes(bool selecionarRepetidos = false)
        {
            try
            {
                var doc = _app?.ActiveDocument;
                if (doc == null) { Resumo.Text = "Nenhum documento aberto."; return; }
                var ms = Moldes.FindAll(doc);
                if (ms.Count == 0) { Resumo.Text = "Nenhum molde definido ainda."; AtualizarPartesAplicar(ms); return; }
                int Ord(string t) { int i = _cfg.Tamanhos.IndexOf(t); return i < 0 ? 999 : i; }
                var linhas = ms.GroupBy(m => m.Parte).OrderBy(g => g.Key).Select(g =>
                {
                    var tem = g.Any(m => m.IsMatriz) ? "matriz ✓" : "SEM MATRIZ ✗";
                    var tams = g.Where(m => !m.IsMatriz)
                        .Select(m => m.Tamanho + (string.IsNullOrWhiteSpace(m.Apelido) ? "" : "-" + m.Apelido))
                        .Distinct().OrderBy(t => Ord(t.Split('-')[0])).ThenBy(t => t);
                    return $"Parte {g.Key}  {tem}\n   {string.Join(" ", tams)}";
                }).ToList();
                var dups = Moldes.Duplicates(ms);
                if (dups.Count > 0)
                {
                    linhas.Add($"ℹ {dups.Count} formas no mesmo tamanho/parte; todas entram na montagem" + (selecionarRepetidos ? " (selecionadas)" : ""));
                    if (selecionarRepetidos) Cdr.Select(doc, dups);
                }
                Resumo.Text = string.Join("\n", linhas);
                AtualizarPartesAplicar(ms);
            }
            catch (Exception ex) { Resumo.Text = "Não foi possível ler: " + ex.Message; }
        }

        private void AtualizarPartesAplicar(List<MoldeInfo> ms)
        {
            var sel = AplicarParte.SelectedIndex;
            var itens = new List<string> { "Todas as partes" };
            itens.AddRange(ms.Where(m => m.IsMatriz).Select(m => m.Parte).Distinct().OrderBy(p => p).Select(p => $"Somente parte {p}"));
            AplicarParte.ItemsSource = itens;
            AplicarParte.SelectedIndex = sel >= 0 && sel < itens.Count ? sel : 0;
        }

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != Tabs) return;
            var header = Convert.ToString((Tabs.SelectedItem as TabItem)?.Header);
            if (header == "OS")
            {
                if (string.IsNullOrWhiteSpace(OSNomePedido.Text)) OSNomePedido.Text = Pedido.Text;
                AtualizarResumoOS();
            }
        }

        // ================================================================ MODO CLIQUE

        private bool _cliqueLigado;
        private bool _cliqueOcupado;
        private bool _cliqueCallbackPendente;
        private int _ultimoClicado;
        private DIVGApplicationEvents_SelectionChangeEventHandler _selHandler;

        private string TamanhoClique => TamClique.SelectedItem as string ?? "";

        private void ModoClique_Click(object sender, RoutedEventArgs e)
        {
            if (_cliqueLigado) DesligarClique();
            else LigarClique();
        }

        private void LigarClique()
        {
            if (_app?.ActiveDocument == null) { Aviso("Abra o documento com os moldes primeiro."); return; }
            if (TamanhoClique.Length == 0) { Aviso("Escolha o tamanho."); return; }
            if (_selHandler == null) _selHandler = OnSelectionChange;
            ((DIVGApplicationEvents_Event)_app).SelectionChange += _selHandler;
            _cliqueLigado = true;
            _ultimoClicado = 0;
            try { _app.ActiveDocument.ClearSelection(); } catch { }
            BtnClique.Content = "■  Parar modo clique";
            BtnClique.Background = (System.Windows.Media.Brush)FindResource("Danger");
            CliqueBanner.BorderBrush = (System.Windows.Media.Brush)FindResource("Live");
            AtualizarBanner();
        }

        private void DesligarClique()
        {
            if (_selHandler != null && _app != null)
                try { ((DIVGApplicationEvents_Event)_app).SelectionChange -= _selHandler; } catch { }
            _cliqueLigado = false;
            BtnClique.Content = "▶  Ligar modo clique";
            BtnClique.ClearValue(BackgroundProperty);
            CliqueBanner.BorderBrush = (System.Windows.Media.Brush)FindResource("Line");
            CliqueBanner.Background = (System.Windows.Media.Brush)FindResource("Input");
            CliqueTitulo.ClearValue(TextBlock.ForegroundProperty);
            CliqueSub.ClearValue(TextBlock.ForegroundProperty);
            CliqueTitulo.Text = "Modo clique desligado";
            CliqueSub.Text = "Ligue para começar a nomear as partes.";
        }

        private int NumPartes => Math.Max(1, ParseInt(PartesPorPeca.Text, _cfg.PartesPorPeca));

        private bool TamanhoConcluido => ParseInt(ParteClique.Text, 1) > NumPartes;

        private void AtualizarBanner(string ultimo = null)
        {
            if (!_cliqueLigado) return;
            if (TamanhoConcluido)
            {
                CliqueBanner.Background = (System.Windows.Media.Brush)FindResource("Live");
                CliqueTitulo.Text = $"✓ Tamanho {TamanhoClique} configurado";
                CliqueTitulo.Foreground = System.Windows.Media.Brushes.White;
                CliqueSub.Text = $"{NumPartes} partes definidas. Clique em \"Próximo tamanho\" para continuar.";
                CliqueSub.Foreground = System.Windows.Media.Brushes.White;
                return;
            }
            CliqueBanner.Background = (System.Windows.Media.Brush)FindResource("Input");
            CliqueTitulo.ClearValue(TextBlock.ForegroundProperty);
            CliqueSub.ClearValue(TextBlock.ForegroundProperty);
            CliqueTitulo.Text = $"Clique na parte {ParseInt(ParteClique.Text, 1)} de {NumPartes}  ·  {TamanhoClique}";
            CliqueSub.Text = ultimo != null ? $"Último definido: {ultimo}" : "Clique em uma parte do molde no desenho.";
        }

        private void PartesPorPeca_LostFocus(object sender, RoutedEventArgs e)
        {
            int n = ParseInt(PartesPorPeca.Text, 0);
            if (n < 1) { PartesPorPeca.Text = _cfg.PartesPorPeca.ToString(); return; }
            if (n != _cfg.PartesPorPeca) { _cfg.PartesPorPeca = n; try { _cfg.Save(); } catch { } }
            AtualizarBanner();
        }

        private void TamClique_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ParteClique == null) return;
            ParteClique.Text = "1";
            _ultimoClicado = 0;
            AtualizarBanner();
        }

        private void ProximoTamanho_Click(object sender, RoutedEventArgs e)
        {
            int i = TamClique.SelectedIndex;
            if (i + 1 < TamClique.Items.Count) TamClique.SelectedIndex = i + 1;
            else SetStatus("Este é o último tamanho da lista.");
        }

        /// <summary>Chamado pelo CorelDRAW a cada mudança de seleção, enquanto o modo clique está ligado.</summary>
        private void OnSelectionChange()
        {
            if (!_cliqueLigado || _cliqueOcupado || _cliqueCallbackPendente) return;
            // espera o Corel terminar o clique antes de mexer no documento
            _cliqueCallbackPendente = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _cliqueCallbackPendente = false;
                DefinirClicado();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void DefinirClicado()
        {
            if (!_cliqueLigado || _cliqueOcupado) return;
            ShapeRange r;
            try { r = _app.ActiveSelectionRange; } catch { return; }
            if (r == null || r.Count != 1) return;
            var s = r[1];
            int id = s.StaticID;
            if (id == _ultimoClicado) return;
            if (TamanhoConcluido) { AtualizarBanner(); return; }

            if (!Cdr.IsClosed(s))
            {
                CliqueSub.Text = Cdr.IsGroup(s) ? "Isso é um grupo: segure Ctrl ao clicar para pegar só a parte." : "Isso não é uma curva fechada; ignorado.";
                return;
            }
            var tam = TamanhoClique;
            var atual = Moldes.Info(s);
            if (atual != null && atual.IsMatriz && tam != Engine.Tag.Matriz)
            {
                CliqueSub.Text = $"Essa é a {atual.Nome}; ignorada. Para refazer, escolha MATRIZ.";
                return;
            }

            int parte = ParseInt(ParteClique.Text, 1);
            _cliqueOcupado = true;
            try
            {
                string nome = null;
                Run("Definir por clique", doc =>
                {
                    Moldes.Define(_app, new[] { s }, tam, parte, "", _cfg);
                    nome = Moldes.NomeDe(tam == Engine.Tag.Matriz ? tam : OrderImport.NormalizeSize(tam), parte, "");
                });
                if (nome != null)
                {
                    _ultimoClicado = id;
                    ParteClique.Text = (parte + 1).ToString();
                    SetStatus($"{nome} definido.");
                    AtualizarBanner(nome);
                }
            }
            finally { _cliqueOcupado = false; }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_cliqueLigado) DesligarClique();
        }

        // ================================================================ APLICAR

        private void Aplicar_Click(object sender, RoutedEventArgs e)
        {
            if (_cliqueLigado) DesligarClique();
            int? parte = null;
            var p = AplicarParte.SelectedItem as string;
            if (p != null && p.StartsWith("Somente parte ")) parte = ParseInt(p.Substring(14), 0);
            bool soSel = AplicarSoSelecao.IsChecked == true;
            _cfg.SubstituirAoAplicar = AplicarSubst.IsChecked == true;
            _cfg.EscalaArte = (ArtScale)Math.Max(0, AplicarEscala.SelectedIndex);
            try { _cfg.Save(); } catch { }

            Run("Aplicar matrizes", doc =>
            {
                var r = Assembler.AplicarMatrizes(_app, _cfg, parte, soSel ? Selecao() : null);
                var msg = $"Matriz aplicada em {r.Moldes} molde(s) de {r.Partes} parte(s).";
                SetStatus(msg);
                if (r.Avisos.Count > 0) Aviso(msg + "\n\n" + string.Join("\n", r.Avisos));
            }, agruparDesfazer: false);
        }

        private void RemoverDefinicao_Click(object sender, RoutedEventArgs e)
            => Run("Remover definição", doc => SetStatus($"{Moldes.Undefine(_app, Selecao())} definição(ões) removida(s)."), atualizarDefinicoes: true);

        private void InserirCampos_Click(object sender, RoutedEventArgs e)
            => Run("Inserir campos", doc =>
            {
                var sel = Selecao();
                if (sel.Count != 1) throw new UserException("Selecione apenas a matriz.");
                Tools.InserirCamposPadrao(_app, sel[0]);
                SetStatus("Campos NOME, Nº e ADICIONAL inseridos. Ajuste fonte, cor e posição à vontade.");
            });

        private void Marcar_Click(object sender, RoutedEventArgs e)
        {
            var kind = (string)((Button)sender).Tag;
            Run("Marcar " + kind, doc => SetStatus($"{Tools.Marcar(_app, Selecao(), kind)} objeto(s) marcados como {kind}."));
        }

        private void Desmarcar_Click(object sender, RoutedEventArgs e)
            => Run("Desmarcar", doc => SetStatus($"{Tools.Desmarcar(Selecao())} objeto(s) desmarcados."));

        // ================================================================ MONTAGEM

        private const string TodosTipos = "Todos os tipos";
        private bool UsandoCalcao => UsarCalcao.IsChecked == true;

        /// <summary>Linhas que entram na montagem: tipo escolhido e com tamanho (camiseta ou calção).</summary>
        private List<OrderLine> LinhasAtivas()
        {
            var tipo = FiltroTipo.SelectedItem as string;
            return _linhas.Where(l => (tipo == null || tipo == TodosTipos || MesmoTipoPedido(l.Tipo, tipo))
                                      && !string.IsNullOrWhiteSpace(TamanhoDaLinha(l))).ToList();
        }

        private string TamanhoDaLinha(OrderLine linha)
        {
            var tamanho = UsandoCalcao ? linha.TamanhoCalcao : linha.TamanhoCamiseta;
            if (string.IsNullOrWhiteSpace(tamanho) && string.IsNullOrWhiteSpace(linha.TamanhoCamiseta) && string.IsNullOrWhiteSpace(linha.TamanhoCalcao))
                tamanho = linha.Tamanho;
            return OrderImport.NormalizeSize(tamanho);
        }

        private bool FiltrarLinhaDaGrade(object item)
        {
            var tipo = FiltroTipo?.SelectedItem as string;
            return item is OrderLine linha && (string.IsNullOrWhiteSpace(tipo) || tipo == TodosTipos || MesmoTipoPedido(linha.Tipo, tipo));
        }

        private static bool MesmoTipoPedido(string tipoLinha, string tipoFiltro)
            => string.Equals(NormalizarTipoPedido(tipoLinha), NormalizarTipoPedido(tipoFiltro), StringComparison.OrdinalIgnoreCase);

        private static string NormalizarTipoPedido(string tipo)
        {
            tipo = (tipo ?? "").Trim();
            int i = 0;
            while (i < tipo.Length && char.IsDigit(tipo[i])) i++;
            if (i > 0)
            {
                while (i < tipo.Length && char.IsWhiteSpace(tipo[i])) i++;
                if (i < tipo.Length && (tipo[i] == '-' || tipo[i] == '\u2013' || tipo[i] == '\u2014' || tipo[i] == '\u2212'))
                    tipo = tipo.Substring(i + 1).Trim();
            }
            var semAcentos = tipo.Normalize(System.Text.NormalizationForm.FormD);
            return new string(semAcentos.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).Trim();
        }

        private void AtualizarTipos()
        {
            var atual = FiltroTipo.SelectedItem as string;
            var tipos = new List<string> { TodosTipos };
            tipos.AddRange(_linhas.Select(l => (l.Tipo ?? "").Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
            FiltroTipo.ItemsSource = tipos;
            FiltroTipo.SelectedItem = atual != null && tipos.Contains(atual) ? atual : TodosTipos;
            System.Windows.Data.CollectionViewSource.GetDefaultView(_linhas)?.Refresh();
        }

        private void AtualizarResumo()
        {
            if (ResumoPedido == null) return;
            AtualizarVisibilidadeCalcao();
            var ativas = LinhasAtivas();
            int pecas = ativas.Sum(l => Math.Max(1, l.Quantidade));
            var qual = UsandoCalcao ? "calções" : "camisetas";
            ResumoPedido.Text = _linhas.Count == 0 ? "Importe a planilha do pedido para continuar." :
                $"{pecas} {qual} para montar: " +
                string.Join("  ", ativas.GroupBy(TamanhoDaLinha)
                    .OrderBy(g => { int i = _cfg.Tamanhos.IndexOf(g.Key); return i < 0 ? 999 : i; })
                    .Select(g => $"{g.Key}={g.Sum(l => Math.Max(1, l.Quantidade))}"));
            ColunasInfo.Text = "Planilha: Tipo · Nome · Número · Adicional · Tam. Camiseta · Tam. Calção · Quantidade";
        }

        private void AtualizarVisibilidadeCalcao()
        {
            if (OSGrupoCalcao == null) return;
            bool temCalcao = _linhas.Any(l => !string.IsNullOrWhiteSpace(l.TamanhoCalcao) ||
                (UsandoCalcao && !string.IsNullOrWhiteSpace(l.Tamanho)));
            OSGrupoCalcao.Visibility = temCalcao ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UsarTamanho_Checked(object sender, RoutedEventArgs e)
        {
            if (Grade == null) return;
            _cfg.UsarTamanhoCalcao = UsandoCalcao;
            AtualizarResumo();
        }

        private void FiltroTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            System.Windows.Data.CollectionViewSource.GetDefaultView(_linhas)?.Refresh();
            AtualizarResumo();
        }

        private void Grade_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            var linha = e.Row?.Item as OrderLine;
            bool tipoAlterado = string.Equals(Convert.ToString(e.Column?.Header), "Tipo", StringComparison.OrdinalIgnoreCase);
            // CellEditEnding ocorre antes de o DataGrid gravar o valor no objeto. Adiar
            // para depois do commit evita reentrância no grid e exceções não tratadas no Corel.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (linha != null)
                    {
                        linha.TamanhoCamiseta = OrderImport.NormalizeSize(linha.TamanhoCamiseta);
                        linha.TamanhoCalcao = OrderImport.NormalizeSize(linha.TamanhoCalcao);
                    }
                    if (tipoAlterado) AtualizarTipos();
                    AtualizarResumo();
                }
                catch (Exception ex)
                {
                    SetStatus("Não foi possível atualizar o resumo da linha: " + ex.Message);
                }
            }), DispatcherPriority.ContextIdle);
        }

        private void CarregarLinhas(IEnumerable<string[]> rows, string origem)
        {
            var avisos = new List<string>();
            _cfg.UsarTamanhoCalcao = UsandoCalcao;
            var novas = OrderImport.FromRows(rows, _cfg, avisos);
            _suspenderResumo = true;
            try { foreach (var l in novas) _linhas.Add(l); }
            finally { _suspenderResumo = false; }
            var desconhecidos = novas.SelectMany(l => new[] { l.TamanhoCamiseta, l.TamanhoCalcao })
                .Where(t => !string.IsNullOrEmpty(t)).Distinct().Where(t => !_cfg.Tamanhos.Contains(t)).ToList();
            if (desconhecidos.Count > 0) avisos.Add("Tamanhos fora da lista de configuração: " + string.Join(", ", desconhecidos));
            AtualizarTipos();
            AtualizarResumo();
            SetStatus($"{novas.Count} linha(s) importadas de {origem}.");
            if (avisos.Count > 0) Aviso(string.Join("\n", avisos.Take(25)));
        }

        private void Modelo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "modelo_pedido.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                Filter = "Planilha Excel (*.xlsx)|*.xlsx",
                Title = "Salvar modelo de planilha"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                OrderImport.SalvarModelo(dlg.FileName, _cfg);
                SetStatus("Modelo salvo: " + dlg.FileName);
                try { System.Diagnostics.Process.Start(dlg.FileName); } catch { }
            }
            catch (Exception ex) { Aviso(ex.Message); }
        }
        private void Planilha_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Planilhas (*.xlsx;*.xls;*.xlsm;*.csv;*.txt)|*.xlsx;*.xls;*.xlsm;*.csv;*.txt|Todos|*.*",
                Title = "Abrir planilha do pedido"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                _planilhaOrigem = dlg.FileName;
                CarregarLinhas(OrderImport.ReadFile(dlg.FileName), Path.GetFileName(dlg.FileName));
                if (string.IsNullOrWhiteSpace(Pedido.Text)) Pedido.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
            catch (Exception ex) { Aviso(ex.Message); }
            finally { Cursor = null; }
        }

        private void LimparLista_Click(object sender, RoutedEventArgs e)
        {
            _linhas.Clear();
            AtualizarTipos();
            AtualizarResumo();
        }

        private void Montar_Click(object sender, RoutedEventArgs e)
        {
            Grade.CommitEdit(DataGridEditingUnit.Row, true);
            var linhas = LinhasAtivas();
            if (linhas.Count == 0)
            {
                Aviso(_linhas.Count == 0 ? "A lista do pedido está vazia." : $"Nenhuma linha tem tamanho de {(UsandoCalcao ? "calção" : "camiseta")} para o tipo escolhido.");
                return;
            }
            foreach (var linha in linhas) linha.Tamanho = TamanhoDaLinha(linha);

            bool? branco = null;
            List<MoldeInfo> moldesLidos = null;
            if (_app?.ActiveDocument != null)
            {
                bool semCor = false;
                Run("Verificar matrizes", doc =>
                {
                    moldesLidos = Moldes.FindAll(doc);
                    semCor = moldesLidos.Any(m => m.IsMatriz && m.Shape.Fill.Type == cdrFillType.cdrNoFill);
                }, agruparDesfazer: false);
                if (semCor && _cfg.PreencherBrancoSemCor)
                    branco = Pergunta("Há matriz sem preenchimento. Preencher as peças de branco (C0 M0 Y0 K0) e continuar?");
            }

            Run("Montar pedido", doc =>
            {
                var r = Assembler.Montar(_app, linhas, Pedido.Text?.Trim(), _cfg, branco, moldesLidos);
                if (r.Criadas.Count <= 250) Cdr.Select(doc, r.Criadas);
                else
                {
                    try { doc.ClearSelection(); } catch { }
                    SetStatus($"Montagem concluída: {r.Pecas} peça(s) em {r.Linhas} linha(s). Para manter o Corel leve, não selecionei todas as peças geradas.");
                }
                if (r.Criadas.Count <= 250) SetStatus($"Montagem concluída: {r.Pecas} peça(s) em {r.Linhas} linha(s).");
                if (r.Avisos.Count > 0) Aviso("Montagem concluída com avisos:\n\n" + string.Join("\n", r.Avisos.Take(30)));
            }, agruparDesfazer: false);
        }

        private void SelecionarPecas_Click(object sender, RoutedEventArgs e)
        {
            var parte = (SelParte.Text ?? "").Trim().ToUpperInvariant();
            var tam = OrderImport.NormalizeSize(SelTamanho.Text);
            Run("Selecionar peças", doc =>
            {
                var ps = Assembler.FindPecas(doc, t =>
                    (parte.Length == 0 || t["P"] == parte || t["A"].ToUpperInvariant() == parte) &&
                    (tam.Length == 0 || t["T"] == tam));
                Cdr.Select(doc, ps);
                SetStatus($"{ps.Count} peça(s) selecionadas.");
            });
        }

        // ================================================================ FERRAMENTAS

        private void Origem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sel = Selecao();
                if (sel.Count != 1) throw new UserException("Selecione apenas o molde de origem.");
                if (!Cdr.HasContents(sel[0])) throw new UserException("O molde de origem não tem arte dentro (PowerClip).");
                _origem = sel[0];
                var info = Moldes.Info(_origem);
                OrigemInfo.Text = "Origem: " + (info != null ? $"{info.Tamanho} · {info.ParteLabel}" : (_origem.Name ?? "molde sem nome"));
                SetStatus("Origem definida. Agora selecione os destinos.");
            }
            catch (Exception ex) { Aviso(ex.Message); }
        }

        private void Transferir_Click(object sender, RoutedEventArgs e)
        {
            bool tudo = TransfTudo.IsChecked == true, subst = TransfSubst.IsChecked == true;
            Run("Transferir", doc =>
            {
                int n = Assembler.Transferir(_app, _origem, Selecao(), tudo, subst, _cfg);
                SetStatus($"Arte transferida para {n} molde(s).");
            });
        }

        private void Copias_Click(object sender, RoutedEventArgs e)
        {
            int n = ParseInt(NumCopias.Text, 1);
            Run("Criar cópias", doc =>
            {
                var c = Assembler.CriarCopias(_app, Selecao(), n, _cfg);
                Cdr.Select(doc, c);
                SetStatus($"{c.Count} cópia(s) criadas com novo sequencial.");
            });
        }

        private void Espelhar_Click(object sender, RoutedEventArgs e)
        {
            bool h = (string)((Button)sender).Tag == "H";
            Run("Espelhar", doc => Tools.Espelhar(Selecao(), h));
        }

        private void Girar_Click(object sender, RoutedEventArgs e)
        {
            double a = double.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture);
            Run("Girar", doc => Tools.Girar(Selecao(), a));
        }

        private void EmpilharCentro_Click(object sender, RoutedEventArgs e) => Run("Empilhar ao centro", doc => Tools.EmpilharCentro(Selecao()));
        private void EmpilharHorizontal_Click(object sender, RoutedEventArgs e) => Run("Empilhar horizontal", doc => Tools.EmpilharHorizontal(Selecao(), _cfg.Espacamento));

        private void TextoCurvas_Click(object sender, RoutedEventArgs e)
        {
            bool dentro = CurvasDentro.IsChecked == true;
            Run("Texto em curvas", doc => SetStatus($"{Tools.TextoParaCurvas(Selecao(), dentro)} texto(s) convertidos em curvas (IDs preservados)."));
        }

        private void Contorno_Click(object sender, RoutedEventArgs e)
        {
            double mm = ParseDbl(ContornoMm.Text, _cfg.ContornoMm);
            int cor = ContornoCor.SelectedIndex;
            _cfg.ContornoMm = mm; _cfg.ContornoCor = cor;
            Run("Contorno", doc => SetStatus($"Contorno aplicado em {Tools.AplicarContorno(_app, Selecao(), mm, cor)} objeto(s)."));
        }

        private void PegarCor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var s = Selecao()[0];
                if (s.Fill.Type != cdrFillType.cdrUniformFill) throw new UserException("O objeto selecionado não tem preenchimento uniforme.");
                _corOriginal = s.Fill.UniformColor;
                CorOriginalInfo.Text = Tools.DescreverCor(_corOriginal);
            }
            catch (Exception ex) { Aviso(ex.Message); }
        }

        private void SubstituirCor_Click(object sender, RoutedEventArgs e)
        {
            if (_corOriginal == null) { Aviso("Pegue primeiro a cor original."); return; }
            var nova = _app.CreateCMYKColor(ParseInt(NovaC.Text, 0), ParseInt(NovaM.Text, 0), ParseInt(NovaY.Text, 0), ParseInt(NovaK.Text, 0));
            bool f = CorPreench.IsChecked == true, c = CorContornos.IsChecked == true, d = CorDentro.IsChecked == true;
            Run("Substituir cor", doc => SetStatus($"{Tools.SubstituirCor(_app, Selecao(), _corOriginal, nova, f, c, d)} cor(es) substituídas."));
        }

        private void Limpar_Click(object sender, RoutedEventArgs e)
        {
            bool fill = LimparFill.IsChecked == true;
            if (!Pergunta("Remover a arte de dentro dos moldes selecionados?")) return;
            Run("Limpar", doc => SetStatus($"{Assembler.Limpar(_app, Selecao(), fill)} molde(s) limpos."));
        }

        private void Relatorio_Click(object sender, RoutedEventArgs e)
        {
            Run("Relatório", doc =>
            {
                var r = _app.ActiveSelectionRange;
                var fonte = r != null && r.Count > 0 ? Cdr.Items(r).ToList() : Cdr.Items(doc.ActivePage.Shapes).ToList();
                var txt = Tools.RelatorioTexto(Tools.Contar(fonte), _cfg.Tamanhos);
                try { System.Windows.Clipboard.SetText(txt); } catch { }
                Info(txt + "\n\n(copiado para a área de transferência)");
            });
        }

        // ================================================================ ENCAIXE

        private void Encaixar_Click(object sender, RoutedEventArgs e)
        {
            LerEncaixe();
            Run("Encaixe sobre outro", doc =>
            {
                var r = Nesting.Encaixar(_app, Selecao(), _cfg, null, Exporter.EscPressionado);
                var msg = $"{r.Encaixadas} peça(s) encaixadas" + (r.Copiadas > 0 ? $" ({r.Copiadas} cópias criadas)" : "") + ".";
                if (r.SemPar.Count > 0)
                {
                    msg += $"\n{r.SemPar.Count} forma(s)-base sem par ficaram com contorno vermelho e foram selecionadas.";
                    Cdr.Select(doc, r.SemPar);
                }
                SetStatus(msg.Replace("\n", " "));
                Info(msg);
            });
            _cfg.Save();
        }

        private void LerEncaixe()
        {
            _cfg.EncaixeTolerancia = ParseDbl(EncTol.Text, _cfg.EncaixeTolerancia);
            _cfg.EncaixeSimilaridadeNos = ParseDbl(EncSim.Text, _cfg.EncaixeSimilaridadeNos);
            _cfg.EncaixeEspelhar = EncEspelhar.IsChecked == true;
            _cfg.EncaixeCopiarFaltantes = EncCopiar.IsChecked == true;
            _cfg.EncaixeApagarBase = EncApagar.IsChecked == true;
        }

        // ================================================================ EXPORTAR

        private void EscolherPasta_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = ExpPasta.Text, Description = "Pasta de exportação" })
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) ExpPasta.Text = dlg.SelectedPath;
        }

        private void LerExportacao()
        {
            _cfg.Formato = (ExpFormato.SelectedItem as ComboBoxItem)?.Content as string ?? "JPG";
            _cfg.Cor = (ExpCor.SelectedItem as ComboBoxItem)?.Content as string ?? "CMYK";
            _cfg.CompressaoPdf = (ExpPdfComp.SelectedItem as ComboBoxItem)?.Content as string ?? "JPEG";
            _cfg.Dpi = Math.Max(30, ParseInt(ExpDpi.Text, 150));
            _cfg.PastaExportacao = ExpPasta.Text.Trim();
            _cfg.GerarRelatorio = ExpRelatorio.IsChecked == true;
            _cfg.TecidoSelecionado = Math.Max(0, ExpTecido.SelectedIndex);
        }

        private void Exportar_Click(object sender, RoutedEventArgs e)
        {
            LerExportacao();
            var tecido = ExpTecido.SelectedItem as Tecido;
            bool umPorObjeto = ExpUmPorObjeto.IsChecked == true;
            string nomeBase = ExpNome.Text.Trim();

            int qtd = _app?.ActiveSelectionRange?.Count ?? 0;
            if (umPorObjeto && qtd > 100 && !Pergunta($"Serão gerados {qtd} arquivos. Continuar?")) return;

            Run("Exportar", doc =>
            {
                if (nomeBase.Length == 0)
                {
                    nomeBase = Path.GetFileNameWithoutExtension(doc.FileName ?? "");
                    if (!string.IsNullOrWhiteSpace(Pedido.Text)) nomeBase = (nomeBase.Length > 0 ? nomeBase + "_" : "") + Pedido.Text.Trim();
                }
                var r = Exporter.Exportar(_app, Selecao(), nomeBase, tecido, umPorObjeto, _cfg);
                var msg = (r.Cancelado ? "Exportação CANCELADA. " : "") + $"{r.Arquivos.Count} arquivo(s) em {_cfg.PastaExportacao}.";
                if (r.Relatorio != null) msg += $"\nRelatório: {r.Relatorio}";
                if (r.Avisos.Count > 0) msg += "\n\nAvisos:\n" + string.Join("\n", r.Avisos.Take(20));
                SetStatus(msg.Split('\n')[0]);
                Info(msg);
            });
            _cfg.Save();
        }

        private void ExportarPedidoPdf_Click(object sender, RoutedEventArgs e)
        {
            LerExportacao();
            Run("Exportar pedido em PDF", doc =>
            {
                var r = Exporter.ExportarMontagemPdf50(_app, _cfg.PastaExportacao, Pedido.Text, _cfg);
                var msg = $"{r.Arquivos.Count} PDF(s) gerado(s) em:\n{Path.Combine(_cfg.PastaExportacao, r.PastaPedido)}";
                if (r.Avisos.Count > 0) msg += "\n\nAvisos:\n" + string.Join("\n", r.Avisos);
                SetStatus($"PDFs do pedido exportados: {r.Arquivos.Count} arquivo(s).");
                Info(msg);
            });
            _cfg.Save();
        }

        private void AtualizarResumoOS()
        {
            if (_linhas.Count == 0)
            {
                OSResumo.Text = "O YUNO vai ler as peças montadas, os tamanhos, as quantidades e as variações de cor diretamente do documento CorelDRAW.";
                return;
            }
            var resumo = _linhas.GroupBy(l => new { Tipo = string.IsNullOrWhiteSpace(l.Tipo) ? "Uniforme" : l.Tipo, Tamanho = string.IsNullOrWhiteSpace(l.Tamanho) ? "—" : l.Tamanho })
                .Select(g => $"{g.Key.Tipo}  |  {g.Key.Tamanho}: {g.Sum(l => Math.Max(1, l.Quantidade))}").ToList();
            OSResumo.Text = $"{_linhas.Sum(l => Math.Max(1, l.Quantidade))} uniforme(s) no pedido\n" + string.Join("\n", resumo);
        }

        private void AbrirPastaLogo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(OrderServiceReport.LogoFolder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(OrderServiceReport.LogoFolder) { UseShellExecute = true });
            }
            catch (Exception ex) { Aviso("Não consegui abrir a pasta da logo: " + ex.Message); }
        }

        private void GerarOS_Click(object sender, RoutedEventArgs e)
        {
            Run("Gerar ordem de serviço", doc =>
            {
                var r = OrderServiceReport.Gerar(_app, doc, _cfg, OSNomePedido.Text, OSTecido.Text, OSDataEntrega.SelectedDate,
                    (OSGola.SelectedItem as ComboBoxItem)?.Content as string,
                    (OSAcabamentoGola.SelectedItem as ComboBoxItem)?.Content as string,
                    (OSPunho.SelectedItem as ComboBoxItem)?.Content as string,
                    OSModeloCalcao.Text, OSBolsoCalcao.Text, OSDetalhesCalcao.Text,
                    TextoCombo(OSEtiqueta), OSObservacoes.Text, _linhas.ToList());
                OSDestino.Text = "PDF salvo em: " + r.PdfPath;
                _pdfOsAtual = r.PdfPath;
                SetStatus($"OS gerada: {r.Uniformes} uniforme(s), {r.VariacoesCor} variação(ões) de cor.");
                var confirmacao = $"Ordem de serviço gerada.\n\n{r.PdfPath}\n\nUniformes: {r.Uniformes}\nVariações de cor: {r.VariacoesCor}";
                if (!string.IsNullOrWhiteSpace(r.Aviso)) confirmacao += "\n\n" + r.Aviso;
                Info(confirmacao);
            });
        }

        // ================================================================ CONFIG

        private static void SelecionarItem(ComboBox cb, string valor)
        {
            foreach (var it in cb.Items)
                if (it is ComboBoxItem ci && string.Equals(ci.Content as string, valor, StringComparison.OrdinalIgnoreCase)) { cb.SelectedItem = ci; return; }
            cb.SelectedIndex = 0;
        }

        private void CarregarConfigNaTela()
        {
            var c = _cfg;
            SelTamanho.ItemsSource = new[] { "" }.Concat(c.Tamanhos).ToList();

            CfgTamanhos.Text = string.Join(Environment.NewLine, c.Tamanhos);
            CfgTiposJogador.Text = string.Join(Environment.NewLine, c.TiposJogador);
            CfgModelosCalcao.Text = string.Join(Environment.NewLine, c.ModelosCalcao);
            CfgOpcoesBolsoCalcao.Text = string.Join(Environment.NewLine, c.OpcoesBolsoCalcao);
            CfgRotulo.IsChecked = c.CriarRotulo;
            CfgColTipo.Text = c.ColunaTipo; CfgColCalcao.Text = c.ColunaTamanhoCalcao; PartesPorPeca.Text = c.PartesPorPeca.ToString();
            UsarCalcao.IsChecked = c.UsarTamanhoCalcao; UsarCamiseta.IsChecked = !c.UsarTamanhoCalcao;
            CfgColTam.Text = c.ColunaTamanho; CfgColNome.Text = c.ColunaNome; CfgColNum.Text = c.ColunaNumero;
            CfgColAd.Text = c.ColunaAdicional; CfgColQtd.Text = c.ColunaQuantidade;
            CfgCabecalho.IsChecked = c.PrimeiraLinhaCabecalho;
            CfgLargNome.Text = Fmt(c.LarguraNomePct); CfgLargNum.Text = Fmt(c.LarguraNumeroPct); CfgLargAd.Text = Fmt(c.LarguraAdicionalPct);
            CfgEspaco.Text = Fmt(c.Espacamento); CfgMargem.Text = Fmt(c.MargemTextoPct); CfgFonteInd.Text = Fmt(c.FonteIndicador);
            CfgAlturaPagina.Text = Fmt(c.AlturaMaximaMontagem);
            AplicarEscala.SelectedIndex = (int)c.EscalaArte;
            AplicarSubst.IsChecked = c.SubstituirAoAplicar;
            var tamClique = new List<string> { Engine.Tag.Matriz };
            tamClique.AddRange(c.Tamanhos);
            var tamAtual = TamClique.SelectedItem as string;
            TamClique.ItemsSource = tamClique;
            TamClique.SelectedItem = tamAtual != null && tamClique.Contains(tamAtual) ? tamAtual : Engine.Tag.Matriz;
            CfgNomeMaius.IsChecked = c.NomeMaiusculo; CfgAdMaius.IsChecked = c.AdicionalMaiusculo;
            CfgAjustarGab.IsChecked = c.AjustarAoGabarito;
            CfgAgrupar.IsChecked = c.AgruparLinha; CfgPaginasTamanho.IsChecked = c.SepararTamanhosEmPaginas; CfgBranco.IsChecked = c.PreencherBrancoSemCor;
            CfgGerarId.IsChecked = c.GerarId; CfgIdPed.IsChecked = c.IdPedido; CfgIdSeq.IsChecked = c.IdSequencial;
            CfgIdTam.IsChecked = c.IdTamanho; CfgIdParte.IsChecked = c.IdParte; CfgIdApelido.IsChecked = c.IdApelido;
            CfgIdFonte.Text = c.FonteIdNome; CfgIdTam2.Text = Fmt(c.FonteId);
            CfgTecidos.Text = string.Join(Environment.NewLine, c.Tecidos.Select(t => $"{t.Nome};{Fmt(t.LarguraCm)}"));
            CfgPastaRel.Text = c.PastaRelatorio;
            CfgPastaPedidos.Text = c.PastaPedidosLocais;

            ContornoMm.Text = Fmt(c.ContornoMm); ContornoCor.SelectedIndex = Math.Max(0, Math.Min(3, c.ContornoCor));

            EncTol.Text = Fmt(c.EncaixeTolerancia); EncSim.Text = Fmt(c.EncaixeSimilaridadeNos);
            EncEspelhar.IsChecked = c.EncaixeEspelhar; EncCopiar.IsChecked = c.EncaixeCopiarFaltantes; EncApagar.IsChecked = c.EncaixeApagarBase;

            SelecionarItem(ExpFormato, c.Formato); SelecionarItem(ExpCor, c.Cor); SelecionarItem(ExpPdfComp, c.CompressaoPdf);
            ExpDpi.Text = c.Dpi.ToString(); ExpPasta.Text = c.PastaExportacao; ExpRelatorio.IsChecked = c.GerarRelatorio;
            ExpTecido.ItemsSource = c.Tecidos.ToList();
            ExpTecido.SelectedIndex = Math.Min(Math.Max(0, c.TecidoSelecionado), c.Tecidos.Count - 1);
            OSTecido.ItemsSource = c.Tecidos.ToList();
            if (OSTecido.Items.Count > 0 && string.IsNullOrWhiteSpace(OSTecido.Text)) OSTecido.SelectedIndex = Math.Min(Math.Max(0, c.TecidoSelecionado), OSTecido.Items.Count - 1);
            OSModeloCalcao.ItemsSource = c.ModelosCalcao.ToList();
            OSBolsoCalcao.ItemsSource = c.OpcoesBolsoCalcao.ToList();
            if (string.IsNullOrWhiteSpace(OSModeloCalcao.Text)) OSModeloCalcao.Text = c.ModelosCalcao.FirstOrDefault() ?? "Não informado";
            if (string.IsNullOrWhiteSpace(OSBolsoCalcao.Text)) OSBolsoCalcao.Text = c.OpcoesBolsoCalcao.FirstOrDefault() ?? "Não informado";
            OSDestino.Text = "A OS será salva dentro da pasta do pedido, em " + c.PastaExportacao;
        }

        private void SalvarConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var c = _cfg;
                var tams = CfgTamanhos.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(OrderImport.NormalizeSize).Where(t => t.Length > 0 && t != Engine.Tag.Matriz).Distinct().ToList();
                if (tams.Count == 0) throw new UserException("Informe pelo menos um tamanho.");
                c.Tamanhos = tams;
                var tiposJogador = CfgTiposJogador.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (tiposJogador.Count == 0) throw new UserException("Cadastre pelo menos um tipo de jogador para o modelo da planilha.");
                c.TiposJogador = tiposJogador;
                c.ModelosCalcao = CfgModelosCalcao.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                c.OpcoesBolsoCalcao = CfgOpcoesBolsoCalcao.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (c.ModelosCalcao.Count == 0) c.ModelosCalcao = new Config().ModelosCalcao;
                if (c.OpcoesBolsoCalcao.Count == 0) c.OpcoesBolsoCalcao = new Config().OpcoesBolsoCalcao;
                c.CriarRotulo = CfgRotulo.IsChecked == true;
                c.ColunaTamanho = CfgColTam.Text.Trim(); c.ColunaNome = CfgColNome.Text.Trim(); c.ColunaNumero = CfgColNum.Text.Trim();
                c.ColunaTipo = CfgColTipo.Text.Trim(); c.ColunaTamanhoCalcao = CfgColCalcao.Text.Trim();
                c.PartesPorPeca = Math.Max(1, ParseInt(PartesPorPeca.Text, c.PartesPorPeca)); c.UsarTamanhoCalcao = UsandoCalcao;
                c.ColunaAdicional = CfgColAd.Text.Trim(); c.ColunaQuantidade = CfgColQtd.Text.Trim();
                c.PrimeiraLinhaCabecalho = CfgCabecalho.IsChecked == true;
                c.LarguraNomePct = ParseDbl(CfgLargNome.Text, c.LarguraNomePct);
                c.LarguraNumeroPct = ParseDbl(CfgLargNum.Text, c.LarguraNumeroPct);
                c.LarguraAdicionalPct = ParseDbl(CfgLargAd.Text, c.LarguraAdicionalPct);
                c.Espacamento = ParseDbl(CfgEspaco.Text, c.Espacamento);
                c.AlturaMaximaMontagem = Math.Max(0, ParseDbl(CfgAlturaPagina.Text, c.AlturaMaximaMontagem));
                c.MargemTextoPct = Math.Max(0, Math.Min(60, ParseDbl(CfgMargem.Text, c.MargemTextoPct)));
                c.FonteIndicador = (float)ParseDbl(CfgFonteInd.Text, c.FonteIndicador);
                c.EscalaArte = (ArtScale)Math.Max(0, AplicarEscala.SelectedIndex);
                c.SubstituirAoAplicar = AplicarSubst.IsChecked == true;
                c.NomeMaiusculo = CfgNomeMaius.IsChecked == true; c.AdicionalMaiusculo = CfgAdMaius.IsChecked == true;
                c.AjustarAoGabarito = CfgAjustarGab.IsChecked == true;
                c.AgruparLinha = CfgAgrupar.IsChecked == true; c.SepararTamanhosEmPaginas = CfgPaginasTamanho.IsChecked == true; c.PreencherBrancoSemCor = CfgBranco.IsChecked == true;
                c.GerarId = CfgGerarId.IsChecked == true; c.IdPedido = CfgIdPed.IsChecked == true; c.IdSequencial = CfgIdSeq.IsChecked == true;
                c.IdTamanho = CfgIdTam.IsChecked == true; c.IdParte = CfgIdParte.IsChecked == true; c.IdApelido = CfgIdApelido.IsChecked == true;
                c.FonteIdNome = string.IsNullOrWhiteSpace(CfgIdFonte.Text) ? "Arial" : CfgIdFonte.Text.Trim();
                c.FonteId = (float)ParseDbl(CfgIdTam2.Text, c.FonteId);
                c.PastaRelatorio = CfgPastaRel.Text.Trim();
                c.PastaPedidosLocais = CfgPastaPedidos.Text.Trim();
                if (string.IsNullOrWhiteSpace(c.PastaPedidosLocais)) throw new UserException("Escolha uma pasta para salvar os pedidos locais.");
                var tecidos = new List<Tecido>();
                foreach (var l in CfgTecidos.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = l.Split(';');
                    if (p[0].Trim().Length == 0) continue;
                    tecidos.Add(new Tecido { Nome = p[0].Trim(), LarguraCm = p.Length > 1 ? ParseDbl(p[1], 0) : 0 });
                }
                if (tecidos.Count > 0) c.Tecidos = tecidos;
                LerEncaixe();
                LerExportacao();
                c.Save();
                CarregarConfigNaTela();
                AtualizarPedidosLocais();
                AtualizarResumo();
                SetStatus("Configurações salvas.");
            }
            catch (Exception ex) { Aviso(ex.Message); }
        }

        private void RestaurarConfig_Click(object sender, RoutedEventArgs e)
        {
            if (!Pergunta("Restaurar todas as configurações para o padrão?")) return;
            _cfg = new Config();
            _shared = _cfg;
            _cfg.Save();
            CarregarConfigNaTela();
            AtualizarResumo();
            SetStatus("Configurações restauradas.");
        }

        // ================================================================ PAINEL ÚNICO (barra de ferramentas)

        /// <summary>Mostra só uma das abas, sem a barra de abas (usado pelos botões da barra de ferramentas).</summary>
        protected void MostrarSomente(int aba, string titulo, string subtitulo, string dockerGuid = null)
        {
            if (aba < 0 || aba >= AbaPorFuncao.Length) return;
            var tab = Tabs.Items.Cast<TabItem>().First(t => Convert.ToString(t.Header) == AbaPorFuncao[aba]);
            var conteudo = tab.Content;
            tab.Content = null;
            Tabs.Visibility = Visibility.Collapsed;
            SingleHost.Content = conteudo;
            SingleHost.Visibility = Visibility.Visible;
            TituloTxt.Text = titulo;
            SubTxt.Text = subtitulo;
            if (_app != null && !string.IsNullOrWhiteSpace(dockerGuid))
            {
                // Cada comando YUNO tem seu próprio docker no Corel. Fechar os demais
                // e reabrir explicitamente o escolhido evita que o Corel perca o primeiro clique.
                int request = System.Threading.Interlocked.Increment(ref _dockerActivationRequest);
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (request != System.Threading.Volatile.Read(ref _dockerActivationRequest)) return;
                    foreach (var guid in DockerGuids)
                    {
                        if (string.Equals(guid, dockerGuid, StringComparison.OrdinalIgnoreCase)) continue;
                        try { _app.FrameWork.HideDocker(guid); } catch { }
                    }
                    try { _app.FrameWork.ShowDocker(dockerGuid); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private static readonly string[] AbaPorFuncao = { "Montagem", "Pedido", "OS", "Exportar" };

        private static readonly string[] DockerGuids =
        {
            "74ca6974-78e4-49ab-af1f-4804be8afc9f", "9e6dd6e7-adce-5e08-8b91-c4f96dcead4e",
            "d42ad130-bfe3-5425-a89c-8eb049c96e6c", "02edb4fb-53e0-5dc4-aef8-366e55479653",
            "ae35b949-ced2-5ac3-ac17-2e2801a0f9e1", "700ff718-0858-5b4c-b5ba-9922ff67be8b",
            "d388a882-7133-5d1c-8ebf-29dfdc2a1229", "dfa57b98-d259-5419-aea5-18bbafb5cc91",
            "f8340c7e-e692-55aa-9477-1d93603b9a90"
        };

        private void Sobre_Click(object sender, RoutedEventArgs e)
        {
            var v = typeof(MainDocker).Assembly.GetName().Version;
            Info($"YUNO {v.ToString(3)}\n\nMontagem de moldes graduados para sublimação no CorelDRAW.\n\nConfigurações: {Config.FilePath}");
        }
    }

    public sealed class HalfTabWidthConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double width = value is double d && !double.IsNaN(d) ? d : 320;
            return Math.Max(120, (width - 14) / 2);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}

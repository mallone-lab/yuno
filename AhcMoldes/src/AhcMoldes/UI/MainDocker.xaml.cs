using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AhcMoldes.Engine;
using Corel.Interop.VGCore;
using CorelApp = Corel.Interop.VGCore.Application;
using CorelColor = Corel.Interop.VGCore.Color;

namespace AhcMoldes.UI
{
    /// <summary>Docker principal do AHC Moldes (hospedado pelo CorelDRAW via AppUI.xslt).</summary>
    public partial class MainDocker : UserControl
    {
        private readonly CorelApp _app;
        private static Config _shared;
        private Config _cfg = new Config();
        private readonly ObservableCollection<OrderLine> _linhas = new ObservableCollection<OrderLine>();
        private Shape _origem;
        private CorelColor _corOriginal;

        public MainDocker()
        {
            InitializeComponent();
        }

        /// <summary>Construtor chamado pelo CorelDRAW (recebe o objeto Application).</summary>
        public MainDocker(object app) : this()
        {
            _app = app as CorelApp;
            if (_shared == null) { try { _shared = Config.Load(); } catch { _shared = new Config(); } }
            _cfg = _shared;
            IsVisibleChanged += (s, e) => { if (IsVisible) { CarregarConfigNaTela(); AtualizarResumo(); AtualizarDefinicoes(); } };
            Grade.ItemsSource = _linhas;
            _linhas.CollectionChanged += (s, e) => AtualizarResumo();
            CarregarConfigNaTela();
            AtualizarResumo();
            AtualizarDefinicoes();
        }

        // ================================================================ infraestrutura

        private void SetStatus(string msg) => Status.Text = msg;

        private static void Info(string msg) => MessageBox.Show(msg, "AHC Moldes", MessageBoxButton.OK, MessageBoxImage.Information);
        private static void Aviso(string msg) => MessageBox.Show(msg, "AHC Moldes", MessageBoxButton.OK, MessageBoxImage.Warning);
        private static bool Pergunta(string msg) => MessageBox.Show(msg, "AHC Moldes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        /// <summary>Executa uma ação com 1 "desfazer", unidade em cm e tratamento de erro.</summary>
        private void Run(string nome, Action<Document> acao)
        {
            if (_app == null) { Aviso("CorelDRAW não disponível."); return; }
            var cursor = Cursor;
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                using (var sc = new CorelScope(_app, "AHC: " + nome)) acao(sc.Doc);
            }
            catch (UserException ex) { SetStatus(ex.Message); Aviso(ex.Message); }
            catch (Exception ex)
            {
                SetStatus("Erro: " + ex.Message);
                MessageBox.Show($"{nome}: ocorreu um erro inesperado.\n\n{ex.Message}\n\n{ex.StackTrace}", "AHC Moldes", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = cursor;
                if (Resumo.IsVisible || AplicarParte.IsVisible) AtualizarDefinicoes();
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

        private void DefinirTamanho_Click(object sender, RoutedEventArgs e)
        {
            var tam = (TamanhoDef.Text ?? "").Trim();
            Run("Definir tamanho", doc =>
            {
                if (tam.Equals(Engine.Tag.Matriz, StringComparison.OrdinalIgnoreCase)) tam = Engine.Tag.Matriz;
                int n = Moldes.Define(_app, Selecao(), tam, ParseInt(ParteNum.Text, 1), Apelido.Text, _cfg);
                SetStatus($"{n} molde(s) definidos como {OrderImport.NormalizeSize(tam)}, parte {ParteNum.Text}.");
            });
            // avança para o próximo tamanho da lista, agilizando a definição manual
            int i = _cfg.Tamanhos.IndexOf(OrderImport.NormalizeSize(tam));
            if (i >= 0 && i + 1 < _cfg.Tamanhos.Count) TamanhoDef.Text = _cfg.Tamanhos[i + 1];
        }

        private void DefinirMatriz_Click(object sender, RoutedEventArgs e)
        {
            Run("Definir matriz", doc =>
            {
                Moldes.Define(_app, Selecao(), Engine.Tag.Matriz, ParseInt(ParteNum.Text, 1), Apelido.Text, _cfg);
                SetStatus($"Matriz da parte {ParteNum.Text} definida.");
            });
        }

        private void DefinirAuto_Click(object sender, RoutedEventArgs e)
        {
            var tams = TamanhosAuto.Items.Cast<string>().Where(t => TamanhosAuto.SelectedItems.Contains(t)).ToList();
            if (tams.Count == 0) { Aviso("Marque na lista os tamanhos que estão selecionados no desenho."); return; }
            Run("Definir tamanhos automático", doc =>
            {
                int n = Moldes.AutoDefine(_app, Selecao(), tams, ParseInt(ParteNum.Text, 1), Apelido.Text, _cfg);
                SetStatus($"{n} moldes definidos por área: {string.Join(", ", tams)}.");
            });
        }

        private void VerDefinicoes_Click(object sender, RoutedEventArgs e) => AtualizarDefinicoes(true);

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
                    var tams = g.Where(m => !m.IsMatriz).Select(m => m.Tamanho).Distinct().OrderBy(Ord).ThenBy(t => t);
                    return $"Parte {g.Key}  {tem}\n   {string.Join(" ", tams)}";
                }).ToList();
                var dups = Moldes.Duplicates(ms);
                if (dups.Count > 0)
                {
                    linhas.Add($"⚠ {dups.Count} moldes com nome repetido" + (selecionarRepetidos ? " (selecionados)" : ""));
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
            if (Tabs.SelectedIndex == 0 || Tabs.SelectedIndex == 1) AtualizarDefinicoes();
        }

        // ================================================================ MODO CLIQUE

        private bool _cliqueLigado;
        private bool _cliqueOcupado;
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
            AtualizarDefinicoes();
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
            if (!_cliqueLigado || _cliqueOcupado) return;
            // espera o Corel terminar o clique antes de mexer no documento
            Dispatcher.BeginInvoke(new Action(DefinirClicado), System.Windows.Threading.DispatcherPriority.Background);
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
            });
        }

        private void RemoverDefinicao_Click(object sender, RoutedEventArgs e)
            => Run("Remover definição", doc => SetStatus($"{Moldes.Undefine(_app, Selecao())} definição(ões) removida(s)."));

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
            OrderImport.UsarTamanhos(_linhas, UsandoCalcao);
            var tipo = FiltroTipo.SelectedItem as string;
            return _linhas.Where(l => (tipo == null || tipo == TodosTipos || string.Equals((l.Tipo ?? "").Trim(), tipo, StringComparison.OrdinalIgnoreCase))
                                      && !string.IsNullOrWhiteSpace(l.Tamanho)).ToList();
        }

        private void AtualizarTipos()
        {
            var atual = FiltroTipo.SelectedItem as string;
            var tipos = new List<string> { TodosTipos };
            tipos.AddRange(_linhas.Select(l => (l.Tipo ?? "").Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
            FiltroTipo.ItemsSource = tipos;
            FiltroTipo.SelectedItem = atual != null && tipos.Contains(atual) ? atual : TodosTipos;
        }

        private void AtualizarResumo()
        {
            if (ResumoPedido == null) return;
            var ativas = LinhasAtivas();
            int pecas = ativas.Sum(l => Math.Max(1, l.Quantidade));
            var qual = UsandoCalcao ? "calções" : "camisetas";
            ResumoPedido.Text = _linhas.Count == 0 ? "Importe a planilha do pedido ou cole as linhas do Excel." :
                $"{pecas} {qual} para montar: " +
                string.Join("  ", ativas.GroupBy(l => l.Tamanho)
                    .OrderBy(g => { int i = _cfg.Tamanhos.IndexOf(g.Key); return i < 0 ? 999 : i; })
                    .Select(g => $"{g.Key}={g.Sum(l => Math.Max(1, l.Quantidade))}"));
            ColunasInfo.Text = "Planilha: Tipo · Nome · Número · Adicional · Tam. Camiseta · Tam. Calção · Quantidade";
        }

        private void UsarTamanho_Checked(object sender, RoutedEventArgs e)
        {
            if (Grade == null) return;
            _cfg.UsarTamanhoCalcao = UsandoCalcao;
            OrderImport.UsarTamanhos(_linhas, UsandoCalcao);
            Grade.Items.Refresh();
            AtualizarResumo();
        }

        private void FiltroTipo_SelectionChanged(object sender, SelectionChangedEventArgs e) => AtualizarResumo();

        private void Grade_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
            => Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var l in _linhas)
                {
                    l.TamanhoCamiseta = OrderImport.NormalizeSize(l.TamanhoCamiseta);
                    l.TamanhoCalcao = OrderImport.NormalizeSize(l.TamanhoCalcao);
                }
                AtualizarTipos();
                AtualizarResumo();
            }));

        private void CarregarLinhas(IEnumerable<string[]> rows, string origem)
        {
            var avisos = new List<string>();
            _cfg.UsarTamanhoCalcao = UsandoCalcao;
            var novas = OrderImport.FromRows(rows, _cfg, avisos);
            foreach (var l in novas) _linhas.Add(l);
            var desconhecidos = novas.SelectMany(l => new[] { l.TamanhoCamiseta, l.TamanhoCalcao })
                .Where(t => !string.IsNullOrEmpty(t)).Distinct().Where(t => !_cfg.Tamanhos.Contains(t)).ToList();
            if (desconhecidos.Count > 0) avisos.Add("Tamanhos fora da lista de configuração: " + string.Join(", ", desconhecidos));
            AtualizarTipos();
            Grade.Items.Refresh();
            AtualizarResumo();
            SetStatus($"{novas.Count} linha(s) importadas de {origem}.");
            if (avisos.Count > 0) Aviso(string.Join("\n", avisos.Take(25)));
        }

        private void Modelo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "modelo_pedido.csv",
                Filter = "Planilha CSV (*.csv)|*.csv",
                Title = "Salvar modelo de planilha"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                OrderImport.SalvarModelo(dlg.FileName);
                SetStatus("Modelo salvo: " + dlg.FileName);
                try { System.Diagnostics.Process.Start(dlg.FileName); } catch { }
            }
            catch (Exception ex) { Aviso(ex.Message); }
        }
        private void Colar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var txt = System.Windows.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(txt)) { Aviso("A área de transferência está vazia. Copie as linhas no Excel primeiro."); return; }
                CarregarLinhas(OrderImport.ParseDelimited(txt), "área de transferência");
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

            bool? branco = null;
            if (_app?.ActiveDocument != null)
            {
                bool semCor = false;
                Run("Verificar matrizes", doc =>
                {
                    semCor = Moldes.FindAll(doc).Any(m => m.IsMatriz && m.Shape.Fill.Type == cdrFillType.cdrNoFill);
                });
                if (semCor && _cfg.PreencherBrancoSemCor)
                    branco = Pergunta("Há matriz sem preenchimento. Preencher as peças de branco (C0 M0 Y0 K0) e continuar?");
            }

            Run("Montar pedido", doc =>
            {
                var r = Assembler.Montar(_app, linhas, Pedido.Text?.Trim(), _cfg, branco);
                Cdr.Select(doc, r.Criadas);
                SetStatus($"Montagem concluída: {r.Pecas} peça(s) em {r.Linhas} linha(s).");
                if (r.Avisos.Count > 0) Aviso("Montagem concluída com avisos:\n\n" + string.Join("\n", r.Avisos.Take(30)));
            });
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
            TamanhoDef.ItemsSource = null;
            TamanhoDef.ItemsSource = c.Tamanhos.ToList();
            if (string.IsNullOrEmpty(TamanhoDef.Text) && c.Tamanhos.Count > 0) TamanhoDef.Text = c.Tamanhos[0];
            TamanhosAuto.ItemsSource = c.Tamanhos.ToList();
            SelTamanho.ItemsSource = new[] { "" }.Concat(c.Tamanhos).ToList();

            CfgTamanhos.Text = string.Join(Environment.NewLine, c.Tamanhos);
            CfgRotulo.IsChecked = c.CriarRotulo;
            CfgColTipo.Text = c.ColunaTipo; CfgColCalcao.Text = c.ColunaTamanhoCalcao; PartesPorPeca.Text = c.PartesPorPeca.ToString();
            UsarCalcao.IsChecked = c.UsarTamanhoCalcao; UsarCamiseta.IsChecked = !c.UsarTamanhoCalcao;
            CfgColTam.Text = c.ColunaTamanho; CfgColNome.Text = c.ColunaNome; CfgColNum.Text = c.ColunaNumero;
            CfgColAd.Text = c.ColunaAdicional; CfgColQtd.Text = c.ColunaQuantidade;
            CfgCabecalho.IsChecked = c.PrimeiraLinhaCabecalho;
            CfgLargNome.Text = Fmt(c.LarguraNomePct); CfgLargNum.Text = Fmt(c.LarguraNumeroPct); CfgLargAd.Text = Fmt(c.LarguraAdicionalPct);
            CfgEspaco.Text = Fmt(c.Espacamento); CfgMargem.Text = Fmt(c.MargemTextoPct); CfgFonteInd.Text = Fmt(c.FonteIndicador);
            AplicarEscala.SelectedIndex = (int)c.EscalaArte;
            AplicarSubst.IsChecked = c.SubstituirAoAplicar;
            var tamClique = new List<string> { Engine.Tag.Matriz };
            tamClique.AddRange(c.Tamanhos);
            var tamAtual = TamClique.SelectedItem as string;
            TamClique.ItemsSource = tamClique;
            TamClique.SelectedItem = tamAtual != null && tamClique.Contains(tamAtual) ? tamAtual : Engine.Tag.Matriz;
            CfgNomeMaius.IsChecked = c.NomeMaiusculo; CfgAdMaius.IsChecked = c.AdicionalMaiusculo;
            CfgAjustarGab.IsChecked = c.AjustarAoGabarito; CfgIndicador.IsChecked = c.GerarIndicador;
            CfgAgrupar.IsChecked = c.AgruparLinha; CfgBranco.IsChecked = c.PreencherBrancoSemCor;
            CfgGerarId.IsChecked = c.GerarId; CfgIdPed.IsChecked = c.IdPedido; CfgIdSeq.IsChecked = c.IdSequencial;
            CfgIdTam.IsChecked = c.IdTamanho; CfgIdParte.IsChecked = c.IdParte; CfgIdApelido.IsChecked = c.IdApelido;
            CfgIdFonte.Text = c.FonteIdNome; CfgIdTam2.Text = Fmt(c.FonteId);
            CfgTecidos.Text = string.Join(Environment.NewLine, c.Tecidos.Select(t => $"{t.Nome};{Fmt(t.LarguraCm)}"));
            CfgPastaRel.Text = c.PastaRelatorio;
            CfgSupaUrl.Text = c.SupabaseUrl ?? ""; CfgSupaChave.Text = c.SupabaseChave ?? ""; CfgLinkForm.Text = c.LinkFormulario ?? "";

            ContornoMm.Text = Fmt(c.ContornoMm); ContornoCor.SelectedIndex = Math.Max(0, Math.Min(3, c.ContornoCor));

            EncTol.Text = Fmt(c.EncaixeTolerancia); EncSim.Text = Fmt(c.EncaixeSimilaridadeNos);
            EncEspelhar.IsChecked = c.EncaixeEspelhar; EncCopiar.IsChecked = c.EncaixeCopiarFaltantes; EncApagar.IsChecked = c.EncaixeApagarBase;

            SelecionarItem(ExpFormato, c.Formato); SelecionarItem(ExpCor, c.Cor); SelecionarItem(ExpPdfComp, c.CompressaoPdf);
            ExpDpi.Text = c.Dpi.ToString(); ExpPasta.Text = c.PastaExportacao; ExpRelatorio.IsChecked = c.GerarRelatorio;
            ExpTecido.ItemsSource = c.Tecidos.ToList();
            ExpTecido.SelectedIndex = Math.Min(Math.Max(0, c.TecidoSelecionado), c.Tecidos.Count - 1);
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
                c.MargemTextoPct = Math.Max(0, Math.Min(60, ParseDbl(CfgMargem.Text, c.MargemTextoPct)));
                c.FonteIndicador = (float)ParseDbl(CfgFonteInd.Text, c.FonteIndicador);
                c.EscalaArte = (ArtScale)Math.Max(0, AplicarEscala.SelectedIndex);
                c.SubstituirAoAplicar = AplicarSubst.IsChecked == true;
                c.NomeMaiusculo = CfgNomeMaius.IsChecked == true; c.AdicionalMaiusculo = CfgAdMaius.IsChecked == true;
                c.AjustarAoGabarito = CfgAjustarGab.IsChecked == true; c.GerarIndicador = CfgIndicador.IsChecked == true;
                c.AgruparLinha = CfgAgrupar.IsChecked == true; c.PreencherBrancoSemCor = CfgBranco.IsChecked == true;
                c.GerarId = CfgGerarId.IsChecked == true; c.IdPedido = CfgIdPed.IsChecked == true; c.IdSequencial = CfgIdSeq.IsChecked == true;
                c.IdTamanho = CfgIdTam.IsChecked == true; c.IdParte = CfgIdParte.IsChecked == true; c.IdApelido = CfgIdApelido.IsChecked == true;
                c.FonteIdNome = string.IsNullOrWhiteSpace(CfgIdFonte.Text) ? "Arial" : CfgIdFonte.Text.Trim();
                c.FonteId = (float)ParseDbl(CfgIdTam2.Text, c.FonteId);
                c.PastaRelatorio = CfgPastaRel.Text.Trim();
                c.SupabaseUrl = CfgSupaUrl.Text.Trim(); c.SupabaseChave = CfgSupaChave.Text.Trim(); c.LinkFormulario = CfgLinkForm.Text.Trim();
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
                AtualizarResumo();
                SetStatus("Configurações salvas.");
            }
            catch (Exception ex) { Aviso(ex.Message); }
        }

        private void RestaurarConfig_Click(object sender, RoutedEventArgs e)
        {
            if (!Pergunta("Restaurar todas as configurações para o padrão?")) return;
            var antigo = _cfg;
            _cfg = new Config
            {
                SupabaseUrl = antigo.SupabaseUrl, SupabaseChave = antigo.SupabaseChave, LinkFormulario = antigo.LinkFormulario
            };
            _shared = _cfg;
            _cfg.Save();
            CarregarConfigNaTela();
            AtualizarResumo();
            SetStatus("Configurações restauradas.");
        }

        // ================================================================ PEDIDOS ONLINE

        private void PedidosOnline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var w = new PedidosWindow(_cfg);
                try { w.Owner = System.Windows.Window.GetWindow(this); } catch { }
                if (w.ShowDialog() != true || w.Escolhido == null) return;
                var p = w.Escolhido;
                Cursor = System.Windows.Input.Cursors.Wait;
                var itens = new Cloud(_cfg).Itens(p.Id, UsandoCalcao);
                _linhas.Clear();
                foreach (var l in itens) _linhas.Add(l);
                Pedido.Text = p.Referencia;
                AtualizarTipos();
                Grade.Items.Refresh();
                AtualizarResumo();
                SetStatus($"Pedido \"{p.Referencia}\" carregado: {itens.Count} linha(s). Confira e clique em MONTAR PEDIDO.");
                if (itens.Count == 0) Aviso("Este pedido ainda não tem itens (o cliente não enviou).");
            }
            catch (UserException ex) { Aviso(ex.Message); }
            catch (Exception ex) { Aviso("Erro ao carregar o pedido: " + ex.Message); }
            finally { Cursor = null; }
        }

        // ================================================================ PAINEL ÚNICO (barra de ferramentas)

        /// <summary>Mostra só uma das abas, sem a barra de abas (usado pelos botões da barra de ferramentas).</summary>
        protected void MostrarSomente(int aba, string titulo, string subtitulo)
        {
            var tab = (TabItem)Tabs.Items[aba];
            var conteudo = tab.Content;
            tab.Content = null;
            Tabs.Visibility = Visibility.Collapsed;
            SingleHost.Content = conteudo;
            SingleHost.Visibility = Visibility.Visible;
            TituloTxt.Text = titulo;
            SubTxt.Text = subtitulo;
            if (aba == 0 || aba == 1) AtualizarDefinicoes();
        }

        private void Sobre_Click(object sender, RoutedEventArgs e)
        {
            var v = typeof(MainDocker).Assembly.GetName().Version;
            Info($"AHC Moldes {v.ToString(3)}\n\nMontagem de moldes graduados para sublimação no CorelDRAW.\n\nConfigurações: {Config.FilePath}");
        }
    }
}

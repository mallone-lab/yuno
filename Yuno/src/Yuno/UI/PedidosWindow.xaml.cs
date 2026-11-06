using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Yuno.Engine;

namespace Yuno.UI
{
    /// <summary>Lista os pedidos online; o escolhido volta em <see cref="Escolhido"/>.</summary>
    public partial class PedidosWindow : Window
    {
        private readonly Config _cfg;
        private string _novoLink;
        public PedidoOnline Escolhido { get; private set; }

        public PedidosWindow(Config cfg)
        {
            InitializeComponent();
            _cfg = cfg;
            Loaded += (s, e) => Carregar();
        }

        private void Erro(Exception ex)
            => MessageBox.Show(this, ex.Message, "Pedidos online", MessageBoxButton.OK, ex is UserException ? MessageBoxImage.Warning : MessageBoxImage.Error);

        private void Carregar()
        {
            try
            {
                Cursor = Cursors.Wait;
                var lista = new Cloud(_cfg).Listar(MostrarEncerrados.IsChecked == true);
                Lista.ItemsSource = lista;
                Info.Text = $"{lista.Count} pedido(s) · {lista.Count(p => p.Status == "enviado")} enviado(s) pelo cliente";
            }
            catch (Exception ex) { Info.Text = "Erro ao carregar."; Erro(ex); }
            finally { Cursor = null; }
        }

        private PedidoOnline Sel => Lista.SelectedItem as PedidoOnline;

        private void Atualizar_Click(object sender, RoutedEventArgs e) => Carregar();

        private void Novo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = new Cloud(_cfg).Criar(NovaRef.Text);
                _novoLink = Cloud.Link(_cfg, p.Id);
                LinkTxt.Text = _novoLink;
                LinkBox.Visibility = Visibility.Visible;
                try { Clipboard.SetText(_novoLink); } catch { }
                Info.Text = "Pedido criado. Link copiado: mande para o cliente.";
                NovaRef.Text = "";
                Carregar();
            }
            catch (Exception ex) { Erro(ex); }
        }

        private void CopiarNovo_Click(object sender, RoutedEventArgs e)
        {
            if (_novoLink == null) return;
            try { Clipboard.SetText(_novoLink); Info.Text = "Link copiado."; } catch { }
        }

        private void AbrirNovo_Click(object sender, RoutedEventArgs e)
        {
            if (_novoLink != null && _novoLink.StartsWith("http")) System.Diagnostics.Process.Start(_novoLink);
        }

        private void CopiarSel_Click(object sender, RoutedEventArgs e)
        {
            if (Sel == null) { Info.Text = "Selecione um pedido."; return; }
            try { Clipboard.SetText(Cloud.Link(_cfg, Sel.Id)); Info.Text = "Link do pedido copiado."; } catch { }
        }

        private void Encerrar_Click(object sender, RoutedEventArgs e)
        {
            if (Sel == null) { Info.Text = "Selecione um pedido."; return; }
            if (MessageBox.Show(this, $"Encerrar o pedido \"{Sel.Referencia}\"? O cliente não poderá mais alterar.", "Pedidos online",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { new Cloud(_cfg).MudarStatus(Sel.Id, "encerrado"); Carregar(); }
            catch (Exception ex) { Erro(ex); }
        }

        private void Usar_Click(object sender, RoutedEventArgs e)
        {
            if (Sel == null) { Info.Text = "Selecione um pedido."; return; }
            Escolhido = Sel;
            DialogResult = true;
        }

        private void Lista_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Sel != null) Usar_Click(sender, e);
        }
    }
}

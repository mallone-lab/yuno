using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace YunoSetup
{
    public partial class MainWindow : Window
    {
        private bool _desinstalar;
        private readonly string _target;
        private CorelInstall _corel;
        private bool _concluido;

        public MainWindow(bool desinstalar, string target = null)
        {
            InitializeComponent();
            _target = target;
            _desinstalar = desinstalar;
            VersaoTxt.Text = "versão " + Installer.Versao;
            if (desinstalar)
            {
                SerialPanel.Visibility = Visibility.Collapsed;
                Titulo.Text = "Remover o YUNO";
                Sub.Text = "Remove a barra e os painéis do YUNO do CorelDRAW. Suas configurações são mantidas.";
                BtnAcao.Content = "Remover";
            }
            Loaded += (s, e) => { if (desinstalar) AbrirAcao(true); };
        }

        private void EscolherInstalar_Click(object sender, RoutedEventArgs e) => AbrirAcao(false);
        private void EscolherRemover_Click(object sender, RoutedEventArgs e) => AbrirAcao(true);
        private void AbrirAcao(bool remover)
        {
            _desinstalar = remover;
            _concluido = false;
            SerialInput.Clear();
            Log.Text = "";
            SerialPanel.Visibility = remover ? Visibility.Collapsed : Visibility.Visible;
            Titulo.Text = remover ? "Remover YUNO" : "Instalar YUNO";
            Sub.Text = remover ? "Escolha de qual CorelDRAW deseja remover o YUNO. Suas configurações serão mantidas."
                : "Escolha o CorelDRAW e informe seu serial. O YUNO importa planilhas diretamente no CorelDRAW.";
            BtnAcao.Content = remover ? "Remover" : "Instalar";
            BtnFechar.Content = "Voltar";
            CorelLista.IsEnabled = true;
            WelcomePage.Visibility = Visibility.Collapsed;
            ActionPage.Visibility = Visibility.Visible;
            Detectar(_target);
        }
        private void Voltar_Click(object sender, RoutedEventArgs e)
        {
            SerialInput.Clear();
            ActionPage.Visibility = Visibility.Collapsed;
            WelcomePage.Visibility = Visibility.Visible;
        }

        private void Escrever(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                Log.Text += (Log.Text.Length > 0 ? "\n" : "") + "• " + msg;
                LogScroll.ScrollToEnd();
            });
        }

        private void Detectar(string target)
        {
            var lista = Installer.EncontrarCorel();
            CorelLista.ItemsSource = lista;
            CorelLista.SelectedItem = target == null
                ? (lista.FirstOrDefault(c => _desinstalar ? System.IO.Directory.Exists(c.AddonDir) : c.Suportado) ?? lista.FirstOrDefault())
                : lista.FirstOrDefault(c => string.Equals(c.Programs64, target, StringComparison.OrdinalIgnoreCase));
            AtualizarSelecao();
        }
        private void Corel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            => AtualizarSelecao();
        private void AtualizarSelecao()
        {
            _corel = CorelLista.SelectedItem as CorelInstall;
            bool available = _corel != null && (_desinstalar ? System.IO.Directory.Exists(_corel.AddonDir) : _corel.Suportado);
            BtnAcao.IsEnabled = available;
            CorelAviso.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
            CorelAviso.Text = _corel == null ? "Selecione uma instalação do CorelDRAW de 64 bits."
                : _desinstalar ? "O YUNO não está instalado nesta versão."
                : _corel.Major < 20 ? "O YUNO requer CorelDRAW versão 20 (2018) ou mais recente, de 64 bits."
                : "Este instalador está incompleto. Obtenha novamente o setup do YUNO.";
        }

        private async void Acao_Click(object sender, RoutedEventArgs e)
        {
            if (_concluido) { Close(); return; }
            if (_corel == null) return;
            var serial = _desinstalar ? null : SerialInput.Password;

            var avisoCorel = Installer.AvisoCorelAberto();
            if (avisoCorel != null)
            {
                MessageBox.Show(this, avisoCorel,
                    "YUNO", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BtnAcao.IsEnabled = false; BtnFechar.IsEnabled = false; CorelLista.IsEnabled = false;
            try
            {
                if (_desinstalar) await Task.Run(() => Installer.Desinstalar(_corel, Escrever));
                else await Task.Run(() => Installer.Instalar(_corel, Escrever, serial));
                SerialInput.Clear();
                _concluido = true;
                Titulo.Text = _desinstalar ? "YUNO removido" : "Pronto!";
                Sub.Text = _desinstalar ? "Você pode fechar esta janela."
                                        : "Abra o CorelDRAW: a barra YUNO está no topo. Também em Janela > YUNO.";
                BtnAcao.Content = "Concluir";
                if (!_desinstalar)
                {
                    try { WelcomeGuide.Open(_corel); }
                    catch { Escrever("Instalação concluída. Não foi possível abrir o guia automaticamente. Procure Bem-vindo.html na pasta do YUNO."); }
                }
            }
            catch (Exception ex)
            {
                Escrever("ERRO: " + ex.Message);
                MessageBox.Show(this, ex.Message, "YUNO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnAcao.IsEnabled = true; BtnFechar.IsEnabled = true; CorelLista.IsEnabled = !_concluido; }
        }

        private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
    }
}

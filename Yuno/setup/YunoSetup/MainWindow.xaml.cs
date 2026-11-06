using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace YunoSetup
{
    public partial class MainWindow : Window
    {
        private readonly bool _desinstalar;
        private CorelInstall _corel;
        private bool _concluido;

        public MainWindow(bool desinstalar)
        {
            InitializeComponent();
            _desinstalar = desinstalar;
            VersaoTxt.Text = "versão " + Installer.Versao;
            if (desinstalar)
            {
                Titulo.Text = "Remover o YUNO";
                Sub.Text = "Remove a barra e os painéis do YUNO do CorelDRAW. Suas configurações são mantidas.";
                BtnAcao.Content = "Remover";
            }
            Loaded += (s, e) => Detectar();
        }

        private void Escrever(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                Log.Text += (Log.Text.Length > 0 ? "\n" : "") + "• " + msg;
                LogScroll.ScrollToEnd();
            });
        }

        private void Detectar()
        {
            var lista = Installer.EncontrarCorel();
            _corel = lista.FirstOrDefault(c => c.Suportado) ?? lista.FirstOrDefault();
            if (_corel == null)
            {
                CorelTxt.Text = "Nenhum CorelDRAW encontrado";
                CorelAviso.Text = "Instale o CorelDRAW 2026 e rode este instalador de novo.";
                CorelAviso.Visibility = Visibility.Visible;
                BtnAcao.IsEnabled = false;
                return;
            }
            CorelTxt.Text = _corel.Nome;
            if (!_corel.Suportado)
            {
                CorelAviso.Text = "Esta versão ainda não é suportada. O YUNO funciona no CorelDRAW 2026.";
                CorelAviso.Visibility = Visibility.Visible;
                BtnAcao.IsEnabled = false;
            }
            var outros = lista.Where(c => c != _corel).Select(c => c.Nome).ToList();
            Escrever("Encontrado: " + _corel.Nome + (outros.Count > 0 ? " (também: " + string.Join(", ", outros) + ")" : ""));
        }

        private async void Acao_Click(object sender, RoutedEventArgs e)
        {
            if (_concluido) { Close(); return; }
            if (_corel == null) return;

            // CorelDRAW aberto: a barra só é ativada com ele fechado
            var abertos = Installer.CorelAberto(true);
            if (abertos.Count > 0)
            {
                var r = MessageBox.Show(this,
                    "O CorelDRAW está aberto.\n\nSalve seus arquivos. Deseja que o instalador feche o CorelDRAW agora?\n\n(Não: continue mesmo assim e a barra aparece depois que você fechar e abrir o Corel.)",
                    "YUNO", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return;
                if (r == MessageBoxResult.Yes) { Escrever("Fechando o CorelDRAW..."); await Task.Run(Installer.FecharCorel); }
            }

            BtnAcao.IsEnabled = false; BtnFechar.IsEnabled = false;
            try
            {
                if (_desinstalar) await Task.Run(() => Installer.Desinstalar(_corel, Escrever));
                else await Task.Run(() => Installer.Instalar(_corel, Escrever));
                _concluido = true;
                Titulo.Text = _desinstalar ? "YUNO removido" : "Pronto!";
                Sub.Text = _desinstalar ? "Você pode fechar esta janela."
                                        : "Abra o CorelDRAW: a barra YUNO está no topo. Também em Janela > YUNO.";
                BtnAcao.Content = "Concluir";
            }
            catch (Exception ex)
            {
                Escrever("ERRO: " + ex.Message);
                MessageBox.Show(this, ex.Message, "YUNO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnAcao.IsEnabled = true; BtnFechar.IsEnabled = true; }
        }

        private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
    }
}

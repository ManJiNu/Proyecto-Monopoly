using System.Windows;

namespace Monopoly.GUI
{
    public partial class LauncherWindow : Window
    {
        public LauncherWindow()
        {
            InitializeComponent();
        }

        private void BtnLocal_Click(object sender, RoutedEventArgs e)
        {
            new MainWindow().Show();
            Close();
        }

        private void BtnRed_Click(object sender, RoutedEventArgs e)
        {
            new VentanaRed().Show();
            Close();
        }
    }
}

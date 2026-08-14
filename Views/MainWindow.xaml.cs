using ProjectSky.ViewModels;
using ProjectSky.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProjectSky
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.ContextMenu.DataContext = button.DataContext;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void AbilityComposer_Click(object sender, RoutedEventArgs e)
        {
            var composer = new AbilityBattleComposer { Owner = this };
            composer.ShowDialog();
        }

        private void AreaRandomizer_Click(object sender, RoutedEventArgs e)
        {
            var randomizer = new AreaRandomizer { Owner = this };
            randomizer.ShowDialog();
        }
    }
}

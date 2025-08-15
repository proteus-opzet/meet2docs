using Avalonia.Controls;
using Meet2Docs.Gui.ViewModels;

namespace Meet2Docs.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
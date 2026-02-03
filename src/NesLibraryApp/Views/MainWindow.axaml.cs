using Avalonia.Controls;
using Avalonia.Input;
using NesLibraryApp.ViewModels;

namespace NesLibraryApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Close popup with Escape key
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm && vm.IsPopupVisible)
        {
            vm.GameDetailsViewModel?.BackCommand.Execute(null);
            e.Handled = true;
        }
    }
}

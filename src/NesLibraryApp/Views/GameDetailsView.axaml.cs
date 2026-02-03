using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NesLibraryApp.ViewModels;

namespace NesLibraryApp.Views;

public partial class GameDetailsView : UserControl
{
    public GameDetailsView()
    {
        InitializeComponent();
        Focusable = true;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape || e.Key == Key.Back)
        {
            if (DataContext is GameDetailsViewModel vm)
            {
                vm.BackCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using NesEmulator.Input;
using NesLibraryApp.ViewModels;

namespace NesLibraryApp.Views;

public partial class EmulatorView : UserControl
{
    public EmulatorView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is EmulatorViewModel vm)
        {
            var button = MapKeyToButton(e.Key);
            if (button != 0)
            {
                vm.HandleKeyDown(button);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.ExitCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is EmulatorViewModel vm)
        {
            var button = MapKeyToButton(e.Key);
            if (button != 0)
            {
                vm.HandleKeyUp(button);
                e.Handled = true;
            }
        }
    }

    private static Controller.Buttons MapKeyToButton(Key key)
    {
        return key switch
        {
            Key.Up => Controller.Buttons.Up,
            Key.Down => Controller.Buttons.Down,
            Key.Left => Controller.Buttons.Left,
            Key.Right => Controller.Buttons.Right,
            Key.Z => Controller.Buttons.A,
            Key.X => Controller.Buttons.B,
            Key.Enter or Key.Return => Controller.Buttons.Start,
            Key.LeftShift or Key.RightShift => Controller.Buttons.Select,
            _ => 0
        };
    }
}

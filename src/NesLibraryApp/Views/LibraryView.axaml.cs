using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using NesLibraryApp.ViewModels;

namespace NesLibraryApp.Views;

public partial class LibraryView : UserControl
{
    private const int ColumnsPerRow = 5; // Portrait cards at 1920px window

    public LibraryView()
    {
        InitializeComponent();
        Focusable = true;
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Auto-scan on load if directory exists
        if (DataContext is LibraryViewModel viewModel)
        {
            _ = viewModel.ScanRomsCommand.ExecuteAsync(null);
        }

        // Focus for keyboard input
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not LibraryViewModel vm) return;
        if (vm.Games.Count == 0) return;

        var currentIndex = vm.SelectedGame != null
            ? vm.Games.IndexOf(vm.SelectedGame)
            : -1;

        int newIndex = currentIndex;

        switch (e.Key)
        {
            case Key.Left:
                newIndex = Math.Max(0, currentIndex - 1);
                e.Handled = true;
                break;

            case Key.Right:
                newIndex = Math.Min(vm.Games.Count - 1, currentIndex + 1);
                e.Handled = true;
                break;

            case Key.Up:
                newIndex = Math.Max(0, currentIndex - ColumnsPerRow);
                e.Handled = true;
                break;

            case Key.Down:
                newIndex = Math.Min(vm.Games.Count - 1, currentIndex + ColumnsPerRow);
                e.Handled = true;
                break;

            case Key.Enter:
            case Key.Space:
                if (vm.SelectedGame != null)
                {
                    vm.OpenGameDetailsCommand.Execute(vm.SelectedGame);
                }
                e.Handled = true;
                break;

            case Key.Home:
                newIndex = 0;
                e.Handled = true;
                break;

            case Key.End:
                newIndex = vm.Games.Count - 1;
                e.Handled = true;
                break;
        }

        if (newIndex != currentIndex && newIndex >= 0 && newIndex < vm.Games.Count)
        {
            vm.SelectGameCommand.Execute(vm.Games[newIndex]);
        }
        else if (currentIndex == -1 && vm.Games.Count > 0)
        {
            // If no game selected, select first one
            vm.SelectGameCommand.Execute(vm.Games[0]);
        }
    }

    private void OnGameCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is GameItemViewModel game)
        {
            if (DataContext is LibraryViewModel vm)
            {
                vm.SelectGameCommand.Execute(game);
            }
        }
    }

    private void OnGameCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is GameItemViewModel game)
        {
            if (DataContext is LibraryViewModel vm)
            {
                vm.OpenGameDetailsCommand.Execute(game);
            }
        }
    }
}

public class BoolToStarConverter : IValueConverter
{
    public static readonly BoolToStarConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "★" : "☆";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FavoriteMenuConverter : IValueConverter
{
    public static readonly FavoriteMenuConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "Remove from Favorites" : "Add to Favorites";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Avalonia.Media.Brushes.LimeGreen : Avalonia.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToStatusConverter : IValueConverter
{
    public static readonly BoolToStatusConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "Connected" : "Not Connected";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToSelectionBrushConverter : IValueConverter
{
    public static readonly BoolToSelectionBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // NVIDIA green for selected, transparent for not selected
        return value is true
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#76B900"))
            : Avalonia.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

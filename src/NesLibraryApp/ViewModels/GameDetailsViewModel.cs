using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NesLibraryApp.ViewModels;

public partial class GameDetailsViewModel : ViewModelBase
{
    [ObservableProperty]
    private GameItemViewModel _game;

    private readonly Action _onBack;

    public GameDetailsViewModel(GameItemViewModel game, Action onBack)
    {
        _game = game;
        _onBack = onBack;
    }

    [RelayCommand]
    private void Back()
    {
        _onBack?.Invoke();
    }
}

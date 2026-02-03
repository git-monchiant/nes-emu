using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NesLibraryApp.ViewModels;

public partial class GameDetailsViewModel : ViewModelBase
{
    [ObservableProperty]
    private GameItemViewModel _game;

    private readonly Action _onBack;
    private readonly Action<GameItemViewModel>? _onPlay;

    [ObservableProperty]
    private bool _canPlay;

    public GameDetailsViewModel(GameItemViewModel game, Action onBack, Action<GameItemViewModel>? onPlay = null)
    {
        _game = game;
        _onBack = onBack;
        _onPlay = onPlay;

        // Enable play if file exists and has a real path
        CanPlay = !string.IsNullOrEmpty(game.FilePath) && File.Exists(game.FilePath);
    }

    [RelayCommand]
    private void Back()
    {
        _onBack?.Invoke();
    }

    [RelayCommand]
    private void Play()
    {
        if (CanPlay)
        {
            _onPlay?.Invoke(Game);
        }
    }
}

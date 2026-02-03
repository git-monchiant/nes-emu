using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NesLibraryApp.Services;

namespace NesLibraryApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly InputService _inputService;

    [ObservableProperty]
    private LibraryViewModel _libraryViewModel;

    [ObservableProperty]
    private GameDetailsViewModel? _gameDetailsViewModel;

    [ObservableProperty]
    private bool _isPopupVisible;

    [ObservableProperty]
    private bool _isGamepadConnected;

    public MainWindowViewModel()
    {
        _libraryViewModel = new LibraryViewModel();
        _libraryViewModel.NavigateToGameDetails = ShowGameDetailsPopup;

        // Initialize gamepad input
        _inputService = new InputService();
        _inputService.ButtonPressed += OnGamepadButtonPressed;
    }

    private void OnGamepadButtonPressed(GamepadButton button)
    {
        // Dispatch to UI thread
        Dispatcher.UIThread.Post(() =>
        {
            IsGamepadConnected = _inputService.IsGamepadConnected;

            if (IsPopupVisible && GameDetailsViewModel != null)
            {
                HandlePopupInput(GameDetailsViewModel, button);
            }
            else
            {
                HandleLibraryInput(LibraryViewModel, button);
            }
        });
    }

    private void HandleLibraryInput(LibraryViewModel vm, GamepadButton button)
    {
        if (vm.Games.Count == 0) return;

        var currentIndex = vm.SelectedGame != null
            ? vm.Games.IndexOf(vm.SelectedGame)
            : -1;

        const int columnsPerRow = 5; // Portrait cards
        int newIndex = currentIndex;

        switch (button)
        {
            case GamepadButton.DPadLeft:
                newIndex = Math.Max(0, currentIndex - 1);
                break;

            case GamepadButton.DPadRight:
                newIndex = Math.Min(vm.Games.Count - 1, currentIndex + 1);
                break;

            case GamepadButton.DPadUp:
                newIndex = Math.Max(0, currentIndex - columnsPerRow);
                break;

            case GamepadButton.DPadDown:
                newIndex = Math.Min(vm.Games.Count - 1, currentIndex + columnsPerRow);
                break;

            case GamepadButton.A:
                if (vm.SelectedGame != null)
                {
                    vm.OpenGameDetailsCommand.Execute(vm.SelectedGame);
                }
                return;

            case GamepadButton.Y:
                if (vm.SelectedGame != null)
                {
                    vm.SelectedGame.ToggleFavoriteCommand.Execute(null);
                }
                return;
        }

        if (newIndex != currentIndex && newIndex >= 0 && newIndex < vm.Games.Count)
        {
            vm.SelectGameCommand.Execute(vm.Games[newIndex]);
        }
        else if (currentIndex == -1 && vm.Games.Count > 0)
        {
            vm.SelectGameCommand.Execute(vm.Games[0]);
        }
    }

    private void HandlePopupInput(GameDetailsViewModel vm, GamepadButton button)
    {
        switch (button)
        {
            case GamepadButton.B:
            case GamepadButton.Back:
                ClosePopup();
                break;

            case GamepadButton.Y:
                vm.Game.ToggleFavoriteCommand.Execute(null);
                break;
        }
    }

    private void ShowGameDetailsPopup(GameItemViewModel game)
    {
        GameDetailsViewModel = new GameDetailsViewModel(game, ClosePopup);
        IsPopupVisible = true;
    }

    private void ClosePopup()
    {
        IsPopupVisible = false;
        GameDetailsViewModel = null;
    }

    public void Dispose()
    {
        _inputService.ButtonPressed -= OnGamepadButtonPressed;
        _inputService.Dispose();
    }
}

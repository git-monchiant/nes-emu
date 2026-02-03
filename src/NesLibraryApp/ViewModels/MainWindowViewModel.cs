using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NesLibraryApp.Services;

namespace NesLibraryApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly InputService _inputService;
    private readonly MetadataStore _metadataStore;
    private EmulatorService? _emulatorService;

    [ObservableProperty]
    private LibraryViewModel _libraryViewModel;

    [ObservableProperty]
    private GameDetailsViewModel? _gameDetailsViewModel;

    [ObservableProperty]
    private EmulatorViewModel? _emulatorViewModel;

    [ObservableProperty]
    private bool _isPopupVisible;

    [ObservableProperty]
    private bool _isEmulatorVisible;

    [ObservableProperty]
    private bool _isGamepadConnected;

    public MainWindowViewModel()
    {
        _metadataStore = new MetadataStore();
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

            if (IsEmulatorVisible && EmulatorViewModel != null)
            {
                HandleEmulatorInput(button);
            }
            else if (IsPopupVisible && GameDetailsViewModel != null)
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

            case GamepadButton.A:
            case GamepadButton.Start:
                // Play the game
                LaunchGame(vm.Game);
                break;

            case GamepadButton.Y:
                vm.Game.ToggleFavoriteCommand.Execute(null);
                break;
        }
    }

    private void HandleEmulatorInput(GamepadButton button)
    {
        if (EmulatorViewModel == null || _emulatorService == null) return;

        // Map gamepad to NES controller
        var nesButton = button switch
        {
            GamepadButton.DPadUp => NesEmulator.Input.Controller.Buttons.Up,
            GamepadButton.DPadDown => NesEmulator.Input.Controller.Buttons.Down,
            GamepadButton.DPadLeft => NesEmulator.Input.Controller.Buttons.Left,
            GamepadButton.DPadRight => NesEmulator.Input.Controller.Buttons.Right,
            GamepadButton.A => NesEmulator.Input.Controller.Buttons.A,
            GamepadButton.B or GamepadButton.X => NesEmulator.Input.Controller.Buttons.B,
            GamepadButton.Start => NesEmulator.Input.Controller.Buttons.Start,
            GamepadButton.Back => NesEmulator.Input.Controller.Buttons.Select,
            _ => (NesEmulator.Input.Controller.Buttons)0
        };

        if (nesButton != 0)
        {
            _emulatorService.SetButton(nesButton, true);
            // Note: In a real implementation, we'd need to track button release too
        }

        // Handle Back button for exit
        if (button == GamepadButton.Back && _inputService.IsButtonHeld(GamepadButton.Start))
        {
            ExitEmulator();
        }
    }

    private void ShowGameDetailsPopup(GameItemViewModel game)
    {
        GameDetailsViewModel = new GameDetailsViewModel(game, ClosePopup, LaunchGame);
        IsPopupVisible = true;
    }

    private void ClosePopup()
    {
        IsPopupVisible = false;
        GameDetailsViewModel = null;
    }

    private void LaunchGame(GameItemViewModel game)
    {
        if (string.IsNullOrEmpty(game.FilePath))
            return;

        // Close popup first
        ClosePopup();

        // Create emulator service
        _emulatorService?.Dispose();
        _emulatorService = new EmulatorService(_metadataStore);

        // Load the game
        if (!_emulatorService.LoadGame(game.FilePath, game.Id))
        {
            _emulatorService.Dispose();
            _emulatorService = null;
            return;
        }

        // Create emulator view model
        EmulatorViewModel = new EmulatorViewModel(_emulatorService, game.DisplayName, ExitEmulator);

        // Show emulator
        IsEmulatorVisible = true;

        // Start emulation
        _emulatorService.Start();
    }

    private void ExitEmulator()
    {
        IsEmulatorVisible = false;

        _emulatorService?.Stop();
        _emulatorService?.Dispose();
        _emulatorService = null;

        EmulatorViewModel?.Dispose();
        EmulatorViewModel = null;
    }

    public void Dispose()
    {
        _inputService.ButtonPressed -= OnGamepadButtonPressed;
        _inputService.Dispose();
        _emulatorService?.Dispose();
        _metadataStore.Dispose();
    }
}

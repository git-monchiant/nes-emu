using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NesEmulator.Input;
using NesLibraryApp.Services;

namespace NesLibraryApp.ViewModels;

public partial class EmulatorViewModel : ViewModelBase, IDisposable
{
    private readonly EmulatorService _emulatorService;
    private readonly Action _onExit;

    [ObservableProperty]
    private WriteableBitmap? _currentFrame;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _showControls = true;

    [ObservableProperty]
    private string _gameName = string.Empty;

    public string PauseResumeText => IsPaused ? "Resume" : "Pause";

    public EmulatorViewModel(EmulatorService emulatorService, string gameName, Action onExit)
    {
        _emulatorService = emulatorService;
        _gameName = gameName;
        _onExit = onExit;

        _emulatorService.FrameRendered += OnFrameRendered;
    }

    private void OnFrameRendered(WriteableBitmap bitmap)
    {
        CurrentFrame = bitmap;
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (IsPaused)
        {
            _emulatorService.Resume();
            IsPaused = false;
        }
        else
        {
            _emulatorService.Pause();
            IsPaused = true;
        }
        OnPropertyChanged(nameof(PauseResumeText));
    }

    [RelayCommand]
    private void Reset()
    {
        _emulatorService.Reset();
    }

    [RelayCommand]
    private void Exit()
    {
        _emulatorService.Stop();
        _onExit?.Invoke();
    }

    public void HandleKeyDown(Controller.Buttons button)
    {
        _emulatorService.SetButton(button, true);
    }

    public void HandleKeyUp(Controller.Buttons button)
    {
        _emulatorService.SetButton(button, false);
    }

    public void Dispose()
    {
        _emulatorService.FrameRendered -= OnFrameRendered;
    }
}

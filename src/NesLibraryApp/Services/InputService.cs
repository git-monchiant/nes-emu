using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NesLibraryApp.Services;

public enum GamepadButton
{
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    A,      // Xbox A / PlayStation X
    B,      // Xbox B / PlayStation Circle
    X,      // Xbox X / PlayStation Square
    Y,      // Xbox Y / PlayStation Triangle
    Start,
    Back,
    LeftShoulder,
    RightShoulder
}

public class GamepadState
{
    public bool IsConnected { get; set; }
    public Dictionary<GamepadButton, bool> Buttons { get; } = new();
    public float LeftStickX { get; set; }
    public float LeftStickY { get; set; }

    public GamepadState()
    {
        foreach (GamepadButton button in Enum.GetValues<GamepadButton>())
        {
            Buttons[button] = false;
        }
    }
}

public class InputService : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly GamepadState _currentState = new();
    private readonly GamepadState _previousState = new();
    private Task? _pollingTask;
    private IntPtr _sdlGameController = IntPtr.Zero;
    private bool _sdlInitialized;

    public event Action<GamepadButton>? ButtonPressed;
    public event Action<GamepadButton>? ButtonReleased;
#pragma warning disable CS0067 // Event is never used (reserved for analog stick navigation)
    public event Action<float, float>? LeftStickMoved;
#pragma warning restore CS0067

    public bool IsGamepadConnected => _currentState.IsConnected;

    public InputService()
    {
        InitializeSDL();
    }

    private void InitializeSDL()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Try to load SDL2 from common macOS locations
                if (SDL_Init(SDL_INIT_GAMECONTROLLER) == 0)
                {
                    _sdlInitialized = true;
                    StartPolling();
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (SDL_Init(SDL_INIT_GAMECONTROLLER) == 0)
                {
                    _sdlInitialized = true;
                    StartPolling();
                }
            }
        }
        catch
        {
            // SDL2 not available, gamepad support disabled
            _sdlInitialized = false;
        }
    }

    private void StartPolling()
    {
        _pollingTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    PollGamepad();
                    await Task.Delay(16, _cts.Token); // ~60fps polling
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);
    }

    private void PollGamepad()
    {
        if (!_sdlInitialized) return;

        try
        {
            SDL_GameControllerUpdate();

            // Check for controller connection
            if (_sdlGameController == IntPtr.Zero)
            {
                for (int i = 0; i < SDL_NumJoysticks(); i++)
                {
                    if (SDL_IsGameController(i))
                    {
                        _sdlGameController = SDL_GameControllerOpen(i);
                        if (_sdlGameController != IntPtr.Zero)
                        {
                            _currentState.IsConnected = true;
                            break;
                        }
                    }
                }
            }

            if (_sdlGameController == IntPtr.Zero)
            {
                _currentState.IsConnected = false;
                return;
            }

            // Check if still connected
            if (!SDL_GameControllerGetAttached(_sdlGameController))
            {
                SDL_GameControllerClose(_sdlGameController);
                _sdlGameController = IntPtr.Zero;
                _currentState.IsConnected = false;
                return;
            }

            // Store previous state
            foreach (var button in _currentState.Buttons.Keys)
            {
                _previousState.Buttons[button] = _currentState.Buttons[button];
            }

            // Poll buttons
            _currentState.Buttons[GamepadButton.DPadUp] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_DPAD_UP) == 1;
            _currentState.Buttons[GamepadButton.DPadDown] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_DPAD_DOWN) == 1;
            _currentState.Buttons[GamepadButton.DPadLeft] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_DPAD_LEFT) == 1;
            _currentState.Buttons[GamepadButton.DPadRight] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_DPAD_RIGHT) == 1;
            _currentState.Buttons[GamepadButton.A] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_A) == 1;
            _currentState.Buttons[GamepadButton.B] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_B) == 1;
            _currentState.Buttons[GamepadButton.X] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_X) == 1;
            _currentState.Buttons[GamepadButton.Y] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_Y) == 1;
            _currentState.Buttons[GamepadButton.Start] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_START) == 1;
            _currentState.Buttons[GamepadButton.Back] = SDL_GameControllerGetButton(_sdlGameController, SDL_CONTROLLER_BUTTON_BACK) == 1;

            // Poll left stick
            short lx = SDL_GameControllerGetAxis(_sdlGameController, SDL_CONTROLLER_AXIS_LEFTX);
            short ly = SDL_GameControllerGetAxis(_sdlGameController, SDL_CONTROLLER_AXIS_LEFTY);
            _currentState.LeftStickX = lx / 32767f;
            _currentState.LeftStickY = ly / 32767f;

            // Fire events for button state changes
            foreach (var button in _currentState.Buttons.Keys)
            {
                bool current = _currentState.Buttons[button];
                bool previous = _previousState.Buttons[button];

                if (current && !previous)
                {
                    ButtonPressed?.Invoke(button);
                }
                else if (!current && previous)
                {
                    ButtonReleased?.Invoke(button);
                }
            }
        }
        catch
        {
            // Ignore errors during polling
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _pollingTask?.Wait(1000);
        _cts.Dispose();

        if (_sdlGameController != IntPtr.Zero && _sdlInitialized)
        {
            SDL_GameControllerClose(_sdlGameController);
        }

        if (_sdlInitialized)
        {
            SDL_Quit();
        }
    }

    #region SDL2 P/Invoke

    private const int SDL_INIT_GAMECONTROLLER = 0x00002000;

    private const int SDL_CONTROLLER_BUTTON_A = 0;
    private const int SDL_CONTROLLER_BUTTON_B = 1;
    private const int SDL_CONTROLLER_BUTTON_X = 2;
    private const int SDL_CONTROLLER_BUTTON_Y = 3;
    private const int SDL_CONTROLLER_BUTTON_BACK = 4;
    private const int SDL_CONTROLLER_BUTTON_START = 6;
    private const int SDL_CONTROLLER_BUTTON_DPAD_UP = 11;
    private const int SDL_CONTROLLER_BUTTON_DPAD_DOWN = 12;
    private const int SDL_CONTROLLER_BUTTON_DPAD_LEFT = 13;
    private const int SDL_CONTROLLER_BUTTON_DPAD_RIGHT = 14;

    private const int SDL_CONTROLLER_AXIS_LEFTX = 0;
    private const int SDL_CONTROLLER_AXIS_LEFTY = 1;

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_Init(int flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_Quit();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_NumJoysticks();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SDL_IsGameController(int joystick_index);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GameControllerOpen(int joystick_index);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_GameControllerClose(IntPtr gamecontroller);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SDL_GameControllerGetAttached(IntPtr gamecontroller);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_GameControllerUpdate();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern byte SDL_GameControllerGetButton(IntPtr gamecontroller, int button);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern short SDL_GameControllerGetAxis(IntPtr gamecontroller, int axis);

    #endregion
}

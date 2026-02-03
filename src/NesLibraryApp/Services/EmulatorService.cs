using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NesEmulator.Core;
using NesEmulator.Input;

namespace NesLibraryApp.Services;

public class EmulatorService : IDisposable
{
    private readonly Nes _nes;
    private readonly MetadataStore _metadataStore;

    private Thread? _emulationThread;
    private volatile bool _running;
    private volatile bool _paused;

    private readonly Stopwatch _frameTimer = new();
    private const double TargetFrameTime = 1000.0 / 60.0988; // NTSC ~60.0988 Hz

    private WriteableBitmap? _frameBitmap;
    private readonly object _frameLock = new();

    public event Action<WriteableBitmap>? FrameRendered;
    public event Action? EmulationStarted;
    public event Action? EmulationStopped;

    public bool IsRunning => _running;
    public bool IsPaused => _paused;

    // Current button state
    private Controller.Buttons _controllerState;

    public EmulatorService(MetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
        _nes = new Nes();

        _nes.FrameReady += OnNesFrameReady;
        _nes.AudioSamplesReady += OnAudioSamplesReady;
    }

    public bool LoadGame(string filePath, int gameId)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return false;

        bool loaded = _nes.LoadRom(filePath);
        if (loaded && gameId > 0)
        {
            _metadataStore.UpdateLastPlayed(gameId, DateTime.Now);
        }
        return loaded;
    }

    public void Start()
    {
        if (_running) return;

        _running = true;
        _paused = false;

        _emulationThread = new Thread(EmulationLoop)
        {
            Name = "NES Emulation",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _emulationThread.Start();

        EmulationStarted?.Invoke();
    }

    public void Stop()
    {
        _running = false;
        _emulationThread?.Join(1000);
        _emulationThread = null;

        EmulationStopped?.Invoke();
    }

    public void Pause() => _paused = true;
    public void Resume() => _paused = false;

    public void Reset()
    {
        _nes.Reset();
    }

    public void SetButton(Controller.Buttons button, bool pressed)
    {
        if (pressed)
            _controllerState |= button;
        else
            _controllerState &= ~button;

        _nes.SetControllerState(0, _controllerState);
    }

    private void EmulationLoop()
    {
        _frameTimer.Start();
        double nextFrameTime = _frameTimer.Elapsed.TotalMilliseconds;
        int frameCount = 0;

        while (_running)
        {
            if (_paused)
            {
                Thread.Sleep(16);
                nextFrameTime = _frameTimer.Elapsed.TotalMilliseconds;
                continue;
            }

            // Run one frame
            _nes.RunUntilFrame();

            // Diagnostic output every 60 frames (1 second)
            frameCount++;
            if (frameCount <= 5 || frameCount % 60 == 0)
            {
                Console.WriteLine($"[EMU] Frame {_nes.PpuFrameCount}, CPU PC=${_nes.CpuPC:X4}, PPUCTRL=${_nes.PpuCtrl:X2}, PPUMASK=${_nes.PpuMask:X2}, Cycles={_nes.TotalCycles}");
            }

            // Frame limiting
            nextFrameTime += TargetFrameTime;
            double elapsed = _frameTimer.Elapsed.TotalMilliseconds;

            if (elapsed < nextFrameTime)
            {
                int sleepMs = (int)(nextFrameTime - elapsed);
                if (sleepMs > 1)
                    Thread.Sleep(sleepMs - 1);

                // Spin for precision
                while (_frameTimer.Elapsed.TotalMilliseconds < nextFrameTime)
                    Thread.SpinWait(100);
            }
            else if (elapsed > nextFrameTime + TargetFrameTime * 5)
            {
                // Too far behind, reset timing
                nextFrameTime = elapsed;
            }
        }
    }

    private static int _frameReadyCount = 0;
    private void OnNesFrameReady(uint[] frameBuffer)
    {
        _frameReadyCount++;
        // Check first few pixels to see what colors are being rendered
        if (_frameReadyCount <= 5)
        {
            Console.WriteLine($"[PPU] FrameReady #{_frameReadyCount}, pixel[0]=${frameBuffer[0]:X8}, pixel[1000]=${frameBuffer[1000]:X8}");
        }

        lock (_frameLock)
        {
            _frameBitmap ??= new WriteableBitmap(
                new PixelSize(256, 240),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Opaque);

            using var fb = _frameBitmap.Lock();
            unsafe
            {
                fixed (uint* src = frameBuffer)
                {
                    Buffer.MemoryCopy(src, (void*)fb.Address,
                        frameBuffer.Length * 4, frameBuffer.Length * 4);
                }
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            lock (_frameLock)
            {
                if (_frameBitmap != null)
                    FrameRendered?.Invoke(_frameBitmap);
            }
        });
    }

    private void OnAudioSamplesReady(float[] samples, int count)
    {
        // TODO: Implement audio output using SDL2 or NAudio
        // For now, audio is disabled
    }

    public void Dispose()
    {
        Stop();
        _nes.Dispose();
    }
}

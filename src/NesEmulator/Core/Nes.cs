using System;
using System.IO;
using NesEmulator.Apu;
using NesEmulator.Cartridge;
using NesEmulator.Cpu;
using NesEmulator.Input;
using NesEmulator.Memory;
using NesEmulator.Ppu;

namespace NesEmulator.Core;

public class Nes : IDisposable
{
    private Cpu6502? _cpu;
    private Ppu2C02? _ppu;
    private Apu2A03? _apu;
    private MemoryBus? _bus;
    private Cartridge.Cartridge? _cartridge;
    private Controller? _controller1;
    private Controller? _controller2;

    private bool _running;
    private long _totalCycles;

    public event Action<uint[]>? FrameReady;
    public event Action<float[], int>? AudioSamplesReady;

    public bool IsRunning => _running;
    public long TotalCycles => _totalCycles;
    public int MapperNumber => _cartridge?.Header?.MapperNumber ?? -1;

    // Diagnostic properties
    public ushort CpuPC => _cpu?.Registers.PC ?? 0;
    public byte PpuCtrl => _ppu?.ReadPpuCtrl() ?? 0;
    public byte PpuMask => _ppu?.ReadPpuMask() ?? 0;
    public long PpuFrameCount => _ppu?.FrameCount ?? 0;

    public Nes()
    {
        _controller1 = new Controller();
        _controller2 = new Controller();
    }

    public bool LoadRom(string path)
    {
        if (!File.Exists(path))
            return false;

        var data = File.ReadAllBytes(path);
        return LoadRom(data);
    }

    public bool LoadRom(byte[] data)
    {
        _cartridge = new Cartridge.Cartridge(data);
        if (!_cartridge.IsValid)
            return false;

        _ppu = new Ppu2C02(_cartridge);
        _apu = new Apu2A03();
        _bus = new MemoryBus();

        _bus.Connect(_ppu, _apu, _cartridge, _controller1!, _controller2!);

        _cpu = new Cpu6502(_bus);

        // Wire up events
        _ppu.FrameReady += OnFrameReady;
        _ppu.TriggerNmi = () => _cpu.TriggerNmi();

        _apu.SamplesReady += OnAudioSamplesReady;
        _apu.TriggerIrq = () => _cpu.TriggerIrq();
        _apu.SetMemoryReader(addr => _bus.Read(addr));

        Reset();
        return true;
    }

    public void Reset()
    {
        _cpu?.Reset();
        _ppu?.Reset();
        _totalCycles = 0;
        _running = true;

        // Diagnostic: print reset vector and starting PC
        if (_cpu != null)
        {
            Console.WriteLine($"[NES] Reset - PC=${_cpu.Registers.PC:X4}, Mapper={MapperNumber}");
        }
    }

    public void SetControllerState(int controller, Controller.Buttons buttons)
    {
        if (controller == 0)
            _controller1?.SetButtonState(buttons);
        else
            _controller2?.SetButtonState(buttons);
    }

    public void SetButton(int controller, Controller.Buttons button, bool pressed)
    {
        if (controller == 0)
            _controller1?.SetButton(button, pressed);
        else
            _controller2?.SetButton(button, pressed);
    }

    /// <summary>
    /// Run one CPU instruction and associated PPU/APU cycles
    /// </summary>
    public void Step()
    {
        if (_cpu == null || _ppu == null || _apu == null || _bus == null)
            return;

        int cpuCycles = _cpu.Step();

        // Handle DMA cycles
        int dmaCycles = _bus.DmaCycles;
        if (dmaCycles > 0)
        {
            cpuCycles += dmaCycles;
            _bus.ClearDmaCycles();
        }

        // PPU runs at 3x CPU clock
        for (int i = 0; i < cpuCycles * 3; i++)
            _ppu.Clock();

        // APU runs at CPU clock
        for (int i = 0; i < cpuCycles; i++)
            _apu.Clock();

        // Handle mapper IRQ (e.g., MMC3)
        if (_cartridge?.IrqPending == true)
        {
            _cpu.TriggerIrq();
            _cartridge.AcknowledgeIrq();
        }

        _totalCycles += cpuCycles;
    }

    /// <summary>
    /// Run approximately one frame (~29780 CPU cycles for NTSC)
    /// </summary>
    public void RunFrame()
    {
        const int cyclesPerFrame = 29780;
        long targetCycles = _totalCycles + cyclesPerFrame;

        while (_totalCycles < targetCycles && _running)
        {
            Step();
        }
    }

    /// <summary>
    /// Run until the next frame is ready
    /// </summary>
    public void RunUntilFrame()
    {
        if (_ppu == null) return;

        long startFrame = _ppu.FrameCount;
        while (_ppu.FrameCount == startFrame && _running)
        {
            Step();
        }
    }

    public void Stop()
    {
        _running = false;
    }

    private void OnFrameReady(uint[] frameBuffer)
    {
        FrameReady?.Invoke(frameBuffer);
    }

    private void OnAudioSamplesReady(float[] samples, int count)
    {
        AudioSamplesReady?.Invoke(samples, count);
    }

    public void Dispose()
    {
        Stop();
        if (_ppu != null)
            _ppu.FrameReady -= OnFrameReady;
        if (_apu != null)
            _apu.SamplesReady -= OnAudioSamplesReady;
    }
}

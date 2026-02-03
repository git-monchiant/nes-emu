using System;

namespace NesEmulator.Apu;

public class Apu2A03
{
    public const int SampleRate = 44100;
    private const double CpuClockRate = 1789773.0;
    private const int SamplesPerBuffer = 735; // ~1/60 second

    private readonly PulseChannel _pulse1 = new(true);
    private readonly PulseChannel _pulse2 = new(false);
    private readonly TriangleChannel _triangle = new();
    private readonly NoiseChannel _noise = new();
    private readonly DmcChannel _dmc;

    private int _frameCounter;
    private int _frameCounterMode; // 0: 4-step, 1: 5-step
    private bool _irqInhibit;
    private bool _frameIrqPending;

    private readonly float[] _outputBuffer = new float[SamplesPerBuffer * 2];
    private int _outputIndex;
    private double _sampleAccumulator;
    private readonly double _samplePeriod;

    public event Action<float[], int>? SamplesReady;
    public Action? TriggerIrq;

    public Apu2A03()
    {
        _dmc = new DmcChannel(ReadMemory);
        _samplePeriod = CpuClockRate / SampleRate;
    }

    // For DMC memory reads
    private Func<ushort, byte>? _memoryRead;
    public void SetMemoryReader(Func<ushort, byte> reader) => _memoryRead = reader;
    private byte ReadMemory(ushort addr) => _memoryRead?.Invoke(addr) ?? 0;

    public void Clock()
    {
        // Clock triangle at CPU rate
        _triangle.ClockTimer();

        // Clock others at half CPU rate (APU rate)
        _frameCounter++;
        if (_frameCounter % 2 == 0)
        {
            _pulse1.ClockTimer();
            _pulse2.ClockTimer();
            _noise.ClockTimer();
            _dmc.ClockTimer();
        }

        // Frame counter
        ClockFrameCounter();

        // Sample output
        _sampleAccumulator++;
        if (_sampleAccumulator >= _samplePeriod)
        {
            _sampleAccumulator -= _samplePeriod;
            OutputSample();
        }
    }

    private void ClockFrameCounter()
    {
        // Frame counter runs at ~240 Hz (CPU / 7457)
        int step = _frameCounter;

        if (_frameCounterMode == 0)
        {
            // 4-step sequence
            switch (step)
            {
                case 3729:
                    QuarterFrame();
                    break;
                case 7457:
                    QuarterFrame();
                    HalfFrame();
                    break;
                case 11186:
                    QuarterFrame();
                    break;
                case 14915:
                    QuarterFrame();
                    HalfFrame();
                    if (!_irqInhibit)
                    {
                        _frameIrqPending = true;
                        TriggerIrq?.Invoke();
                    }
                    _frameCounter = 0;
                    break;
            }
        }
        else
        {
            // 5-step sequence
            switch (step)
            {
                case 3729:
                    QuarterFrame();
                    break;
                case 7457:
                    QuarterFrame();
                    HalfFrame();
                    break;
                case 11186:
                    QuarterFrame();
                    break;
                case 18641:
                    QuarterFrame();
                    HalfFrame();
                    _frameCounter = 0;
                    break;
            }
        }
    }

    private void QuarterFrame()
    {
        _pulse1.ClockEnvelope();
        _pulse2.ClockEnvelope();
        _triangle.ClockLinearCounter();
        _noise.ClockEnvelope();
    }

    private void HalfFrame()
    {
        _pulse1.ClockLengthCounter();
        _pulse1.ClockSweep();
        _pulse2.ClockLengthCounter();
        _pulse2.ClockSweep();
        _triangle.ClockLengthCounter();
        _noise.ClockLengthCounter();
    }

    private void OutputSample()
    {
        float pulse1 = _pulse1.GetOutput();
        float pulse2 = _pulse2.GetOutput();
        float triangle = _triangle.GetOutput();
        float noise = _noise.GetOutput();
        float dmc = _dmc.GetOutput();

        float sample = Mix(pulse1, pulse2, triangle, noise, dmc);
        _outputBuffer[_outputIndex++] = sample;

        if (_outputIndex >= SamplesPerBuffer)
        {
            SamplesReady?.Invoke(_outputBuffer, _outputIndex);
            _outputIndex = 0;
        }
    }

    private static float Mix(float pulse1, float pulse2, float triangle, float noise, float dmc)
    {
        // Non-linear mixing as per NES hardware
        float pulseOut = 0;
        float pulseSum = pulse1 + pulse2;
        if (pulseSum > 0)
            pulseOut = 95.88f / (8128.0f / pulseSum + 100);

        float tndOut = 0;
        float tndSum = triangle / 8227.0f + noise / 12241.0f + dmc / 22638.0f;
        if (tndSum > 0)
            tndOut = 159.79f / (1 / tndSum + 100);

        return pulseOut + tndOut;
    }

    public byte ReadStatus()
    {
        byte status = 0;
        if (_pulse1.LengthCounter > 0) status |= 0x01;
        if (_pulse2.LengthCounter > 0) status |= 0x02;
        if (_triangle.LengthCounter > 0) status |= 0x04;
        if (_noise.LengthCounter > 0) status |= 0x08;
        if (_dmc.BytesRemaining > 0) status |= 0x10;
        if (_frameIrqPending) status |= 0x40;
        if (_dmc.IrqPending) status |= 0x80;

        _frameIrqPending = false;
        return status;
    }

    public void WriteRegister(int addr, byte value)
    {
        switch (addr)
        {
            // Pulse 1
            case 0x4000: _pulse1.WriteControl(value); break;
            case 0x4001: _pulse1.WriteSweep(value); break;
            case 0x4002: _pulse1.WriteTimerLo(value); break;
            case 0x4003: _pulse1.WriteTimerHi(value); break;

            // Pulse 2
            case 0x4004: _pulse2.WriteControl(value); break;
            case 0x4005: _pulse2.WriteSweep(value); break;
            case 0x4006: _pulse2.WriteTimerLo(value); break;
            case 0x4007: _pulse2.WriteTimerHi(value); break;

            // Triangle
            case 0x4008: _triangle.WriteLinearCounter(value); break;
            case 0x400A: _triangle.WriteTimerLo(value); break;
            case 0x400B: _triangle.WriteTimerHi(value); break;

            // Noise
            case 0x400C: _noise.WriteControl(value); break;
            case 0x400E: _noise.WritePeriod(value); break;
            case 0x400F: _noise.WriteLengthCounter(value); break;

            // DMC
            case 0x4010: _dmc.WriteControl(value); break;
            case 0x4011: _dmc.WriteDirectLoad(value); break;
            case 0x4012: _dmc.WriteAddress(value); break;
            case 0x4013: _dmc.WriteLength(value); break;

            // Status
            case 0x4015:
                _pulse1.Enabled = (value & 0x01) != 0;
                _pulse2.Enabled = (value & 0x02) != 0;
                _triangle.Enabled = (value & 0x04) != 0;
                _noise.Enabled = (value & 0x08) != 0;
                _dmc.Enabled = (value & 0x10) != 0;
                _dmc.ClearIrq();
                break;

            // Frame counter
            case 0x4017:
                _frameCounterMode = (value & 0x80) != 0 ? 1 : 0;
                _irqInhibit = (value & 0x40) != 0;
                if (_irqInhibit)
                    _frameIrqPending = false;
                if (_frameCounterMode == 1)
                {
                    QuarterFrame();
                    HalfFrame();
                }
                _frameCounter = 0;
                break;
        }
    }
}

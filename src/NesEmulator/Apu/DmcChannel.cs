using System;

namespace NesEmulator.Apu;

public class DmcChannel
{
    private static readonly int[] DmcPeriods =
    {
        428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54
    };

    private readonly Func<ushort, byte> _memoryRead;

    private bool _irqEnabled;
    private bool _loop;
    private int _timerPeriod;
    private int _timerValue;

    private byte _outputLevel;
    private ushort _sampleAddress;
    private ushort _currentAddress;
    private int _sampleLength;

    private byte _sampleBuffer;
    private bool _sampleBufferEmpty = true;
    private byte _shiftRegister;
    private int _bitsRemaining;

    public int BytesRemaining { get; private set; }
    public bool IrqPending { get; private set; }
    public bool Enabled { get; set; }

    public DmcChannel(Func<ushort, byte> memoryRead)
    {
        _memoryRead = memoryRead;
    }

    public void WriteControl(byte value)
    {
        _irqEnabled = (value & 0x80) != 0;
        _loop = (value & 0x40) != 0;
        _timerPeriod = DmcPeriods[value & 0x0F];

        if (!_irqEnabled)
            IrqPending = false;
    }

    public void WriteDirectLoad(byte value)
    {
        _outputLevel = (byte)(value & 0x7F);
    }

    public void WriteAddress(byte value)
    {
        _sampleAddress = (ushort)(0xC000 | (value << 6));
    }

    public void WriteLength(byte value)
    {
        _sampleLength = (value << 4) | 1;
    }

    public void ClockTimer()
    {
        if (_timerValue == 0)
        {
            _timerValue = _timerPeriod;

            if (!_sampleBufferEmpty)
            {
                if ((_shiftRegister & 1) != 0)
                {
                    if (_outputLevel <= 125)
                        _outputLevel += 2;
                }
                else
                {
                    if (_outputLevel >= 2)
                        _outputLevel -= 2;
                }

                _shiftRegister >>= 1;
                _bitsRemaining--;

                if (_bitsRemaining == 0)
                {
                    _bitsRemaining = 8;
                    _sampleBufferEmpty = true;
                }
            }

            if (_sampleBufferEmpty && BytesRemaining > 0)
            {
                _sampleBuffer = _memoryRead(_currentAddress);
                _sampleBufferEmpty = false;

                _currentAddress++;
                if (_currentAddress == 0)
                    _currentAddress = 0x8000;

                BytesRemaining--;

                if (BytesRemaining == 0)
                {
                    if (_loop)
                    {
                        _currentAddress = _sampleAddress;
                        BytesRemaining = _sampleLength;
                    }
                    else if (_irqEnabled)
                    {
                        IrqPending = true;
                    }
                }

                _shiftRegister = _sampleBuffer;
            }
        }
        else
        {
            _timerValue--;
        }
    }

    public float GetOutput()
    {
        return _outputLevel;
    }

    public void ClearIrq()
    {
        IrqPending = false;
    }
}

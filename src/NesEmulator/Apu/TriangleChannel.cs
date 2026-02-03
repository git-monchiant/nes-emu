namespace NesEmulator.Apu;

public class TriangleChannel
{
    private static readonly byte[] TriangleWaveform =
    {
        15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
    };

    private static readonly byte[] LengthTable =
    {
        10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
        12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
    };

    private bool _control;
    private int _linearCounterLoad;
    private int _linearCounter;
    private bool _linearCounterReload;

    private int _timerPeriod;
    private int _timerValue;
    private int _sequencePos;

    public int LengthCounter { get; private set; }
    public bool Enabled { get; set; }

    public void WriteLinearCounter(byte value)
    {
        _control = (value & 0x80) != 0;
        _linearCounterLoad = value & 0x7F;
    }

    public void WriteTimerLo(byte value)
    {
        _timerPeriod = (_timerPeriod & 0x700) | value;
    }

    public void WriteTimerHi(byte value)
    {
        _timerPeriod = (_timerPeriod & 0x0FF) | ((value & 0x07) << 8);
        if (Enabled)
            LengthCounter = LengthTable[(value >> 3) & 0x1F];
        _linearCounterReload = true;
    }

    public void ClockTimer()
    {
        if (_timerValue == 0)
        {
            _timerValue = _timerPeriod;
            if (LengthCounter > 0 && _linearCounter > 0)
            {
                _sequencePos = (_sequencePos + 1) & 0x1F;
            }
        }
        else
        {
            _timerValue--;
        }
    }

    public void ClockLinearCounter()
    {
        if (_linearCounterReload)
        {
            _linearCounter = _linearCounterLoad;
        }
        else if (_linearCounter > 0)
        {
            _linearCounter--;
        }

        if (!_control)
            _linearCounterReload = false;
    }

    public void ClockLengthCounter()
    {
        if (!_control && LengthCounter > 0)
            LengthCounter--;
    }

    public float GetOutput()
    {
        if (!Enabled || LengthCounter == 0 || _linearCounter == 0)
            return 0;

        if (_timerPeriod < 2)
            return 0; // Ultrasonic frequency - mute

        return TriangleWaveform[_sequencePos];
    }
}

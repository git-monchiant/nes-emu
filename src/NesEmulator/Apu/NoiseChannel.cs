namespace NesEmulator.Apu;

public class NoiseChannel
{
    private static readonly int[] NoisePeriods =
    {
        4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068
    };

    private static readonly byte[] LengthTable =
    {
        10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
        12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
    };

    private bool _lengthCounterHalt;
    private bool _constantVolume;
    private int _volume;
    private bool _mode;
    private int _timerPeriod;
    private int _timerValue;
    private ushort _shiftRegister = 1;

    private int _envelopeCounter;
    private int _envelopeDecay;
    private bool _envelopeStart;

    public int LengthCounter { get; private set; }
    public bool Enabled { get; set; }

    public void WriteControl(byte value)
    {
        _lengthCounterHalt = (value & 0x20) != 0;
        _constantVolume = (value & 0x10) != 0;
        _volume = value & 0x0F;
    }

    public void WritePeriod(byte value)
    {
        _mode = (value & 0x80) != 0;
        _timerPeriod = NoisePeriods[value & 0x0F];
    }

    public void WriteLengthCounter(byte value)
    {
        if (Enabled)
            LengthCounter = LengthTable[(value >> 3) & 0x1F];
        _envelopeStart = true;
    }

    public void ClockTimer()
    {
        if (_timerValue == 0)
        {
            _timerValue = _timerPeriod;

            int feedback = (_shiftRegister & 1) ^
                          ((_shiftRegister >> (_mode ? 6 : 1)) & 1);
            _shiftRegister = (ushort)((_shiftRegister >> 1) | (feedback << 14));
        }
        else
        {
            _timerValue--;
        }
    }

    public void ClockEnvelope()
    {
        if (_envelopeStart)
        {
            _envelopeStart = false;
            _envelopeDecay = 15;
            _envelopeCounter = _volume;
        }
        else if (_envelopeCounter > 0)
        {
            _envelopeCounter--;
        }
        else
        {
            _envelopeCounter = _volume;
            if (_envelopeDecay > 0)
                _envelopeDecay--;
            else if (_lengthCounterHalt)
                _envelopeDecay = 15;
        }
    }

    public void ClockLengthCounter()
    {
        if (!_lengthCounterHalt && LengthCounter > 0)
            LengthCounter--;
    }

    public float GetOutput()
    {
        if (!Enabled || LengthCounter == 0)
            return 0;

        if ((_shiftRegister & 1) != 0)
            return 0;

        int volume = _constantVolume ? _volume : _envelopeDecay;
        return volume;
    }
}

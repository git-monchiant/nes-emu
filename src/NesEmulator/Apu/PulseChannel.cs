namespace NesEmulator.Apu;

public class PulseChannel
{
    private static readonly byte[][] DutyCycles =
    {
        new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 },  // 12.5%
        new byte[] { 0, 1, 1, 0, 0, 0, 0, 0 },  // 25%
        new byte[] { 0, 1, 1, 1, 1, 0, 0, 0 },  // 50%
        new byte[] { 1, 0, 0, 1, 1, 1, 1, 1 },  // 25% negated
    };

    private static readonly byte[] LengthTable =
    {
        10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
        12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
    };

    private readonly bool _isChannel1;

    private int _duty;
    private bool _lengthCounterHalt;
    private bool _constantVolume;
    private int _volume;

    private bool _sweepEnabled;
    private int _sweepPeriod;
    private bool _sweepNegate;
    private int _sweepShift;
    private bool _sweepReload;
    private int _sweepCounter;

    private int _timerPeriod;
    private int _timerValue;
    private int _sequencePos;

    private int _envelopeCounter;
    private int _envelopeDecay;
    private bool _envelopeStart;

    public int LengthCounter { get; private set; }
    public bool Enabled { get; set; }

    public PulseChannel(bool isChannel1)
    {
        _isChannel1 = isChannel1;
    }

    public void WriteControl(byte value)
    {
        _duty = (value >> 6) & 0x03;
        _lengthCounterHalt = (value & 0x20) != 0;
        _constantVolume = (value & 0x10) != 0;
        _volume = value & 0x0F;
    }

    public void WriteSweep(byte value)
    {
        _sweepEnabled = (value & 0x80) != 0;
        _sweepPeriod = (value >> 4) & 0x07;
        _sweepNegate = (value & 0x08) != 0;
        _sweepShift = value & 0x07;
        _sweepReload = true;
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
        _sequencePos = 0;
        _envelopeStart = true;
    }

    public void ClockTimer()
    {
        if (_timerValue == 0)
        {
            _timerValue = _timerPeriod;
            _sequencePos = (_sequencePos + 1) & 0x07;
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

    public void ClockSweep()
    {
        int targetPeriod = CalculateSweepTarget();
        bool muting = _timerPeriod < 8 || targetPeriod > 0x7FF;

        if (_sweepCounter == 0 && _sweepEnabled && !muting && _sweepShift > 0)
        {
            _timerPeriod = targetPeriod;
        }

        if (_sweepCounter == 0 || _sweepReload)
        {
            _sweepCounter = _sweepPeriod;
            _sweepReload = false;
        }
        else
        {
            _sweepCounter--;
        }
    }

    private int CalculateSweepTarget()
    {
        int delta = _timerPeriod >> _sweepShift;
        if (_sweepNegate)
        {
            delta = -delta;
            if (_isChannel1)
                delta--; // Channel 1 uses one's complement
        }
        return _timerPeriod + delta;
    }

    public float GetOutput()
    {
        if (!Enabled || LengthCounter == 0)
            return 0;

        if (_timerPeriod < 8 || CalculateSweepTarget() > 0x7FF)
            return 0;

        if (DutyCycles[_duty][_sequencePos] == 0)
            return 0;

        int volume = _constantVolume ? _volume : _envelopeDecay;
        return volume;
    }
}

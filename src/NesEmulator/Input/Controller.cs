using System;

namespace NesEmulator.Input;

public class Controller
{
    [Flags]
    public enum Buttons : byte
    {
        A = 0x01,
        B = 0x02,
        Select = 0x04,
        Start = 0x08,
        Up = 0x10,
        Down = 0x20,
        Left = 0x40,
        Right = 0x80
    }

    private byte _buttonState;
    private byte _shiftRegister;
    private bool _strobe;

    public void SetButtonState(Buttons buttons)
    {
        _buttonState = (byte)buttons;
    }

    public void SetButton(Buttons button, bool pressed)
    {
        if (pressed)
            _buttonState |= (byte)button;
        else
            _buttonState &= (byte)~button;
    }

    public void Strobe(byte value)
    {
        _strobe = (value & 0x01) != 0;
        if (_strobe)
            _shiftRegister = _buttonState;
    }

    public byte Read()
    {
        if (_strobe)
            return (byte)(_buttonState & 0x01);

        byte result = (byte)(_shiftRegister & 0x01);
        _shiftRegister >>= 1;
        _shiftRegister |= 0x80; // Open bus returns 1s
        return result;
    }
}

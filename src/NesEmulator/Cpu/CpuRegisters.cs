namespace NesEmulator.Cpu;

public struct CpuRegisters
{
    public byte A;      // Accumulator
    public byte X;      // X index
    public byte Y;      // Y index
    public ushort PC;   // Program counter
    public byte SP;     // Stack pointer

    // Status flags (P register)
    public bool C;  // Carry
    public bool Z;  // Zero
    public bool I;  // Interrupt disable
    public bool D;  // Decimal (not used in NES but can be set)
    public bool B;  // Break
    public bool V;  // Overflow
    public bool N;  // Negative

    public byte GetStatus(bool withBreak = false)
    {
        byte status = 0x20; // Bit 5 always set
        if (C) status |= 0x01;
        if (Z) status |= 0x02;
        if (I) status |= 0x04;
        if (D) status |= 0x08;
        if (withBreak) status |= 0x10;
        if (V) status |= 0x40;
        if (N) status |= 0x80;
        return status;
    }

    public void SetStatus(byte value)
    {
        C = (value & 0x01) != 0;
        Z = (value & 0x02) != 0;
        I = (value & 0x04) != 0;
        D = (value & 0x08) != 0;
        V = (value & 0x40) != 0;
        N = (value & 0x80) != 0;
    }

    public void SetZN(byte value)
    {
        Z = value == 0;
        N = (value & 0x80) != 0;
    }
}

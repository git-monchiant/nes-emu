using NesEmulator.Memory;

namespace NesEmulator.Cpu;

public partial class Cpu6502
{
    private readonly MemoryBus _bus;
    private CpuRegisters _regs;
    private int _cycles;
    private bool _nmiPending;
    private bool _irqPending;
    private bool _nmiEdge;

    public long TotalCycles { get; private set; }
    public ref CpuRegisters Registers => ref _regs;

    public Cpu6502(MemoryBus bus)
    {
        _bus = bus;
        InitializeInstructionTable();
    }

    public void Reset()
    {
        _regs.A = 0;
        _regs.X = 0;
        _regs.Y = 0;
        _regs.SP = 0xFD;
        _regs.SetStatus(0x24); // IRQ disabled

        // Read reset vector
        byte lo = _bus.Read(0xFFFC);
        byte hi = _bus.Read(0xFFFD);
        _regs.PC = (ushort)(lo | (hi << 8));

        _cycles = 8;
        TotalCycles = 7;
    }

    public int Step()
    {
        // Handle interrupts
        if (_nmiPending)
        {
            HandleNmi();
            _nmiPending = false;
            return _cycles;
        }

        if (_irqPending && !_regs.I)
        {
            HandleIrq();
            return _cycles;
        }

        // Fetch and execute instruction
        byte opcode = Fetch();
        _instructions[opcode]();

        TotalCycles += _cycles;
        return _cycles;
    }

    public void TriggerNmi()
    {
        _nmiPending = true;
    }

    public void TriggerIrq()
    {
        _irqPending = true;
    }

    public void ClearIrq()
    {
        _irqPending = false;
    }

    private byte Fetch()
    {
        return _bus.Read(_regs.PC++);
    }

    private ushort FetchWord()
    {
        byte lo = Fetch();
        byte hi = Fetch();
        return (ushort)(lo | (hi << 8));
    }

    private byte Read(ushort addr) => _bus.Read(addr);

    private void Write(ushort addr, byte value) => _bus.Write(addr, value);

    private void Push(byte value)
    {
        _bus.Write((ushort)(0x0100 + _regs.SP), value);
        _regs.SP--;
    }

    private void PushWord(ushort value)
    {
        Push((byte)(value >> 8));
        Push((byte)(value & 0xFF));
    }

    private byte Pop()
    {
        _regs.SP++;
        return _bus.Read((ushort)(0x0100 + _regs.SP));
    }

    private ushort PopWord()
    {
        byte lo = Pop();
        byte hi = Pop();
        return (ushort)(lo | (hi << 8));
    }

    private void HandleNmi()
    {
        PushWord(_regs.PC);
        Push(_regs.GetStatus(false));
        _regs.I = true;

        byte lo = _bus.Read(0xFFFA);
        byte hi = _bus.Read(0xFFFB);
        _regs.PC = (ushort)(lo | (hi << 8));
        _cycles = 7;
    }

    private void HandleIrq()
    {
        PushWord(_regs.PC);
        Push(_regs.GetStatus(false));
        _regs.I = true;

        byte lo = _bus.Read(0xFFFE);
        byte hi = _bus.Read(0xFFFF);
        _regs.PC = (ushort)(lo | (hi << 8));
        _cycles = 7;
    }

    // Addressing modes
    private ushort Immediate() => _regs.PC++;

    private ushort ZeroPage() => Fetch();

    private ushort ZeroPageX() => (byte)(Fetch() + _regs.X);

    private ushort ZeroPageY() => (byte)(Fetch() + _regs.Y);

    private ushort Absolute() => FetchWord();

    private ushort AbsoluteX(bool checkPageCross = true)
    {
        ushort addr = FetchWord();
        ushort result = (ushort)(addr + _regs.X);
        if (checkPageCross && (addr & 0xFF00) != (result & 0xFF00))
            _cycles++;
        return result;
    }

    private ushort AbsoluteY(bool checkPageCross = true)
    {
        ushort addr = FetchWord();
        ushort result = (ushort)(addr + _regs.Y);
        if (checkPageCross && (addr & 0xFF00) != (result & 0xFF00))
            _cycles++;
        return result;
    }

    private ushort IndirectX()
    {
        byte zp = (byte)(Fetch() + _regs.X);
        byte lo = _bus.Read(zp);
        byte hi = _bus.Read((byte)(zp + 1));
        return (ushort)(lo | (hi << 8));
    }

    private ushort IndirectY(bool checkPageCross = true)
    {
        byte zp = Fetch();
        byte lo = _bus.Read(zp);
        byte hi = _bus.Read((byte)(zp + 1));
        ushort addr = (ushort)(lo | (hi << 8));
        ushort result = (ushort)(addr + _regs.Y);
        if (checkPageCross && (addr & 0xFF00) != (result & 0xFF00))
            _cycles++;
        return result;
    }
}

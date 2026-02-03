using System;

namespace NesEmulator.Cpu;

public partial class Cpu6502
{
    private Action[] _instructions = null!;

    private void InitializeInstructionTable()
    {
        _instructions = new Action[256];

        // Initialize all to NOP (illegal opcodes)
        for (int i = 0; i < 256; i++)
            _instructions[i] = () => { _cycles = 2; };

        // BRK
        _instructions[0x00] = BRK;

        // ORA
        _instructions[0x01] = () => { _regs.A |= Read(IndirectX()); _regs.SetZN(_regs.A); _cycles = 6; };
        _instructions[0x05] = () => { _regs.A |= Read(ZeroPage()); _regs.SetZN(_regs.A); _cycles = 3; };
        _instructions[0x09] = () => { _regs.A |= Read(Immediate()); _regs.SetZN(_regs.A); _cycles = 2; };
        _instructions[0x0D] = () => { _regs.A |= Read(Absolute()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x11] = () => { _regs.A |= Read(IndirectY()); _regs.SetZN(_regs.A); _cycles = 5; };
        _instructions[0x15] = () => { _regs.A |= Read(ZeroPageX()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x19] = () => { _regs.A |= Read(AbsoluteY()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x1D] = () => { _regs.A |= Read(AbsoluteX()); _regs.SetZN(_regs.A); _cycles = 4; };

        // ASL
        _instructions[0x06] = () => { var a = ZeroPage(); ASL_M(a); _cycles = 5; };
        _instructions[0x0A] = () => { ASL_A(); _cycles = 2; };
        _instructions[0x0E] = () => { var a = Absolute(); ASL_M(a); _cycles = 6; };
        _instructions[0x16] = () => { var a = ZeroPageX(); ASL_M(a); _cycles = 6; };
        _instructions[0x1E] = () => { var a = AbsoluteX(false); ASL_M(a); _cycles = 7; };

        // PHP/PLP/PHA/PLA
        _instructions[0x08] = () => { Push(_regs.GetStatus(true)); _cycles = 3; };
        _instructions[0x28] = () => { _regs.SetStatus(Pop()); _cycles = 4; };
        _instructions[0x48] = () => { Push(_regs.A); _cycles = 3; };
        _instructions[0x68] = () => { _regs.A = Pop(); _regs.SetZN(_regs.A); _cycles = 4; };

        // BPL/BMI/BVC/BVS/BCC/BCS/BNE/BEQ
        _instructions[0x10] = () => Branch(!_regs.N);
        _instructions[0x30] = () => Branch(_regs.N);
        _instructions[0x50] = () => Branch(!_regs.V);
        _instructions[0x70] = () => Branch(_regs.V);
        _instructions[0x90] = () => Branch(!_regs.C);
        _instructions[0xB0] = () => Branch(_regs.C);
        _instructions[0xD0] = () => Branch(!_regs.Z);
        _instructions[0xF0] = () => Branch(_regs.Z);

        // CLC/SEC/CLI/SEI/CLV/CLD/SED
        _instructions[0x18] = () => { _regs.C = false; _cycles = 2; };
        _instructions[0x38] = () => { _regs.C = true; _cycles = 2; };
        _instructions[0x58] = () => { _regs.I = false; _cycles = 2; };
        _instructions[0x78] = () => { _regs.I = true; _cycles = 2; };
        _instructions[0xB8] = () => { _regs.V = false; _cycles = 2; };
        _instructions[0xD8] = () => { _regs.D = false; _cycles = 2; };
        _instructions[0xF8] = () => { _regs.D = true; _cycles = 2; };

        // JSR
        _instructions[0x20] = JSR;

        // AND
        _instructions[0x21] = () => { _regs.A &= Read(IndirectX()); _regs.SetZN(_regs.A); _cycles = 6; };
        _instructions[0x25] = () => { _regs.A &= Read(ZeroPage()); _regs.SetZN(_regs.A); _cycles = 3; };
        _instructions[0x29] = () => { _regs.A &= Read(Immediate()); _regs.SetZN(_regs.A); _cycles = 2; };
        _instructions[0x2D] = () => { _regs.A &= Read(Absolute()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x31] = () => { _regs.A &= Read(IndirectY()); _regs.SetZN(_regs.A); _cycles = 5; };
        _instructions[0x35] = () => { _regs.A &= Read(ZeroPageX()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x39] = () => { _regs.A &= Read(AbsoluteY()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x3D] = () => { _regs.A &= Read(AbsoluteX()); _regs.SetZN(_regs.A); _cycles = 4; };

        // BIT
        _instructions[0x24] = () => { BIT(Read(ZeroPage())); _cycles = 3; };
        _instructions[0x2C] = () => { BIT(Read(Absolute())); _cycles = 4; };

        // ROL
        _instructions[0x26] = () => { var a = ZeroPage(); ROL_M(a); _cycles = 5; };
        _instructions[0x2A] = () => { ROL_A(); _cycles = 2; };
        _instructions[0x2E] = () => { var a = Absolute(); ROL_M(a); _cycles = 6; };
        _instructions[0x36] = () => { var a = ZeroPageX(); ROL_M(a); _cycles = 6; };
        _instructions[0x3E] = () => { var a = AbsoluteX(false); ROL_M(a); _cycles = 7; };

        // RTI
        _instructions[0x40] = RTI;

        // EOR
        _instructions[0x41] = () => { _regs.A ^= Read(IndirectX()); _regs.SetZN(_regs.A); _cycles = 6; };
        _instructions[0x45] = () => { _regs.A ^= Read(ZeroPage()); _regs.SetZN(_regs.A); _cycles = 3; };
        _instructions[0x49] = () => { _regs.A ^= Read(Immediate()); _regs.SetZN(_regs.A); _cycles = 2; };
        _instructions[0x4D] = () => { _regs.A ^= Read(Absolute()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x51] = () => { _regs.A ^= Read(IndirectY()); _regs.SetZN(_regs.A); _cycles = 5; };
        _instructions[0x55] = () => { _regs.A ^= Read(ZeroPageX()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x59] = () => { _regs.A ^= Read(AbsoluteY()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0x5D] = () => { _regs.A ^= Read(AbsoluteX()); _regs.SetZN(_regs.A); _cycles = 4; };

        // LSR
        _instructions[0x46] = () => { var a = ZeroPage(); LSR_M(a); _cycles = 5; };
        _instructions[0x4A] = () => { LSR_A(); _cycles = 2; };
        _instructions[0x4E] = () => { var a = Absolute(); LSR_M(a); _cycles = 6; };
        _instructions[0x56] = () => { var a = ZeroPageX(); LSR_M(a); _cycles = 6; };
        _instructions[0x5E] = () => { var a = AbsoluteX(false); LSR_M(a); _cycles = 7; };

        // JMP
        _instructions[0x4C] = () => { _regs.PC = Absolute(); _cycles = 3; };
        _instructions[0x6C] = JMP_IND;

        // RTS
        _instructions[0x60] = () => { _regs.PC = (ushort)(PopWord() + 1); _cycles = 6; };

        // ADC
        _instructions[0x61] = () => { ADC(Read(IndirectX())); _cycles = 6; };
        _instructions[0x65] = () => { ADC(Read(ZeroPage())); _cycles = 3; };
        _instructions[0x69] = () => { ADC(Read(Immediate())); _cycles = 2; };
        _instructions[0x6D] = () => { ADC(Read(Absolute())); _cycles = 4; };
        _instructions[0x71] = () => { ADC(Read(IndirectY())); _cycles = 5; };
        _instructions[0x75] = () => { ADC(Read(ZeroPageX())); _cycles = 4; };
        _instructions[0x79] = () => { ADC(Read(AbsoluteY())); _cycles = 4; };
        _instructions[0x7D] = () => { ADC(Read(AbsoluteX())); _cycles = 4; };

        // ROR
        _instructions[0x66] = () => { var a = ZeroPage(); ROR_M(a); _cycles = 5; };
        _instructions[0x6A] = () => { ROR_A(); _cycles = 2; };
        _instructions[0x6E] = () => { var a = Absolute(); ROR_M(a); _cycles = 6; };
        _instructions[0x76] = () => { var a = ZeroPageX(); ROR_M(a); _cycles = 6; };
        _instructions[0x7E] = () => { var a = AbsoluteX(false); ROR_M(a); _cycles = 7; };

        // STA
        _instructions[0x81] = () => { Write(IndirectX(), _regs.A); _cycles = 6; };
        _instructions[0x85] = () => { Write(ZeroPage(), _regs.A); _cycles = 3; };
        _instructions[0x8D] = () => { Write(Absolute(), _regs.A); _cycles = 4; };
        _instructions[0x91] = () => { Write(IndirectY(false), _regs.A); _cycles = 6; };
        _instructions[0x95] = () => { Write(ZeroPageX(), _regs.A); _cycles = 4; };
        _instructions[0x99] = () => { Write(AbsoluteY(false), _regs.A); _cycles = 5; };
        _instructions[0x9D] = () => { Write(AbsoluteX(false), _regs.A); _cycles = 5; };

        // STX
        _instructions[0x86] = () => { Write(ZeroPage(), _regs.X); _cycles = 3; };
        _instructions[0x8E] = () => { Write(Absolute(), _regs.X); _cycles = 4; };
        _instructions[0x96] = () => { Write(ZeroPageY(), _regs.X); _cycles = 4; };

        // STY
        _instructions[0x84] = () => { Write(ZeroPage(), _regs.Y); _cycles = 3; };
        _instructions[0x8C] = () => { Write(Absolute(), _regs.Y); _cycles = 4; };
        _instructions[0x94] = () => { Write(ZeroPageX(), _regs.Y); _cycles = 4; };

        // DEY/TAY/INY
        _instructions[0x88] = () => { _regs.Y--; _regs.SetZN(_regs.Y); _cycles = 2; };
        _instructions[0xA8] = () => { _regs.Y = _regs.A; _regs.SetZN(_regs.Y); _cycles = 2; };
        _instructions[0xC8] = () => { _regs.Y++; _regs.SetZN(_regs.Y); _cycles = 2; };

        // DEX/TAX/INX
        _instructions[0xCA] = () => { _regs.X--; _regs.SetZN(_regs.X); _cycles = 2; };
        _instructions[0xAA] = () => { _regs.X = _regs.A; _regs.SetZN(_regs.X); _cycles = 2; };
        _instructions[0xE8] = () => { _regs.X++; _regs.SetZN(_regs.X); _cycles = 2; };

        // TXA/TXS/TSX/TYA
        _instructions[0x8A] = () => { _regs.A = _regs.X; _regs.SetZN(_regs.A); _cycles = 2; };
        _instructions[0x9A] = () => { _regs.SP = _regs.X; _cycles = 2; };
        _instructions[0xBA] = () => { _regs.X = _regs.SP; _regs.SetZN(_regs.X); _cycles = 2; };
        _instructions[0x98] = () => { _regs.A = _regs.Y; _regs.SetZN(_regs.A); _cycles = 2; };

        // LDA
        _instructions[0xA1] = () => { _regs.A = Read(IndirectX()); _regs.SetZN(_regs.A); _cycles = 6; };
        _instructions[0xA5] = () => { _regs.A = Read(ZeroPage()); _regs.SetZN(_regs.A); _cycles = 3; };
        _instructions[0xA9] = () => { _regs.A = Read(Immediate()); _regs.SetZN(_regs.A); _cycles = 2; };
        _instructions[0xAD] = () => { _regs.A = Read(Absolute()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0xB1] = () => { _regs.A = Read(IndirectY()); _regs.SetZN(_regs.A); _cycles = 5; };
        _instructions[0xB5] = () => { _regs.A = Read(ZeroPageX()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0xB9] = () => { _regs.A = Read(AbsoluteY()); _regs.SetZN(_regs.A); _cycles = 4; };
        _instructions[0xBD] = () => { _regs.A = Read(AbsoluteX()); _regs.SetZN(_regs.A); _cycles = 4; };

        // LDX
        _instructions[0xA2] = () => { _regs.X = Read(Immediate()); _regs.SetZN(_regs.X); _cycles = 2; };
        _instructions[0xA6] = () => { _regs.X = Read(ZeroPage()); _regs.SetZN(_regs.X); _cycles = 3; };
        _instructions[0xAE] = () => { _regs.X = Read(Absolute()); _regs.SetZN(_regs.X); _cycles = 4; };
        _instructions[0xB6] = () => { _regs.X = Read(ZeroPageY()); _regs.SetZN(_regs.X); _cycles = 4; };
        _instructions[0xBE] = () => { _regs.X = Read(AbsoluteY()); _regs.SetZN(_regs.X); _cycles = 4; };

        // LDY
        _instructions[0xA0] = () => { _regs.Y = Read(Immediate()); _regs.SetZN(_regs.Y); _cycles = 2; };
        _instructions[0xA4] = () => { _regs.Y = Read(ZeroPage()); _regs.SetZN(_regs.Y); _cycles = 3; };
        _instructions[0xAC] = () => { _regs.Y = Read(Absolute()); _regs.SetZN(_regs.Y); _cycles = 4; };
        _instructions[0xB4] = () => { _regs.Y = Read(ZeroPageX()); _regs.SetZN(_regs.Y); _cycles = 4; };
        _instructions[0xBC] = () => { _regs.Y = Read(AbsoluteX()); _regs.SetZN(_regs.Y); _cycles = 4; };

        // CMP
        _instructions[0xC1] = () => { CMP(_regs.A, Read(IndirectX())); _cycles = 6; };
        _instructions[0xC5] = () => { CMP(_regs.A, Read(ZeroPage())); _cycles = 3; };
        _instructions[0xC9] = () => { CMP(_regs.A, Read(Immediate())); _cycles = 2; };
        _instructions[0xCD] = () => { CMP(_regs.A, Read(Absolute())); _cycles = 4; };
        _instructions[0xD1] = () => { CMP(_regs.A, Read(IndirectY())); _cycles = 5; };
        _instructions[0xD5] = () => { CMP(_regs.A, Read(ZeroPageX())); _cycles = 4; };
        _instructions[0xD9] = () => { CMP(_regs.A, Read(AbsoluteY())); _cycles = 4; };
        _instructions[0xDD] = () => { CMP(_regs.A, Read(AbsoluteX())); _cycles = 4; };

        // CPX
        _instructions[0xE0] = () => { CMP(_regs.X, Read(Immediate())); _cycles = 2; };
        _instructions[0xE4] = () => { CMP(_regs.X, Read(ZeroPage())); _cycles = 3; };
        _instructions[0xEC] = () => { CMP(_regs.X, Read(Absolute())); _cycles = 4; };

        // CPY
        _instructions[0xC0] = () => { CMP(_regs.Y, Read(Immediate())); _cycles = 2; };
        _instructions[0xC4] = () => { CMP(_regs.Y, Read(ZeroPage())); _cycles = 3; };
        _instructions[0xCC] = () => { CMP(_regs.Y, Read(Absolute())); _cycles = 4; };

        // DEC
        _instructions[0xC6] = () => { var a = ZeroPage(); DEC_M(a); _cycles = 5; };
        _instructions[0xCE] = () => { var a = Absolute(); DEC_M(a); _cycles = 6; };
        _instructions[0xD6] = () => { var a = ZeroPageX(); DEC_M(a); _cycles = 6; };
        _instructions[0xDE] = () => { var a = AbsoluteX(false); DEC_M(a); _cycles = 7; };

        // INC
        _instructions[0xE6] = () => { var a = ZeroPage(); INC_M(a); _cycles = 5; };
        _instructions[0xEE] = () => { var a = Absolute(); INC_M(a); _cycles = 6; };
        _instructions[0xF6] = () => { var a = ZeroPageX(); INC_M(a); _cycles = 6; };
        _instructions[0xFE] = () => { var a = AbsoluteX(false); INC_M(a); _cycles = 7; };

        // SBC
        _instructions[0xE1] = () => { SBC(Read(IndirectX())); _cycles = 6; };
        _instructions[0xE5] = () => { SBC(Read(ZeroPage())); _cycles = 3; };
        _instructions[0xE9] = () => { SBC(Read(Immediate())); _cycles = 2; };
        _instructions[0xED] = () => { SBC(Read(Absolute())); _cycles = 4; };
        _instructions[0xF1] = () => { SBC(Read(IndirectY())); _cycles = 5; };
        _instructions[0xF5] = () => { SBC(Read(ZeroPageX())); _cycles = 4; };
        _instructions[0xF9] = () => { SBC(Read(AbsoluteY())); _cycles = 4; };
        _instructions[0xFD] = () => { SBC(Read(AbsoluteX())); _cycles = 4; };

        // NOP
        _instructions[0xEA] = () => { _cycles = 2; };
    }

    private void BRK()
    {
        _regs.PC++;
        PushWord(_regs.PC);
        Push(_regs.GetStatus(true));
        _regs.I = true;
        byte lo = _bus.Read(0xFFFE);
        byte hi = _bus.Read(0xFFFF);
        _regs.PC = (ushort)(lo | (hi << 8));
        _cycles = 7;
    }

    private void JSR()
    {
        ushort addr = FetchWord();
        PushWord((ushort)(_regs.PC - 1));
        _regs.PC = addr;
        _cycles = 6;
    }

    private void RTI()
    {
        _regs.SetStatus(Pop());
        _regs.PC = PopWord();
        _cycles = 6;
    }

    private void JMP_IND()
    {
        ushort ptr = FetchWord();
        // 6502 bug: if low byte is 0xFF, high byte comes from same page
        byte lo = _bus.Read(ptr);
        byte hi = _bus.Read((ushort)((ptr & 0xFF00) | ((ptr + 1) & 0x00FF)));
        _regs.PC = (ushort)(lo | (hi << 8));
        _cycles = 5;
    }

    private void Branch(bool condition)
    {
        sbyte offset = (sbyte)Fetch();
        _cycles = 2;
        if (condition)
        {
            _cycles++;
            ushort newPC = (ushort)(_regs.PC + offset);
            if ((_regs.PC & 0xFF00) != (newPC & 0xFF00))
                _cycles++;
            _regs.PC = newPC;
        }
    }

    private void ADC(byte value)
    {
        int sum = _regs.A + value + (_regs.C ? 1 : 0);
        _regs.C = sum > 0xFF;
        _regs.V = ((~(_regs.A ^ value) & (_regs.A ^ sum)) & 0x80) != 0;
        _regs.A = (byte)sum;
        _regs.SetZN(_regs.A);
    }

    private void SBC(byte value)
    {
        ADC((byte)~value);
    }

    private void CMP(byte reg, byte value)
    {
        int result = reg - value;
        _regs.C = reg >= value;
        _regs.SetZN((byte)result);
    }

    private void BIT(byte value)
    {
        _regs.Z = (_regs.A & value) == 0;
        _regs.V = (value & 0x40) != 0;
        _regs.N = (value & 0x80) != 0;
    }

    private void ASL_A()
    {
        _regs.C = (_regs.A & 0x80) != 0;
        _regs.A <<= 1;
        _regs.SetZN(_regs.A);
    }

    private void ASL_M(ushort addr)
    {
        byte value = Read(addr);
        _regs.C = (value & 0x80) != 0;
        value <<= 1;
        Write(addr, value);
        _regs.SetZN(value);
    }

    private void LSR_A()
    {
        _regs.C = (_regs.A & 0x01) != 0;
        _regs.A >>= 1;
        _regs.SetZN(_regs.A);
    }

    private void LSR_M(ushort addr)
    {
        byte value = Read(addr);
        _regs.C = (value & 0x01) != 0;
        value >>= 1;
        Write(addr, value);
        _regs.SetZN(value);
    }

    private void ROL_A()
    {
        bool newC = (_regs.A & 0x80) != 0;
        _regs.A = (byte)((_regs.A << 1) | (_regs.C ? 1 : 0));
        _regs.C = newC;
        _regs.SetZN(_regs.A);
    }

    private void ROL_M(ushort addr)
    {
        byte value = Read(addr);
        bool newC = (value & 0x80) != 0;
        value = (byte)((value << 1) | (_regs.C ? 1 : 0));
        _regs.C = newC;
        Write(addr, value);
        _regs.SetZN(value);
    }

    private void ROR_A()
    {
        bool newC = (_regs.A & 0x01) != 0;
        _regs.A = (byte)((_regs.A >> 1) | (_regs.C ? 0x80 : 0));
        _regs.C = newC;
        _regs.SetZN(_regs.A);
    }

    private void ROR_M(ushort addr)
    {
        byte value = Read(addr);
        bool newC = (value & 0x01) != 0;
        value = (byte)((value >> 1) | (_regs.C ? 0x80 : 0));
        _regs.C = newC;
        Write(addr, value);
        _regs.SetZN(value);
    }

    private void INC_M(ushort addr)
    {
        byte value = (byte)(Read(addr) + 1);
        Write(addr, value);
        _regs.SetZN(value);
    }

    private void DEC_M(ushort addr)
    {
        byte value = (byte)(Read(addr) - 1);
        Write(addr, value);
        _regs.SetZN(value);
    }
}

using System;

namespace NesEmulator.Cartridge.Mappers;

// UxROM
public class Mapper002 : IMapper
{
    private readonly byte[] _prgRom;
    private readonly byte[] _chrRam = new byte[8192];
    private readonly MirrorMode _mirrorMode;
    private int _prgBank;

    public Mapper002(byte[] prgRom, byte[] chrRom, bool verticalMirroring)
    {
        _prgRom = prgRom;
        if (chrRom.Length > 0)
            Array.Copy(chrRom, _chrRam, Math.Min(chrRom.Length, _chrRam.Length));
        _mirrorMode = verticalMirroring ? MirrorMode.Vertical : MirrorMode.Horizontal;
    }

    public byte CpuRead(ushort addr)
    {
        if (addr >= 0x8000 && addr < 0xC000)
        {
            // Switchable 16KB bank
            int index = _prgBank * 16384 + (addr - 0x8000);
            return _prgRom[index % _prgRom.Length];
        }
        if (addr >= 0xC000)
        {
            // Fixed to last 16KB bank
            int index = _prgRom.Length - 16384 + (addr - 0xC000);
            return _prgRom[index];
        }
        return 0;
    }

    public void CpuWrite(ushort addr, byte value)
    {
        if (addr >= 0x8000)
            _prgBank = value & 0x0F;
    }

    public byte PpuRead(ushort addr)
    {
        if (addr < 0x2000)
            return _chrRam[addr];
        return 0;
    }

    public void PpuWrite(ushort addr, byte value)
    {
        if (addr < 0x2000)
            _chrRam[addr] = value;
    }

    public MirrorMode GetMirrorMode() => _mirrorMode;
    public void ScanlineCounter() { }
    public bool IrqPending => false;
    public void AcknowledgeIrq() { }
}

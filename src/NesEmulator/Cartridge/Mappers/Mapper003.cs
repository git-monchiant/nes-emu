namespace NesEmulator.Cartridge.Mappers;

// CNROM
public class Mapper003 : IMapper
{
    private readonly byte[] _prgRom;
    private readonly byte[] _chrRom;
    private readonly MirrorMode _mirrorMode;
    private int _chrBank;

    public Mapper003(byte[] prgRom, byte[] chrRom, bool verticalMirroring)
    {
        _prgRom = prgRom;
        _chrRom = chrRom.Length > 0 ? chrRom : new byte[8192];
        _mirrorMode = verticalMirroring ? MirrorMode.Vertical : MirrorMode.Horizontal;
    }

    public byte CpuRead(ushort addr)
    {
        if (addr >= 0x8000)
        {
            int index = addr - 0x8000;
            if (_prgRom.Length == 16384)
                index &= 0x3FFF; // Mirror 16KB
            return _prgRom[index % _prgRom.Length];
        }
        return 0;
    }

    public void CpuWrite(ushort addr, byte value)
    {
        if (addr >= 0x8000)
            _chrBank = value & 0x03;
    }

    public byte PpuRead(ushort addr)
    {
        if (addr < 0x2000)
        {
            int index = _chrBank * 8192 + (int)addr;
            return _chrRom[index % _chrRom.Length];
        }
        return 0;
    }

    public void PpuWrite(ushort addr, byte value)
    {
        // CHR ROM is read-only for CNROM
    }

    public MirrorMode GetMirrorMode() => _mirrorMode;
    public void ScanlineCounter() { }
    public bool IrqPending => false;
    public void AcknowledgeIrq() { }
}

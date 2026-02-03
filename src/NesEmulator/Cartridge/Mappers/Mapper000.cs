namespace NesEmulator.Cartridge.Mappers;

// NROM - No mapper, simplest configuration
public class Mapper000 : IMapper
{
    private readonly byte[] _prgRom;
    private readonly byte[] _chrRom;
    private readonly bool _prgMirror;
    private readonly MirrorMode _mirrorMode;

    public Mapper000(byte[] prgRom, byte[] chrRom, bool verticalMirroring)
    {
        _prgRom = prgRom;
        _chrRom = chrRom;
        _prgMirror = prgRom.Length == 16384; // 16KB PRG mirrored
        _mirrorMode = verticalMirroring ? MirrorMode.Vertical : MirrorMode.Horizontal;
    }

    public byte CpuRead(ushort addr)
    {
        if (addr >= 0x8000)
        {
            int index = addr - 0x8000;
            if (_prgMirror) index &= 0x3FFF;
            return _prgRom[index];
        }
        return 0;
    }

    public void CpuWrite(ushort addr, byte value)
    {
        // NROM has no writable PRG
    }

    public byte PpuRead(ushort addr)
    {
        if (addr < 0x2000)
            return _chrRom[addr];
        return 0;
    }

    public void PpuWrite(ushort addr, byte value)
    {
        // CHR RAM writes (if no CHR ROM)
        if (addr < 0x2000)
            _chrRom[addr] = value;
    }

    public MirrorMode GetMirrorMode() => _mirrorMode;
    public void ScanlineCounter() { }
    public bool IrqPending => false;
    public void AcknowledgeIrq() { }
}

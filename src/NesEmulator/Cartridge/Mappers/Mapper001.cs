namespace NesEmulator.Cartridge.Mappers;

// MMC1 / SxROM
public class Mapper001 : IMapper
{
    private readonly byte[] _prgRom;
    private readonly byte[] _chrRom;
    private readonly byte[] _prgRam = new byte[8192];

    private byte _shiftRegister = 0x10;
    private int _writeCount;

    // Control register
    private MirrorMode _mirrorMode;
    private int _prgMode;   // 0,1: 32KB, 2: fix first, 3: fix last
    private int _chrMode;   // 0: 8KB, 1: 4KB

    // Bank registers
    private int _chrBank0;
    private int _chrBank1;
    private int _prgBank;

    public Mapper001(byte[] prgRom, byte[] chrRom, bool verticalMirroring)
    {
        _prgRom = prgRom;
        _chrRom = chrRom;
        _mirrorMode = verticalMirroring ? MirrorMode.Vertical : MirrorMode.Horizontal;
        _prgMode = 3; // Fix last bank at startup
    }

    public byte CpuRead(ushort addr)
    {
        if (addr >= 0x6000 && addr < 0x8000)
            return _prgRam[addr - 0x6000];

        if (addr >= 0x8000)
        {
            int bank;
            int offset;

            switch (_prgMode)
            {
                case 0:
                case 1: // 32KB mode
                    bank = (_prgBank & 0x0E) * 16384;
                    offset = addr - 0x8000;
                    break;
                case 2: // Fix first bank, switch second
                    if (addr < 0xC000)
                    {
                        bank = 0;
                        offset = addr - 0x8000;
                    }
                    else
                    {
                        bank = _prgBank * 16384;
                        offset = addr - 0xC000;
                    }
                    break;
                default: // Fix last bank, switch first
                    if (addr < 0xC000)
                    {
                        bank = _prgBank * 16384;
                        offset = addr - 0x8000;
                    }
                    else
                    {
                        bank = _prgRom.Length - 16384;
                        offset = addr - 0xC000;
                    }
                    break;
            }

            int index = (bank + offset) % _prgRom.Length;
            return _prgRom[index];
        }

        return 0;
    }

    public void CpuWrite(ushort addr, byte value)
    {
        if (addr >= 0x6000 && addr < 0x8000)
        {
            _prgRam[addr - 0x6000] = value;
            return;
        }

        if (addr >= 0x8000)
        {
            if ((value & 0x80) != 0)
            {
                _shiftRegister = 0x10;
                _writeCount = 0;
                _prgMode = 3;
                return;
            }

            _shiftRegister = (byte)((_shiftRegister >> 1) | ((value & 1) << 4));
            _writeCount++;

            if (_writeCount == 5)
            {
                int reg = (addr >> 13) & 0x03;
                byte data = _shiftRegister;

                switch (reg)
                {
                    case 0: // Control
                        _mirrorMode = (data & 0x03) switch
                        {
                            0 => MirrorMode.SingleScreenLower,
                            1 => MirrorMode.SingleScreenUpper,
                            2 => MirrorMode.Vertical,
                            _ => MirrorMode.Horizontal
                        };
                        _prgMode = (data >> 2) & 0x03;
                        _chrMode = (data >> 4) & 0x01;
                        break;
                    case 1: // CHR bank 0
                        _chrBank0 = data;
                        break;
                    case 2: // CHR bank 1
                        _chrBank1 = data;
                        break;
                    case 3: // PRG bank
                        _prgBank = data & 0x0F;
                        break;
                }

                _shiftRegister = 0x10;
                _writeCount = 0;
            }
        }
    }

    public byte PpuRead(ushort addr)
    {
        if (addr < 0x2000)
        {
            int bank;
            int offset;

            if (_chrMode == 0)
            {
                // 8KB mode
                bank = (_chrBank0 & 0x1E) * 4096;
                offset = (int)addr;
            }
            else
            {
                // 4KB mode
                if (addr < 0x1000)
                {
                    bank = _chrBank0 * 4096;
                    offset = (int)addr;
                }
                else
                {
                    bank = _chrBank1 * 4096;
                    offset = (int)addr - 0x1000;
                }
            }

            int index = (bank + offset) % _chrRom.Length;
            return _chrRom[index];
        }
        return 0;
    }

    public void PpuWrite(ushort addr, byte value)
    {
        if (addr < 0x2000)
            _chrRom[addr] = value;
    }

    public MirrorMode GetMirrorMode() => _mirrorMode;
    public void ScanlineCounter() { }
    public bool IrqPending => false;
    public void AcknowledgeIrq() { }
}

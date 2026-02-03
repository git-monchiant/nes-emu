namespace NesEmulator.Cartridge.Mappers;

// MMC3 / TxROM
public class Mapper004 : IMapper
{
    private readonly byte[] _prgRom;
    private readonly byte[] _chrRom;
    private readonly byte[] _prgRam = new byte[8192];

    private int _bankSelect;
    private bool _prgRomBankMode;
    private bool _chrA12Inversion;
    private readonly int[] _bankRegisters = new int[8];

    private MirrorMode _mirrorMode;
    private bool _irqEnabled;
    private byte _irqCounter;
    private byte _irqLatch;
    private bool _irqReload;
    private bool _irqPending;

    public Mapper004(byte[] prgRom, byte[] chrRom, bool verticalMirroring)
    {
        _prgRom = prgRom;
        _chrRom = chrRom.Length > 0 ? chrRom : new byte[8192];
        _mirrorMode = verticalMirroring ? MirrorMode.Vertical : MirrorMode.Horizontal;
    }

    public byte CpuRead(ushort addr)
    {
        if (addr >= 0x6000 && addr < 0x8000)
            return _prgRam[addr - 0x6000];

        if (addr >= 0x8000)
        {
            int bank = GetPrgBank(addr);
            int offset = addr & 0x1FFF;
            int index = (bank * 8192 + offset) % _prgRom.Length;
            return _prgRom[index];
        }
        return 0;
    }

    private int GetPrgBank(ushort addr)
    {
        int numBanks = _prgRom.Length / 8192;

        if (!_prgRomBankMode)
        {
            return (addr & 0x6000) switch
            {
                0x0000 => _bankRegisters[6] & (numBanks - 1),
                0x2000 => _bankRegisters[7] & (numBanks - 1),
                0x4000 => (numBanks - 2) & (numBanks - 1),
                _ => (numBanks - 1) & (numBanks - 1)
            };
        }
        else
        {
            return (addr & 0x6000) switch
            {
                0x0000 => (numBanks - 2) & (numBanks - 1),
                0x2000 => _bankRegisters[7] & (numBanks - 1),
                0x4000 => _bankRegisters[6] & (numBanks - 1),
                _ => (numBanks - 1) & (numBanks - 1)
            };
        }
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
            bool even = (addr & 1) == 0;

            switch (addr & 0xE000)
            {
                case 0x8000:
                    if (even)
                    {
                        _bankSelect = value & 0x07;
                        _prgRomBankMode = (value & 0x40) != 0;
                        _chrA12Inversion = (value & 0x80) != 0;
                    }
                    else
                    {
                        _bankRegisters[_bankSelect] = value;
                    }
                    break;

                case 0xA000:
                    if (even)
                        _mirrorMode = (value & 1) == 0 ? MirrorMode.Vertical : MirrorMode.Horizontal;
                    // Odd: PRG RAM protect (not implemented)
                    break;

                case 0xC000:
                    if (even)
                        _irqLatch = value;
                    else
                        _irqReload = true;
                    break;

                case 0xE000:
                    if (even)
                    {
                        _irqEnabled = false;
                        _irqPending = false;
                    }
                    else
                    {
                        _irqEnabled = true;
                    }
                    break;
            }
        }
    }

    public byte PpuRead(ushort addr)
    {
        if (addr < 0x2000)
        {
            int bank = GetChrBank(addr);
            int offset = addr & 0x03FF;
            int index = (bank * 1024 + offset) % _chrRom.Length;
            return _chrRom[index];
        }
        return 0;
    }

    private int GetChrBank(ushort addr)
    {
        int numBanks = _chrRom.Length / 1024;
        int slot = (addr >> 10) & 0x07;

        if (_chrA12Inversion)
            slot ^= 0x04;

        int bank = slot switch
        {
            0 => _bankRegisters[0] & 0xFE,
            1 => _bankRegisters[0] | 0x01,
            2 => _bankRegisters[1] & 0xFE,
            3 => _bankRegisters[1] | 0x01,
            4 => _bankRegisters[2],
            5 => _bankRegisters[3],
            6 => _bankRegisters[4],
            7 => _bankRegisters[5],
            _ => 0
        };

        return bank & (numBanks - 1);
    }

    public void PpuWrite(ushort addr, byte value)
    {
        if (addr < 0x2000)
            _chrRom[addr] = value;
    }

    public void ScanlineCounter()
    {
        if (_irqCounter == 0 || _irqReload)
        {
            _irqCounter = _irqLatch;
            _irqReload = false;
        }
        else
        {
            _irqCounter--;
        }

        if (_irqCounter == 0 && _irqEnabled)
            _irqPending = true;
    }

    public MirrorMode GetMirrorMode() => _mirrorMode;
    public bool IrqPending => _irqPending;
    public void AcknowledgeIrq() => _irqPending = false;
}

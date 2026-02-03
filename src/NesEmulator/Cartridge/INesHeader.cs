namespace NesEmulator.Cartridge;

public class INesHeader
{
    public bool IsValid { get; }
    public int PrgRomSize { get; }      // In 16KB units
    public int ChrRomSize { get; }      // In 8KB units (0 = CHR RAM)
    public int MapperNumber { get; }
    public bool VerticalMirroring { get; }
    public bool BatteryBacked { get; }
    public bool HasTrainer { get; }
    public bool FourScreenVram { get; }

    public INesHeader(byte[] data)
    {
        if (data.Length < 16)
        {
            IsValid = false;
            return;
        }

        // Check "NES\x1A" magic
        IsValid = data[0] == 'N' && data[1] == 'E' &&
                  data[2] == 'S' && data[3] == 0x1A;

        if (!IsValid) return;

        PrgRomSize = data[4];
        ChrRomSize = data[5];

        byte flags6 = data[6];
        byte flags7 = data[7];

        VerticalMirroring = (flags6 & 0x01) != 0;
        BatteryBacked = (flags6 & 0x02) != 0;
        HasTrainer = (flags6 & 0x04) != 0;
        FourScreenVram = (flags6 & 0x08) != 0;

        MapperNumber = (flags6 >> 4) | (flags7 & 0xF0);
    }
}

namespace NesEmulator.Ppu;

public struct PpuRegisters
{
    // $2000 - PPUCTRL
    public byte Ctrl;
    public int BaseNametableAddr => 0x2000 + (Ctrl & 0x03) * 0x400;
    public int VramIncrement => (Ctrl & 0x04) != 0 ? 32 : 1;
    public int SpritePatternAddr => (Ctrl & 0x08) != 0 ? 0x1000 : 0x0000;
    public int BgPatternAddr => (Ctrl & 0x10) != 0 ? 0x1000 : 0x0000;
    public bool TallSprites => (Ctrl & 0x20) != 0;
    public bool NmiEnabled => (Ctrl & 0x80) != 0;

    // $2001 - PPUMASK
    public byte Mask;
    public bool Grayscale => (Mask & 0x01) != 0;
    public bool ShowLeftBg => (Mask & 0x02) != 0;
    public bool ShowLeftSprites => (Mask & 0x04) != 0;
    public bool ShowBg => (Mask & 0x08) != 0;
    public bool ShowSprites => (Mask & 0x10) != 0;
    public bool RenderingEnabled => (Mask & 0x18) != 0;
    public byte EmphasizeBits => (byte)((Mask >> 5) & 0x07);

    // $2002 - PPUSTATUS
    public byte Status;
    public bool SpriteOverflow
    {
        get => (Status & 0x20) != 0;
        set => Status = (byte)(value ? Status | 0x20 : Status & ~0x20);
    }
    public bool Sprite0Hit
    {
        get => (Status & 0x40) != 0;
        set => Status = (byte)(value ? Status | 0x40 : Status & ~0x40);
    }
    public bool VBlank
    {
        get => (Status & 0x80) != 0;
        set => Status = (byte)(value ? Status | 0x80 : Status & ~0x80);
    }

    // Internal registers (loopy registers)
    public ushort V;        // Current VRAM address (15 bits)
    public ushort T;        // Temporary VRAM address (15 bits)
    public byte FineX;      // Fine X scroll (3 bits)
    public bool W;          // Write toggle (first/second write)

    public byte DataBuffer;
    public byte OamAddr;
}

using System;
using NesEmulator.Cartridge;

namespace NesEmulator.Ppu;

public class Ppu2C02
{
    public const int ScreenWidth = 256;
    public const int ScreenHeight = 240;
    public const int CyclesPerScanline = 341;
    public const int ScanlinesPerFrame = 262;

    private readonly Cartridge.Cartridge _cartridge;
    private PpuRegisters _regs;

    // Memory
    private readonly byte[] _vram = new byte[2048];
    private readonly byte[] _palette = new byte[32];
    private readonly byte[] _oam = new byte[256];
    private readonly byte[] _secondaryOam = new byte[32];

    // Rendering state
    private int _scanline;
    private int _cycle;
    private long _frameCount;
    private bool _oddFrame;
    private readonly uint[] _frameBuffer = new uint[ScreenWidth * ScreenHeight];

    // Background rendering
    private byte _ntByte;
    private byte _atByte;
    private byte _bgLo;
    private byte _bgHi;
    private ushort _bgShiftLo;
    private ushort _bgShiftHi;
    private ushort _atShiftLo;
    private ushort _atShiftHi;
    private byte _atLatchLo;
    private byte _atLatchHi;

    // Sprite rendering
    private int _spriteCount;
    private readonly byte[] _spriteLo = new byte[8];
    private readonly byte[] _spriteHi = new byte[8];
    private readonly byte[] _spriteAttr = new byte[8];
    private readonly byte[] _spriteX = new byte[8];
    private bool _sprite0OnLine;
    private bool _sprite0Rendering;

    public event Action<uint[]>? FrameReady;
    public Action? TriggerNmi;

    public long FrameCount => _frameCount;

    // Diagnostic accessors
    public byte ReadPpuCtrl() => _regs.Ctrl;
    public byte ReadPpuMask() => _regs.Mask;
    public int CurrentScanline => _scanline;
    public int CurrentCycle => _cycle;

    // NES color palette (2C02 NTSC)
    private static readonly uint[] NesPalette = {
        0xFF666666, 0xFF002A88, 0xFF1412A7, 0xFF3B00A4, 0xFF5C007E, 0xFF6E0040, 0xFF6C0600, 0xFF561D00,
        0xFF333500, 0xFF0B4800, 0xFF005200, 0xFF004F08, 0xFF00404D, 0xFF000000, 0xFF000000, 0xFF000000,
        0xFFADADAD, 0xFF155FD9, 0xFF4240FF, 0xFF7527FE, 0xFFA01ACC, 0xFFB71E7B, 0xFFB53120, 0xFF994E00,
        0xFF6B6D00, 0xFF388700, 0xFF0C9300, 0xFF008F32, 0xFF007C8D, 0xFF000000, 0xFF000000, 0xFF000000,
        0xFFFFFEFF, 0xFF64B0FF, 0xFF9290FF, 0xFFC676FF, 0xFFF36AFF, 0xFFFE6ECC, 0xFFFE8170, 0xFFEA9E22,
        0xFFBCBE00, 0xFF88D800, 0xFF5CE430, 0xFF45E082, 0xFF48CDDE, 0xFF4F4F4F, 0xFF000000, 0xFF000000,
        0xFFFFFEFF, 0xFFC0DFFF, 0xFFD3D2FF, 0xFFE8C8FF, 0xFFFBC2FF, 0xFFFEC4EA, 0xFFFECCC5, 0xFFF7D8A5,
        0xFFE4E594, 0xFFCFEF96, 0xFFBDF4AB, 0xFFB3F3CC, 0xFFB5EBF2, 0xFFB8B8B8, 0xFF000000, 0xFF000000,
    };

    public Ppu2C02(Cartridge.Cartridge cartridge)
    {
        _cartridge = cartridge;
    }

    public void Reset()
    {
        _regs = new PpuRegisters();
        // Start at pre-render scanline to properly initialize before first visible frame
        _scanline = 261;
        _cycle = 0;
        _frameCount = 0;
        _oddFrame = false;

        // Clear sprite state
        _spriteCount = 0;
        Array.Clear(_spriteLo);
        Array.Clear(_spriteHi);
        Array.Clear(_spriteAttr);
        Array.Clear(_spriteX);
    }

    public void Clock()
    {
        // Pre-render scanline
        if (_scanline == 261)
        {
            if (_cycle == 1)
            {
                _regs.VBlank = false;
                _regs.Sprite0Hit = false;
                _regs.SpriteOverflow = false;
            }
            if (_cycle >= 280 && _cycle <= 304 && _regs.RenderingEnabled)
            {
                // Copy vertical bits from T to V
                _regs.V = (ushort)((_regs.V & ~0x7BE0) | (_regs.T & 0x7BE0));
            }
        }

        // Visible scanlines
        if (_scanline < 240 || _scanline == 261)
        {
            // Background fetches
            if ((_cycle >= 1 && _cycle <= 256) || (_cycle >= 321 && _cycle <= 336))
            {
                ShiftRegisters();

                switch (_cycle % 8)
                {
                    case 1:
                        LoadShiftRegisters();
                        FetchNametable();
                        break;
                    case 3:
                        FetchAttribute();
                        break;
                    case 5:
                        FetchBgLo();
                        break;
                    case 7:
                        FetchBgHi();
                        break;
                    case 0:
                        IncrementX();
                        break;
                }
            }

            // Increment Y at cycle 256
            if (_cycle == 256)
                IncrementY();

            // Copy horizontal bits at cycle 257
            if (_cycle == 257 && _regs.RenderingEnabled)
            {
                _regs.V = (ushort)((_regs.V & ~0x041F) | (_regs.T & 0x041F));
            }

            // Sprite evaluation (simplified)
            // Evaluate at cycle 257 for visible scanlines AND pre-render scanline
            if (_cycle == 257 && (_scanline < 240 || _scanline == 261))
                EvaluateSprites();
        }

        // Render pixel
        if (_scanline < 240 && _cycle >= 1 && _cycle <= 256)
        {
            RenderPixel();
        }

        // VBlank start
        if (_scanline == 241 && _cycle == 1)
        {
            _regs.VBlank = true;
            if (_regs.NmiEnabled)
                TriggerNmi?.Invoke();
        }

        // Advance cycle/scanline
        _cycle++;
        if (_cycle > 340)
        {
            _cycle = 0;
            _scanline++;
            if (_scanline > 261)
            {
                _scanline = 0;
                _frameCount++;
                _oddFrame = !_oddFrame;
                FrameReady?.Invoke(_frameBuffer);
            }
        }

        // Skip cycle on odd frames when rendering enabled
        if (_scanline == 0 && _cycle == 0 && _oddFrame && _regs.RenderingEnabled)
            _cycle = 1;
    }

    private void RenderPixel()
    {
        int x = _cycle - 1;
        int y = _scanline;

        byte bgPixel = 0;
        byte bgPalette = 0;
        byte sprPixel = 0;
        byte sprPalette = 0;
        bool sprPriority = false;
        bool sprZero = false;

        // Background pixel
        if (_regs.ShowBg && (_regs.ShowLeftBg || x >= 8))
        {
            ushort mask = (ushort)(0x8000 >> _regs.FineX);
            byte lo = (byte)((_bgShiftLo & mask) != 0 ? 1 : 0);
            byte hi = (byte)((_bgShiftHi & mask) != 0 ? 2 : 0);
            bgPixel = (byte)(lo | hi);

            if (bgPixel != 0)
            {
                byte atLo = (byte)((_atShiftLo & mask) != 0 ? 1 : 0);
                byte atHi = (byte)((_atShiftHi & mask) != 0 ? 2 : 0);
                bgPalette = (byte)(atLo | atHi);
            }
        }

        // Sprite pixel
        if (_regs.ShowSprites && (_regs.ShowLeftSprites || x >= 8))
        {
            for (int i = 0; i < _spriteCount; i++)
            {
                if (_spriteX[i] != 0)
                {
                    _spriteX[i]--;
                    continue;
                }

                byte lo = (byte)((_spriteLo[i] & 0x80) != 0 ? 1 : 0);
                byte hi = (byte)((_spriteHi[i] & 0x80) != 0 ? 2 : 0);
                byte pixel = (byte)(lo | hi);

                _spriteLo[i] <<= 1;
                _spriteHi[i] <<= 1;

                if (pixel != 0 && sprPixel == 0)
                {
                    sprPixel = pixel;
                    sprPalette = (byte)((_spriteAttr[i] & 0x03) + 4);
                    sprPriority = (_spriteAttr[i] & 0x20) != 0;
                    sprZero = i == 0 && _sprite0Rendering;
                }
            }
        }

        // Sprite 0 hit detection
        if (sprZero && bgPixel != 0 && sprPixel != 0 && x != 255)
            _regs.Sprite0Hit = true;

        // Priority multiplexer
        byte finalPixel;
        byte finalPalette;

        if (bgPixel == 0 && sprPixel == 0)
        {
            finalPixel = 0;
            finalPalette = 0;
        }
        else if (bgPixel == 0 && sprPixel != 0)
        {
            finalPixel = sprPixel;
            finalPalette = sprPalette;
        }
        else if (bgPixel != 0 && sprPixel == 0)
        {
            finalPixel = bgPixel;
            finalPalette = bgPalette;
        }
        else
        {
            if (sprPriority)
            {
                finalPixel = bgPixel;
                finalPalette = bgPalette;
            }
            else
            {
                finalPixel = sprPixel;
                finalPalette = sprPalette;
            }
        }

        byte colorIndex = ReadPalette((finalPalette << 2) | finalPixel);
        if (_regs.Grayscale)
            colorIndex &= 0x30;

        _frameBuffer[y * ScreenWidth + x] = NesPalette[colorIndex & 0x3F];
    }

    private void ShiftRegisters()
    {
        if (_regs.ShowBg)
        {
            _bgShiftLo <<= 1;
            _bgShiftHi <<= 1;
            _atShiftLo <<= 1;
            _atShiftHi <<= 1;
            _atShiftLo |= _atLatchLo;
            _atShiftHi |= _atLatchHi;
        }
    }

    private void LoadShiftRegisters()
    {
        _bgShiftLo = (ushort)((_bgShiftLo & 0xFF00) | _bgLo);
        _bgShiftHi = (ushort)((_bgShiftHi & 0xFF00) | _bgHi);

        _atLatchLo = (byte)((_atByte & 0x01) != 0 ? 1 : 0);
        _atLatchHi = (byte)((_atByte & 0x02) != 0 ? 1 : 0);
    }

    private void FetchNametable()
    {
        ushort addr = (ushort)(0x2000 | (_regs.V & 0x0FFF));
        _ntByte = PpuRead(addr);
    }

    private void FetchAttribute()
    {
        ushort addr = (ushort)(0x23C0 | (_regs.V & 0x0C00) |
                              ((_regs.V >> 4) & 0x38) | ((_regs.V >> 2) & 0x07));
        byte at = PpuRead(addr);

        int shift = ((_regs.V >> 4) & 0x04) | (_regs.V & 0x02);
        _atByte = (byte)((at >> shift) & 0x03);
    }

    private static int _ppuWriteCount = 0;
    private static bool _dumpedState = false;
    private static int _ntWriteCount = 0;
    private static int _chrWriteCount = 0;
    private void FetchBgLo()
    {
        ushort addr = (ushort)(_regs.BgPatternAddr + _ntByte * 16 + ((_regs.V >> 12) & 0x07));
        _bgLo = PpuRead(addr);

        // Debug: dump PPU state once after frame 60
        if (!_dumpedState && _frameCount == 60 && _scanline == 120 && _cycle == 5)
        {
            _dumpedState = true;
            Console.WriteLine($"[PPU] Frame={_frameCount} CTRL=${_regs.Ctrl:X2} MASK=${_regs.Mask:X2}");
            Console.WriteLine($"[PPU] ShowBg={_regs.ShowBg} ShowSpr={_regs.ShowSprites} BgPat=${_regs.BgPatternAddr:X4}");
            Console.WriteLine($"[PPU] V=${_regs.V:X4} ntByte={_ntByte:X2} bgLo={_bgLo:X2}");
            int nonZero = 0;
            for (int i = 0; i < 2048; i++) if (_vram[i] != 0) nonZero++;
            Console.WriteLine($"[PPU] VRAM non-zero: {nonZero}/2048, NT writes: {_ntWriteCount}, CHR writes: {_chrWriteCount}");
            Console.Write("[PPU] Palette: ");
            for (int i = 0; i < 16; i++) Console.Write($"{_palette[i]:X2} ");
            Console.WriteLine();
        }
    }

    private void FetchBgHi()
    {
        ushort addr = (ushort)(_regs.BgPatternAddr + _ntByte * 16 + ((_regs.V >> 12) & 0x07) + 8);
        _bgHi = PpuRead(addr);
    }

    private void IncrementX()
    {
        if (!_regs.RenderingEnabled) return;

        if ((_regs.V & 0x001F) == 31)
        {
            _regs.V &= 0xFFE0;
            _regs.V ^= 0x0400;
        }
        else
        {
            _regs.V++;
        }
    }

    private void IncrementY()
    {
        if (!_regs.RenderingEnabled) return;

        if ((_regs.V & 0x7000) != 0x7000)
        {
            _regs.V += 0x1000;
        }
        else
        {
            _regs.V &= 0x8FFF;
            int y = (_regs.V & 0x03E0) >> 5;
            if (y == 29)
            {
                y = 0;
                _regs.V ^= 0x0800;
            }
            else if (y == 31)
            {
                y = 0;
            }
            else
            {
                y++;
            }
            _regs.V = (ushort)((_regs.V & ~0x03E0) | (y << 5));
        }
    }

    private void EvaluateSprites()
    {
        _spriteCount = 0;
        _sprite0OnLine = false;

        int spriteHeight = _regs.TallSprites ? 16 : 8;
        // Evaluate for the NEXT scanline (sprite data is used on the next line)
        int nextScanline = (_scanline + 1) % 262;

        for (int i = 0; i < 64 && _spriteCount < 8; i++)
        {
            int y = _oam[i * 4];
            int row = nextScanline - y;

            if (row >= 0 && row < spriteHeight)
            {
                if (i == 0) _sprite0OnLine = true;

                byte tile = _oam[i * 4 + 1];
                byte attr = _oam[i * 4 + 2];
                byte x = _oam[i * 4 + 3];

                bool flipH = (attr & 0x40) != 0;
                bool flipV = (attr & 0x80) != 0;

                if (flipV) row = spriteHeight - 1 - row;

                ushort patternAddr;
                if (_regs.TallSprites)
                {
                    int bank = (tile & 0x01) * 0x1000;
                    int tileNum = tile & 0xFE;
                    if (row >= 8)
                    {
                        tileNum++;
                        row -= 8;
                    }
                    patternAddr = (ushort)(bank + tileNum * 16 + row);
                }
                else
                {
                    patternAddr = (ushort)(_regs.SpritePatternAddr + tile * 16 + row);
                }

                byte lo = PpuRead(patternAddr);
                byte hi = PpuRead((ushort)(patternAddr + 8));

                if (flipH)
                {
                    lo = ReverseBits(lo);
                    hi = ReverseBits(hi);
                }

                _spriteLo[_spriteCount] = lo;
                _spriteHi[_spriteCount] = hi;
                _spriteAttr[_spriteCount] = attr;
                _spriteX[_spriteCount] = x;
                _spriteCount++;
            }
        }

        _sprite0Rendering = _sprite0OnLine;
    }

    private static byte ReverseBits(byte b)
    {
        b = (byte)((b & 0xF0) >> 4 | (b & 0x0F) << 4);
        b = (byte)((b & 0xCC) >> 2 | (b & 0x33) << 2);
        b = (byte)((b & 0xAA) >> 1 | (b & 0x55) << 1);
        return b;
    }

    public byte ReadRegister(int addr)
    {
        byte result = 0;

        switch (addr)
        {
            case 2: // PPUSTATUS
                result = (byte)((_regs.Status & 0xE0) | (_regs.DataBuffer & 0x1F));
                _regs.VBlank = false;
                _regs.W = false;
                break;

            case 4: // OAMDATA
                result = _oam[_regs.OamAddr];
                break;

            case 7: // PPUDATA
                result = _regs.DataBuffer;
                _regs.DataBuffer = PpuRead(_regs.V);
                if (_regs.V >= 0x3F00)
                    result = ReadPalette(_regs.V & 0x1F);
                _regs.V += (ushort)_regs.VramIncrement;
                break;
        }

        return result;
    }

    public void WriteRegister(int addr, byte value)
    {
        switch (addr)
        {
            case 0: // PPUCTRL
                _regs.Ctrl = value;
                _regs.T = (ushort)((_regs.T & 0xF3FF) | ((value & 0x03) << 10));
                break;

            case 1: // PPUMASK
                _regs.Mask = value;
                break;

            case 3: // OAMADDR
                _regs.OamAddr = value;
                break;

            case 4: // OAMDATA
                _oam[_regs.OamAddr++] = value;
                break;

            case 5: // PPUSCROLL
                if (!_regs.W)
                {
                    _regs.T = (ushort)((_regs.T & 0xFFE0) | (value >> 3));
                    _regs.FineX = (byte)(value & 0x07);
                }
                else
                {
                    _regs.T = (ushort)((_regs.T & 0x8C1F) | ((value & 0x07) << 12) | ((value & 0xF8) << 2));
                }
                _regs.W = !_regs.W;
                break;

            case 6: // PPUADDR
                if (!_regs.W)
                {
                    _regs.T = (ushort)((_regs.T & 0x00FF) | ((value & 0x3F) << 8));
                }
                else
                {
                    _regs.T = (ushort)((_regs.T & 0xFF00) | value);
                    _regs.V = _regs.T;
                }
                _regs.W = !_regs.W;
                break;

            case 7: // PPUDATA
                // Debug: log NON-ZERO writes to background pattern table ($1000-$1FFF)
                if (_regs.V >= 0x1000 && _regs.V < 0x2000 && value != 0 && _ppuWriteCount < 20)
                {
                    Console.WriteLine($"[PPUDATA] BG Pattern Write ${value:X2} to ${_regs.V:X4}");
                    _ppuWriteCount++;
                }
                PpuWrite(_regs.V, value);
                _regs.V += (ushort)_regs.VramIncrement;
                break;
        }
    }

    public void WriteOamDma(byte[] data)
    {
        Array.Copy(data, 0, _oam, _regs.OamAddr, Math.Min(256 - _regs.OamAddr, data.Length));
        if (data.Length > 256 - _regs.OamAddr)
            Array.Copy(data, 256 - _regs.OamAddr, _oam, 0, _regs.OamAddr);
    }

    private byte PpuRead(ushort addr)
    {
        addr &= 0x3FFF;

        if (addr < 0x2000)
            return _cartridge.PpuRead(addr);

        if (addr < 0x3F00)
        {
            addr = MirrorNametable(addr);
            return _vram[addr & 0x07FF];
        }

        return ReadPalette(addr & 0x1F);
    }

    private void PpuWrite(ushort addr, byte value)
    {
        addr &= 0x3FFF;

        if (addr < 0x2000)
        {
            _chrWriteCount++;
            _cartridge.PpuWrite(addr, value);
            return;
        }

        if (addr < 0x3F00)
        {
            addr = MirrorNametable(addr);
            _vram[addr & 0x07FF] = value;
            _ntWriteCount++;
            return;
        }

        WritePalette(addr & 0x1F, value);
    }

    private ushort MirrorNametable(ushort addr)
    {
        addr &= 0x2FFF;
        var mode = _cartridge.GetMirrorMode();

        return mode switch
        {
            MirrorMode.Horizontal => (ushort)((addr & 0x03FF) | ((addr & 0x0800) >> 1)),
            MirrorMode.Vertical => (ushort)(addr & 0x07FF),
            MirrorMode.SingleScreenLower => (ushort)(addr & 0x03FF),
            MirrorMode.SingleScreenUpper => (ushort)((addr & 0x03FF) | 0x0400),
            _ => (ushort)(addr & 0x0FFF)
        };
    }

    private byte ReadPalette(int addr)
    {
        addr &= 0x1F;
        if ((addr & 0x13) == 0x10) addr &= 0x0F;
        return _palette[addr];
    }

    private void WritePalette(int addr, byte value)
    {
        addr &= 0x1F;
        if ((addr & 0x13) == 0x10) addr &= 0x0F;
        _palette[addr] = value;
    }
}

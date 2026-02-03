using System;
using NesEmulator.Cartridge.Mappers;

namespace NesEmulator.Cartridge;

public class Cartridge
{
    private readonly IMapper? _mapper;
    public bool IsValid { get; }
    public INesHeader? Header { get; }

    public Cartridge(byte[] data)
    {
        Header = new INesHeader(data);
        if (!Header.IsValid)
        {
            IsValid = false;
            return;
        }

        int offset = 16;
        if (Header.HasTrainer) offset += 512;

        int prgSize = Header.PrgRomSize * 16384;
        int chrSize = Header.ChrRomSize * 8192;

        if (data.Length < offset + prgSize + chrSize)
        {
            IsValid = false;
            return;
        }

        byte[] prgRom = new byte[prgSize];
        byte[] chrRom = chrSize > 0 ? new byte[chrSize] : new byte[8192]; // CHR RAM if no ROM

        Array.Copy(data, offset, prgRom, 0, prgSize);
        if (chrSize > 0)
            Array.Copy(data, offset + prgSize, chrRom, 0, chrSize);

        bool verticalMirroring = Header.VerticalMirroring;

        // Debug: show CHR size
        Console.WriteLine($"[ROM] PRG={prgSize/1024}KB, CHR={chrSize/1024}KB, Mapper={Header.MapperNumber}");

        _mapper = Header.MapperNumber switch
        {
            0 => new Mapper000(prgRom, chrRom, verticalMirroring),
            1 => new Mapper001(prgRom, chrRom, verticalMirroring),
            2 => new Mapper002(prgRom, chrRom, verticalMirroring),
            3 => new Mapper003(prgRom, chrRom, verticalMirroring),
            4 => new Mapper004(prgRom, chrRom, verticalMirroring),
            _ => null
        };

        IsValid = _mapper != null;
    }

    public byte CpuRead(ushort addr) => _mapper?.CpuRead(addr) ?? 0;
    public void CpuWrite(ushort addr, byte value) => _mapper?.CpuWrite(addr, value);
    public byte PpuRead(ushort addr) => _mapper?.PpuRead(addr) ?? 0;
    public void PpuWrite(ushort addr, byte value) => _mapper?.PpuWrite(addr, value);
    public MirrorMode GetMirrorMode() => _mapper?.GetMirrorMode() ?? MirrorMode.Horizontal;
    public void ScanlineCounter() => _mapper?.ScanlineCounter();
    public bool IrqPending => _mapper?.IrqPending ?? false;
    public void AcknowledgeIrq() => _mapper?.AcknowledgeIrq();
}

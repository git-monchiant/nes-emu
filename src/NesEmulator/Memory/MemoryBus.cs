using NesEmulator.Apu;
using NesEmulator.Cartridge;
using NesEmulator.Input;
using NesEmulator.Ppu;

namespace NesEmulator.Memory;

public class MemoryBus
{
    private readonly byte[] _ram = new byte[2048];
    private Ppu2C02? _ppu;
    private Apu2A03? _apu;
    private Cartridge.Cartridge? _cartridge;
    private Controller? _controller1;
    private Controller? _controller2;

    public int DmaCycles { get; private set; }

    public void Connect(Ppu2C02 ppu, Apu2A03 apu, Cartridge.Cartridge cartridge,
                       Controller controller1, Controller controller2)
    {
        _ppu = ppu;
        _apu = apu;
        _cartridge = cartridge;
        _controller1 = controller1;
        _controller2 = controller2;
    }

    public byte Read(ushort addr)
    {
        return addr switch
        {
            < 0x2000 => _ram[addr & 0x07FF],
            < 0x4000 => _ppu?.ReadRegister(addr & 0x07) ?? 0,
            0x4015 => _apu?.ReadStatus() ?? 0,
            0x4016 => _controller1?.Read() ?? 0,
            0x4017 => _controller2?.Read() ?? 0,
            < 0x4020 => 0,
            _ => _cartridge?.CpuRead(addr) ?? 0
        };
    }

    public void Write(ushort addr, byte value)
    {
        switch (addr)
        {
            case < 0x2000:
                _ram[addr & 0x07FF] = value;
                break;
            case < 0x4000:
                _ppu?.WriteRegister(addr & 0x07, value);
                break;
            case 0x4014:
                PerformOamDma(value);
                break;
            case < 0x4016:
                _apu?.WriteRegister(addr, value);
                break;
            case 0x4016:
                _controller1?.Strobe(value);
                _controller2?.Strobe(value);
                break;
            case 0x4017:
                _apu?.WriteRegister(addr, value);
                break;
            case >= 0x4020:
                _cartridge?.CpuWrite(addr, value);
                break;
        }
    }

    private void PerformOamDma(byte page)
    {
        if (_ppu == null) return;

        var data = new byte[256];
        ushort addr = (ushort)(page << 8);
        for (int i = 0; i < 256; i++)
            data[i] = Read((ushort)(addr + i));
        _ppu.WriteOamDma(data);
        DmaCycles = 513;
    }

    public void ClearDmaCycles() => DmaCycles = 0;
}

namespace NesEmulator.Cartridge;

public interface IMapper
{
    byte CpuRead(ushort addr);
    void CpuWrite(ushort addr, byte value);
    byte PpuRead(ushort addr);
    void PpuWrite(ushort addr, byte value);
    MirrorMode GetMirrorMode();
    void ScanlineCounter();
    bool IrqPending { get; }
    void AcknowledgeIrq();
}

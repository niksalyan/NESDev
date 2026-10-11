namespace NesSharp.Core.Mappers;

public sealed class Mapper000 : MapperBase
{
    public Mapper000(int prgBanks, int chrBanks) : base(prgBanks, chrBanks)
    {
    }

    public override bool CpuMapRead(ushort address, out uint mappedAddress, ref byte data)
    {
        if (address >= 0x8000)
        {
            mappedAddress = (uint)(address & (PrgBanks > 1 ? 0x7FFF : 0x3FFF));
            return true;
        }

        mappedAddress = 0;
        return false;
    }

    public override bool CpuMapWrite(ushort address, out uint mappedAddress, byte data = 0)
    {
        if (address >= 0x8000)
        {
            mappedAddress = (uint)(address & (PrgBanks > 1 ? 0x7FFF : 0x3FFF));
            return true;
        }

        mappedAddress = 0;
        return false;
    }

    public override bool PpuMapRead(ushort address, out uint mappedAddress)
    {
        if (address <= 0x1FFF)
        {
            mappedAddress = address;
            return true;
        }

        mappedAddress = 0;
        return false;
    }

    public override bool PpuMapWrite(ushort address, out uint mappedAddress)
    {
        if (address <= 0x1FFF)
        {
            if (ChrBanks == 0)
            {
                // Treat as RAM
                mappedAddress = address;
                return true;
            }
        }

        mappedAddress = 0;
        return false;
    }
}
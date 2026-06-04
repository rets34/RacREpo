using System;

public class PS2RAM
{
    public const int RAM_SIZE = 32 * 1024 * 1024;

    private readonly byte[] _ram = new byte[RAM_SIZE];

    public void Write(uint address, byte[] data)
    {
        uint baseAddress = address & 0x01FFFFFF;
        Array.Copy(data, 0, _ram, (int)baseAddress, data.Length);
    }

    public byte Read8(uint address)
    {
        return _ram[address & 0x01FFFFFF];
    }

    public ushort Read16(uint address)
    {
        uint a = address & 0x01FFFFFF;
        return BitConverter.ToUInt16(_ram, (int)a);
    }

    public uint Read32(uint address)
    {
        uint a = address & 0x01FFFFFF;
        return BitConverter.ToUInt32(_ram, (int)a);
    }
}

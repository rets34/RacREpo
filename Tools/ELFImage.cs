using System.Collections.Generic;

public class ElfImage
{
    public uint EntryPoint;

    public List<ElfSegment> Segments { get; set; } = new();
}

public class ElfSegment
{
    public uint VAddr;
    public uint FileOffset;
    public uint FileSize;
    public uint MemSize;

    public byte Flags;

    public byte[] Data { get; set; } = Array.Empty<byte>();
}
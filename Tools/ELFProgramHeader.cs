public class ELFProgramHeader
{
    public uint Type { get; set; }

    public uint Offset { get; set; }

    public uint VirtualAddress { get; set; }

    public uint PhysicalAddress { get; set; }

    public uint FileSize { get; set; }

    public uint MemorySize { get; set; }

    public uint Flags { get; set; }

    public uint Align { get; set; }
}
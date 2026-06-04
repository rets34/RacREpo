public class ELFFile
{
    public uint EntryPoint { get; set; }

    public List<ELFProgramHeader> ProgramHeaders { get; set; } = new();
}
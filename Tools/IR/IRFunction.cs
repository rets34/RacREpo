namespace Tools.IR;

public class IRFunction
{
    public uint EntryPoint;

    public List<IRBlock> Blocks = new();

    public string? Name;
}
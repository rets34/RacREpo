namespace Tools.IR;

public class IRBlock
{
    public uint StartAddress;

    public List<IRInstruction> Instructions = new();

    public List<uint> Successors = new();
}
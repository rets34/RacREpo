namespace Tools.IR;

public class IRInstruction
{
    public IROp Op;

    public uint Address;

    public string? Dest;

    public string[] Src = [];

    public string? Comment;
}
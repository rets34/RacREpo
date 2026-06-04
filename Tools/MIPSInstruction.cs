using System;

public enum MIPSInstructionType
{
    Unknown,
    RType,
    IType,
    JType
}

public class MIPSInstruction
{
    public uint Address;
    public uint Raw;

    public string Mnemonic = "";
    public string Operands = "";

    public MIPSInstructionType Type = MIPSInstructionType.Unknown;

    public uint Opcode;
    public uint Rs;
    public uint Rt;
    public uint Rd;
    public uint Shamt;
    public uint Funct;

    public short Immediate;
    public ushort UImmediate;

    public uint Target;
    public uint TargetAddress;
    public uint BranchTarget;

    //SYSCALL SUPPORT
    public bool IsSyscall;
    public int SyscallCode;

    public bool IsBranch;
    public bool IsJump;
    public bool IsReturn;
    public bool IsLoad;
    public bool IsStore;

    public override string ToString()
    {
        return $"{Address:X8}: {Mnemonic} {Operands}";
    }
}
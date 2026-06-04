using System;

public class PS2SyscallContext
{
    public uint Address;
    public int SyscallId;
    public string SyscallName;

    public int a0, a1, a2, a3;
    public int v0, v1;

    // =========================
    // Constructor (primary)
    // =========================
    public PS2SyscallContext(uint address, int syscallId)
    {
        Address = address;
        SyscallId = syscallId;
        SyscallName = $"sys_{syscallId:X}";
    }

    // =========================
    // Optional convenience ctor (future use)
    // =========================
    public PS2SyscallContext(MIPSInstruction instr)
    {
        Address = instr.Address;
        SyscallId = unchecked((int)instr.SyscallCode);
        SyscallName = $"sys_{SyscallId:X}";
    }
}
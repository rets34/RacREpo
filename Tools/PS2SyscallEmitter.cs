using System;

public static class PS2SyscallEmitter
{
    public static string Emit(MIPSInstruction instr)
    {
        if (!instr.IsSyscall)
            return $"// not syscall: {instr}";

        int id = instr.SyscallCode;

        string name = PS2SyscallABIResolver.GetName(id);

        // Basic syscall emission layer (expand later with ABI args)
        return id switch
        {
            0x3C => "PS2.FlushCache();",
            0x3D => "PS2.EnableInterrupts();",
            0x3E => "PS2.DisableInterrupts();",

            0x01 => "return PS2.Exit();",
            0x02 => "PS2.Print(v0);",
            0x10 => "PS2.IO(a0, a1, a2, a3);",

            _ => $"// syscall {id:X} ({name})"
        };
    }
}
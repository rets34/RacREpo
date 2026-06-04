using System;
using System.Collections.Generic;

public static class PS2SyscallHandler
{
    public enum SyscallId
    {
        Unknown = -1,

        // Common PS2 / early boot style syscalls (you can expand these later)
        FlushCache = 0x3C,
        EnableInterrupts = 0x3D,
        DisableInterrupts = 0x3E,

        // placeholders for future expansion
        Exit = 0x01,
        Print = 0x02,
        IO = 0x10,
    }

    private static readonly Dictionary<int, string> _syscallNames = new()
    {
        { 0x3C, "FlushCache" },
        { 0x3D, "EnableInterrupts" },
        { 0x3E, "DisableInterrupts" },
        { 0x01, "Exit" },
        { 0x02, "Print" },
        { 0x10, "IO" },
    };

    public static string Resolve(int syscallId)
    {
        return _syscallNames.TryGetValue(syscallId, out var name)
            ? name
            : $"Syscall_{syscallId:X}";
    }

    public static SyscallId Classify(int syscallId)
    {
        return Enum.IsDefined(typeof(SyscallId), syscallId)
            ? (SyscallId)syscallId
            : SyscallId.Unknown;
    }

    public static string Emit(int syscallId)
    {
        return Classify(syscallId) switch
        {
            SyscallId.FlushCache => "FlushCache();",
            SyscallId.EnableInterrupts => "EnableInterrupts();",
            SyscallId.DisableInterrupts => "DisableInterrupts();",
            SyscallId.Exit => "ExitProgram();",
            SyscallId.Print => "PrintSyscall();",
            _ => $"// unhandled syscall 0x{syscallId:X}"
        };
    }
}
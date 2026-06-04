using System;
using System.Collections.Generic;

public static class PS2SyscallABIResolver
{
    public enum SyscallKind
    {
        Unknown,
        System,
        IO,
        Memory,
        Debug,
        Process
    }

    // Canonical syscall name table
    private static readonly Dictionary<int, string> _names = new()
    {
        { 0x3C, "FlushCache" },
        { 0x3D, "EnableInterrupts" },
        { 0x3E, "DisableInterrupts" },
        { 0x01, "Exit" },
        { 0x02, "Print" },
        { 0x10, "IO" },
    };

    // Map syscall → category
    public static SyscallKind GetKind(int syscallId)
    {
        return syscallId switch
        {
            0x3C => SyscallKind.Memory,
            0x3D => SyscallKind.System,
            0x3E => SyscallKind.System,
            0x01 => SyscallKind.Process,
            0x02 => SyscallKind.Debug,
            0x10 => SyscallKind.IO,
            _ => SyscallKind.Unknown
        };
    }

    // Human-readable name
    public static string GetName(int syscallId)
    {
        return _names.TryGetValue(syscallId, out var name)
            ? name
            : $"syscall_{syscallId:X}";
    }

    // Direct C# emission for your decompiler output
    public static string EmitCSharp(int syscallId)
    {
        return syscallId switch
        {
            0x3C => "PS2.FlushCache();",
            0x3D => "PS2.EnableInterrupts();",
            0x3E => "PS2.DisableInterrupts();",
            0x01 => "PS2.Exit();",
            0x02 => "PS2.Print();",
            0x10 => "PS2.IO();",
            _ => $"// syscall 0x{syscallId:X} (unhandled)"
        };
    }

    // Whether we recognize it
    public static bool IsKnown(int syscallId)
        => _names.ContainsKey(syscallId);

    // Debug helper (useful while reversing PS2 boot code)
    public static string Describe(int syscallId)
    {
        return $"{GetName(syscallId)} [{GetKind(syscallId)}]";
    }
}
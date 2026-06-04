using System;
using System.Collections.Generic;

public static class PS2SyscallLog
{
    private static readonly List<PS2SyscallContext> _entries = new();

    /// <summary>
    /// Record a syscall event captured during register tracking.
    /// </summary>
    public static void Record(PS2SyscallContext ctx)
    {
        if (ctx == null)
            return;

        _entries.Add(ctx);
    }

    /// <summary>
    /// Returns all captured syscalls in execution order.
    /// </summary>
    public static IReadOnlyList<PS2SyscallContext> GetAll()
    {
        return _entries;
    }

    /// <summary>
    /// Clears captured syscall history (useful per function analysis).
    /// </summary>
    public static void Clear()
    {
        _entries.Clear();
    }

    /// <summary>
    /// Debug dump for inspection while reverse engineering.
    /// </summary>
    public static void Dump()
    {
        Console.WriteLine("=== PS2 SYSCALL LOG ===");

        foreach (var e in _entries)
        {
            Console.WriteLine(
                $"{e.Address:X8} | {e.SyscallName} (0x{e.SyscallId:X}) " +
                $"a0={e.a0} a1={e.a1} a2={e.a2} a3={e.a3}"
            );
        }
    }
}
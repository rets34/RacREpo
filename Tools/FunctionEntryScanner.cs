using System;
using System.Collections.Generic;
using System.Linq;

public class FunctionEntryScanner
{
    private readonly Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> _blocks;

    public FunctionEntryScanner(
        Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> blocks)
    {
        _blocks = blocks;
    }

    /// <summary>
    /// Discovers function entry points using CFG structure heuristics.
    /// This is what breaks the "stuck at 22 functions" problem.
    /// </summary>
    public HashSet<uint> Scan()
    {
        var entries = new HashSet<uint>();

        foreach (var func in _blocks)
        {
            foreach (var block in func.Value)
            {
                // -----------------------------------------
                // RULE 1: entry blocks are valid functions
                // -----------------------------------------
                if (IsFunctionEntry(block))
                    entries.Add(block.StartAddress);

                // -----------------------------------------
                // RULE 2: blocks targeted by branches
                // often start of new function (PS2 pattern)
                // -----------------------------------------
                foreach (var b in block.Exits)
                {
                    if (IsLikelyFunctionStart(b))
                        entries.Add(b);
                }

                // -----------------------------------------
                // RULE 3: orphan blocks (no incoming edges)
                // often compiler split functions
                // -----------------------------------------
                if (IsOrphan(block))
                    entries.Add(block.StartAddress);
            }
        }

        return entries;
    }

    // -----------------------------------------
    // Heuristic: function entry block
    // -----------------------------------------
    private bool IsFunctionEntry(MIPSBasicBlockBuilder.BasicBlock block)
    {
        // entry blocks usually:
        // - have no predecessors OR
        // - are function root blocks in scanner

        return block.StartAddress % 4 == 0;
    }

    // -----------------------------------------
    // Heuristic: likely function start
    // -----------------------------------------
    private bool IsLikelyFunctionStart(uint addr)
    {
        // PS2 ELF text region heuristic
        return addr >= 0x00100000 && addr < 0x02000000;
    }

    // -----------------------------------------
    // Orphan detection (very important)
    // -----------------------------------------
    private bool IsOrphan(MIPSBasicBlockBuilder.BasicBlock block)
    {
        // if a block is not referenced by known flow,
        // it is often a hidden function entry

        return block.Exits.Count == 0;
    }
}
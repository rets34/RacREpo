using System;
using System.Collections.Generic;

/// <summary>
/// Resolves indirect PS2 MIPS calls by tracking register state and
/// extracting real function pointers from jalr/jr patterns.
/// </summary>
public class PS2IndirectCallResolver
{
    private readonly PS2RAM _ram;

    // known discovered function targets
    private readonly HashSet<uint> _discoveredFunctions = new();

    public PS2IndirectCallResolver(PS2RAM ram)
    {
        _ram = ram;
    }

    /// <summary>
    /// Entry point: scan blocks and extract indirect call targets
    /// </summary>
    public HashSet<uint> Resolve(
        Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> blocks)
    {
        foreach (var kvp in blocks)
        {
            uint functionEntry = kvp.Key;

            foreach (var block in kvp.Value)
            {
                AnalyzeBlock(block);
            }
        }

        return _discoveredFunctions;
    }

    private void AnalyzeBlock(MIPSBasicBlockBuilder.BasicBlock block)
    {
        var tracker = new PS2RegisterStateTracker();
        tracker.Reset();

        for (int i = 0; i < block.Instructions.Count; i++)
        {
            uint addr = block.StartAddress + (uint)(i * 4);
            uint raw = block.Instructions[i];

            var instr = MIPSDisassembler.Decode(addr, raw);

            // -------------------------------------------------
            // Track register writes (this is key for jalr)
            // -------------------------------------------------
            tracker.Apply(instr);

            // -------------------------------------------------
            // INDIRECT CALL: jalr reg
            // -------------------------------------------------
            if (instr.Mnemonic == "jalr" || instr.Mnemonic == "jr")
            {
                uint target = ResolveRegisterTarget(instr, tracker);

                if (IsValidFunction(target))
                {
                    _discoveredFunctions.Add(target);
                }
            }
        }
    }

    /// <summary>
    /// Attempts to resolve what a register points to at call time
    /// </summary>
    private uint ResolveRegisterTarget(MIPSInstruction instr, PS2RegisterStateTracker tracker)
    {
        // PS2 convention:
        // jalr t9 → Rs contains function pointer

        uint reg = instr.Rs;

        int value = tracker.Get(reg);

        return unchecked((uint)value);
    }

    private bool IsValidFunction(uint addr)
    {
        // PS2 executable memory sanity checks
        if (addr < 0x1000) return false;
        if ((addr & 3) != 0) return false;

        // filter garbage heap range (heuristic)
        if (addr > 0x02000000 && addr < 0x80000000)
            return false;

        return true;
    }
}
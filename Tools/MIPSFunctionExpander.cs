using System;
using System.Collections.Generic;

public class MIPSFunctionExpander
{
    private readonly PS2RAM _ram;
    private readonly MIPSBasicBlockBuilder _builder;

    private readonly HashSet<uint> _visitedFunctions = new();
    private readonly Queue<uint> _queue = new();

    private readonly Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> _functions = new();

    public MIPSFunctionExpander(PS2RAM ram, MIPSBasicBlockBuilder builder)
    {
        _ram = ram;
        _builder = builder;
    }

    // =========================================================
    // ENTRY
    // =========================================================
    public Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> Build(uint entryPoint)
    {
        _queue.Enqueue(entryPoint);

        while (_queue.Count > 0)
        {
            var func = _queue.Dequeue();

            if (_visitedFunctions.Contains(func))
                continue;

            _visitedFunctions.Add(func);

            var blocks = _builder.Build(func);

            _functions[func] = blocks;

            ScanForCalls(blocks);
        }

        return _functions;
    }

    // =========================================================
    // CALL DISCOVERY
    // =========================================================
    private void ScanForCalls(List<MIPSBasicBlockBuilder.BasicBlock> blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var instr in block.Instructions)
            {
                // SAFE GUARD: supports either decoded or raw model
                var mnemonic = instr.Mnemonic;

                if (mnemonic == "jal" || mnemonic == "jalr")
                {
                    uint target = instr.TargetAddress;

                    if (target != 0 &&
                        !_visitedFunctions.Contains(target) &&
                        IsValid(target))
                    {
                        _queue.Enqueue(target);
                    }
                }
            }
        }
    }

    // =========================================================
    // VALIDATION (PS2 HEURISTICS)
    // =========================================================
    private bool IsValid(uint addr)
    {
        if (addr < 0x00100000) return false;
        if ((addr & 3) != 0) return false;

        // PS2 ELF region heuristic
        if (addr >= 0x02000000 && addr < 0x80000000)
            return false;

        return true;
    }
}
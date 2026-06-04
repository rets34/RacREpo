using System;
using System.Collections.Generic;

public class MIPSFunctionExpander
{
    private readonly PS2RAM _ram;
    private readonly MIPSBasicBlockBuilder _blockBuilder;

    private readonly HashSet<uint> _visitedFunctions = new();
    private readonly Queue<uint> _functionQueue = new();

    // function address → blocks
    private readonly Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> _functions = new();

    public MIPSFunctionExpander(PS2RAM ram, MIPSBasicBlockBuilder blockBuilder)
    {
        _ram = ram;
        _blockBuilder = blockBuilder;
    }

    // =========================================================
    // ENTRY POINT
    // =========================================================
    public Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> Build(uint entryPoint)
    {
        _functionQueue.Enqueue(entryPoint);

        while (_functionQueue.Count > 0)
        {
            uint funcAddr = _functionQueue.Dequeue();

            if (_visitedFunctions.Contains(funcAddr))
                continue;

            _visitedFunctions.Add(funcAddr);

            var blocks = BuildFunction(funcAddr);
            _functions[funcAddr] = blocks;
        }

        return _functions;
    }

    // =========================================================
    // BUILD ONE FUNCTION
    // =========================================================
    private List<MIPSBasicBlockBuilder.BasicBlock> BuildFunction(uint entryPoint)
    {
        var blocks = _blockBuilder.Build(entryPoint);

        foreach (var block in blocks)
        {
            AnalyzeBlockForCalls(block);
        }

        return blocks;
    }

    // =========================================================
    // SCAN BLOCK FOR FUNCTION CALLS
    // =========================================================
    private void AnalyzeBlockForCalls(MIPSBasicBlockBuilder.BasicBlock block)
    {
        for (int i = 0; i < block.Instructions.Count; i++)
        {
            uint raw = block.Instructions[i];
            uint addr = block.StartAddress + (uint)(i * 4);

            var instr = MIPSDisassembler.Decode(addr, raw);

            // -------------------------------------------------
            // FUNCTION CALL DETECTION (jal)
            // -------------------------------------------------
            if (instr.IsJump && instr.Mnemonic == "jal")
            {
                uint target = instr.TargetAddress;

                if (!_visitedFunctions.Contains(target))
                {
                    _functionQueue.Enqueue(target);
                }
            }

            // -------------------------------------------------
            // INDIRECT CALLS (future hook point)
            // -------------------------------------------------
            if (instr.Mnemonic == "jalr")
            {
                // Could resolve via register tracking later
            }
        }
    }

    // =========================================================
    // DEBUG OUTPUT
    // =========================================================
    public void Dump()
    {
        Console.WriteLine("=== FUNCTION EXPANSION GRAPH ===");

        foreach (var fn in _functions)
        {
            Console.WriteLine($"Function: 0x{fn.Key:X8}");

            foreach (var block in fn.Value)
            {
                Console.WriteLine($"  Block: 0x{block.StartAddress:X8}");
            }
        }
    }
}
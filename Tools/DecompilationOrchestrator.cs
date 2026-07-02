using System;
using System.Collections.Generic;
using System.Linq;

public class DecompilationOrchestrator
{
    private readonly PS2RAM _ram;

    public DecompilationOrchestrator(PS2RAM ram)
    {
        _ram = ram;
    }

    // =========================================================
    // ENTRY POINT
    // =========================================================
    public string Run(
        Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> functionBlocks,
        uint entryPoint)
    {
        Console.WriteLine("======================================");
        Console.WriteLine("🚀 DECOMPILATION ORCHESTRATOR START");
        Console.WriteLine("======================================");

        // =====================================================
        // STEP 1: CFG FLATTEN
        // =====================================================
        var flatBlocks = functionBlocks
            .Values
            .SelectMany(x => x)
            .ToList();

        Console.WriteLine($"[1] Raw blocks: {flatBlocks.Count}");

        // =====================================================
        // STEP 2: CFG REFINE
        // =====================================================
        var cfg = new ControlFlowGraphRefiner();
        var refined = cfg.Refine(flatBlocks);

        Console.WriteLine($"[2] CFG blocks: {refined.Count}");

        // =====================================================
        // STEP 3: SEED FUNCTION SET
        // =====================================================
        var seeds = new HashSet<uint>();

        foreach (var b in refined)
        {
            seeds.Add(b.StartAddress);

            foreach (var s in b.Successors)
                seeds.Add(s);
        }

        foreach (var k in functionBlocks.Keys)
            seeds.Add(k);

        Console.WriteLine($"[3] Seed functions: {seeds.Count}");

        // =====================================================
        // STEP 4: UNIFIED FUNCTION EXPANSION ENGINE
        // =====================================================
        var expanded = ExpandFunctions(seeds);

        Console.WriteLine($"[4] Expanded functions: {expanded.Count}");

        // =====================================================
        // STEP 5: FILTER BLOCKS
        // =====================================================
        var expandedBlocks =
    new Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>>();

    var builder = new MIPSBasicBlockBuilder(_ram);

foreach (uint func in expanded)
{
    try
    {
        var blocks = builder.Build(func);

        if (blocks != null && blocks.Count > 0)
            expandedBlocks[func] = blocks;
    }
    catch
    {
        // ignore invalid candidates
    }
}

Console.WriteLine($"[5] Built function CFGs: {expandedBlocks.Count}");
        // =====================================================
        // STEP 6: DECOMPILE
        // =====================================================
        var decompiler = new MIPSDecompiler(_ram);

        string output = decompiler.GenerateCSharpModule(
            "DecompiledGame",
            entryPoint,
            expandedBlocks,
            null
        );

        Console.WriteLine("======================================");
        Console.WriteLine("PIPELINE COMPLETE");
        Console.WriteLine($"Final functions: {expandedBlocks.Count}");
        Console.WriteLine("======================================");

        return output;
    }

    // =========================================================
    // UNIFIED FUNCTION EXPANSION ENGINE
    // =========================================================
    private HashSet<uint> ExpandFunctions(HashSet<uint> seeds)
    {
        var result = new HashSet<uint>(seeds);
        var queue = new Queue<uint>(seeds);

        while (queue.Count > 0)
        {
            uint func = queue.Dequeue();

            foreach (var discovered in ScanFunction(func))
            {
                if (result.Add(discovered))
                    queue.Enqueue(discovered);
            }
        }

        return result;
    }

    // =========================================================
    // SINGLE SCANNER (REPLACES BOTH OLD RESOLVERS)
    // =========================================================
    private List<uint> ScanFunction(uint funcStart)
    {
        var found = new List<uint>();

        uint pc = funcStart;
        const int max = 256;

        for (int i = 0; i < max; i++)
        {
            if (!IsCode(pc))
                break;

            uint raw = _ram.Read32(pc);
            var instr = MIPSDisassembler.Decode(pc, raw);

            // ---------------------------------
            // direct call (jal)
            // ---------------------------------
            if (instr.Mnemonic == "jal")
            {
                if (IsCode(instr.TargetAddress))
                    found.Add(instr.TargetAddress & 0xFFFFFFFC);
            }

            // ---------------------------------
            // indirect call (jalr)
            // ---------------------------------
            if (instr.Mnemonic == "jalr")
            {
                var indirect = GuessIndirect(instr);
                foreach (var t in indirect)
                    found.Add(t);
            }

            // ---------------------------------
            // computed jump / switch table
            // ---------------------------------
            if (instr.IsJump && instr.Mnemonic != "jal")
            {
                if (IsCode(instr.TargetAddress))
                    found.Add(instr.TargetAddress & 0xFFFFFFFC);
            }

            pc += 4;
        }

        return found;
    }

    // =========================================================
    // INDIRECT HEURISTIC (SAFE PLACEHOLDER)
    // =========================================================
    private IEnumerable<uint> GuessIndirect(MIPSInstruction instr)
    {
        // PS2 ABI hint: t9 register often used for function pointers
        if (instr.Rs == 25 || instr.Rt == 25)
        {
            // could be resolved later with register tracking
            yield break;
        }

        yield break;
    }

    // =========================================================
    // VALIDATION
    // =========================================================
    private bool IsCode(uint addr)
    {
        if (addr < 0x00100000)
            return false;

        if (addr >= 0x02000000 && addr < 0x80000000)
            return false;

        if ((addr & 3) != 0)
            return false;

        return true;
    }
}
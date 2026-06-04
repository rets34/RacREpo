using System;
using System.Collections.Generic;
using System.Linq;

public class FunctionDiscoveryEngine
{
    private readonly Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> _rawBlocks;
    private readonly PS2RAM _ram;

    public FunctionDiscoveryEngine(
        Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> rawBlocks,
        PS2RAM ram)
    {
        _rawBlocks = rawBlocks;
        _ram = ram;
    }

    // =========================================================
    // SINGLE SOURCE OF TRUTH FUNCTION DISCOVERY
    // (REPLACES RecursiveFunctionResolver + IndirectFunctionResolver)
    // =========================================================
    public HashSet<uint> DiscoverFunctions()
    {
        Console.WriteLine("[FUNC] Unified discovery pipeline start");

        // -------------------------
        // STEP 1: FLATTEN CFG BLOCKS
        // -------------------------
        var flatBlocks = _rawBlocks.Values
            .SelectMany(x => x)
            .ToList();

        // -------------------------
        // STEP 2: CFG REFINEMENT
        // -------------------------
        var cfg = new ControlFlowGraphRefiner();
        var refined = cfg.Refine(flatBlocks);

        Console.WriteLine($"[FUNC] CFG blocks: {refined.Count}");

        // -------------------------
        // STEP 3: BUILD SEED SET
        // -------------------------
        var seeds = new HashSet<uint>();

        foreach (var b in refined)
        {
            seeds.Add(b.StartAddress);

            foreach (var s in b.Successors)
                seeds.Add(s);
        }

        foreach (var k in _rawBlocks.Keys)
            seeds.Add(k);

        // -------------------------
        // STEP 4: UNIFIED EXPANSION ENGINE
        // -------------------------
        var result = Expand(seeds);

        Console.WriteLine($"[FUNC] expanded functions: {result.Count}");

        // -------------------------
        // STEP 5: FINAL CLEANUP RULESET
        // -------------------------
        return Cleanup(result);
    }

    // =========================================================
    // UNIFIED EXPANSION ENGINE
    // (REPLACES BOTH OLD RESOLVERS)
    // =========================================================
    private HashSet<uint> Expand(HashSet<uint> seeds)
    {
        var result = new HashSet<uint>(seeds);
        var queue = new Queue<uint>(seeds);

        while (queue.Count > 0)
        {
            uint func = queue.Dequeue();

            foreach (var callee in ScanFunction(func))
            {
                if (result.Add(callee))
                    queue.Enqueue(callee);
            }
        }

        return result;
    }

    // =========================================================
    // SINGLE FUNCTION SCANNER (MERGED LOGIC)
    // Handles:
    // - jal (direct calls)
    // - jalr (indirect calls heuristic)
    // - computed jumps
    // =========================================================
    private List<uint> ScanFunction(uint funcStart)
    {
        var found = new List<uint>();

        uint pc = funcStart;
        const int maxInstructions = 256;

        for (int i = 0; i < maxInstructions; i++)
        {
            if (!IsCode(pc))
                break;

            uint raw = _ram.Read32(pc);
            var instr = MIPSDisassembler.Decode(pc, raw);

            // -------------------------
            // DIRECT CALL (jal)
            // -------------------------
            if (instr.Mnemonic == "jal")
            {
                if (IsCode(instr.TargetAddress))
                    found.Add(Normalize(instr.TargetAddress));
            }

            // -------------------------
            // INDIRECT CALL (jalr)
            // -------------------------
            if (instr.Mnemonic == "jalr")
            {
                found.AddRange(ResolveIndirect(instr));
            }

            // -------------------------
            // COMPUTED JUMP
            // -------------------------
            if (instr.IsJump && instr.Mnemonic != "jal")
            {
                if (IsCode(instr.TargetAddress))
                    found.Add(Normalize(instr.TargetAddress));
            }

            pc += 4;
        }

        return found;
    }

    // =========================================================
    // INDIRECT RESOLUTION (HEURISTIC LAYER)
    // (kept intentionally conservative)
    // =========================================================
    private IEnumerable<uint> ResolveIndirect(MIPSInstruction instr)
    {
        // Placeholder safe heuristic:
        // PS2 code often uses register 25 ($t9) for function pointers

        if (instr.Rs == 25 || instr.Rt == 25)
        {
            // In a full system you'd resolve register state here
            yield break;
        }

        yield break;
    }

    // =========================================================
    // NORMALIZATION
    // =========================================================
    private uint Normalize(uint addr)
        => addr & 0xFFFFFFFC;

    // =========================================================
    // VALIDATION RULESET (GLOBAL AUTHORITY)
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

    // =========================================================
    // FINAL CLEANUP RULESET
    // =========================================================
    private HashSet<uint> Cleanup(HashSet<uint> input)
    {
        var output = new HashSet<uint>();

        foreach (var addr in input)
        {
            if (IsCode(addr))
                output.Add(Normalize(addr));
        }

        return output;
    }
}
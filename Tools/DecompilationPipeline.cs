using System;
using System.Collections.Generic;
using System.Linq;

public class DecompilationPipeline
{
    private readonly PS2RAM _ram;

    public DecompilationPipeline(PS2RAM ram)
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
        Console.WriteLine("🚀 DECOMPILATION PIPELINE START");
        Console.WriteLine("======================================");

        Console.WriteLine($"[STAGE 0] Seed functions: {functionBlocks.Count}");

        // =====================================================
        // STAGE 1: SINGLE SOURCE FUNCTION DISCOVERY
        // =====================================================
        var discovery = new FunctionDiscoveryEngine(functionBlocks, _ram);
        var expandedFunctions = discovery.DiscoverFunctions();

        Console.WriteLine($"[STAGE 1] Expanded functions: {expandedFunctions.Count}");

        // =====================================================
        // FILTER BLOCKS USING FINAL FUNCTION SET
        // =====================================================
        var filteredBlocks = functionBlocks
            .Where(f => expandedFunctions.Contains(f.Key))
            .ToDictionary(x => x.Key, x => x.Value);

        Console.WriteLine($"[STAGE 2] Filtered functions: {filteredBlocks.Count}");

        // =====================================================
        // STAGE 3: CFG REFINEMENT (ONLY ONCE)
        // =====================================================
        var cfg = new ControlFlowGraphRefiner();

        var allBlocks = filteredBlocks
            .Values
            .SelectMany(x => x)
            .ToList();

        var refined = cfg.Refine(allBlocks);

        Console.WriteLine($"[STAGE 3] CFG blocks: {refined.Count}");

        // =====================================================
        // STAGE 4: DECOMPILATION
        // =====================================================
        var decompiler = new MIPSDecompiler(_ram);

        string output = decompiler.GenerateCSharpModule(
            "DecompiledGame",
            entryPoint,
            filteredBlocks,
            null // IMPORTANT: no resolver passed anymore (single authority model)
        );

        // =====================================================
        // STAGE 5: SUMMARY
        // =====================================================
        Console.WriteLine("======================================");
        Console.WriteLine("✅ PIPELINE COMPLETE");
        Console.WriteLine($"Final functions: {filteredBlocks.Count}");
        Console.WriteLine("======================================");

        return output;
    }
}
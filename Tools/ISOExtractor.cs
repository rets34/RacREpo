using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ISOExtractor
{
    private readonly string _isoPath;

    public ISOExtractor(string isoPath)
    {
        _isoPath = isoPath;
    }

    public void Run()
    {
        Console.WriteLine("[MAIN] Starting ISO → ELF → CFG pipeline...");
        Console.WriteLine($"[MAIN] ISO: {_isoPath}");

        // =========================
        // 1. ISO PARSE
        // =========================
        var reader = new ISO9660Reader(_isoPath);

        reader.Parse();

        Console.WriteLine("\n[ISO] FILE TREE:");
        reader.DumpTree();

        Console.WriteLine("\n[ISO] ELF DETECTION:");
        reader.DetectElf();

        // =========================
        // 2. EXTRACT BOOT ELF
        // =========================
        string elfPath = reader.ExtractBootElf();

        if (string.IsNullOrEmpty(elfPath))
        {
            Console.WriteLine("[MAIN] Failed to extract ELF. Aborting.");
            return;
        }

        Console.WriteLine($"\n[ISO] Extracted ELF: {elfPath}");

        // =========================
        // 3. LOAD ELF
        // =========================
        var elfLoader = new ElfLoader(elfPath);
        var image = elfLoader.Load();

        Console.WriteLine("[ELF] Loaded successfully");
        Console.WriteLine($"[ELF] Entry Point: 0x{image.EntryPoint:X8}");

        // =========================
        // 4. MAP INTO PS2 RAM
        // =========================
        var ram = new PS2RAM();
        var mapper = new PS2ElfMapper(ram);

        mapper.Map(image);

        Console.WriteLine($"[PS2] Entry Instruction: 0x{ram.Read32(image.EntryPoint):X8}");

        // =========================
        // 5. FUNCTION DISCOVERY (SCAN STAGE)
        // =========================
        var funcScanner = new MIPSFunctionScanner(ram);

        var functions = funcScanner.Scan(image.EntryPoint);

        Console.WriteLine($"\n[FUNC] Functions found: {functions.Count}");

        foreach (var f in functions)
        {
            Console.WriteLine($"[FUNC] 0x{f:X8}");
        }

        // =========================
        // 6. BASIC BLOCK / CFG STAGE (NEW)
        // =========================
        var cfgBuilder = new MIPSBasicBlockBuilder(ram);
        var functionBlocks = new Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>>();

        if (!functions.Contains(image.EntryPoint))
            functions.Insert(0, image.EntryPoint);

        foreach (var func in functions)
        {
            var funcBlocks = cfgBuilder.Build(func);
            functionBlocks[func] = funcBlocks;

            Console.WriteLine($"\n[CFG] Function 0x{func:X8} Basic Blocks: {funcBlocks.Count}");

            foreach (var block in funcBlocks)
            {
                Console.WriteLine($"\n[BLOCK] 0x{block.StartAddress:X8}");

                foreach (var instr in block.Instructions)
                {
                    Console.WriteLine($"    0x{instr:X8}");
                }
            }
        }

        // =========================
        // 7. DECOMPILATION / PSEUDOCODE STAGE
        // =========================
        var decompiler = new MIPSDecompiler(ram);
        var pseudocode = decompiler.DecompileFunction(image.EntryPoint, functionBlocks[image.EntryPoint]);

        Console.WriteLine("\n[DECOMP] Generated pseudocode:");
        Console.WriteLine(pseudocode);

        var csCode = decompiler.GenerateCSharpModule("DecompiledGame", image.EntryPoint, functionBlocks);
        var outputDir = Path.Combine(Path.GetDirectoryName(_isoPath) ?? ".", "decompiled");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "DecompiledGame.cs");
        File.WriteAllText(outputPath, csCode);

        Console.WriteLine($"\n[C#] Generated C# output: {outputPath}");
        Console.WriteLine("\n[MAIN] Pipeline complete.");
        Console.WriteLine("[MAIN] Next stage: MIPS → C# refinement");
    }
}
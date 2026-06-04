using System;
using System.Collections.Generic;
using System.Linq;

public class LoopDetector
{
    public class Loop
    {
        public uint Header;
        public HashSet<uint> Blocks = new();
        public HashSet<uint> Exits = new();

        public override string ToString()
        {
            return $"Loop(header=0x{Header:X8}, blocks={Blocks.Count})";
        }
    }

    // =========================================================
    // ENTRY POINT
    // =========================================================
    public List<Loop> DetectLoops(List<MIPSBasicBlockBuilder.BasicBlock> blocks)
    {
        var cfg = BuildCFG(blocks);
        var loops = new List<Loop>();

        foreach (var block in blocks)
        {
            foreach (var exit in block.Exits ?? new List<uint>())
            {
                // BACK EDGE RULE:
                // if target address is <= current block, likely a loop
                if (exit <= block.StartAddress)
                {
                    var loop = BuildNaturalLoop(cfg, block.StartAddress, exit);

                    if (loop != null)
                        loops.Add(loop);
                }
            }
        }

        return DeduplicateLoops(loops);
    }

    // =========================================================
    // BUILD SIMPLE CFG MAP
    // =========================================================
    private Dictionary<uint, MIPSBasicBlockBuilder.BasicBlock> BuildCFG(
        List<MIPSBasicBlockBuilder.BasicBlock> blocks)
    {
        return blocks.ToDictionary(b => b.StartAddress, b => b);
    }

    // =========================================================
    // NATURAL LOOP CONSTRUCTION
    // =========================================================
    private Loop BuildNaturalLoop(
        Dictionary<uint, MIPSBasicBlockBuilder.BasicBlock> cfg,
        uint tail,
        uint header)
    {
        if (!cfg.ContainsKey(header))
            return null;

        var loop = new Loop { Header = header };

        var worklist = new Stack<uint>();
        var visited = new HashSet<uint>();

        worklist.Push(tail);
        worklist.Push(header);

        while (worklist.Count > 0)
        {
            var node = worklist.Pop();

            if (!visited.Add(node))
                continue;

            loop.Blocks.Add(node);

            if (!cfg.TryGetValue(node, out var block))
                continue;

            foreach (var exit in block.Exits ?? new List<uint>())
            {
                // Stop escaping loop unless it's header path
                if (exit == header || exit <= header)
                {
                    worklist.Push(exit);
                }
                else
                {
                    loop.Exits.Add(exit);
                }
            }
        }

        return loop;
    }

    // =========================================================
    // REMOVE DUPLICATES / MERGE OVERLAPS
    // =========================================================
    private List<Loop> DeduplicateLoops(List<Loop> loops)
    {
        var result = new List<Loop>();

        foreach (var loop in loops.OrderBy(l => l.Header))
        {
            if (result.Any(l => l.Header == loop.Header))
                continue;

            result.Add(loop);
        }

        return result;
    }

    // =========================================================
    // DEBUG OUTPUT
    // =========================================================
    public void Dump(List<Loop> loops)
    {
        Console.WriteLine("=== LOOP DETECTION RESULT ===");

        foreach (var loop in loops)
        {
            Console.WriteLine(loop);

            Console.WriteLine("  Blocks:");
            foreach (var b in loop.Blocks.OrderBy(x => x))
                Console.WriteLine($"    0x{b:X8}");

            Console.WriteLine("  Exits:");
            foreach (var e in loop.Exits)
                Console.WriteLine($"    0x{e:X8}");
        }
    }
}
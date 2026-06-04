using System;
using System.Collections.Generic;
using System.Linq;

public class MIPSDecompiler
{
    private readonly PS2RAM _ram;

    public MIPSDecompiler(PS2RAM ram)
    {
        _ram = ram;
    }

    // =========================================================
    // ENTRY: FUNCTION DECOMPILATION (PSEUDOCODE MODE)
    // =========================================================
    public string DecompileFunction(uint entryPoint, List<MIPSBasicBlockBuilder.BasicBlock> blocks)
    {
        var builder = new PseudocodeBuilder();
        builder.StartFunction(entryPoint);

        foreach (var block in blocks.OrderBy(b => b.StartAddress))
        {
            builder.StartBlock(block.StartAddress);

            for (int i = 0; i < block.Instructions.Count; i++)
            {
                uint raw = block.Instructions[i];

                var instr = MIPSDisassembler.Decode(
                    block.StartAddress + (uint)(i * 4),
                    raw
                );

                builder.EmitInstruction(instr);
            }

            builder.SetExits(block.Exits);
        }

        builder.EndFunction();
        return builder.BuildText();
    }

    // =========================================================
    // ENTRY: C# MODULE GENERATION (UNIFIED PIPELINE VERSION)
    // =========================================================
    public string GenerateCSharpModule(
        string className,
        uint entryPoint,
        Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> functionBlocks,
        FunctionDiscoveryEngine? discoveryEngine = null)
    {
        Console.WriteLine("=== FUNCTION PIPELINE DEBUG ===");
        Console.WriteLine($"Raw functions: {functionBlocks.Count}");

        // =====================================================
        // OPTIONAL: UNIFIED DISCOVERY FILTER
        // =====================================================
        if (discoveryEngine != null)
        {
            var discovered = discoveryEngine.DiscoverFunctions();
            Console.WriteLine($"Discovered functions: {discovered.Count}");

            functionBlocks = functionBlocks
                .Where(f => discovered.Contains(f.Key))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        var lines = new List<string>
        {
            "using System;",
            "",
            $"public static class {className}",
            "{",
            "    public static int zero, at, v0, v1, a0, a1, a2, a3, t0, t1, t2, t3, t4, t5, t6, t7, s0, s1, s2, s3, s4, s5, s6, s7, t8, t9, gp, sp, fp, ra;",
            "    public static int[] MEM = new int[0x02000000 / 4];",
            "",
            "    public static int LoadWord(int addr) => MEM[(addr & 0x01FFFFFF) >> 2];",
            "    public static void StoreWord(int addr, int val) => MEM[(addr & 0x01FFFFFF) >> 2] = val;",
            ""
        };

        foreach (var fn in functionBlocks.OrderBy(k => k.Key))
        {
            var body = DecompileFunctionToCSharp(fn.Key, fn.Value);
            lines.AddRange(body.Split(Environment.NewLine));
            lines.Add("");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    // =========================================================
    // FUNCTION → C# TRANSLATION
    // =========================================================
    public string DecompileFunctionToCSharp(
        uint entryPoint,
        List<MIPSBasicBlockBuilder.BasicBlock> blocks)
    {
        var builder = new CSharpCodeBuilder();
        builder.StartFunction(entryPoint);

        foreach (var block in blocks.OrderBy(b => b.StartAddress))
        {
            builder.StartBlock(block.StartAddress);

            foreach (var raw in block.Instructions)
            {
                var instr = MIPSDisassembler.Decode(block.StartAddress, raw);

                if (instr.IsSyscall)
                {
                    var semantic = MIPSSemanticLayer.Analyze(instr.SyscallCode);
                    builder.EmitSyscall(instr.SyscallCode, semantic);
                    continue;
                }

                builder.EmitInstruction(instr);
            }

            builder.SetExits(block.Exits);
        }

        builder.EndFunction();
        return builder.BuildText();
    }

    // =========================================================
    // C# CODE BUILDER
    // =========================================================
    private class CSharpCodeBuilder
    {
        private readonly List<string> _lines = new();
        private readonly Dictionary<uint, string> _labels = new();
        private readonly HashSet<string> _emittedLabels = new();

        public void StartFunction(uint entryPoint)
        {
            _lines.Add($"    public static void func_{entryPoint:X8}()");
            _lines.Add("    {");
        }

        public void EndFunction()
        {
            _lines.Add("    }");
        }

        public void StartBlock(uint addr)
        {
            EmitLabel(addr);
        }

        public void EmitInstruction(MIPSInstruction instr)
        {
            EmitLabel(instr.Address);
            _lines.Add($"        // {instr}");
        }

        public void EmitSyscall(int id, MIPSSemanticLayer.SyscallSemantic semantic)
        {
            _lines.Add($"        // SYSCALL {id:X}: {semantic.Name}");
            _lines.Add($"        {semantic.Emit}");
        }

        public void SetExits(List<uint> exits)
        {
            if (exits == null) return;

            foreach (var e in exits)
                EmitLabel(e);
        }

        public string BuildText()
            => string.Join(Environment.NewLine, _lines);

        private string GetLabel(uint addr)
        {
            if (!_labels.TryGetValue(addr, out var label))
            {
                label = $"loc_{addr:X8}";
                _labels[addr] = label;
            }
            return label;
        }

        private void EmitLabel(uint addr)
        {
            var label = GetLabel(addr);

            if (_emittedLabels.Add(label))
                _lines.Add($"    {label}:");
        }
    }

    // =========================================================
    // PSEUDOCODE BUILDER
    // =========================================================
    private class PseudocodeBuilder
    {
        private readonly List<string> _lines = new();
        private readonly Dictionary<uint, string> _labels = new();

        public void StartFunction(uint entry)
        {
            _lines.Add($"void func_{entry:X8}()");
            _lines.Add("{");
        }

        public void EndFunction()
        {
            _lines.Add("}");
        }

        public void StartBlock(uint addr)
        {
            _lines.Add($"    {GetLabel(addr)}:");
        }

        public void EmitInstruction(MIPSInstruction instr)
        {
            _lines.Add($"    // {instr}");
        }

        public void SetExits(List<uint> exits)
        {
            if (exits == null) return;

            foreach (var e in exits)
                GetLabel(e);
        }

        public string BuildText()
            => string.Join(Environment.NewLine, _lines);

        private string GetLabel(uint addr)
        {
            if (!_labels.TryGetValue(addr, out var label))
            {
                label = $"loc_{addr:X8}";
                _labels[addr] = label;
            }
            return label;
        }
    }
}
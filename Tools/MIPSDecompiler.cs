using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

            foreach (var instr in block.Instructions)
        {
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

            foreach (var instr in block.Instructions)
{
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

    public void WriteCModule(
        string moduleName,
        uint entryPoint,
        Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> functionBlocks,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var cPath = Path.Combine(outputDirectory, moduleName + ".c");
        var launcherPath = Path.Combine(outputDirectory, moduleName + "_launcher.c");

        var cSource = GenerateCModule(moduleName, entryPoint, functionBlocks);
        var launcherSource = GenerateCLauncher(moduleName, entryPoint);

        File.WriteAllText(cPath, cSource);
        File.WriteAllText(launcherPath, launcherSource);

        TryCompileAndRun(moduleName, outputDirectory);
    }

    public string GenerateCModule(
        string moduleName,
        uint entryPoint,
        Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> functionBlocks)
    {
        var lines = new List<string>
        {
            "#include <stdint.h>",
            "#include <stdio.h>",
            "#include <string.h>",
            "",
            "typedef int32_t s32;",
            "typedef uint32_t u32;",
            "typedef uint8_t u8;",
            "",
            "#define MEM_SIZE 0x02000000",
            "#define MEM_MASK 0x01FFFFFF",
            "",
            "static u32 gpr[32];",
            "static u8 mem[MEM_SIZE];",
            "",
            "static u32 load_u32(u32 addr)",
            "{",
            "    u32 value = 0;",
            "    memcpy(&value, mem + (addr & MEM_MASK), sizeof(value));",
            "    return value;",
            "}",
            "",
            "static void store_u32(u32 addr, u32 value)",
            "{",
            "    memcpy(mem + (addr & MEM_MASK), &value, sizeof(value));",
            "}",
            ""
        };

        foreach (var fn in functionBlocks.OrderBy(k => k.Key))
        {
            lines.Add($"void func_{fn.Key:X8}(void)");
            lines.Add("{");

            var labels = new HashSet<uint>();
            foreach (var block in fn.Value)
            {
                labels.Add(block.StartAddress);
                foreach (var exit in block.Exits)
                    labels.Add(exit);
            }

            foreach (var labelAddr in labels.OrderBy(a => a))
            {
                if (labelAddr == fn.Key)
                    continue;

                lines.Add($"    L_{labelAddr:X8}:");
            }

            foreach (var block in fn.Value.OrderBy(b => b.StartAddress))
            {
                lines.Add($"    L_{block.StartAddress:X8}:");
                foreach (var instr in block.Instructions)
                {
                    var line = TranslateInstructionToC(instr);
                    if (!string.IsNullOrWhiteSpace(line))
                        lines.Add("    " + line);
                }

                if (block.Exits.Count > 0)
                {
                    foreach (var exit in block.Exits.Distinct())
                    {
                        lines.Add($"    /* fallthrough/branch target: 0x{exit:X8} */");
                    }
                }
            }

            lines.Add("}");
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public string GenerateCLauncher(string moduleName, uint entryPoint)
    {
        return $"#include \"{moduleName}.c\"\n\nint main(void) {{\n    func_{entryPoint:X8}();\n    return 0;\n}}\n";
    }

    private string TranslateInstructionToC(MIPSInstruction instr)
    {
        if (instr.Mnemonic == "addiu" || instr.Mnemonic == "addi")
            return $"gpr[{instr.Rt}] = (u32)((s32)gpr[{instr.Rs}] + {instr.Immediate});";

        if (instr.Mnemonic == "addu" || instr.Mnemonic == "add")
            return $"gpr[{instr.Rd}] = gpr[{instr.Rs}] + gpr[{instr.Rt}];";

        if (instr.Mnemonic == "subu" || instr.Mnemonic == "sub")
            return $"gpr[{instr.Rd}] = gpr[{instr.Rs}] - gpr[{instr.Rt}];";

        if (instr.Mnemonic == "ori")
            return $"gpr[{instr.Rt}] = gpr[{instr.Rs}] | 0x{instr.UImmediate:X};";

        if (instr.Mnemonic == "andi")
            return $"gpr[{instr.Rt}] = gpr[{instr.Rs}] & 0x{instr.UImmediate:X};";

        if (instr.Mnemonic == "lui")
            return $"gpr[{instr.Rt}] = 0x{instr.UImmediate:X} << 16;";

        if (instr.Mnemonic == "lw")
            return $"gpr[{instr.Rt}] = load_u32(gpr[{instr.Rs}] + {instr.Immediate});";

        if (instr.Mnemonic == "sw")
            return $"store_u32(gpr[{instr.Rs}] + {instr.Immediate}, gpr[{instr.Rt}]);";

        if (instr.Mnemonic == "beq")
            return $"if (gpr[{instr.Rs}] == gpr[{instr.Rt}]) goto L_{instr.BranchTarget:X8};";

        if (instr.Mnemonic == "bne")
            return $"if (gpr[{instr.Rs}] != gpr[{instr.Rt}]) goto L_{instr.BranchTarget:X8};";

        if (instr.Mnemonic == "bltz")
            return $"if ((s32)gpr[{instr.Rs}] < 0) goto L_{instr.BranchTarget:X8};";

        if (instr.Mnemonic == "bgez")
            return $"if ((s32)gpr[{instr.Rs}] >= 0) goto L_{instr.BranchTarget:X8};";

        if (instr.Mnemonic == "jal")
            return $"gpr[31] = 0; goto func_{instr.TargetAddress:X8};";

        if (instr.Mnemonic == "jalr")
            return $"gpr[31] = 0; return;";

        if (instr.Mnemonic == "jr")
            return "return;";

        if (instr.IsSyscall)
            return $"/* syscall {instr.SyscallCode} */";

        return $"/* {instr.Mnemonic} {instr.Operands} */";
    }

    private void TryCompileAndRun(string moduleName, string outputDirectory)
    {
        try
        {
            string compiler = "cc";
            if (!File.Exists("/usr/bin/cc") && !File.Exists("/usr/local/bin/cc") && !File.Exists("/opt/homebrew/bin/cc"))
                compiler = "clang";

            var sourcePath = Path.Combine(outputDirectory, moduleName + "_launcher.c");
            var exePath = Path.Combine(outputDirectory, moduleName);

            var psi = new ProcessStartInfo
            {
                FileName = compiler,
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add(sourcePath);
            psi.ArgumentList.Add("-std=c99");
            psi.ArgumentList.Add("-O2");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(exePath);

            using var compile = Process.Start(psi);
            if (compile == null)
                return;

            var compileOutput = compile.StandardOutput.ReadToEnd() + compile.StandardError.ReadToEnd();
            compile.WaitForExit();

            if (compile.ExitCode != 0)
            {
                Console.WriteLine($"[C] Compile failed: {compileOutput}");
                return;
            }

            var runPsi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var run = Process.Start(runPsi);
            if (run == null)
                return;

            var runOutput = run.StandardOutput.ReadToEnd() + run.StandardError.ReadToEnd();
            run.WaitForExit();
            Console.WriteLine($"[C] Executed: {runOutput}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[C] Launch failed: {ex.Message}");
        }
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
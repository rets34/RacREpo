using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Collections.Generic;

public class Game
{
    private IWindow? window;
    private GL? gl;
    private RenderManager renderer = new();

    private volatile bool isExtracting = false;
    private string extractionStatus = "Drop an ISO onto the window or press E to extract";
    private StringBuilder extractionLog = new();
    private readonly object _logLock = new();

    private string? extractionIsoPath;
    private string? decompiledGamePath;

    private volatile int functionsFound = -1;
    private volatile int unknownInstructionCount = -1;
    private volatile int compileErrorCount = -1;
    private volatile int compileWarningCount = -1;
    private volatile bool generatedStubReady = false;
    private string executionResult = "NOT RUN";

    private double accumulatedTime;
    private const double TargetFrameTime = 1.0 / 60.0;

    public void Run()
    {
        var options = WindowOptions.Default;

        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.Default,
            new APIVersion(3, 3)
        );

        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RaC Reconstruction";

        window = Window.Create(options);

        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.FileDrop += OnFileDrop;

        window.Run();
    }

    private void OnLoad()
    {
        gl = GL.GetApi(window!);
        renderer.Initialize(gl);

        try
        {
            var view = Window.GetView(null);
            var input = view.CreateInput();

            var keyboard = input.Keyboards.FirstOrDefault();
            var mouse = input.Mice.FirstOrDefault();

            if (keyboard != null)
                keyboard.KeyDown += OnKeyboardKeyDown;

            if (mouse != null)
                mouse.Click += OnMouseClick;
        }
        catch (Exception ex)
        {
            AppendLog("[UI] Input initialization failed: " + ex.Message);
            extractionStatus = "Input initialization failed";
        }
    }

    private void OnUpdate(double dt)
    {
        accumulatedTime += dt;

        while (accumulatedTime >= TargetFrameTime)
        {
            accumulatedTime -= TargetFrameTime;
            RunPS2Frame();
        }
    }

    private void RunPS2Frame()
    {
        FUN_00226e08();
        FUN_00208840();
    }

    private void OnRender(double dt)
    {
        renderer.SetExtractionStatus(extractionStatus, isExtracting);

        renderer.SetDecompSummary(
            functionsFound,
            unknownInstructionCount,
            compileErrorCount,
            compileWarningCount,
            generatedStubReady,
            executionResult
        );

        lock (_logLock)
        {
            renderer.SetExtractionLog(extractionLog.ToString());
        }

        renderer.Render();
    }

    private void OnKeyboardKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        if (key == Key.E)
            StartExtraction();
    }

    private void OnMouseClick(IMouse mouse, MouseButton button, Vector2 position)
    {
        if (button != MouseButton.Left) return;

        if (renderer.CheckButtonClick(position.X, position.Y, out string label))
        {
            if (label == "LAUNCH")
                RunGeneratedStub();
        }
    }

    private void OnFileDrop(string[] paths)
    {
        if (paths == null || paths.Length == 0) return;

        extractionIsoPath = paths[0];
        extractionStatus = $"Ready - press E to extract {Path.GetFileName(extractionIsoPath)}";
        AppendLog("[UI] ISO selected: " + extractionIsoPath);
    }

    // =========================================================
    // LOGGING
    // =========================================================
    private void AppendLog(string msg)
    {
        lock (_logLock)
        {
            extractionLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }

        Console.WriteLine(msg);
    }

    // =========================================================
    // EXTRACTION PIPELINE
    // =========================================================
    private void StartExtraction(string? isoOverride = null)
    {
        if (isExtracting) return;

        isExtracting = true;
        extractionStatus = "Starting extraction...";
        extractionLog.Clear();

        functionsFound = -1;
        unknownInstructionCount = -1;
        compileErrorCount = -1;
        compileWarningCount = -1;
        generatedStubReady = false;
        executionResult = "NOT RUN";

        renderer.SetGameReady(false);

        Task.Run(() =>
        {
            try
            {
                var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
                var isoPath = isoOverride ?? extractionIsoPath ?? Path.Combine(projectRoot, "RAC.iso");

                AppendLog("[PIPE] Using ISO: " + isoPath);

                if (!File.Exists(isoPath))
                {
                    extractionStatus = "ISO not found";
                    AppendLog("[ERROR] ISO not found");
                    return;
                }

                RunFunctionExpansionPipeline(isoPath);

                decompiledGamePath = Path.Combine(
                    Path.GetDirectoryName(isoPath) ?? ".",
                    "decompiled",
                    "DecompiledGame.cs"
                );

                unknownInstructionCount = CountUnknownInstructions(decompiledGamePath);
                generatedStubReady = File.Exists(decompiledGamePath);

                extractionStatus = generatedStubReady
                    ? "Partial decompilation ready"
                    : "Decompiler output missing";
            }
            catch (Exception ex)
            {
                extractionStatus = "Extraction error";
                AppendLog("[FATAL] " + ex);
            }
            finally
            {
                isExtracting = false;
            }
        });
    }

    // =========================================================
    // FUNCTION PIPELINE (UNIFIED ORCHESTRATOR ONLY)
    // =========================================================
    private void RunFunctionExpansionPipeline(string isoPath)
{
    try
    {
        AppendLog("=== PIPELINE START ===");

        // ISO → Boot ELF
        var iso = new ISO9660Reader(isoPath);

        iso.Parse();

        string elfPath = iso.ExtractBootElf();

        if (string.IsNullOrWhiteSpace(elfPath))
        {
            AppendLog("[PIPE] Failed to resolve boot ELF");
            return;
        }

        AppendLog($"[PIPE] Boot ELF: {elfPath}");

        // ELF → RAM
        var elf = new ElfLoader(elfPath).Load();

        var ram = new PS2RAM();

        foreach (var seg in elf.Segments)
            ram.Write(seg.VAddr, seg.Data);

        AppendLog("[PIPE] RAM loaded");

        // CFG build
        var builder = new MIPSBasicBlockBuilder(ram);

        var blocks = builder.Build(elf.EntryPoint);

        AppendLog($"[PIPE] CFG blocks: {blocks.Count}");

        // Unified analysis
        var orchestrator = new DecompilationOrchestrator(ram);

        var functionMap = new Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>>
        {
            { elf.EntryPoint, blocks }
        };

        string output = orchestrator.Run(functionMap, elf.EntryPoint);

        functionsFound = functionMap.Count;

        AppendLog("=== PIPELINE END ===");
    }
    catch (Exception ex)
    {
        AppendLog("[PIPELINE ERROR] " + ex);
    }
}

    // =========================================================
    // RUNTIME COMPILATION
    // =========================================================
    private void RunGeneratedStub()
    {
        if (string.IsNullOrEmpty(decompiledGamePath) || !File.Exists(decompiledGamePath))
            return;

        Task.Run(() =>
        {
            try
            {
                string source = File.ReadAllText(decompiledGamePath);
                var tree = CSharpSyntaxTree.ParseText(source);

                var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_TASSEMBLIES"))?
                    .Split(Path.PathSeparator)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => MetadataReference.CreateFromFile(p))
                    .ToList();

                if (refs == null || refs.Count == 0)
                {
                    executionResult = "NO REFERENCES";
                    AppendLog("[RUNTIME] No references found");
                    return;
                }

                var compilation = CSharpCompilation.Create("DecompiledGameDynamic")
                    .AddReferences(refs)
                    .AddSyntaxTrees(tree)
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    executionResult = "COMPILE FAILED";
                    AppendLog("[RUNTIME] Compile failed");
                    return;
                }

                ms.Seek(0, SeekOrigin.Begin);
                var asm = Assembly.Load(ms.ToArray());

                var type = asm.GetType("DecompiledGame");
                var method = type?.GetMethod("Execute");

                method?.Invoke(null, null);

                executionResult = "RUN COMPLETE";
                AppendLog("[RUNTIME] Execution complete");
            }
            catch (Exception ex)
            {
                executionResult = "RUNTIME ERROR";
                AppendLog("[RUNTIME ERROR] " + ex);
            }
        });
    }

    private static int CountUnknownInstructions(string path)
    {
        if (!File.Exists(path)) return -1;
        return File.ReadLines(path).Count(l => l.Contains(": unknown "));
    }

    // =========================================================
    // PS2 STATE
    // =========================================================
    private int DAT_0015ee24;
    private int DAT_0015f5ec;
    private int DAT_0015ed80 = 1;

    private void FUN_00226e08()
    {
        int iVar1 = DAT_0015ee24;

        if (DAT_0015f5ec == 0)
        {
            iVar1++;

            if (iVar1 % 0x32 == 0 && DAT_0015ed80 != 0)
                iVar1 += 0xB;
        }

        DAT_0015ee24 = iVar1;
    }

    private void FUN_00208840() { }
}
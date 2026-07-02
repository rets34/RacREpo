using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;

public class Game //generic name? for now
{
    private IWindow? window;
    private GL? gl;
    private RenderManager renderer = new();

    private bool rendererInit = false;

    private volatile bool isExtracting;
    private string extractionStatus = "Drop ISO or press E";
    private StringBuilder extractionLog = new();
    private readonly object _logLock = new();

    private string? isoPath;
    private string? decompiledPath;

    private int functionsFound = -1;
    private int unknownInstructionCount = -1;
    private int compileErrorCount = -1;
    private int compileWarningCount = -1;
    private bool stubReady = false;
    private string executionResult = "NOT RUN";

    private double time;
    private const double frame = 1.0 / 60.0;

    public Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>> Functions = new();

    public void Run()
    {
        var options = WindowOptions.Default;

        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.Default,
            new APIVersion(3, 3));

        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "RaC Decompiler";
        
        options.IsEventDriven = false; 
        options.WindowState = WindowState.Normal;

        window = Window.Create(options);

        window.Load += Load;
        window.Update += Update;
        window.Render += Render;
        window.FileDrop += Drop;

        window.Run();
    }

    private void Load()
    {
        Log("[SYSTEM] Window created - waiting for GL init in Render()");
    }

    // =========================================================
    // UPDATE LOOP
    // =========================================================
    private void Update(double dt)
    {
        time += dt;
        while (time >= frame)
        {
            time -= frame;

            try
            {
                FUN_00226e08(); //UpdateGlobalFrameCounter
                FUN_00208840(); //UpdateCurrentGameState
            }
            catch (Exception ex)
            {
                Log($"🔥 [UPDATE ERROR] Loop calculation failure: {ex.Message}");
                time = 0;
                break;
            }
        }
    }

    // =========================================================
    // RENDER (SAFE GL INIT HERE)
    // =========================================================
    private void Render(double dt)
    {
        if (!rendererInit)
        {
            gl = GL.GetApi(window!);
            renderer.Initialize(gl);
            rendererInit = true;

            Log("[GL] Renderer initialized safely on active context");
        }

        renderer.SetExtractionStatus(extractionStatus, isExtracting);

        renderer.SetDecompSummary(
            functionsFound,
            unknownInstructionCount,
            compileErrorCount,
            compileWarningCount,
            stubReady,
            executionResult
        );

        lock (_logLock)
            renderer.SetExtractionLog(extractionLog.ToString());

        renderer.Render();
    }

    // =========================================================
    // FILE DROP
    // =========================================================
    private void Drop(string[] files)
    {
        if (files.Length == 0) return;

        isoPath = files[0];
        extractionStatus = "Ready: " + Path.GetFileName(isoPath);

        Log("[INPUT] ISO dropped: " + isoPath);

        StartExtraction();
    }

    // =========================================================
    // PIPELINE
    // =========================================================
    private void StartExtraction()
    {
        if (isExtracting) return;

        isExtracting = true;
        extractionLog.Clear();
        extractionStatus = "RUNNING";

        Task.Run(() =>
        {
            try
            {
                Log("=== PIPELINE START ===");

                RunPipeline(isoPath ?? "RAC.iso");

                Log("=== PIPELINE SUCCESS ===");
            }
            catch (Exception ex)
            {
                Log("🔥 PIPELINE CRASH (FULL TRACE):");
                Log(ex.ToString());
                extractionStatus = "FAILED";
            }
            finally
            {
                isExtracting = false;
            }
        });
    }

    // =========================================================
    // PIPELINE CORE
    // =========================================================
    private void RunPipeline(string iso)
    {
        Log("[1] ISO Reader Init");
        var isoReader = new ISO9660Reader(iso);

        Log("[2] ISO Parse");
        isoReader.Parse();

        Log("[3] Extract ELF");
        string elfPath = isoReader.ExtractBootElf();

        Log("[4] ELF Load");
        var elf = new ElfLoader(elfPath).Load();

        Log("[5] RAM Init");
        var ram = new PS2RAM();

        Log("[6] Writing segments: " + elf.Segments.Count);

        foreach (var seg in elf.Segments)
        {
            Log($"    SEG VAddr=0x{seg.VAddr:X8}, Size={seg.Data.Length}");

            if (seg.Data == null || seg.Data.Length == 0)
            {
                Log("Empty segment skipped");
                continue;
            }

            // Evaluate both the start location and trailing size.
            // PS2 Main Memory (EE RAM) maps up to exactly 32MB (0x02000000 bytes) Max.
            ulong endAddress = (ulong)seg.VAddr + (ulong)seg.Data.Length;

            if (seg.VAddr >= 0x02000000 || endAddress > 0x02000000)
            {
                Log($"Skipping out-of-bounds segment: VAddr=0x{seg.VAddr:X8}, Size={seg.Data.Length} (Ends at 0x{endAddress:X8})");
                continue;
            }

            ram.Write(seg.VAddr, seg.Data);
        }

        Log("[7] Seed Discovery");
        var seeds = FunctionSeedResolver.DiscoverSeeds(elfPath, elf.EntryPoint, ram, iso);
        Log("[SEED] Seeds: " + seeds.Count);

        Log("[8] CFG Build");
        var cfg = new MIPSBasicBlockBuilder(ram);
        var expanded = new Dictionary<uint, List<MIPSBasicBlockBuilder.BasicBlock>>();

        foreach (var seed in seeds)
        {
            try
            {
                var blocks = cfg.Build(seed, 200);
                if (blocks != null && blocks.Count > 0)
                    expanded[seed] = blocks;
            }
            catch (Exception ex)
            {
                Log($"[SEED] Failed for 0x{seed:X8}: {ex.Message}");
            }
        }

        Log("[9] Function Expansion");
        var expander = new MIPSFunctionExpander(ram, cfg);
        var expandedFromCalls = expander.Build(elf.EntryPoint);

        foreach (var kvp in expandedFromCalls)
        {
            if (!expanded.ContainsKey(kvp.Key))
                expanded[kvp.Key] = kvp.Value;
        }

        Log("[PIPE] Function map size: " + expanded.Count);
        Functions = expanded;
        functionsFound = Functions.Count;

        var decompiler = new MIPSDecompiler(ram);
        var exportDir = Path.Combine(AppContext.BaseDirectory, "decompiled");
        decompiler.WriteCModule("DecompiledGame", elf.EntryPoint, Functions, exportDir);
        Log("[C] Wrote C output to: " + exportDir);

        Log("[PIPE] Functions: " + functionsFound);

        renderer.SetGameReady(true);
        executionResult = "C EXPORT READY";

        Log("=== PIPELINE END ===");
    }

    // =========================================================
    // LOGGING
    // =========================================================
    private void Log(string msg)
    {
        lock (_logLock)
            extractionLog.AppendLine(msg);

        Console.WriteLine(msg);
    }

    // =========================================================
    // SIM HOOKS
    // =========================================================
    private int DAT_0015ee24;
    private int DAT_0015f5ec;
    private int DAT_0015ed80 = 1;
    private void FUN_00226e08() => DAT_0015ee24++; //UpdateGlobalFrameCounter
    private void FUN_00208840() { } //UpdateCurrentGameState
}

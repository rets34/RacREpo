using Silk.NET.OpenGL;
using System;
using System.Numerics;
using System.Collections.Generic;

public unsafe class RenderManager
{
    private GL gl = null!;

    private uint uiVao;
    private uint uiVbo;
    private uint uiProgram;
    private int uScreenLoc;

    private string extractionStatus = "Drop ISO or press E";
    private string extractionLog = "";
    private bool isExtracting;
    private bool gameReady;

    private int functionsFound = -1;
    private int unknownInstructionCount = -1;
    private int compileErrorCount = -1;
    private int compileWarningCount = -1;

    private bool stubReady;
    private string executionResult = "NOT RUN";

    private readonly List<(string label, float x, float y, float w, float h)> buttons = new();

    // =========================================================
    // INIT
    // =========================================================
    public void Initialize(GL graphics)
    {
        gl = graphics;

        gl.Viewport(0, 0, 1280, 720);

        CreateUiProgram();

        uiVao = gl.GenVertexArray();
        uiVbo = gl.GenBuffer();

        gl.BindVertexArray(uiVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, uiVbo);

        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)(5 * sizeof(float)), (nint)0);
        gl.EnableVertexAttribArray(0);

        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)(5 * sizeof(float)), (nint)(2 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
    }

    // =========================================================
    // MAIN RENDER
    // =========================================================
    public void Render()
    {
        gl.ClearColor(0.06f, 0.08f, 0.12f, 1f);
        gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        gl.UseProgram(uiProgram);
        gl.Uniform2(uScreenLoc, 1280f, 720f);

        buttons.Clear();

        DrawText("RAC DECOMPILER", 20, 15, 2.5f, new Vector3(0.95f));

        DrawText(isExtracting
            ? "EXTRACTION IN PROGRESS..."
            : "STATUS: " + extractionStatus,
            20, 70, 1.6f,
            isExtracting ? new Vector3(1f, 0.85f, 0.3f) : new Vector3(0.7f, 1f, 0.7f));

        DrawText("DROP ISO OR PRESS E", 20, 120, 1.4f, new Vector3(0.8f));

        DrawText("FUNCTIONS " + Format(functionsFound), 20, 250, 1.2f, new Vector3(0.8f, 0.9f, 1f));
        DrawText("UNKNOWN OPS " + Format(unknownInstructionCount), 20, 280, 1.2f, new Vector3(1f, 0.75f, 0.5f));
        DrawText("COMPILE ERR " + Format(compileErrorCount), 20, 310, 1.2f, new Vector3(1f, 0.5f, 0.5f));
        DrawText("COMPILE WARN " + Format(compileWarningCount), 20, 340, 1.2f, new Vector3(1f, 0.9f, 0.5f));

        DrawText("STUB " + (stubReady ? "READY" : "NOT READY"), 20, 370, 1.2f,
            stubReady ? new Vector3(0.5f, 1f, 0.6f) : new Vector3(0.9f, 0.6f, 0.6f));

        DrawText("RUN: " + executionResult, 20, 400, 1.2f, new Vector3(0.8f, 0.9f, 1f));

        if (!string.IsNullOrEmpty(extractionLog))
        {
            var last = extractionLog.Split('\n');
            for (int i = last.Length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(last[i]))
                {
                    DrawText("LOG: " + last[i], 20, 680, 1f, new Vector3(0.6f, 0.8f, 0.9f));
                    break;
                }
            }
        }
    }

    // =========================================================
    // STATE SETTERS
    // =========================================================
    public void SetExtractionStatus(string status, bool extracting)
    {
        extractionStatus = status ?? "Ready";
        isExtracting = extracting;
    }

    public void SetExtractionLog(string log) => extractionLog = log ?? "";

    public void SetGameReady(bool ready) => gameReady = ready;

    public void SetDecompSummary(int functions, int unknownOps, int compileErrors,
        int compileWarnings, bool stubReadyState, string runResult)
    {
        functionsFound = functions;
        unknownInstructionCount = unknownOps;
        compileErrorCount = compileErrors;
        compileWarningCount = compileWarnings;
        stubReady = stubReadyState;
        executionResult = string.IsNullOrWhiteSpace(runResult) ? "NOT RUN" : runResult;
    }

    // =========================================================
    // SAFE SHADER CREATION (CRASH FIX)
    // =========================================================
    private void CreateUiProgram()
    {
        string vs = @"#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec3 aColor;
uniform vec2 uScreen;
out vec3 vColor;

void main(){
    vec2 p = aPos / uScreen * 2.0 - 1.0;
    p.y = -p.y;
    gl_Position = vec4(p,0,1);
    vColor = aColor;
}";

        string fs = @"#version 330 core
in vec3 vColor;
out vec4 FragColor;

void main(){
    FragColor = vec4(vColor,1);
}";

        uint v = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(v, vs);
        gl.CompileShader(v);

        uint f = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(f, fs);
        gl.CompileShader(f);

        uiProgram = gl.CreateProgram();
        gl.AttachShader(uiProgram, v);
        gl.AttachShader(uiProgram, f);
        gl.LinkProgram(uiProgram);

        gl.DeleteShader(v);
        gl.DeleteShader(f);

        uScreenLoc = gl.GetUniformLocation(uiProgram, "uScreen");
    }

    // =========================================================
    // TEXT SYSTEM
    // =========================================================
   private unsafe void DrawText(string text, float x, float y, float scale, Vector3 color)
{
    var verts = new List<float>();
    float cx = x;

    foreach (char c0 in text.ToUpperInvariant())
    {
        if (!font5x7.TryGetValue(c0, out var g))
        {
            cx += 6 * scale;
            continue;
        }

        int glyphColumns = Math.Min(5, g.Length);
        for (int col = 0; col < glyphColumns; col++)
        {
            byte bits = g[col];

            for (int row = 0; row < 7; row++)
            {
                if (((bits >> row) & 1) == 0)
                    continue;

                float px = cx + col * scale;
                float py = y + row * scale;

                verts.AddRange(new float[]
                {
                    px, py, color.X, color.Y, color.Z,
                    px + scale, py, color.X, color.Y, color.Z,
                    px + scale, py + scale, color.X, color.Y, color.Z,

                    px, py, color.X, color.Y, color.Z,
                    px + scale, py + scale, color.X, color.Y, color.Z,
                    px, py + scale, color.X, color.Y, color.Z
                });
            }
        }

        cx += 6 * scale;
    }

    if (verts.Count == 0)
        return;

    fixed (float* v = verts.ToArray())
    {
        gl.BindVertexArray(uiVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, uiVbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer,
            (nuint)(verts.Count * sizeof(float)),
            v,
            BufferUsageARB.DynamicDraw);

        gl.DrawArrays(GLEnum.Triangles, 0, (uint)(verts.Count / 5));
    }
}

private static readonly Dictionary<char, byte[]> font5x7 = new()
{
    {' ', new byte[]{0,0,0,0,0}},
    {'A', new byte[]{0x1E,0x05,0x05,0x1E,0}},
    {'B', new byte[]{0x1F,0x15,0x15,0x0A,0}},
    {'C', new byte[]{0x0E,0x11,0x11,0x11,0}},
    {'D', new byte[]{0x1F,0x11,0x11,0x0E,0}},
    {'E', new byte[]{0x1F,0x15,0x15,0x11,0}},
    {'F', new byte[]{0x1F,0x05,0x05,0x01,0}},
    {'G', new byte[]{0x0E,0x11,0x15,0x1D,0}},
    {'H', new byte[]{0x1F,0x04,0x04,0x1F,0}},
    {'I', new byte[]{0x11,0x1F,0x11,0}},
    {'J', new byte[]{0x08,0x10,0x10,0x0F,0}},
    {'K', new byte[]{0x1F,0x04,0x0A,0x11,0}},
    {'L', new byte[]{0x1F,0x10,0x10,0x10,0}},
    {'M', new byte[]{0x1F,0x02,0x04,0x02,0x1F}},
    {'N', new byte[]{0x1F,0x02,0x04,0x1F,0}},
    {'O', new byte[]{0x0E,0x11,0x11,0x0E,0}},
    {'P', new byte[]{0x1F,0x05,0x05,0x02,0}},
    {'Q', new byte[]{0x0E,0x11,0x19,0x1E,0}},
    {'R', new byte[]{0x1F,0x05,0x0D,0x12,0}},
    {'S', new byte[]{0x12,0x15,0x15,0x09,0}},
    {'T', new byte[]{0x01,0x1F,0x01,0}},
    {'U', new byte[]{0x0F,0x10,0x10,0x0F,0}},
    {'V', new byte[]{0x07,0x08,0x10,0x08,0x07}},
    {'W', new byte[]{0x1F,0x08,0x04,0x08,0x1F}},
    {'X', new byte[]{0x11,0x0A,0x04,0x0A,0x11}},
    {'Y', new byte[]{0x01,0x02,0x1C,0x02,0x01}},
    {'Z', new byte[]{0x11,0x19,0x15,0x13,0}},
    {'0', new byte[]{0x0E,0x11,0x11,0x0E,0}},
    {'1', new byte[]{0x12,0x1F,0x10,0}},
    {'2', new byte[]{0x19,0x15,0x15,0x12,0}},
    {'3', new byte[]{0x11,0x15,0x15,0x0A,0}},
    {'4', new byte[]{0x07,0x04,0x1F,0x04}},
    {'5', new byte[]{0x17,0x15,0x15,0x09,0}},
    {'6', new byte[]{0x0E,0x15,0x15,0x08,0}},
    {'7', new byte[]{0x01,0x19,0x05,0x03}},
    {'8', new byte[]{0x0A,0x15,0x15,0x0A,0}},
    {'9', new byte[]{0x02,0x15,0x15,0x0E,0}},
    {'-', new byte[]{0x04,0x04,0x04,0x04,0}},
    {'.', new byte[]{0x00,0x10,0x00,0x00,0}}
};
    private static string Format(int v) => v < 0 ? "-" : v.ToString();
}

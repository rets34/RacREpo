using Silk.NET.OpenGL;
using System;
using System.Numerics;

public unsafe class RenderManager
{
    private GL gl = null!;
    // No 3D geometry anymore; simple background UI only

    private uint uiVao;
    private uint uiVbo;
    private uint uiProgram;
    private int uScreenLoc;

    // Extraction status display
    private string extractionStatus = "Drop an ISO onto the window";
    private string extractionLog = "";
    private bool isExtracting = false;
    private bool gameReady = false;
    private int functionsFound = -1;
    private int unknownInstructionCount = -1;
    private int compileErrorCount = -1;
    private int compileWarningCount = -1;
    private bool generatedStubReady = false;
    private string executionResult = "NOT RUN";
    private System.Collections.Generic.List<(string label, float x, float y, float w, float h)> buttons = new();

    public void Initialize(GL graphics)
    {
        gl = graphics;

        Console.WriteLine("Initializing RenderManager...");

        // Keep a simple viewport; no complex GL state required
        gl.Viewport(0, 0, 1280, 720);

        CreateUiProgram();
        uiVao = gl.GenVertexArray();
        uiVbo = gl.GenBuffer();
        gl.BindVertexArray(uiVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, uiVbo);
        // position (2 floats) + color (3 floats)
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)(5 * sizeof(float)), (nint)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)(5 * sizeof(float)), (nint)(2 * sizeof(float)));
        gl.EnableVertexAttribArray(1);

        Console.WriteLine("RenderManager initialized.");
    }

    public void Render()
    {
        // Simple static background for UI text overlay
        gl.ClearColor(0.06f, 0.08f, 0.12f, 1.0f);
        gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        // Draw UI text using a tiny bitmap font
        gl.UseProgram(uiProgram);
        gl.Uniform2(uScreenLoc, 1280f, 720f);

        buttons.Clear();

        // Title - larger
        DrawText("RAC DECOMPILER", 20, 15, 2.5f, new Vector3(0.95f, 0.95f, 0.95f));

        // Status section - larger
        if (isExtracting)
        {
            DrawText("EXTRACTION IN PROGRESS...", 20, 70, 1.8f, new Vector3(0.95f, 0.85f, 0.3f));
            DrawText(extractionStatus, 20, 120, 1.3f, new Vector3(0.8f, 0.9f, 1.0f));
        }
        else
        {
            DrawText("STATUS: " + extractionStatus, 20, 70, 1.8f, new Vector3(0.7f, 1.0f, 0.7f));
        }

        // Instructions - larger
        DrawText("DROP AN ISO OR PRESS E", 20, 160, 1.5f, new Vector3(0.8f, 0.8f, 0.95f));
        DrawText("PARTIAL DECOMP ONLY", 20, 195, 1.2f, new Vector3(1.0f, 0.75f, 0.35f));

        DrawText("FUNCTIONS " + FormatCount(functionsFound), 20, 250, 1.2f, new Vector3(0.8f, 0.9f, 1.0f));
        DrawText("UNKNOWN OPS " + FormatCount(unknownInstructionCount), 20, 280, 1.2f, new Vector3(1.0f, 0.75f, 0.55f));
        DrawText("COMPILE ERR " + FormatCount(compileErrorCount), 20, 310, 1.2f, new Vector3(compileErrorCount > 0 ? 1.0f : 0.7f, compileErrorCount > 0 ? 0.35f : 1.0f, 0.7f));
        DrawText("COMPILE WARN " + FormatCount(compileWarningCount), 20, 340, 1.2f, new Vector3(0.95f, 0.9f, 0.55f));
        DrawText("STUB " + (generatedStubReady ? "READY" : "NOT READY"), 20, 370, 1.2f, new Vector3(generatedStubReady ? 0.55f : 0.9f, generatedStubReady ? 1.0f : 0.55f, 0.7f));
        DrawText("RUN " + executionResult, 20, 400, 1.2f, new Vector3(0.8f, 0.9f, 1.0f));

        // Generated stub button - only if ready
        if (gameReady)
        {
            float btnX = 20;
            float btnY = 455;
            float btnW = 260;
            float btnH = 50;
            DrawButton("RUN STUB", btnX, btnY, btnW, btnH, 1.8f, new Vector3(0.2f, 1.0f, 0.3f));
            buttons.Add(("LAUNCH", btnX, btnY, btnW, btnH));
        }

        // Show last log line - smaller
        if (!string.IsNullOrEmpty(extractionLog))
        {
            var logLines = extractionLog.Split('\n');
            var lastLine = "";
            for (int i = logLines.Length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(logLines[i]))
                {
                    lastLine = logLines[i].Length > 70 ? logLines[i][..70] + "..." : logLines[i];
                    break;
                }
            }
            if (!string.IsNullOrEmpty(lastLine))
            {
                DrawText("LOG: " + lastLine, 20, 680, 1.0f, new Vector3(0.6f, 0.8f, 0.9f));
            }
        }
    }

    public void SetExtractionStatus(string status, bool extracting)
    {
        extractionStatus = status ?? "Ready";
        isExtracting = extracting;
    }

    public void SetExtractionLog(string log)
    {
        extractionLog = log ?? "";
    }

    public void SetGameReady(bool ready)
    {
        gameReady = ready;
    }

    public void SetDecompSummary(
        int functions,
        int unknownOps,
        int compileErrors,
        int compileWarnings,
        bool stubReady,
        string runResult)
    {
        functionsFound = functions;
        unknownInstructionCount = unknownOps;
        compileErrorCount = compileErrors;
        compileWarningCount = compileWarnings;
        generatedStubReady = stubReady;
        executionResult = string.IsNullOrWhiteSpace(runResult) ? "NOT RUN" : runResult;
    }

    public bool CheckButtonClick(float mouseX, float mouseY, out string buttonLabel)
    {
        buttonLabel = "";
        foreach (var (label, x, y, w, h) in buttons)
        {
            if (mouseX >= x && mouseX <= x + w && mouseY >= y && mouseY <= y + h)
            {
                buttonLabel = label;
                return true;
            }
        }
        return false;
    }

    private void DrawButton(string text, float x, float y, float w, float h, float scale, Vector3 color)
    {
        // Draw button border
        var borderVerts = new System.Collections.Generic.List<float>();
        // Top line
        borderVerts.Add(x); borderVerts.Add(y); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);
        borderVerts.Add(x + w); borderVerts.Add(y); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);
        // Right line
        borderVerts.Add(x + w); borderVerts.Add(y); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);
        borderVerts.Add(x + w); borderVerts.Add(y + h); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);
        // Bottom line
        borderVerts.Add(x + w); borderVerts.Add(y + h); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);
        borderVerts.Add(x); borderVerts.Add(y + h); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);
        // Left line
        borderVerts.Add(x); borderVerts.Add(y + h); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);
        borderVerts.Add(x); borderVerts.Add(y); borderVerts.Add(color.X); borderVerts.Add(color.Y); borderVerts.Add(color.Z);

        unsafe
        {
            fixed (float* v = borderVerts.ToArray())
            {
                gl.BindVertexArray(uiVao);
                gl.BindBuffer(BufferTargetARB.ArrayBuffer, uiVbo);
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(borderVerts.Count * sizeof(float)), v, BufferUsageARB.DynamicDraw);
                gl.DrawArrays((GLEnum)PrimitiveType.Lines, 0, (uint)(borderVerts.Count / 5));
            }
        }

        // Draw text centered in button
        float textX = x + (w - text.Length * 6 * scale * 0.5f) / 2;
        float textY = y + (h - 7 * scale) / 2;
        DrawText(text, textX, textY, scale, color);
    }

    private static string FormatCount(int value)
    {
        return value < 0 ? "-" : value.ToString();
    }

    // -------------------------------------------------
    // Tiny 5x7 bitmap font renderer
    // -------------------------------------------------

    private static readonly System.Collections.Generic.Dictionary<char, byte[]> font5x7 =
        new()
    {
        // Only include uppercase letters, space and a few symbols used in the messages.
        {' ', new byte[]{0x00,0x00,0x00,0x00,0x00}},
        {'A', new byte[]{0x1E,0x05,0x05,0x1E,0x00}},
        {'B', new byte[]{0x1F,0x15,0x15,0x0A,0x00}},
        {'C', new byte[]{0x0E,0x11,0x11,0x11,0x00}},
        {'D', new byte[]{0x1F,0x11,0x11,0x0E,0x00}},
        {'E', new byte[]{0x1F,0x15,0x15,0x11,0x00}},
        {'F', new byte[]{0x1F,0x05,0x05,0x01,0x00}},
        {'G', new byte[]{0x0E,0x11,0x15,0x1D,0x00}},
        {'H', new byte[]{0x1F,0x04,0x04,0x1F,0x00}},
        {'I', new byte[]{0x11,0x1F,0x11,0x00,0x00}},
        {'J', new byte[]{0x08,0x10,0x10,0x0F,0x00}},
        {'K', new byte[]{0x1F,0x04,0x0A,0x11,0x00}},
        {'L', new byte[]{0x1F,0x10,0x10,0x10,0x00}},
        {'M', new byte[]{0x1F,0x02,0x04,0x02,0x1F}},
        {'N', new byte[]{0x1F,0x02,0x04,0x1F,0x00}},
        {'O', new byte[]{0x0E,0x11,0x11,0x0E,0x00}},
        {'P', new byte[]{0x1F,0x05,0x05,0x02,0x00}},
        {'Q', new byte[]{0x0E,0x11,0x19,0x1E,0x00}},
        {'R', new byte[]{0x1F,0x05,0x0D,0x12,0x00}},
        {'S', new byte[]{0x12,0x15,0x15,0x09,0x00}},
        {'T', new byte[]{0x01,0x01,0x1F,0x01,0x01}},
        {'U', new byte[]{0x0F,0x10,0x10,0x0F,0x00}},
        {'V', new byte[]{0x07,0x08,0x10,0x08,0x07}},
        {'W', new byte[]{0x1F,0x08,0x04,0x08,0x1F}},
        {'X', new byte[]{0x11,0x0A,0x04,0x0A,0x11}},
        {'Y', new byte[]{0x01,0x02,0x1C,0x02,0x01}},
        {'Z', new byte[]{0x11,0x19,0x15,0x13,0x00}},
        {'0', new byte[]{0x0E,0x11,0x11,0x0E,0x00}},
        {'1', new byte[]{0x00,0x12,0x1F,0x10,0x00}},
        {'2', new byte[]{0x12,0x19,0x15,0x12,0x00}},
        {'3', new byte[]{0x11,0x15,0x15,0x0A,0x00}},
        {'4', new byte[]{0x07,0x04,0x04,0x1F,0x04}},
        {'5', new byte[]{0x17,0x15,0x15,0x09,0x00}},
        {'6', new byte[]{0x0E,0x15,0x15,0x08,0x00}},
        {'7', new byte[]{0x01,0x01,0x19,0x05,0x03}},
        {'8', new byte[]{0x0A,0x15,0x15,0x0A,0x00}},
        {'9', new byte[]{0x02,0x15,0x15,0x0E,0x00}},
        {'-', new byte[]{0x04,0x04,0x04,0x04,0x00}},
        {'!', new byte[]{0x00,0x00,0x1D,0x00,0x00}},
        {'.', new byte[]{0x00,0x10,0x00,0x00,0x00}}
    };

    private void CreateUiProgram()
    {
        string vs = @"#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec3 aColor;
out vec3 vColor;
uniform vec2 uScreen;
void main(){
    vec2 p = aPos / uScreen * 2.0 - 1.0;
    p.y = -p.y;
    gl_Position = vec4(p, 0.0, 1.0);
    vColor = aColor;
}";

        string fs = @"#version 330 core
in vec3 vColor;
out vec4 FragColor;
void main(){ FragColor = vec4(vColor, 1.0); }";

        uint vsh = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vsh, vs);
        gl.CompileShader(vsh);
        uint fsh = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fsh, fs);
        gl.CompileShader(fsh);
        uiProgram = gl.CreateProgram();
        gl.AttachShader(uiProgram, vsh);
        gl.AttachShader(uiProgram, fsh);
        gl.LinkProgram(uiProgram);
        gl.DeleteShader(vsh);
        gl.DeleteShader(fsh);
        uScreenLoc = gl.GetUniformLocation(uiProgram, "uScreen");
    }

    private unsafe void DrawText(string text, float x, float y, float scale, Vector3 color)
    {
        // build vertex list for quads where font bit is set
        var verts = new System.Collections.Generic.List<float>();
        float cx = x;
        text = text.ToUpperInvariant();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (!font5x7.TryGetValue(c, out var glyph))
            {
                cx += 6f * scale;
                continue;
            }
            for (int col = 0; col < 5; col++)
            {
                byte colBits = glyph[col];
                for (int row = 0; row < 7; row++)
                {
                    bool on = ((colBits >> row) & 1) != 0;
                    if (!on) continue;
                    float px = cx + col * scale;
                    float py = y + row * scale;
                    float w = scale;
                    float h = scale;
                    // two triangles
                    // v0
                    verts.Add(px); verts.Add(py); verts.Add(color.X); verts.Add(color.Y); verts.Add(color.Z);
                    // v1
                    verts.Add(px + w); verts.Add(py); verts.Add(color.X); verts.Add(color.Y); verts.Add(color.Z);
                    // v2
                    verts.Add(px + w); verts.Add(py + h); verts.Add(color.X); verts.Add(color.Y); verts.Add(color.Z);
                    // v0
                    verts.Add(px); verts.Add(py); verts.Add(color.X); verts.Add(color.Y); verts.Add(color.Z);
                    // v2
                    verts.Add(px + w); verts.Add(py + h); verts.Add(color.X); verts.Add(color.Y); verts.Add(color.Z);
                    // v3
                    verts.Add(px); verts.Add(py + h); verts.Add(color.X); verts.Add(color.Y); verts.Add(color.Z);
                }
            }
            cx += 6f * scale; // 5 cols + 1 px spacing
        }

        if (verts.Count == 0) return;

            fixed (float* v = verts.ToArray())
        {
            gl.BindVertexArray(uiVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, uiVbo);
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Count * sizeof(float)), v, BufferUsageARB.DynamicDraw);
            gl.DrawArrays((GLEnum)PrimitiveType.Triangles, 0, (uint)(verts.Count / 5));
        }
    }

    // no geometry creation required for the static UI

    private void CreateShaders()
    {
        // shaders and complex GL pipeline are removed for now
    }
}

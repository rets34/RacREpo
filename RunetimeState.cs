public class RuntimeState
{
    // -------------------------------------------------
    // Global Engine State
    // -------------------------------------------------

    // DAT_0015ee24
    // Global frame counter
    public int GlobalFrameCounter;

    // DAT_0015eeb0
    // Current active game state
    public int CurrentGameState;

    // DAT_00161280
    // Frames spent inside current state
    public int StateFrameCounter;

    // Main engine running flag
    public bool Running = true;

    // -------------------------------------------------
    // PS2 Timing / Flags
    // -------------------------------------------------

    // DAT_0015f5ec
    // Pause / freeze flag
    public int PauseFlag;

    // DAT_0015ed80
    // Special timing flag
    public int SpecialFrameSkip = 1;

    // -------------------------------------------------
    // Rendering State
    // -------------------------------------------------

    public int ScreenWidth = 1280;

    public int ScreenHeight = 720;

    // -------------------------------------------------
    // Debug
    // -------------------------------------------------

    public bool DebugMode;

    public bool ShowFPS;
}
public class FrameTimer
{
    public int FrameCount { get; private set; }

    private int internalCounter;

    public void Update()
    {
        internalCounter++;

        if (ShouldApplySkipLogic())
        {
            internalCounter += 11;
        }

        FrameCount = internalCounter;
    }

    private bool ShouldApplySkipLogic()
    {
        // from DAT_0015f5ec, DAT_0015ed80 logic
        return false; // placeholder until you map flags
    }
}
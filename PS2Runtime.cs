public class Ps2Runtime
{
    public uint Frame;
    public int Field;

    private double accumulator;
    private const double TickRate = 1.0 / 60.0;

    public void Tick(double dt)
    {
        accumulator += dt;

        while (accumulator >= TickRate)
        {
            accumulator -= TickRate;

            RunVBlank();
            RunGameTick();
        }
    }

    private void RunVBlank()
    {
        Frame++;
        Field ^= 1; // PS2 interlace simulation placeholder
    }

    private void RunGameTick()
    {
        // THIS is where your FUN_* goes later
    }
}
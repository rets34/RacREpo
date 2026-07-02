using System;
using Silk.NET.Windowing;

public class Program
{
    public static void Main(string[] args)
    {
        // 🔥 CRITICAL MAC FIX: Force Silk.NET to prioritize the SDL2 backend over GLFW.
        // SDL isolates managed .NET code exceptions from crashing the macOS Window Manager (AppKit).
        Window.PrioritizeSdl();

        try
        {
            Console.WriteLine("[SYSTEM] Initializing Game Instance via SDL backend...");
            var game = new Game();
            game.Run();
        }
        catch (IndexOutOfRangeException ioEx)
        {
            Console.WriteLine("❌ CRITICAL BOUNDS FAULT IDENTIFIED:");
            Console.WriteLine(ioEx.Message);
            Console.WriteLine(ioEx.StackTrace);
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ UNCAUGHT SYSTEM PIPELINE FAILURE:");
            Console.WriteLine(ex.ToString());
        }
    }
}

namespace Tools;

public class ISOExtractorMain
{
    public void Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ISOExtractor <path-to-iso>");
            return;
        }

        string isoPath = args[0];

        Console.WriteLine("[MAIN] Starting ISO extraction pipeline...");
        Console.WriteLine($"[MAIN] ISO: {isoPath}");

        var extractor = new ISOExtractor(isoPath);

        extractor.Run();
        // ^ IMPORTANT: assume ISOExtractor now RETURNS parsed ISO object

        Console.WriteLine();
        Console.WriteLine("[MAIN] Boot pipeline complete.");
        Console.WriteLine("[MAIN] Ready for ELF stage.");
    }
}
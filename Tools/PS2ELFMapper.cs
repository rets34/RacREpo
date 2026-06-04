using System;

public class PS2ElfMapper
{
    private readonly PS2RAM _ram;

    public PS2ElfMapper(PS2RAM ram)
    {
        _ram = ram;
    }

    public void Map(ElfImage image)
    {
        Console.WriteLine("[PS2] Mapping ELF segments into RAM...");

        foreach (var segment in image.Segments)
        {
            Console.WriteLine(
                $"[PS2] Mapping 0x{segment.VAddr:X8} ({segment.FileSize} bytes)"
            );

            _ram.Write(segment.VAddr, segment.Data);
        }

        Console.WriteLine("[PS2] ELF mapping complete.");
    }
}
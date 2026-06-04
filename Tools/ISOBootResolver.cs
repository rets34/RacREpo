using System;
using System.IO;
using System.Text;
using System.Linq;

public static class IsoBootResolver
{
    public static string FindElfPath(string isoPath)
    {
        Console.WriteLine("[ISO] Reading SYSTEM.CNF...");

        // TODO: replace with real ISO9660 reader later
        using var fs = File.OpenRead(isoPath);
        using var br = new BinaryReader(fs);

        byte[] buffer = new byte[fs.Length];
        fs.Read(buffer, 0, buffer.Length);

        string text = Encoding.ASCII.GetString(buffer);

        // PS2 config format:
        // BOOT2 = cdrom0:\SLUS_XXXXX.XX;1
        var bootLine = text.Split('\n')
            .FirstOrDefault(l => l.Contains("BOOT2"));

        if (bootLine == null)
            throw new Exception("[ISO] BOOT2 not found in SYSTEM.CNF");

        string raw = bootLine.Split('=')[1].Trim();

        string elfName = raw
            .Replace("cdrom0:\\", "")
            .Replace(";1", "")
            .Trim();

        Console.WriteLine($"[ISO] Boot ELF: {elfName}");

        // TEMP: assume extracted already
        return Path.Combine(Path.GetDirectoryName(isoPath)!, elfName);
    }
}
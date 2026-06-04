using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ISO9660Reader
{
    private const int SectorSize = 2048;

    private readonly string _path;
    private readonly FileStream _stream;

    public ISOFileEntry Root { get; private set; } = new ISOFileEntry
    {
        Name = "/",
        IsDirectory = true
    };

    public ISO9660Reader(string path)
    {
        _path = path;
        _stream = File.OpenRead(path);
    }

    // =========================
    // ENTRY POINT
    // =========================
    public void Parse()
    {
        Console.WriteLine("[ISO] Opening ISO...");

        var (rootExtent, rootSize) = ReadPrimaryVolumeRoot();

        Console.WriteLine("[ISO] Reading root directory...");

        if (rootExtent > 0 && rootSize > 0)
        {
            Root = ReadDirectory((long)rootExtent * SectorSize, "/", rootSize);
        }
        else
        {
            Console.WriteLine("[ISO] Warning: failed to parse PVD root directory. Falling back to sector 150.");
            Root = ReadDirectory(150 * SectorSize, "/", SectorSize);
        }

        Console.WriteLine("[ISO] Parsing complete.");
    }

    // =========================
    // BOOT ELF PIPELINE
    // =========================
    public string ExtractBootElf()
    {
        Console.WriteLine("[ISO] Searching for SYSTEM.CNF...");

        var cnf = FindFile(Root, "SYSTEM.CNF");

        if (cnf == null)
            throw new Exception("SYSTEM.CNF not found");

        Console.WriteLine("[BOOT] SYSTEM.CNF found: /SYSTEM.CNF");

        string bootElf = ParseBootElfFromSystemCnf();

        Console.WriteLine("\n================================");
        Console.WriteLine("[BOOT] PS2 GAME ENTRY DETECTED");
        Console.WriteLine($"[BOOT] Executable: {bootElf}");
        Console.WriteLine("================================\n");

        var elfNode = FindFile(Root, bootElf);

        if (elfNode == null)
        {
            Console.WriteLine("[BOOT] WARNING: Boot ELF not found in ISO tree!");
            return "";
        }

        string outputPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "extracted_" + elfNode.Name);

        ExtractFile(elfNode, outputPath);

        Console.WriteLine($"[ISO] Extracted: {outputPath}");

        return outputPath;
    }

    // =========================
    // SYSTEM.CNF PARSER
    // =========================
    private string ParseBootElfFromSystemCnf()
    {
        var cnf = FindFile(Root, "SYSTEM.CNF");

        if (cnf == null)
            return "";

        _stream.Seek(cnf.Offset, SeekOrigin.Begin);

        byte[] buffer = new byte[cnf.Size];
        _stream.ReadExactly(buffer);

        string text = Encoding.ASCII.GetString(buffer);

        foreach (var line in text.Split('\n'))
        {
            if (line.TrimStart().StartsWith("BOOT", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('=', 2);
                if (parts.Length > 1)
                {
                    var bootPath = parts[1]
                        .Trim()
                        .Replace("cdrom0:\\", "")
                        .Replace("cdrom0:/", "")
                        .Trim();

                    int semicolonIndex = bootPath.IndexOf(';');
                    if (semicolonIndex >= 0)
                    {
                        bootPath = bootPath.Substring(0, semicolonIndex);
                    }

                    return bootPath.Trim();
                }
            }
        }

        return "";
    }

    // =========================
    // FILE SYSTEM SEARCH
    // =========================
    public ISOFileEntry? FindFile(ISOFileEntry node, string name)
    {
        if (!node.IsDirectory)
        {
            if (node.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return node;
        }

        foreach (var child in node.Children)
        {
            var result = FindFile(child, name);
            if (result != null)
                return result;
        }

        return null;
    }

    // =========================
    // FILE EXTRACTION
    // =========================
    private void ExtractFile(ISOFileEntry entry, string outputPath)
    {
        _stream.Seek(entry.Offset, SeekOrigin.Begin);

        byte[] data = new byte[entry.Size];
        _stream.ReadExactly(data);

        File.WriteAllBytes(outputPath, data);
    }

    // =========================
    // DIRECTORY READING
    // =========================
    private ISOFileEntry ReadDirectory(long offset, string path, long size)
    {
        if (size > int.MaxValue)
            throw new InvalidOperationException("Directory size exceeds supported parser limit.");

        var dir = new ISOFileEntry
        {
            Name = path,
            FullPath = path,
            IsDirectory = true,
            Offset = offset,
            Size = size
        };

        _stream.Seek(offset, SeekOrigin.Begin);

        byte[] buffer = new byte[(int)size];
        _stream.ReadExactly(buffer);

        int i = 0;

        while (i < buffer.Length)
        {
            int length = buffer[i];
            if (length == 0)
            {
                i = ((i / SectorSize) + 1) * SectorSize;
                continue;
            }

            int nameLen = buffer[i + 32];
            int fileFlags = buffer[i + 25];

            string name = Encoding.ASCII.GetString(buffer, i + 33, nameLen);
            name = name.Split(';')[0];

            bool isDir = (fileFlags & 0x02) != 0;

            int extent = BitConverter.ToInt32(buffer, i + 2);
            int entrySize = BitConverter.ToInt32(buffer, i + 10);

            if (name != "\0" && name != "\x01")
            {
                var entry = new ISOFileEntry
                {
                    Name = name,
                    FullPath = path + name + (isDir ? "/" : ""),
                    Offset = (long)extent * SectorSize,
                    Size = entrySize,
                    IsDirectory = isDir
                };

                if (isDir)
                {
                    entry = ReadDirectory(entry.Offset, entry.FullPath, entry.Size);
                }

                dir.Children.Add(entry);
            }

            i += length;
        }

        return dir;
    }

    // =========================
    // SECTOR READ
    // =========================
    private (int extent, int size) ReadPrimaryVolumeRoot()
    {
        var pvd = ReadSector(16);

        if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
            return (-1, -1);

        int extent = BitConverter.ToInt32(pvd, 156 + 2);
        int size = BitConverter.ToInt32(pvd, 156 + 10);

        return (extent, size);
    }

    private byte[] ReadSector(int sector)
    {
        byte[] buffer = new byte[SectorSize];
        _stream.Seek((long)sector * SectorSize, SeekOrigin.Begin);
        _stream.ReadExactly(buffer);
        return buffer;
    }

    // =========================
    // DEBUG TREE
    // =========================
    public void DumpTree()
    {
        DumpNode(Root, 0);
    }

    private void DumpNode(ISOFileEntry node, int depth)
    {
        Console.WriteLine($"{new string(' ', depth * 2)}- {node.Name}");

        foreach (var child in node.Children)
            DumpNode(child, depth + 1);
    }

    // =========================
    // ELF DETECTION
    // =========================
    public void DetectElf()
    {
        Console.WriteLine("[ISO] Searching for SLUS/SCUS executables...");
        FindElf(Root);
    }

    private void FindElf(ISOFileEntry node)
    {
        if (!node.IsDirectory)
        {
            if (node.Name.StartsWith("SLUS") ||
                node.Name.StartsWith("SCUS"))
            {
                Console.WriteLine($"[ELF] Found candidate: {node.FullPath}");
            }
        }

        foreach (var c in node.Children)
            FindElf(c);
    }
}
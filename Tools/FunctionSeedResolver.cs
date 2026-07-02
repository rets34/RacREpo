using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class FunctionSeedResolver
{
    private static readonly string[] CandidateFiles =
    {
        "ghidra_symbols.txt",
        "ghidra_functions.txt",
        "functions.txt",
        "symbols.txt",
        "ghidra_symbols.csv",
        "ghidra_symbols.map"
    };

    public static List<uint> DiscoverSeeds(string elfPath, uint entryPoint, PS2RAM ram, string? isoPath = null)
    {
        var seeds = new HashSet<uint>();
        AddSeed(seeds, entryPoint);

        foreach (var file in EnumerateCandidateFiles(elfPath, isoPath))
        {
            try
            {
                foreach (var seed in ParseSeedFile(file))
                    AddSeed(seeds, seed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEEDS] Failed to parse {file}: {ex.Message}");
            }
        }

        if (File.Exists(elfPath))
        {
            try
            {
                foreach (var seed in DiscoverSeedsFromElfText(elfPath, entryPoint, ram))
                    AddSeed(seeds, seed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEEDS] ELF scan failed: {ex.Message}");
            }
        }

        return seeds
            .Where(IsTextRegionAddress)
            .Select(seed => Normalize(seed))
            .Where(ValidateCandidateAddress)
            .OrderBy(x => x)
            .ToList();
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string elfPath, string? isoPath)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in new[] { elfPath, isoPath })
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                foreach (var file in CandidateFiles)
                    candidates.Add(Path.Combine(directory, file));
            }

            candidates.Add(fullPath + ".sym");
            candidates.Add(fullPath + ".txt");
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                yield return candidate;
        }
    }

    private static IEnumerable<uint> ParseSeedFile(string filePath)
    {
        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            foreach (Match match in Regex.Matches(line, @"0x?[0-9A-Fa-f]{4,8}"))
            {
                if (uint.TryParse(match.Value.Replace("0x", "", StringComparison.OrdinalIgnoreCase), System.Globalization.NumberStyles.HexNumber, null, out var value))
                    yield return value;
            }
        }
    }

    private static List<uint> DiscoverSeedsFromElfText(string elfPath, uint entryPoint, PS2RAM ram)
    {
        using var stream = File.OpenRead(elfPath);
        using var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        const uint ElfMagic = 0x464C457F;
        if (br.ReadUInt32() != ElfMagic)
            throw new InvalidDataException("Not a valid ELF file");

        br.ReadByte(); // class
        br.ReadByte(); // data
        br.ReadBytes(10); // padding

        br.BaseStream.Seek(0x18, SeekOrigin.Begin);
        br.ReadUInt32(); // e_entry
        br.ReadUInt32(); // e_phoff
        uint shOff = br.ReadUInt32();

        br.ReadUInt32(); // e_flags
        ushort ehSize = br.ReadUInt16();
        ushort phEntSize = br.ReadUInt16();
        ushort phNum = br.ReadUInt16();
        ushort shEntSize = br.ReadUInt16();
        ushort shNum = br.ReadUInt16();
        ushort shStrndx = br.ReadUInt16();

        if (shOff == 0 || shNum == 0)
            return new List<uint>();

        var sections = new List<(string Name, uint Type, uint Addr, uint Offset, uint Size, uint NameOffset)>(shNum);
        var sectionNames = new byte[0];

        for (int i = 0; i < shNum; i++)
        {
            br.BaseStream.Seek(shOff + (i * shEntSize), SeekOrigin.Begin);
            uint nameOffset = br.ReadUInt32();
            uint type = br.ReadUInt32();
            br.ReadUInt32(); // flags
            uint addr = br.ReadUInt32();
            uint offset = br.ReadUInt32();
            uint size = br.ReadUInt32();
            br.ReadUInt32(); // link
            br.ReadUInt32(); // info
            br.ReadUInt32(); // addralign
            br.ReadUInt32(); // entsize
            sections.Add((string.Empty, type, addr, offset, size, nameOffset));

            if (i == shStrndx)
            {
                br.BaseStream.Seek(offset, SeekOrigin.Begin);
                sectionNames = br.ReadBytes((int)size);
            }
        }

        var discovered = new HashSet<uint>();
        AddSeed(discovered, entryPoint);

        foreach (var section in sections)
        {
            var sectionName = ReadCString(sectionNames, (int)section.NameOffset);
            if (!IsCodeSection(sectionName))
                continue;

            if (section.Addr == 0 || section.Size < 4)
                continue;

            var data = ReadSectionBytes(br, section.Offset, section.Size);
            AddSeed(discovered, section.Addr);

            for (int offset = 0; offset + 3 < data.Length; offset += 4)
            {
                uint address = section.Addr + (uint)offset;
                uint raw = BitConverter.ToUInt32(data, offset);
                var instr = MIPSDisassembler.Decode(address, raw);

                if (instr.Mnemonic == "jal" || instr.Mnemonic == "j")
                    AddSeed(discovered, instr.TargetAddress);

                if (instr.IsBranch)
                    AddSeed(discovered, instr.BranchTarget);

                if (LooksLikeFunctionPrologue(address, data, offset))
                    AddSeed(discovered, address);
            }
        }

        var expanded = new HashSet<uint>(discovered);
        var queue = new Queue<uint>(discovered);

        while (queue.Count > 0)
        {
            uint current = queue.Dequeue();

            foreach (var target in ScanForCodeTargets(current, ram))
            {
                if (expanded.Add(target))
                    queue.Enqueue(target);
            }
        }

        return expanded
            .Where(IsTextRegionAddress)
            .Select(Normalize)
            .Where(ValidateCandidateAddress)
            .Where(addr => IsLikelyFunction(addr, ram))
            .OrderBy(addr => addr)
            .ToList();
    }

    private static bool LooksLikeFunctionPrologue(uint address, byte[] data, int offset)
    {
        if (offset + 8 >= data.Length)
            return false;

        var first = MIPSDisassembler.Decode(address, BitConverter.ToUInt32(data, offset));
        var second = MIPSDisassembler.Decode(address + 4, BitConverter.ToUInt32(data, offset + 4));

        bool stackAdjust = first.Mnemonic == "addiu" && first.Rs == 29 && first.Rt == 29;
        bool savesRa = second.Mnemonic == "sw" && second.Rt == 31 && second.Rs == 29;
        bool savesFp = second.Mnemonic == "sw" && second.Rt == 30 && second.Rs == 29;

        return stackAdjust && (savesRa || savesFp);
    }

    private static IEnumerable<uint> ScanForCodeTargets(uint startAddress, PS2RAM ram)
    {
        uint pc = Normalize(startAddress);
        const int maxInstructions = 256;

        for (int i = 0; i < maxInstructions; i++)
        {
            if (!IsTextRegionAddress(pc))
                break;

            uint raw = ram.Read32(pc);
            var instr = MIPSDisassembler.Decode(pc, raw);

            if (instr.Mnemonic == "jal" || instr.Mnemonic == "j")
                yield return Normalize(instr.TargetAddress);

            if (instr.IsBranch)
                yield return Normalize(instr.BranchTarget);

            if (instr.IsReturn)
                break;

            pc += 4;
        }
    }

    private static bool IsLikelyFunction(uint addr, PS2RAM ram)
    {
        try
        {
            var blocks = new MIPSBasicBlockBuilder(ram).Build(addr, 80);
            return blocks != null && blocks.Count > 0 && blocks.Any(block => block.Instructions.Any(instr => instr.IsReturn));
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ReadSectionBytes(BinaryReader br, uint offset, uint size)
    {
        br.BaseStream.Seek(offset, SeekOrigin.Begin);
        return br.ReadBytes((int)size);
    }

    private static string ReadCString(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length)
            return string.Empty;

        int end = offset;
        while (end < data.Length && data[end] != 0)
            end++;

        return Encoding.ASCII.GetString(data, offset, end - offset);
    }

    private static void AddSeed(HashSet<uint> seeds, uint value)
    {
        if (value == 0)
            return;

        uint normalized = Normalize(value);
        if (IsTextRegionAddress(normalized))
            seeds.Add(normalized);
    }

    private static uint Normalize(uint addr) => addr & 0xFFFFFFFC;

    private static bool ValidateCandidateAddress(uint addr)
    {
        if (!IsTextRegionAddress(addr))
            return false;

        return addr >= 0x00100000;
    }

    private static bool IsCodeSection(string sectionName)
    {
        return string.Equals(sectionName, ".text", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sectionName, ".text.startup", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sectionName, ".init", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sectionName, ".fini", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextRegionAddress(uint addr)
    {
        if (addr < 0x00100000)
            return false;

        if (addr >= 0x02000000 && addr < 0x80000000)
            return false;

        return (addr & 3) == 0;
    }
}

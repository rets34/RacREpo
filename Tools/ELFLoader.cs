using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ElfLoader
{
    private const uint ELF_MAGIC = 0x464C457F; // 0x7F 'E' 'L' 'F'

    private readonly string _path;
    private readonly FileStream _stream;

    public uint EntryPoint { get; private set; }

    public ElfImage Image { get; private set; } = new ElfImage();

    public ElfLoader(string path)
    {
        _path = path;

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("ELF path is null or empty");

        _stream = File.OpenRead(path);
    }

    public ElfImage Load()
    {
        Console.WriteLine("[ELF] Loading ELF...");
        Console.WriteLine($"[ELF] Path: {_path}");

        if (Path.GetExtension(_path).Equals(".iso", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("[ELF] ERROR: ISO passed into ElfLoader. You must extract the ELF from the ISO first (SYSTEM.CNF → BOOT2).");
        }

        using var br = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);

        // =========================
        // READ MAGIC
        // =========================
        uint magic = br.ReadUInt32();
        Console.WriteLine($"[ELF] Magic: 0x{magic:X8}");

        if (magic != ELF_MAGIC)
        {
            throw new Exception(
                $"[ELF] Invalid ELF magic: 0x{magic:X8}. Expected 0x{ELF_MAGIC:X8}. " +
                "This file is not a valid PS2 ELF (or you passed the wrong file)."
            );
        }

        byte eiClass = br.ReadByte(); // 1 = 32-bit
        byte eiData = br.ReadByte();  // 1 = little endian

        if (eiClass != 1)
            throw new Exception("[ELF] Not a 32-bit ELF.");

        if (eiData != 1)
            throw new Exception("[ELF] Not little-endian ELF.");

        // =========================
        // HEADER SEEK
        // =========================
        br.BaseStream.Seek(0x18, SeekOrigin.Begin);

        EntryPoint = br.ReadUInt32();
        uint phOff = br.ReadUInt32();
        uint shOff = br.ReadUInt32();
        uint flags = br.ReadUInt32();

        ushort ehSize = br.ReadUInt16();
        ushort phEntSize = br.ReadUInt16();
        ushort phCount = br.ReadUInt16();

        Image.EntryPoint = EntryPoint;

        Console.WriteLine($"[ELF] Entry Point: 0x{EntryPoint:X8}");
        Console.WriteLine($"[ELF] Program Headers: {phCount}");

        // =========================
        // PROGRAM HEADERS
        // =========================
        for (int i = 0; i < phCount; i++)
        {
            long offset = phOff + (i * phEntSize);
            br.BaseStream.Seek(offset, SeekOrigin.Begin);

            uint type = br.ReadUInt32();
            uint fileOffset = br.ReadUInt32();
            uint vaddr = br.ReadUInt32();
            uint paddr = br.ReadUInt32();
            uint fileSize = br.ReadUInt32();
            uint memSize = br.ReadUInt32();
            uint flags2 = br.ReadUInt32();
            uint align = br.ReadUInt32();

            const uint PT_LOAD = 1;

            if (type != PT_LOAD)
                continue;

            Console.WriteLine();
            Console.WriteLine("[ELF] LOAD SEGMENT");
            Console.WriteLine($"       File Offset: 0x{fileOffset:X}");
            Console.WriteLine($"       VAddr:       0x{vaddr:X8}");
            Console.WriteLine($"       File Size:   0x{fileSize:X}");
            Console.WriteLine($"       Mem Size:    0x{memSize:X}");
            Console.WriteLine($"       Flags:       0x{flags2:X}");

            byte[] data = new byte[fileSize];

            long restore = br.BaseStream.Position;
            br.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

            int totalRead = 0;

            while (totalRead < fileSize)
            {
                int toRead = (int)Math.Min(4096, fileSize - (uint)totalRead);
                int read = br.Read(data, totalRead, toRead);

                if (read <= 0)
                    break;

                totalRead += read;
            }

            br.BaseStream.Seek(restore, SeekOrigin.Begin);

            Image.Segments.Add(new ElfSegment
            {
                VAddr = vaddr,
                FileOffset = fileOffset,
                FileSize = fileSize,
                MemSize = memSize,
                Flags = (byte)flags2,
                Data = data
            });
        }

        Console.WriteLine();
        Console.WriteLine($"[ELF] Load complete. Segments: {Image.Segments.Count}");

        if (Image.Segments.Count == 0)
            throw new Exception("[ELF] No loadable segments found. File may be corrupt or not a PS2 ELF.");

        return Image;
    }
}
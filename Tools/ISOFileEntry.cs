using System.Collections.Generic;

public class ISOFileEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";

    public bool IsDirectory { get; set; }

    public long Offset { get; set; }   // ✅ FIXED (was int)
    public long Size { get; set; }     // ✅ FIXED (was int)

    public List<ISOFileEntry> Children { get; set; } = new();
}
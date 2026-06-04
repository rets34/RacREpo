using System;
using System.Collections.Generic;

public class MIPSFunctionScanner
{
    private readonly PS2RAM _ram;

    public MIPSFunctionScanner(PS2RAM ram)
    {
        _ram = ram;
    }

    public List<uint> Scan(uint entryPoint, int maxInstructions = 5000)
    {
        Console.WriteLine("[FUNC] Scanning for functions...");

        var functions = new HashSet<uint>();
        var workQueue = new Queue<uint>();

        workQueue.Enqueue(entryPoint);

        while (workQueue.Count > 0)
        {
            uint pc = workQueue.Dequeue();
            if (functions.Contains(pc))
                continue;

            functions.Add(pc);
            Console.WriteLine($"[FUNC] Scanning function at 0x{pc:X8}");

            for (int i = 0; i < maxInstructions; i++)
            {
                uint instr = _ram.Read32(pc);

                if (IsCall(instr))
                {
                    uint target = GetJumpTarget(pc, instr);
                    if (!functions.Contains(target))
                    {
                        Console.WriteLine($"[FUNC] Found call target 0x{target:X8}");
                        workQueue.Enqueue(target);
                    }
                }

                if (IsReturn(instr))
                {
                    break;
                }

                pc += 4;
            }
        }

        return new List<uint>(functions);
    }

    private bool IsCall(uint instr)
    {
        uint op = instr >> 26;
        return op == 0x03; // jal
    }

    private bool IsFunctionStart(uint pc)
    {
        uint i1 = _ram.Read32(pc);
        uint i2 = _ram.Read32(pc + 4);

        // addiu sp, sp, -imm
        bool stackFrame =
            ((i1 >> 26) == 0x09) &&
            (((i1 >> 21) & 0x1F) == 29);

        // sw ra, offset(sp)
        bool savesRa =
            ((i2 >> 26) == 0x2B) &&
            (((i2 >> 16) & 0x1F) == 31);

        return stackFrame && savesRa;
    }

    private bool IsReturn(uint instr)
    {
        return (instr & 0x3F) == 0x08; // jr
    }

    private uint GetJumpTarget(uint pc, uint instr)
    {
        uint target = instr & 0x03FFFFFF;
        return (pc & 0xF0000000) | (target << 2);
    }
}
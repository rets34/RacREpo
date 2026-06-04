using System;
using System.Collections.Generic;

public class MIPSBasicBlockBuilder
{
    private readonly PS2RAM _ram;

    public MIPSBasicBlockBuilder(PS2RAM ram)
    {
        _ram = ram;
    }

    public List<BasicBlock> Build(uint startAddress, int maxInstructions = 500)
    {
        Console.WriteLine("[CFG] Building basic blocks...");

        var blocks = new List<BasicBlock>();
        var visited = new HashSet<uint>();
        var blockMap = new Dictionary<uint, BasicBlock>();
        var workQueue = new Queue<uint>();

        workQueue.Enqueue(startAddress);

        while (workQueue.Count > 0)
        {
            uint pc = workQueue.Dequeue();
            if (visited.Contains(pc))
                continue;

            var block = new BasicBlock(pc);
            blockMap[pc] = block;
            blocks.Add(block);

            uint currentPc = pc;

            for (int i = 0; i < maxInstructions; i++)
            {
                if (visited.Contains(currentPc))
                    break;

                visited.Add(currentPc);

                uint instr = _ram.Read32(currentPc);
                block.Instructions.Add(instr);

                if (IsBranch(instr))
                {
                    uint target = GetBranchTarget(currentPc, instr);
                    uint fallthrough = currentPc + 8;
                    block.Instructions.Add(_ram.Read32(currentPc + 4));

                    block.Exits.Add(target);
                    block.Exits.Add(fallthrough);

                    Console.WriteLine($"[CFG] Branch at 0x{currentPc:X8} → 0x{target:X8} / 0x{fallthrough:X8}");

                    if (!visited.Contains(target) && !blockMap.ContainsKey(target))
                        workQueue.Enqueue(target);

                    if (!visited.Contains(fallthrough) && !blockMap.ContainsKey(fallthrough))
                        workQueue.Enqueue(fallthrough);

                    break;
                }

                if (IsJump(instr))
                {
                    uint target = GetJumpTarget(currentPc, instr);
                    block.Instructions.Add(_ram.Read32(currentPc + 4));
                    block.Exits.Add(target);

                    Console.WriteLine($"[CFG] Jump at 0x{currentPc:X8} → 0x{target:X8}");

                    if (!visited.Contains(target) && !blockMap.ContainsKey(target))
                        workQueue.Enqueue(target);

                    break;
                }

                if (IsReturn(instr))
                {
                    block.Instructions.Add(_ram.Read32(currentPc + 4));
                    Console.WriteLine($"[CFG] Return at 0x{currentPc:X8}");
                    break;
                }

                currentPc += 4;
            }
        }

        Console.WriteLine($"[CFG] Blocks created: {blocks.Count}");

        return blocks;
    }

    // =========================
    // BASIC BLOCK STRUCTURE
    // =========================
    public class BasicBlock
    {
        public uint StartAddress;
        public List<uint> Instructions = new();
        public List<uint> Exits = new();

        public BasicBlock(uint start)
        {
            StartAddress = start;
        }
    }

    // =========================
    // INSTRUCTION HELPERS
    // =========================
    private bool IsBranch(uint instr)
    {
        uint op = instr >> 26;
        return op == 0x04 || op == 0x05; // beq, bne
    }

    private bool IsJump(uint instr)
    {
        uint op = instr >> 26;
        return op == 0x02 || op == 0x03; // j, jal
    }

    private bool IsReturn(uint instr)
    {
        return (instr & 0x3F) == 0x08; // jr
    }

    private uint GetBranchTarget(uint pc, uint instr)
    {
        short imm = (short)(instr & 0xFFFF);
        return (uint)(pc + 4 + (imm << 2));
    }

    private uint GetJumpTarget(uint pc, uint instr)
    {
        uint target = instr & 0x03FFFFFF;
        return (pc & 0xF0000000) | (target << 2);
    }
}

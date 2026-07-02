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

                uint raw = _ram.Read32(currentPc);

                MIPSInstruction instr =
                    MIPSDisassembler.Decode(currentPc, raw);

                block.Instructions.Add(instr);

                // =========================
                // BRANCH
                // =========================
                if (instr.IsBranch)
                {
                    uint target = instr.BranchTarget;
                    uint fallthrough = currentPc + 8;

                    uint delayRaw = _ram.Read32(currentPc + 4);

                    block.Instructions.Add(
                        MIPSDisassembler.Decode(
                            currentPc + 4,
                            delayRaw
                        )
                    );

                    block.Exits.Add(target);
                    block.Exits.Add(fallthrough);

                    Console.WriteLine(
                        $"[CFG] Branch at 0x{currentPc:X8} → 0x{target:X8} / 0x{fallthrough:X8}"
                    );

                    if (!visited.Contains(target) &&
                        !blockMap.ContainsKey(target))
                    {
                        workQueue.Enqueue(target);
                    }

                    if (!visited.Contains(fallthrough) &&
                        !blockMap.ContainsKey(fallthrough))
                    {
                        workQueue.Enqueue(fallthrough);
                    }

                    break;
                }

                // =========================
                // JUMP / CALL
                // =========================
                if (instr.IsJump)
                {
                    uint target = instr.TargetAddress;

                    uint delayRaw = _ram.Read32(currentPc + 4);

                    block.Instructions.Add(
                        MIPSDisassembler.Decode(
                            currentPc + 4,
                            delayRaw
                        )
                    );

                    block.Exits.Add(target);

                    Console.WriteLine(
                        $"[CFG] Jump at 0x{currentPc:X8} → 0x{target:X8}"
                    );

                    if (!visited.Contains(target) &&
                        !blockMap.ContainsKey(target))
                    {
                        workQueue.Enqueue(target);
                    }

                    break;
                }

                // =========================
                // RETURN
                // =========================
                if (instr.IsReturn)
                {
                    uint delayRaw = _ram.Read32(currentPc + 4);

                    block.Instructions.Add(
                        MIPSDisassembler.Decode(
                            currentPc + 4,
                            delayRaw
                        )
                    );

                    Console.WriteLine(
                        $"[CFG] Return at 0x{currentPc:X8}"
                    );

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

        public List<MIPSInstruction> Instructions = new();

        public List<uint> Exits = new();

        public BasicBlock(uint start)
        {
            StartAddress = start;
        }
    }
}
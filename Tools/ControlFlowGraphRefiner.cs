using System;
using System.Collections.Generic;
using System.Linq;

public class ControlFlowGraphRefiner
{
    public class RefinedBlock
    {
        public uint StartAddress;

        // CHANGED:
        public List<MIPSInstruction> Instructions = new();

        public List<uint> Successors = new();

        public bool IsLoopHeader;
        public bool IsLoopBackEdge;
        public bool IsIfHeader;
        public bool IsElseHeader;

        public uint? LoopBackTarget;
    }

    private readonly Dictionary<uint, RefinedBlock> _blocks = new();

    // =========================================================
    // ENTRY POINT
    // =========================================================
    public List<RefinedBlock> Refine(List<MIPSBasicBlockBuilder.BasicBlock> rawBlocks)
    {
        _blocks.Clear();

        Build(rawBlocks);
        DetectBackEdges();
        DetectLoops();
        DetectIfElse();

        ExpandReachability();

        return _blocks.Values
            .OrderBy(b => b.StartAddress)
            .ToList();
    }

    // =========================================================
    // BUILD
    // =========================================================
    private void Build(List<MIPSBasicBlockBuilder.BasicBlock> rawBlocks)
    {
        foreach (var block in rawBlocks)
        {
            if (_blocks.ContainsKey(block.StartAddress))
                continue;

            _blocks[block.StartAddress] = new RefinedBlock
            {
                StartAddress = block.StartAddress,

                // NOW COPIES MIPSInstruction OBJECTS
                Instructions = block.Instructions.ToList(),

                Successors = block.Exits
                    .Distinct()
                    .ToList()
            };
        }
    }

    // =========================================================
    // LOOP DETECTION (BACK EDGES)
    // =========================================================
    private void DetectBackEdges()
    {
        foreach (var block in _blocks.Values)
        {
            foreach (var succ in block.Successors)
            {
                if (_blocks.ContainsKey(succ) &&
                    succ <= block.StartAddress)
                {
                    block.IsLoopBackEdge = true;
                    block.LoopBackTarget = succ;
                }
            }
        }
    }

    // =========================================================
    // LOOP HEADERS
    // =========================================================
    private void DetectLoops()
    {
        foreach (var block in _blocks.Values)
        {
            if (block.LoopBackTarget == null)
                continue;

            if (_blocks.ContainsKey(block.LoopBackTarget.Value))
            {
                _blocks[block.LoopBackTarget.Value]
                    .IsLoopHeader = true;
            }
        }
    }

    // =========================================================
    // IF / ELSE DETECTION
    // =========================================================
    private void DetectIfElse()
    {
        foreach (var block in _blocks.Values)
        {
            if (block.Successors.Count != 2)
                continue;

            uint a = block.Successors[0];
            uint b = block.Successors[1];

            if (!_blocks.ContainsKey(a) ||
                !_blocks.ContainsKey(b))
                continue;

            block.IsIfHeader = true;

            if (a < b)
                _blocks[b].IsElseHeader = true;
            else
                _blocks[a].IsElseHeader = true;
        }
    }

    // =========================================================
    // REACHABILITY EXPANSION
    // =========================================================
    private void ExpandReachability()
    {
        bool changed;

        do
        {
            changed = false;

            foreach (var block in _blocks.Values)
            {
                var additions = new List<uint>();

                foreach (var succ in block.Successors.ToList())
                {
                    if (!_blocks.TryGetValue(succ, out var target))
                        continue;

                    foreach (var t in target.Successors)
                    {
                        if (!_blocks.ContainsKey(t))
                            continue;

                        if (!block.Successors.Contains(t))
                        {
                            additions.Add(t);
                            changed = true;
                        }
                    }
                }

                if (additions.Count > 0)
                {
                    block.Successors.AddRange(
                        additions.Distinct()
                    );
                }
            }

        } while (changed);
    }
}
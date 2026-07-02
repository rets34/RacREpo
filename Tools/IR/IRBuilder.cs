using System;
using System.Collections.Generic;
using Tools;

namespace Tools.IR;

public class IRBuilder
{
    public IRFunction Build(uint entry, List<MIPSBasicBlockBuilder.BasicBlock> blocks)
    {
        var func = new IRFunction
        {
            EntryPoint = entry
        };

        foreach (var block in blocks)
        {
            var irBlock = new IRBlock
            {
                StartAddress = block.StartAddress
            };

            foreach (var inst in block.Instructions)
            {
                irBlock.Instructions.Add(Convert(inst));
            }

            func.Blocks.Add(irBlock);
        }

        return func;
    }

    private IRInstruction Convert(MIPSInstruction inst)
    {
        var ir = new IRInstruction
        {
            Address = inst.Address,
            Op = Map(inst),
            Comment = inst.ToString()
        };

        // VERY BASIC operand mapping (temporary but correct)

        switch (inst.Type)
        {
            case MIPSInstructionType.RType:
                ir.Dest = $"r{inst.Rd}";
                ir.Src = new[]
                {
                    $"r{inst.Rs}",
                    $"r{inst.Rt}"
                };
                break;

            case MIPSInstructionType.IType:
                if (inst.IsLoad)
                {
                    ir.Dest = $"r{inst.Rt}";
                    ir.Src = new[] { $"mem[r{inst.Rs} + {inst.Immediate}]" };
                }
                else if (inst.IsStore)
                {
                    ir.Dest = $"mem[r{inst.Rs} + {inst.Immediate}]";
                    ir.Src = new[] { $"r{inst.Rt}" };
                }
                else
                {
                    ir.Dest = $"r{inst.Rt}";
                    ir.Src = new[] { $"r{inst.Rs}", inst.Immediate.ToString() };
                }
                break;

            case MIPSInstructionType.JType:
                ir.Dest = inst.IsReturn ? "return" : null;
                ir.Src = new[] { inst.TargetAddress.ToString("X8") };
                break;

            default:
                ir.Dest = null;
                ir.Src = Array.Empty<string>();
                break;
        }

        return ir;
    }

    private IROp Map(MIPSInstruction inst)
    {
        if (inst.IsLoad) return IROp.Load;
        if (inst.IsStore) return IROp.Store;
        if (inst.IsBranch) return IROp.Branch;
        if (inst.IsJump) return IROp.Jump;
        if (inst.IsReturn) return IROp.Return;
        if (inst.IsSyscall) return IROp.Syscall;

        return inst.Mnemonic switch
        {
            "add" or "addu" => IROp.Add,
            "sub" or "subu" => IROp.Sub,
            "mul" => IROp.Mul,
            "div" => IROp.Div,
            "jr" => IROp.Return,
            "jal" => IROp.Call,
            _ => IROp.Unknown
        };
    }
}
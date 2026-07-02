using System;

public static class MIPSDisassembler
{
    public static MIPSInstruction Decode(uint address, uint instruction)
    {
        uint opcode = instruction >> 26;

        var result = new MIPSInstruction
        {
            Address = address,
            Raw = instruction,
            Mnemonic = "unknown",
            Operands = $"0x{instruction:X8}",
            Opcode = opcode
        };

        switch (opcode)
        {
            case 0x00:
                DecodeSpecial(instruction, result);
                break;

            case 0x01:
                DecodeRegImm(address, instruction, result);
                break;

            case 0x02:
                DecodeJType(address, instruction, "j", result);
                break;

            case 0x03:
                DecodeJType(address, instruction, "jal", result);
                break;

            case 0x04:
            case 0x05:
            case 0x08:
            case 0x09:
            case 0x0A:
            case 0x0B:
            case 0x0C:
            case 0x0D:
            case 0x0E:
            case 0x0F:
            case 0x20:
            case 0x21:
            case 0x23:
            case 0x24:
            case 0x25:
            case 0x28:
            case 0x29:
            case 0x2B:
                DecodeIType(address, instruction, opcode, result);
                break;

            default:
                result.Operands = $"0x{instruction:X8}";
                break;
        }

        return result;
    }

    private static void DecodeSpecial(uint instruction, MIPSInstruction result)
    {
        result.Type = MIPSInstructionType.RType;

        result.Rs = (instruction >> 21) & 0x1F;
        result.Rt = (instruction >> 16) & 0x1F;
        result.Rd = (instruction >> 11) & 0x1F;
        result.Shamt = (instruction >> 6) & 0x1F;
        result.Funct = instruction & 0x3F;

        // =========================
        // PS2 / MIPS SYSCALL
        // =========================
        if (result.Funct == 0x0C)
        {
            result.Mnemonic = "syscall";
            result.IsSyscall = true;

            // FIX: correct uint extraction (NO cast to int)
            result.SyscallCode = (int)((instruction >> 6) & 0xFFFF);

            // wire into handler for readable decompilation
            result.Operands = PS2SyscallHandler.Resolve((int)result.SyscallCode);

            return;
        }

        switch (result.Funct)
        {
            case 0x00:
                result.Mnemonic = "sll";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rt)}, {result.Shamt}";
                break;

            case 0x02:
                result.Mnemonic = "srl";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rt)}, {result.Shamt}";
                break;

            case 0x03:
                result.Mnemonic = "sra";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rt)}, {result.Shamt}";
                break;

            case 0x08:
                result.IsReturn = true;
                result.Mnemonic = "jr";
                result.Operands = Reg(result.Rs);
                break;

            case 0x09:
                result.IsJump = true;
                result.Mnemonic = "jalr";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}";
                break;

            case 0x20:
                result.Mnemonic = "add";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x21:
                result.Mnemonic = "addu";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x22:
                result.Mnemonic = "sub";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x23:
                result.Mnemonic = "subu";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x24:
                result.Mnemonic = "and";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x25:
                result.Mnemonic = "or";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x26:
                result.Mnemonic = "xor";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x27:
                result.Mnemonic = "nor";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x2A:
                result.Mnemonic = "slt";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            case 0x2B:
                result.Mnemonic = "sltu";
                result.Operands = $"{Reg(result.Rd)}, {Reg(result.Rs)}, {Reg(result.Rt)}";
                break;

            default:
                result.Mnemonic = "unknown";
                result.Operands = $"0x{instruction:X8}";
                break;
        }
    }

    private static void DecodeJType(uint address, uint instruction, string mnemonic, MIPSInstruction result)
    {
        uint target = instruction & 0x03FFFFFF;

        result.Type = MIPSInstructionType.JType;
        result.IsJump = true;

        result.Target = target;
        result.TargetAddress = (address & 0xF0000000) | (target << 2);

        result.Mnemonic = mnemonic;
        result.Operands = $"0x{result.TargetAddress:X8}";
    }

private static void DecodeRegImm(
    uint address,
    uint instruction,
    MIPSInstruction result)
{
    result.Type = MIPSInstructionType.IType;

    result.Rs = (instruction >> 21) & 0x1F;
    uint rt = (instruction >> 16) & 0x1F;

    result.Immediate = (short)(instruction & 0xFFFF);

    uint target =
        (uint)(address + 4 + (result.Immediate << 2));

    switch (rt)
    {
        case 0x00:
            result.IsBranch = true;
            result.BranchTarget = target;
            result.Mnemonic = "bltz";
            result.Operands =
                $"{Reg(result.Rs)}, 0x{target:X8}";
            break;

        case 0x01:
            result.IsBranch = true;
            result.BranchTarget = target;
            result.Mnemonic = "bgez";
            result.Operands =
                $"{Reg(result.Rs)}, 0x{target:X8}";
            break;

        default:
            result.Mnemonic = "regimm";
            result.Operands = $"0x{instruction:X8}";
            break;
    }
}

    private static void DecodeIType(
    uint address,
    uint instruction,
    uint opcode,
    MIPSInstruction result)
{
    result.Type = MIPSInstructionType.IType;

    result.Rs = (instruction >> 21) & 0x1F;
    result.Rt = (instruction >> 16) & 0x1F;

    result.Immediate = (short)(instruction & 0xFFFF);
    result.UImmediate = (ushort)(instruction & 0xFFFF);

    switch (opcode)
    {
        case 0x04: // beq
        {
            result.IsBranch = true;

            result.BranchTarget =
                (uint)(address + 4 + (result.Immediate << 2));

            result.Mnemonic = "beq";

            result.Operands =
                $"{Reg(result.Rs)}, {Reg(result.Rt)}, 0x{result.BranchTarget:X8}";
            break;
        }

        case 0x05: // bne
        {
            result.IsBranch = true;

            result.BranchTarget =
                (uint)(address + 4 + (result.Immediate << 2));

            result.Mnemonic = "bne";

            result.Operands =
                $"{Reg(result.Rs)}, {Reg(result.Rt)}, 0x{result.BranchTarget:X8}";
            break;
        }

        case 0x08:
            result.Mnemonic = "addi";
            result.Operands =
                $"{Reg(result.Rt)}, {Reg(result.Rs)}, {result.Immediate}";
            break;

        case 0x09:
            result.Mnemonic = "addiu";
            result.Operands =
                $"{Reg(result.Rt)}, {Reg(result.Rs)}, {result.Immediate}";
            break;

        case 0x0C:
            result.Mnemonic = "andi";
            result.Operands =
                $"{Reg(result.Rt)}, {Reg(result.Rs)}, 0x{result.UImmediate:X}";
            break;

        case 0x0D:
            result.Mnemonic = "ori";
            result.Operands =
                $"{Reg(result.Rt)}, {Reg(result.Rs)}, 0x{result.UImmediate:X}";
            break;

        case 0x0E:
            result.Mnemonic = "xori";
            result.Operands =
                $"{Reg(result.Rt)}, {Reg(result.Rs)}, 0x{result.UImmediate:X}";
            break;

        case 0x0F:
            result.Mnemonic = "lui";
            result.Operands =
                $"{Reg(result.Rt)}, 0x{result.UImmediate:X}";
            break;

        case 0x20:
            result.IsLoad = true;
            result.Mnemonic = "lb";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        case 0x21:
            result.IsLoad = true;
            result.Mnemonic = "lh";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        case 0x23:
            result.IsLoad = true;
            result.Mnemonic = "lw";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        case 0x24:
            result.IsLoad = true;
            result.Mnemonic = "lbu";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        case 0x25:
            result.IsLoad = true;
            result.Mnemonic = "lhu";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        case 0x28:
            result.IsStore = true;
            result.Mnemonic = "sb";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        case 0x29:
            result.IsStore = true;
            result.Mnemonic = "sh";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        case 0x2B:
            result.IsStore = true;
            result.Mnemonic = "sw";
            result.Operands =
                $"{Reg(result.Rt)}, {result.Immediate}({Reg(result.Rs)})";
            break;

        default:
            result.Mnemonic = "unknown";
            result.Operands = $"0x{instruction:X8}";
            break;
    }
}
    private static string Reg(uint r)
    {
        return r switch
        {
            0 => "zero",
            1 => "at",
            2 => "v0",
            3 => "v1",
            4 => "a0",
            5 => "a1",
            6 => "a2",
            7 => "a3",
            8 => "t0",
            9 => "t1",
            10 => "t2",
            11 => "t3",
            12 => "t4",
            13 => "t5",
            14 => "t6",
            15 => "t7",
            16 => "s0",
            17 => "s1",
            18 => "s2",
            19 => "s3",
            20 => "s4",
            21 => "s5",
            22 => "s6",
            23 => "s7",
            24 => "t8",
            25 => "t9",
            28 => "gp",
            29 => "sp",
            30 => "fp",
            31 => "ra",
            _ => $"r{r}"
        };
    }
}
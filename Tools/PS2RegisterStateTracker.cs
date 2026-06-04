using System;

public class PS2RegisterStateTracker
{
    // MIPS 32 general-purpose registers
    private readonly int[] _regs = new int[32];

    public void Reset()
    {
        Array.Clear(_regs, 0, _regs.Length);
    }

    public int Get(uint reg)
    {
        if (reg == 0) return 0;
        return _regs[reg];
    }

    public void Set(uint reg, int value)
    {
        if (reg == 0) return;
        _regs[reg] = value;
    }

    public void Apply(MIPSInstruction instr)
    {
        if (instr == null)
            return;

        // =========================
        // R-type arithmetic
        // =========================
        switch (instr.Mnemonic)
        {
            case "add":
            case "addu":
                Set(instr.Rd, Get(instr.Rs) + Get(instr.Rt));
                break;

            case "sub":
            case "subu":
                Set(instr.Rd, Get(instr.Rs) - Get(instr.Rt));
                break;

            case "and":
                Set(instr.Rd, Get(instr.Rs) & Get(instr.Rt));
                break;

            case "or":
                Set(instr.Rd, Get(instr.Rs) | Get(instr.Rt));
                break;

            case "xor":
                Set(instr.Rd, Get(instr.Rs) ^ Get(instr.Rt));
                break;

            case "nor":
                Set(instr.Rd, ~(Get(instr.Rs) | Get(instr.Rt)));
                break;

            case "slt":
                Set(instr.Rd, Get(instr.Rs) < Get(instr.Rt) ? 1 : 0);
                break;
        }

        // =========================
        // Immediate arithmetic
        // =========================
        switch (instr.Mnemonic)
        {
            case "addi":
            case "addiu":
                Set(instr.Rt, Get(instr.Rs) + instr.Immediate);
                break;

            case "andi":
                Set(instr.Rt, Get(instr.Rs) & instr.UImmediate);
                break;

            case "ori":
                Set(instr.Rt, Get(instr.Rs) | instr.UImmediate);
                break;

            case "xori":
                Set(instr.Rt, Get(instr.Rs) ^ instr.UImmediate);
                break;

            case "lui":
                Set(instr.Rt, unchecked((int)(instr.UImmediate << 16)));
                break;
        }

        // =========================
        // Loads / stores (approx model)
        // =========================
        switch (instr.Mnemonic)
        {
            case "lw":
            case "lb":
            case "lbu":
            case "lh":
            case "lhu":
                Set(instr.Rt, unchecked((int)0xDEADBEEF));
                break;
        }

        // =========================
        // SYSCALL HANDLING (FIXED + CONNECTED)
        // =========================
        if (instr.IsSyscall)
        {
            // Extract syscall ID safely
            int syscallId = unchecked((int)instr.SyscallCode);

            // Resolve semantic layer
            var semantic = MIPSSemanticLayer.Analyze(syscallId);

            var ctx = new PS2SyscallContext(
                instr.Address,
                syscallId
            )
            {
                SyscallName = semantic.Name,

                a0 = Get(4),
                a1 = Get(5),
                a2 = Get(6),
                a3 = Get(7),

                v0 = Get(2),
                v1 = Get(3)
            };

            PS2SyscallLog.Record(ctx);
        }
    }
}
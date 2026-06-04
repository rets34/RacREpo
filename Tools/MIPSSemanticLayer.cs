using System;

public static class MIPSSemanticLayer
{
    public enum SyscallCategory
    {
        Unknown,
        Kernel,
        Interrupt,
        IO,
        Debug,
        Process
    }

    public struct SyscallSemantic
    {
        public string Name;
        public string Emit;
        public SyscallCategory Category;
    }

    public static SyscallSemantic Analyze(int syscallId)
    {
        return syscallId switch
        {
            // =========================
            // PS2-style kernel control
            // =========================
            0x3C => new SyscallSemantic
            {
                Name = "FlushCache",
                Emit = "FlushCache();",
                Category = SyscallCategory.Kernel
            },

            0x3D => new SyscallSemantic
            {
                Name = "EnableInterrupts",
                Emit = "EnableInterrupts();",
                Category = SyscallCategory.Interrupt
            },

            0x3E => new SyscallSemantic
            {
                Name = "DisableInterrupts",
                Emit = "DisableInterrupts();",
                Category = SyscallCategory.Interrupt
            },

            // =========================
            // Debug / bootstrap style
            // =========================
            0x01 => new SyscallSemantic
            {
                Name = "Exit",
                Emit = "ExitProgram();",
                Category = SyscallCategory.Process
            },

            0x02 => new SyscallSemantic
            {
                Name = "Print",
                Emit = "PrintSyscall();",
                Category = SyscallCategory.Debug
            },

            // =========================
            // IO / unknown hardware layer
            // =========================
            0x10 => new SyscallSemantic
            {
                Name = "IO",
                Emit = "// IO syscall (unmapped)",
                Category = SyscallCategory.IO
            },

            _ => new SyscallSemantic
            {
                Name = $"Syscall_{syscallId:X}",
                Emit = $"// unhandled syscall 0x{syscallId:X}",
                Category = SyscallCategory.Unknown
            }
        };
    }

    public static bool IsCritical(SyscallCategory category)
    {
        return category switch
        {
            SyscallCategory.Kernel => true,
            SyscallCategory.Interrupt => true,
            _ => false
        };
    }
}
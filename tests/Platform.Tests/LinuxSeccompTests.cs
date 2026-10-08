using System.Buffers.Binary;
using System.Runtime.InteropServices;
using VisualWeb.Platform.Linux.Sandbox;
using Xunit;

namespace VisualWeb.Platform.Tests;

public sealed class LinuxSeccompTests
{
    [Theory]
    [InlineData(Architecture.X64, 0xc000003eU)]
    [InlineData(Architecture.Arm64, 0xc00000b7U)]
    public void ExportedFilterPreservesRequiredActionsAndRejectsOtherArchitectures(Architecture architecture, uint audit)
    {
        if (!OperatingSystem.IsLinux()) { return; }
        var path = Path.Combine(Path.GetTempPath(), "visualweb-filter-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var file = File.Create(path))
            {
                LinuxRendererSandbox.ExportFilter(file.SafeFileHandle.DangerousGetHandle().ToInt32(), architecture);
            }
            var filter = File.ReadAllBytes(path);
            Assert.NotEmpty(filter);
            Assert.Equal(0, filter.Length % 8);
            foreach (var name in LinuxRendererSandbox.AllowedSyscalls(architecture))
            {
                Assert.Equal(0x7fff0000U, Action(name));
            }
            foreach (var name in new[] { "socket", "socketpair", "ptrace", "process_vm_readv", "process_vm_writev",
                "mount", "umount2", "unshare", "setns", "bpf", "io_uring_setup", "io_uring_enter", "io_uring_register" })
            {
                Assert.Equal(0x00050001U, Action(name));
            }
            if (architecture == Architecture.X64)
            {
                Assert.Equal(0x00050001U, Action("fork"));
                Assert.Equal(0x00050001U, Action("vfork"));
                Assert.Equal(0U, Evaluate(filter, audit, 0x40000000, 0));
            }
            foreach (var name in LinuxRendererSandbox.AllowedSyscalls(Architecture.X64)
                .Except(LinuxRendererSandbox.AllowedSyscalls(Architecture.Arm64)))
            {
                Assert.True(Native.seccomp_syscall_resolve_name_arch(0xc000003eU, name) >= 0);
                Assert.True(Native.seccomp_syscall_resolve_name_arch(0xc00000b7U, name) < 0);
            }
            Assert.Equal(0x00050026U, Action("clone3"));
            Assert.Equal(0x7fff0000U, Action("sched_setaffinity", 0));
            Assert.Equal(0x00050001U, Action("sched_setaffinity", 1));
            Assert.Equal(0x7fff0000U, Action("clone", 0x3d0f00));
            Assert.Equal(0x00050001U, Action("clone", 17));
            Assert.Equal(0x00050001U, Action("clone", 0x3d0f00 | 17));
            foreach (var flag in new ulong[] { 0x2000, 0x800000, 0x20000, 0x2000000, 0x4000000,
                0x8000000, 0x10000000, 0x20000000, 0x40000000 })
            {
                Assert.Equal(0x00050001U, Action("clone", 0x3d0f00 | flag));
            }
            foreach (var other in new[] { 0xc000003eU, 0xc00000b7U, 0x40000003U })
            {
                if (other != audit) { Assert.Equal(0U, Evaluate(filter, other, 0, 0)); }
            }
            Assert.Equal(0x00050001U, Evaluate(filter, audit, 999999, 0));

            uint Action(string name, ulong first = 0)
            {
                var number = Native.seccomp_syscall_resolve_name_arch(audit, name);
                Assert.True(number >= 0, $"Required {architecture} syscall {name} is absent.");
                return Evaluate(filter, audit, number, first);
            }
        }
        finally { File.Delete(path); }
    }

    private static uint Evaluate(byte[] filter, uint audit, int syscall, ulong first)
    {
        var data = new byte[64];
        BinaryPrimitives.WriteInt32LittleEndian(data, syscall);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), audit);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(16), first);
        uint accumulator = 0;
        for (var pc = 0; pc < filter.Length / 8; pc++)
        {
            var instruction = filter.AsSpan(pc * 8, 8);
            var code = BinaryPrimitives.ReadUInt16LittleEndian(instruction);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(instruction[4..]);
            switch (code)
            {
                case 0x20: accumulator = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(checked((int)value), 4)); break;
                case 0x54: accumulator &= value; break;
                case 0x15: pc += accumulator == value ? instruction[2] : instruction[3]; break;
                case 0x25: pc += accumulator > value ? instruction[2] : instruction[3]; break;
                case 0x35: pc += accumulator >= value ? instruction[2] : instruction[3]; break;
                case 0x45: pc += (accumulator & value) != 0 ? instruction[2] : instruction[3]; break;
                case 0x05: pc += checked((int)value); break;
                case 0x06: return value;
                default: throw new InvalidOperationException($"Unsupported test BPF instruction {code:x}.");
            }
        }
        throw new InvalidOperationException("Seccomp filter did not return an action.");
    }

    private static class Native
    {
        [DllImport("libseccomp.so.2")]
        internal static extern int seccomp_syscall_resolve_name_arch(uint architecture, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    }
}

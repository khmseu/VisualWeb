using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VisualWeb.Platform.Windows.Sandbox;

/// <summary>Starts a renderer in a capability-free AppContainer and bounded Job Object.</summary>
/// <remarks>References: <c>windows-appcontainer</c> and <c>windows-job-objects</c>.
/// <see href="https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer">AppContainer process launch</see>,
/// <see href="https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-getappcontainerfolderpath">AppContainer profile storage</see>,
/// <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute">process startup attributes</see>,
/// <see href="https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_extended_limit_information">Job Object memory limits</see>,
/// and <see href="https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects">Job Objects</see>.</remarks>
public static class WindowsRendererSandbox
{
    public const string Profile = "windows-appcontainer-job-v1";
    public const long MemoryLimitBytes = 512L * 1024 * 1024;
    public const uint ActiveProcessLimit = 1;
    private const int ProcessAttributeSecurityCapabilities = 0x00020009;
    private const int ProcessAttributeJobList = 0x0002000D;
    private const int ProcessAttributeHandleList = 0x00020002;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint GenericExecute = 0x20000000;
    private const uint Delete = 0x00010000;
    private const uint WriteDac = 0x00040000;
    private const uint WriteOwner = 0x00080000;
    private const uint DeleteChild = 0x00000040;
    private const uint ObjectInherit = 1;
    private const uint ContainerInherit = 2;
    private const int SetAccess = 2;
    private const int DenyAccess = 3;
    private const uint FileObject = 1;
    private const uint DaclSecurityInformation = 4;
    private const uint CreateSuspended = 4;
    private const uint CreateUnicodeEnvironment = 0x400;
    private const uint ExtendedStartupInfoPresent = 0x80000;
    private const uint StartfUseStdHandles = 0x100;
    private const uint HandleFlagInherit = 1;
    private const uint JobObjectLimitActiveProcess = 8;
    private const uint JobObjectLimitProcessMemory = 0x100;
    private const uint JobObjectLimitJobMemory = 0x200;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;
    private const uint JobObjectCpuRateControlEnable = 1;
    private const uint JobObjectCpuRateControlHardCap = 4;

    public static void RequireSupport()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
        {
            throw new PlatformNotSupportedException("Windows renderer confinement requires Windows 10 version 1709 or later.");
        }
    }

    private static void DenyAppContainerWrites(string path, IntPtr sid)
    {
        var trustee = new Native.Trustee
        {
            TrusteeForm = 0,
            TrusteeType = 1,
            Name = sid
        };
        var access = new Native.ExplicitAccess
        {
            Permissions = GenericWrite | Delete | DeleteChild | WriteDac | WriteOwner,
            AccessMode = DenyAccess,
            Inheritance = ObjectInherit | ContainerInherit,
            Trustee = trustee
        };
        var result = Native.GetNamedSecurityInfoW(path, FileObject, DaclSecurityInformation,
            IntPtr.Zero, IntPtr.Zero, out var existingAcl, IntPtr.Zero, out var descriptor);
        if (result != 0) { throw new Win32Exception(unchecked((int)result), "Cannot inspect AppContainer profile storage permissions."); }
        IntPtr acl = IntPtr.Zero;
        try
        {
            result = Native.SetEntriesInAclW(1, ref access, existingAcl, out acl);
            if (result != 0) { throw new Win32Exception(unchecked((int)result), "Cannot restrict AppContainer profile storage."); }
            result = Native.SetNamedSecurityInfoW(path, FileObject, DaclSecurityInformation,
                IntPtr.Zero, IntPtr.Zero, acl, IntPtr.Zero);
            if (result != 0) { throw new Win32Exception(unchecked((int)result), "Cannot deny renderer writes to AppContainer profile storage."); }
        }
        finally
        {
            if (acl != IntPtr.Zero) { _ = Native.LocalFree(acl); }
            if (descriptor != IntPtr.Zero) { _ = Native.LocalFree(descriptor); }
        }
    }

    private static string GetAppContainerFolderPath(IntPtr sid)
    {
        var result = Native.GetAppContainerFolderPath(sid, out var path);
        if (result != 0) { throw new Win32Exception(result, "Cannot resolve AppContainer profile storage."); }
        try
        {
            return Marshal.PtrToStringUni(path)
                ?? throw new InvalidOperationException("AppContainer profile storage path is empty.");
        }
        finally { Native.CoTaskMemFree(path); }
    }

    internal static int RemoveAppContainerProfile(string name) => Native.DeleteAppContainerProfile(name);

    public static void VerifyWorker()
    {
        RequireSupport();
        if (!Native.OpenProcessToken(Native.GetCurrentProcess(), 0x0008, out var token))
        {
            throw Native.Failure("open renderer token");
        }
        using (token)
        {
            var value = 0;
            if (!Native.GetTokenInformation(token, 29, ref value, sizeof(int), out _) || value != 1)
            {
                throw new InvalidOperationException("Renderer is not running with an AppContainer token.");
            }
        }
        if (!Native.IsProcessInJob(Native.GetCurrentProcess(), IntPtr.Zero, out var inJob) || !inJob)
        {
            throw new InvalidOperationException("Renderer is not assigned to its required Job Object.");
        }
        var limits = new Native.ExtendedLimitInformation();
        var limitFlags = JobObjectLimitActiveProcess | JobObjectLimitProcessMemory
            | JobObjectLimitJobMemory | JobObjectLimitKillOnJobClose;
        if (!Native.QueryInformationJobObject(IntPtr.Zero, 9, ref limits,
                (uint)Marshal.SizeOf<Native.ExtendedLimitInformation>(), IntPtr.Zero)
            || (limits.BasicLimitInformation.LimitFlags & limitFlags) != limitFlags
            || limits.BasicLimitInformation.ActiveProcessLimit != ActiveProcessLimit
            || limits.ProcessMemoryLimit.ToUInt64() != (ulong)MemoryLimitBytes
            || limits.JobMemoryLimit.ToUInt64() != (ulong)MemoryLimitBytes)
        {
            throw new InvalidOperationException("Renderer Job Object memory/process limits are absent or different.");
        }
        var cpu = new Native.CpuRateControl();
        if (!Native.QueryInformationJobObject(IntPtr.Zero, 15, ref cpu,
                (uint)Marshal.SizeOf<Native.CpuRateControl>(), IntPtr.Zero)
            || (cpu.ControlFlags & (JobObjectCpuRateControlEnable | JobObjectCpuRateControlHardCap))
                != (JobObjectCpuRateControlEnable | JobObjectCpuRateControlHardCap)
            || cpu.CpuRate != 10000)
        {
            throw new InvalidOperationException("Renderer Job Object CPU hard cap is absent or different.");
        }
        if (Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics") != "0"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
        {
            throw new InvalidOperationException("Required confined .NET runtime configuration is absent.");
        }
    }

    public static WindowsRendererWorker Start(string applicationDirectory, string fontPath,
        string runtimeDirectory, string workerAssembly = "VisualWeb.Renderer.dll",
        IReadOnlyList<string>? workerArguments = null)
    {
        RequireSupport();
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDirectory);
        if (Path.GetFileName(workerAssembly) != workerAssembly || !workerAssembly.EndsWith(".dll", StringComparison.Ordinal))
        {
            throw new ArgumentException("Sandbox worker assembly must be a trusted DLL filename.", nameof(workerAssembly));
        }

        var appSource = Existing(applicationDirectory, directory: true);
        var fontSource = Existing(fontPath, directory: false);
        var runtimeSource = Existing(runtimeDirectory, directory: true);
        if (!File.Exists(Path.Combine(appSource, workerAssembly)))
        {
            throw new FileNotFoundException("Windows confinement requires the framework-dependent renderer DLL.", workerAssembly);
        }

        var runtimeRoot = new DirectoryInfo(runtimeSource).Parent?.Parent?.Parent?.FullName
            ?? throw new InvalidOperationException("Unsupported .NET runtime directory layout.");
        var hostSource = Existing(Path.Combine(runtimeRoot, "dotnet.exe"), directory: false);
        var hostFxrSource = Existing(Path.Combine(runtimeRoot, "host", "fxr"), directory: true);
        var runtimeVersion = Path.GetFileName(Path.TrimEndingDirectorySeparator(runtimeSource));
        var profileName = "VisualWeb.Renderer-" + Guid.NewGuid().ToString("N");
        var staging = Path.Combine(Path.GetTempPath(), "VisualWeb", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        IntPtr appContainerSid = IntPtr.Zero;
        SafeFileHandle? job = null;
        try
        {
            var result = Native.CreateAppContainerProfile(profileName, "VisualWeb renderer",
                "Per-tab static renderer", IntPtr.Zero, 0, out appContainerSid);
            if (result != 0)
            {
                throw new Win32Exception(result, "Cannot create a unique renderer AppContainer profile.");
            }

            var profileStorage = GetAppContainerFolderPath(appContainerSid);
            Directory.CreateDirectory(profileStorage);
            DenyAppContainerWrites(profileStorage, appContainerSid);

            var stagedRoot = Path.Combine(staging, "worker");
            var stagedApp = Path.Combine(stagedRoot, "app");
            var stagedRuntime = Path.Combine(stagedRoot, "runtime");
            var stagedFont = Path.Combine(stagedRoot, "font", "font");
            var stagedTemp = Path.Combine(stagedRoot, "tmp");
            Directory.CreateDirectory(stagedRoot);
            SetAppContainerAccess(stagedRoot, appContainerSid, writable: false);

            CopyTree(appSource, stagedApp);
            CopyTree(hostFxrSource, Path.Combine(stagedRuntime, "host", "fxr"));
            CopyTree(runtimeSource, Path.Combine(stagedRuntime, "shared", "Microsoft.NETCore.App", runtimeVersion));
            File.Copy(hostSource, Path.Combine(stagedRuntime, "dotnet.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(stagedFont)!);
            File.Copy(fontSource, stagedFont);
            Directory.CreateDirectory(stagedTemp);
            SetAppContainerAccess(stagedTemp, appContainerSid, writable: false);

            job = CreateJob();
            return CreateWorker(profileName, appContainerSid, job, staging, profileStorage, stagedApp,
                stagedRuntime, stagedFont, stagedTemp, workerAssembly, workerArguments);
        }
        catch
        {
            job?.Dispose();
            if (appContainerSid != IntPtr.Zero) { _ = Native.FreeSid(appContainerSid); }
            _ = Native.DeleteAppContainerProfile(profileName);
            try { Directory.Delete(staging, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static WindowsRendererWorker CreateWorker(string profileName, IntPtr sid, SafeFileHandle job,
        string staging, string profileStorage, string app, string runtime, string font, string temp,
        string workerAssembly, IReadOnlyList<string>? workerArguments)
    {
        CreatePipePair(childReads: true, out var childInput, out var parentInput);
        CreatePipePair(childReads: false, out var parentOutput, out var childOutput);
        CreatePipePair(childReads: false, out var parentError, out var childError);
        var streamsOwnHandles = false;
        try
        {
            using (childInput)
            using (childOutput)
            using (childError)
            {
                var handles = new[] { childInput.DangerousGetHandle(), childOutput.DangerousGetHandle(), childError.DangerousGetHandle() };
                var capabilities = new Native.SecurityCapabilities { AppContainerSid = sid };
                var attributeBytes = IntPtr.Zero;
                var attributeListInitialized = false;
                var size = IntPtr.Zero;
                var jobHandle = job.DangerousGetHandle();
                var jobHandles = Marshal.AllocHGlobal(IntPtr.Size);
                var inheritedHandles = Marshal.AllocHGlobal(3 * IntPtr.Size);
                var processInfo = default(Native.ProcessInformation);
                try
                {
                    Marshal.WriteIntPtr(jobHandles, jobHandle);
                    for (var index = 0; index < handles.Length; index++)
                    {
                        Marshal.WriteIntPtr(inheritedHandles, index * IntPtr.Size, handles[index]);
                    }
                    _ = Native.InitializeProcThreadAttributeList(IntPtr.Zero, 3, 0, ref size);
                    if (size == IntPtr.Zero) { throw Native.Failure("size renderer process attributes"); }
                    attributeBytes = Marshal.AllocHGlobal(size);
                    if (!Native.InitializeProcThreadAttributeList(attributeBytes, 3, 0, ref size))
                    {
                        throw Native.Failure("initialize renderer process attributes");
                    }
                    attributeListInitialized = true;
                    if (!Native.UpdateProcThreadAttribute(attributeBytes, 0, ProcessAttributeSecurityCapabilities,
                            ref capabilities, (IntPtr)Marshal.SizeOf<Native.SecurityCapabilities>(), IntPtr.Zero, IntPtr.Zero)
                        || !Native.UpdateProcThreadAttribute(attributeBytes, 0, ProcessAttributeJobList,
                            jobHandles, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero)
                        || !Native.UpdateProcThreadAttribute(attributeBytes, 0, ProcessAttributeHandleList,
                            inheritedHandles, (IntPtr)(3 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                    {
                        throw Native.Failure("configure renderer process attributes");
                    }

                    var startup = new Native.StartupInfoEx
                    {
                        StartupInfo = new Native.StartupInfo
                        {
                            Cb = Marshal.SizeOf<Native.StartupInfoEx>(),
                            Flags = StartfUseStdHandles,
                            StandardInput = handles[0],
                            StandardOutput = handles[1],
                            StandardError = handles[2]
                        },
                        AttributeList = attributeBytes
                    };
                    var command = new List<string> { Path.Combine(runtime, "dotnet.exe"), Path.Combine(app, workerAssembly),
                    "--windows-sandbox-worker", "--font", font };
                    if (workerArguments is not null) { command.AddRange(workerArguments); }
                    var commandLine = new System.Text.StringBuilder(string.Join(" ", command.Select(QuoteArgument)));
                    var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    var environment = BuildEnvironment(runtime, temp, windows);
                    try
                    {
                        if (!Native.CreateProcessW(command[0], commandLine, IntPtr.Zero, IntPtr.Zero, true,
                                CreateSuspended | CreateUnicodeEnvironment | ExtendedStartupInfoPresent | 0x08000000,
                                environment, app, ref startup, out processInfo))
                        {
                            throw Native.Failure("start AppContainer renderer");
                        }
                    }
                    finally { Marshal.FreeHGlobal(environment); }
                    if (Native.ResumeThread(processInfo.Thread) == uint.MaxValue)
                    {
                        _ = Native.TerminateProcess(processInfo.Process, 1);
                        throw Native.Failure("resume AppContainer renderer");
                    }
                    _ = Native.CloseHandle(processInfo.Thread);
                    processInfo.Thread = IntPtr.Zero;
                    var process = Process.GetProcessById(unchecked((int)processInfo.ProcessId));
                    var input = new FileStream(parentInput, FileAccess.Write, 4096, isAsync: false);
                    var output = new FileStream(parentOutput, FileAccess.Read, 4096, isAsync: false);
                    var error = new FileStream(parentError, FileAccess.Read, 4096, isAsync: false);
                    var worker = new WindowsRendererWorker(process, job, staging, profileStorage, profileName, sid, input, output, error);
                    streamsOwnHandles = true;
                    return worker;
                }
                catch
                {
                    if (processInfo.Process != IntPtr.Zero)
                    {
                        _ = Native.TerminateProcess(processInfo.Process, 1);
                        _ = Native.WaitForSingleObject(processInfo.Process, uint.MaxValue);
                    }
                    throw;
                }
                finally
                {
                    if (processInfo.Thread != IntPtr.Zero) { _ = Native.CloseHandle(processInfo.Thread); }
                    if (processInfo.Process != IntPtr.Zero) { _ = Native.CloseHandle(processInfo.Process); }
                    if (attributeBytes != IntPtr.Zero)
                    {
                        if (attributeListInitialized) { Native.DeleteProcThreadAttributeList(attributeBytes); }
                        Marshal.FreeHGlobal(attributeBytes);
                    }
                    Marshal.FreeHGlobal(jobHandles);
                    Marshal.FreeHGlobal(inheritedHandles);
                }
            }
        }
        finally
        {
            if (!streamsOwnHandles)
            {
                parentInput.Dispose();
                parentOutput.Dispose();
                parentError.Dispose();
            }
        }
    }

    private static SafeFileHandle CreateJob()
    {
        var job = Native.CreateJobObjectW(IntPtr.Zero, null);
        if (job.IsInvalid) { job.Dispose(); throw Native.Failure("create renderer Job Object"); }
        var limits = new Native.ExtendedLimitInformation
        {
            BasicLimitInformation = new Native.BasicLimitInformation
            {
                LimitFlags = JobObjectLimitActiveProcess | JobObjectLimitProcessMemory
                    | JobObjectLimitJobMemory | JobObjectLimitKillOnJobClose,
                ActiveProcessLimit = ActiveProcessLimit
            },
            ProcessMemoryLimit = (UIntPtr)MemoryLimitBytes,
            JobMemoryLimit = (UIntPtr)MemoryLimitBytes
        };
        if (!Native.SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<Native.ExtendedLimitInformation>()))
        {
            job.Dispose();
            throw Native.Failure("set renderer Job Object limits");
        }
        var cpu = new Native.CpuRateControl { ControlFlags = JobObjectCpuRateControlEnable | JobObjectCpuRateControlHardCap, CpuRate = 10000 };
        if (!Native.SetInformationJobObject(job, 15, ref cpu, Marshal.SizeOf<Native.CpuRateControl>()))
        {
            job.Dispose();
            throw Native.Failure("set renderer Job Object CPU limit");
        }
        return job;
    }

    private static void SetAppContainerAccess(string path, IntPtr sid, bool writable)
    {
        var trustee = new Native.Trustee
        {
            TrusteeForm = 0,
            TrusteeType = 1,
            Name = sid
        };
        var access = new Native.ExplicitAccess
        {
            Permissions = GenericRead | GenericExecute | (writable ? GenericWrite : 0),
            AccessMode = SetAccess,
            Inheritance = ObjectInherit | ContainerInherit,
            Trustee = trustee
        };
        var result = Native.GetNamedSecurityInfoW(path, FileObject, DaclSecurityInformation,
            IntPtr.Zero, IntPtr.Zero, out var existingAcl, IntPtr.Zero, out var descriptor);
        if (result != 0) { throw new Win32Exception(unchecked((int)result), "Cannot inspect staged renderer permissions."); }
        IntPtr acl = IntPtr.Zero;
        try
        {
            result = Native.SetEntriesInAclW(1, ref access, existingAcl, out acl);
            if (result != 0) { throw new Win32Exception(unchecked((int)result), "Cannot set renderer AppContainer access."); }
            result = Native.SetNamedSecurityInfoW(path, FileObject, DaclSecurityInformation,
                IntPtr.Zero, IntPtr.Zero, acl, IntPtr.Zero);
            if (result != 0) { throw new Win32Exception(unchecked((int)result), "Cannot grant the renderer AppContainer access to staged files."); }
        }
        finally
        {
            if (acl != IntPtr.Zero) { _ = Native.LocalFree(acl); }
            if (descriptor != IntPtr.Zero) { _ = Native.LocalFree(descriptor); }
        }
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) { continue; }
            var target = Path.Combine(destination, Path.GetFileName(entry));
            if ((attributes & FileAttributes.Directory) != 0) { CopyTree(entry, target); }
            else { File.Copy(entry, target); }
        }
    }

    private static string Existing(string path, bool directory)
    {
        var full = Path.GetFullPath(path);
        if (directory ? !Directory.Exists(full) : !File.Exists(full))
        {
            throw new FileNotFoundException("Required Windows renderer input is absent.", full);
        }
        return full;
    }

    private static string QuoteArgument(string value)
    {
        if (value.Length > 0 && !value.Any(char.IsWhiteSpace) && !value.Contains('"')) { return value; }
        var builder = new System.Text.StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            if (character == '"')
            {
                builder.Append('\\', slashes * 2 + 1).Append('"');
                slashes = 0;
                continue;
            }
            builder.Append('\\', slashes).Append(character);
            slashes = 0;
        }
        return builder.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static void CreatePipePair(bool childReads, out SafeFileHandle child, out SafeFileHandle parent)
    {
        var security = new Native.SecurityAttributes { Length = Marshal.SizeOf<Native.SecurityAttributes>(), InheritHandle = true };
        if (!Native.CreatePipe(out var read, out var write, ref security, 0))
        {
            throw Native.Failure("create private renderer pipe");
        }
        child = childReads ? read : write;
        parent = childReads ? write : read;
        if (!Native.SetHandleInformation(parent, HandleFlagInherit, 0))
        {
            child.Dispose();
            parent.Dispose();
            throw Native.Failure("restrict renderer pipe inheritance");
        }
    }

    private static IntPtr BuildEnvironment(string runtime, string temp, string windows)
    {
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_EnableDiagnostics"] = "0",
            ["DOTNET_GCHeapHardLimit"] = "10000000",
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["DOTNET_ROOT"] = runtime,
            ["SystemRoot"] = windows,
            ["TEMP"] = temp,
            ["TMP"] = temp,
            ["WINDIR"] = windows
        };
        var block = string.Join('\0', values.Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
        return Marshal.StringToHGlobalUni(block);
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SecurityCapabilities
        {
            internal IntPtr AppContainerSid;
            internal IntPtr Capabilities;
            internal uint CapabilityCount;
            internal uint Reserved;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct SecurityAttributes
        {
            internal int Length;
            internal IntPtr SecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)] internal bool InheritHandle;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StartupInfo
        {
            internal int Cb;
            internal string? Reserved;
            internal string? Desktop;
            internal string? Title;
            internal int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute;
            internal uint Flags;
            internal short ShowWindow, Reserved2Length;
            internal IntPtr Reserved2, StandardInput, StandardOutput, StandardError;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct StartupInfoEx
        {
            internal StartupInfo StartupInfo;
            internal IntPtr AttributeList;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessInformation
        {
            internal IntPtr Process, Thread;
            internal uint ProcessId, ThreadId;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Trustee
        {
            internal IntPtr MultipleTrustee;
            internal int MultipleTrusteeOperation;
            internal int TrusteeForm;
            internal int TrusteeType;
            internal IntPtr Name;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct ExplicitAccess
        {
            internal uint Permissions;
            internal int AccessMode;
            internal uint Inheritance;
            internal Trustee Trustee;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BasicLimitInformation
        {
            internal long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            internal uint LimitFlags;
            internal UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            internal uint ActiveProcessLimit;
            internal UIntPtr Affinity;
            internal uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct IoCounters
        {
            internal ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            internal ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct ExtendedLimitInformation
        {
            internal BasicLimitInformation BasicLimitInformation;
            internal IoCounters IoInfo;
            internal UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct CpuRateControl
        {
            internal uint ControlFlags, CpuRate;
        }

        [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
        internal static extern int CreateAppContainerProfile(
            string name, string displayName, string description, IntPtr capabilities, uint capabilityCount, out IntPtr sid);
        [DllImport("userenv.dll", CharSet = CharSet.Unicode)] internal static extern int DeleteAppContainerProfile(string name);
        [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetAppContainerFolderPath(IntPtr appContainerSid, out IntPtr path);
        [DllImport("ole32.dll")] internal static extern void CoTaskMemFree(IntPtr memory);
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenProcessToken(IntPtr process, uint access, out SafeFileHandle token);
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetTokenInformation(SafeFileHandle token, int informationClass, ref int information,
            int informationLength, out int returnLength);
        [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint SetEntriesInAclW(
            uint count, ref ExplicitAccess entries, IntPtr oldAcl, out IntPtr newAcl);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint GetNamedSecurityInfoW(
            string name, uint objectType, uint securityInfo, IntPtr owner, IntPtr group, out IntPtr dacl,
            IntPtr sacl, out IntPtr descriptor);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint SetNamedSecurityInfoW(
            string name, uint objectType, uint securityInfo, IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);
        [DllImport("kernel32.dll")] internal static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("advapi32.dll")] internal static extern IntPtr FreeSid(IntPtr sid);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimitInformation info, int length);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref CpuRateControl info, int length);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimitInformation info,
            uint length, IntPtr returnLength);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryInformationJobObject(IntPtr job, int infoClass, ref CpuRateControl info,
            uint length, IntPtr returnLength);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes attributes, int size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, int attribute, ref SecurityCapabilities value,
            IntPtr size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, int attribute, IntPtr value,
            IntPtr size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] internal static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcessW(string application, System.Text.StringBuilder commandLine,
            IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags, IntPtr environment, string currentDirectory, ref StartupInfoEx startup, out ProcessInformation info);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(IntPtr process, uint exitCode);
        [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle(IntPtr handle);
        internal static Exception Failure(string action) => new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot " + action + ".");
    }
}

public sealed class WindowsRendererWorker : IDisposable
{
    private readonly string staging;
    public string ProfileStoragePath { get; }
    private readonly string profile;
    private IntPtr sid;
    private SafeFileHandle job;
    private bool disposed;
    public Process Process { get; }
    public Stream Input { get; }
    public Stream Output { get; }
    public Stream Error { get; }
    public void Terminate()
    {
        if (!disposed && !job.IsClosed) { job.Dispose(); }
    }

    internal WindowsRendererWorker(Process process, SafeFileHandle job, string staging, string profileStorage, string profile,
        IntPtr sid, Stream input, Stream output, Stream error)
    {
        Process = process; this.job = job; this.staging = staging; ProfileStoragePath = profileStorage;
        this.profile = profile; this.sid = sid;
        Input = input; Output = output; Error = error;
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        Input.Dispose(); Output.Dispose(); Error.Dispose();
        Terminate();
        Process.WaitForExit();
        Process.Dispose();
        if (sid != IntPtr.Zero) { _ = FreeSid(sid); sid = IntPtr.Zero; }
        var result = WindowsRendererSandbox.RemoveAppContainerProfile(profile);
        try { Directory.Delete(staging, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if (result != 0) { throw new Win32Exception(result, "Cannot remove renderer AppContainer profile."); }
    }

    [DllImport("advapi32.dll")] private static extern IntPtr FreeSid(IntPtr sid);
}

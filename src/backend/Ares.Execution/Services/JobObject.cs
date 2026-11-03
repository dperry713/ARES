using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ares.Execution.Services;

/// <summary>
/// Windows Job Object wrapper for process confinement and resource control.
/// Provides out-of-process isolation for security test module execution.
/// </summary>
public sealed class JobObject : IDisposable
{
    private readonly IntPtr _handle;
    private readonly bool _ownsHandle;
    private bool _disposed;

    /// <summary>
    /// Creates a new Job Object with the specified limits.
    /// </summary>
    /// <param name="memoryLimitMB">Memory limit in MB (or null for no limit)</param>
    /// <param name="activeProcessLimit">Maximum number of processes (or null for no limit)</param>
    /// <param name="killOnClose">Whether to kill all processes when the job is closed</param>
    public JobObject(long? memoryLimitMB = null, int? activeProcessLimit = null, bool killOnClose = true)
    {
        _handle = CreateJobObject(IntPtr.Zero, Guid.NewGuid().ToString("N"));
        if (_handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create Job Object");
        }

        _ownsHandle = true;

        // Set memory limit if specified
        if (memoryLimitMB.HasValue)
        {
            var limitInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION();
            limitInfo.MinimumWorkingSetSize = (int)(memoryLimitMB.Value * 1024);
            limitInfo.MaximumWorkingSetSize = (int)(memoryLimitMB.Value * 1024);
            SetInformationJobObject(_handle, 2, Marshal.AllocHGlobal(Marshal.SizeOf(limitInfo)), Marshal.SizeOf(limitInfo));
        }

        // Set active process limit if specified
        if (activeProcessLimit.HasValue)
        {
            var limitInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION();
            limitInfo.ActiveProcessLimit = activeProcessLimit.Value;
            limitInfo.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            SetInformationJobObject(_handle, 2, Marshal.AllocHGlobal(Marshal.SizeOf(limitInfo)), Marshal.SizeOf(limitInfo));
        }

        // Set kill on close if specified
        if (killOnClose)
        {
            var limitInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION();
            limitInfo.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            SetInformationJobObject(_handle, 2, Marshal.AllocHGlobal(Marshal.SizeOf(limitInfo)), Marshal.SizeOf(limitInfo));
        }
    }

    ~JobObject()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsHandle && _handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
        }
    }

    /// <summary>
    /// Assigns the specified process to the job object.
    /// </summary>
    /// <param name="processId">The process ID to assign.</param>
    /// <returns>True if the process was successfully assigned.</returns>
    public bool AssignProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return AssignProcessToJobObject(_handle, process.Handle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Terminates all processes in the job object and closes the job.
    /// </summary>
    /// <param name="exitCode">The exit code to use when terminating processes.</param>
    public void Close(int exitCode = 0)
    {
        TerminateJobObject(_handle, (uint)exitCode);
        CloseHandle(_handle);
    }

    /// <summary>
    /// Gets whether Job Object isolation is available and active.
    /// </summary>
    public bool IsAvailable => _handle != IntPtr.Zero && _handle != new IntPtr(-1);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJobObject, int jobObjectInfoClass, IntPtr lpJobObject, int cbJobObjectInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // Job Object info classes
    private const int JobObjectBasicLimitInformation = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public int MinimumWorkingSetSize;
        public int MaximumWorkingSetSize;
        public int ActiveProcessLimit;
        public IntPtr Affinity;
        public int PriorityClass;
        public uint CPURate;
    }
}
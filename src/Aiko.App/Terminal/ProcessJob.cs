using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.JobObjects;

namespace Aiko.App;

/// A Windows job around one Claude Code: everything it starts belongs to the job, and the whole
/// job ends when Aiko ends it or when Aiko itself is gone.
///
/// Without it a Claude Code that Aiko stops would leave its own children behind: the shells of
/// its tools, an MCP server, a build that still runs.
[SupportedOSPlatform("windows10.0.17763")]
sealed class ProcessJob : IDisposable
{
    private readonly SafeFileHandle _job;

    private ProcessJob(SafeFileHandle job) => _job = job;

    /// Null when Windows refused; the caller then ends the process tree the slower way.
    public static ProcessJob? Around(Process process) => Around(process.SafeHandle);

    /// The same for a process Aiko started itself, by its handle (a pseudo console).
    public static unsafe ProcessJob? Around(SafeHandle process)
    {
        var job = PInvoke.CreateJobObject(null, null);
        if (job.IsInvalid)
        {
            job.Dispose();
            return null;
        }

        var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = { LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE },
        };

        if (!PInvoke.SetInformationJobObject(
                (HANDLE)job.DangerousGetHandle(),
                JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                &limits,
                (uint)sizeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION))
            || !PInvoke.AssignProcessToJobObject(job, process))
        {
            job.Dispose();
            return null;
        }

        return new ProcessJob(job);
    }

    public void End() => PInvoke.TerminateJobObject(_job, 1);

    public void Dispose() => _job.Dispose();
}

using System.Collections.Concurrent;
using System.Diagnostics;

namespace InstallBunker.Compiler.Core.Services;

public static class CompilerChildProcessRegistry
{
    private static readonly ConcurrentDictionary<int, byte> _trackedProcessIds = new();

    public static void Register(Process process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                _trackedProcessIds[process.Id] = 0;
            }
        }
        catch
        {
            // ignored
        }
    }

    public static void Unregister(Process process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            _trackedProcessIds.TryRemove(process.Id, out _);
        }
        catch
        {
            // ignored
        }
    }

    public static void KillAllTrackedProcesses()
    {
        foreach (var processId in _trackedProcessIds.Keys.ToArray())
        {
            try
            {
                using var process = Process.GetProcessById(processId);

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch
            {
                // ignored
            }
            finally
            {
                _trackedProcessIds.TryRemove(processId, out _);
            }
        }
    }
}
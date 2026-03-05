namespace InstallBunker.Compiler.Core.Models;

public sealed class CompilerTelemetryEvent
{
    public string StageKey { get; set; } = string.Empty;

    public string StageDisplayName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int? ProgressPercent { get; set; }

    public bool IsStatus { get; set; }

    public bool IsLog { get; set; }

    public bool IsWarning { get; set; }

    public bool IsError { get; set; }

    public static CompilerTelemetryEvent Status(
        string stageKey,
        string stageDisplayName,
        string message,
        int? progressPercent = null)
    {
        return new CompilerTelemetryEvent
        {
            StageKey = stageKey,
            StageDisplayName = stageDisplayName,
            Message = message,
            ProgressPercent = progressPercent,
            IsStatus = true
        };
    }

    public static CompilerTelemetryEvent Log(
        string stageKey,
        string stageDisplayName,
        string message,
        int? progressPercent = null)
    {
        return new CompilerTelemetryEvent
        {
            StageKey = stageKey,
            StageDisplayName = stageDisplayName,
            Message = message,
            ProgressPercent = progressPercent,
            IsLog = true
        };
    }

    public static CompilerTelemetryEvent Warning(
        string stageKey,
        string stageDisplayName,
        string message,
        int? progressPercent = null)
    {
        return new CompilerTelemetryEvent
        {
            StageKey = stageKey,
            StageDisplayName = stageDisplayName,
            Message = message,
            ProgressPercent = progressPercent,
            IsStatus = true,
            IsLog = true,
            IsWarning = true
        };
    }

    public static CompilerTelemetryEvent Error(
        string stageKey,
        string stageDisplayName,
        string message,
        int? progressPercent = null)
    {
        return new CompilerTelemetryEvent
        {
            StageKey = stageKey,
            StageDisplayName = stageDisplayName,
            Message = message,
            ProgressPercent = progressPercent,
            IsStatus = true,
            IsLog = true,
            IsError = true
        };
    }
}
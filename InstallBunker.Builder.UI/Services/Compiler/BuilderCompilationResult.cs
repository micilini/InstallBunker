using InstallBunker.Compiler.Core.Models;

namespace InstallBunker.Builder.UI.Services.Compiler;

public sealed class BuilderCompilationResult
{
    public CompilerResult CompilerResult { get; set; } = new();

    public bool UsedCompilerTemplates { get; set; }

    public List<string> StatusMessages { get; set; } = new();

    public List<string> LogMessages { get; set; } = new();
}
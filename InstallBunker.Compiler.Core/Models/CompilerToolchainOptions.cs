public sealed class CompilerToolchainOptions
{
    public string Configuration { get; set; } = "Release";

    public string RuntimeIdentifier { get; set; } = "win-x64";

    public bool PublishSingleFile { get; set; } = true;

    public bool SelfContained { get; set; } = true;

    public bool IncludeNativeLibrariesForSelfExtract { get; set; } = true;

    public bool PreferEmbeddedToolchain { get; set; } = true;

    public bool AllowSystemDotNetFallback { get; set; } = true;

    public bool IsDevelopmentMode { get; set; }

    public bool RequireEmbeddedToolchainInProductMode { get; set; } = true;

    public bool KeepWorkspaceOnSuccess { get; set; }

    public bool KeepWorkspaceOnFailure { get; set; }

    public string EmbeddedToolchainRelativeDirectory { get; set; } =
        Path.Combine("BuilderResources", "EmbeddedToolchain");

    public string? ExplicitDotNetHostPath { get; set; }
}
namespace InstallBunker.Domain.Models;

public sealed class PackageFileEntry
{
    public string Source { get; set; } = string.Empty;

    public string RelativeTarget { get; set; } = string.Empty;

    public bool Overwrite { get; set; } = true;
}

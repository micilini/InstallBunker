namespace InstallBunker.Installer.Core.Models;

public sealed class ExtractedPackageValidationResult
{
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public void AddError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Errors.Add(message);
    }
}
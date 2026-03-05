namespace InstallBunker.Domain.Models;

public sealed class ShortcutOptions
{
    public bool Desktop { get; set; } = true;

    public bool StartMenu { get; set; } = true;
}
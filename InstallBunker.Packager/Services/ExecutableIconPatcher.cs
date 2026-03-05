using Vestris.ResourceLib;

namespace InstallBunker.Packager.Services;

public sealed class ExecutableIconPatcher
{
    public void ApplyIcon(string executablePath, string iconFilePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Executable path is required.");
        }

        if (string.IsNullOrWhiteSpace(iconFilePath))
        {
            throw new InvalidOperationException("Icon file path is required.");
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Executable file not found.", executablePath);
        }

        if (!File.Exists(iconFilePath))
        {
            throw new FileNotFoundException("Icon file not found.", iconFilePath);
        }

        if (!string.Equals(Path.GetExtension(iconFilePath), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "To embed the physical icon into Setup.exe or Uninstall.exe, the selected icon file must be a .ico file.");
        }

        var iconFile = new IconFile(iconFilePath);
        var iconDirectoryResource = new IconDirectoryResource(iconFile);
        iconDirectoryResource.SaveTo(executablePath);
    }
}
namespace InstallBunker.Common.Paths;

public static class InstallBunkerPaths
{
    public static string GetBuilderDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InstallBunker",
            "Builder");
    }

    public static string GetBuilderRecentProjectsFilePath()
    {
        return Path.Combine(GetBuilderDataDirectory(), "recent-projects.json");
    }

    public static string GetSuggestedOutputRootDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }
}
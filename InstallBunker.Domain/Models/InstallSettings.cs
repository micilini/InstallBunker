using InstallBunker.Domain.Enums;

namespace InstallBunker.Domain.Models;

public sealed class InstallSettings
{
    public InstallScope DefaultInstallScope { get; set; } = InstallScope.PerUser;

    public bool AllowPerUser { get; set; } = true;

    public bool AllowPerMachine { get; set; } = true;

    public string DefaultInstallDirPerUser { get; set; } = @"%LocalAppData%\Programs\MyApp";

    public string DefaultInstallDirPerMachine { get; set; } = @"%ProgramFiles%\MyApp";
}
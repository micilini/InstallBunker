using InstallBunker.Compiler.Core.Models;
using System.Text;
using System.Text.Json;

namespace InstallBunker.Compiler.Core.Services;

public sealed class TemplateMutationService
{
    public TemplateMutationResult Mutate(
        string templateDisplayName,
        string projectFilePath,
        CompilerRequest request)
    {
        if (string.IsNullOrWhiteSpace(templateDisplayName))
        {
            throw new InvalidOperationException("Template display name is required.");
        }

        if (string.IsNullOrWhiteSpace(projectFilePath))
        {
            throw new InvalidOperationException("Project file path is required.");
        }

        if (request is null)
        {
            throw new InvalidOperationException("CompilerRequest is required.");
        }

        var result = new TemplateMutationResult
        {
            TemplateDisplayName = templateDisplayName,
            ProjectFilePath = projectFilePath,
            ProjectDirectory = Path.GetDirectoryName(projectFilePath) ?? string.Empty
        };

        if (!File.Exists(projectFilePath))
        {
            result.ValidationErrors.Add($"Template project file not found: {projectFilePath}");
            return result;
        }

        if (string.IsNullOrWhiteSpace(result.ProjectDirectory) || !Directory.Exists(result.ProjectDirectory))
        {
            result.ValidationErrors.Add($"Template project directory not found: {result.ProjectDirectory}");
            return result;
        }

        var sourceIconFullPath = Path.GetFullPath(Path.Combine(request.SourceDirectory, request.IconRelativePath));

        if (!File.Exists(sourceIconFullPath))
        {
            result.ValidationErrors.Add($"Compiler icon file not found: {sourceIconFullPath}");
            return result;
        }

        result.GeneratedBrandingJsonPath = Path.Combine(result.ProjectDirectory, "compiler.branding.json");
        result.GeneratedBrandingCodePath = Path.Combine(result.ProjectDirectory, "GeneratedCompilerBranding.g.cs");
        result.GeneratedPropsPath = Path.Combine(result.ProjectDirectory, "compiler.generated.props");
        result.GeneratedIconPath = Path.Combine(result.ProjectDirectory, "compiler.icon.ico");

        File.Copy(sourceIconFullPath, result.GeneratedIconPath, overwrite: true);
        result.Logs.Add($"[{templateDisplayName}] Icon copied to workspace: {result.GeneratedIconPath}");

        WriteBrandingJson(result.GeneratedBrandingJsonPath, templateDisplayName, request);
        result.Logs.Add($"[{templateDisplayName}] Branding JSON generated: {result.GeneratedBrandingJsonPath}");

        WriteGeneratedBrandingCode(result.GeneratedBrandingCodePath, templateDisplayName, request);
        result.Logs.Add($"[{templateDisplayName}] Generated branding source created: {result.GeneratedBrandingCodePath}");

        WriteGeneratedProps(result.GeneratedPropsPath, templateDisplayName, request);
        result.Logs.Add($"[{templateDisplayName}] Generated props created: {result.GeneratedPropsPath}");

        EnsureProjectImportsGeneratedProps(projectFilePath);
        result.Logs.Add($"[{templateDisplayName}] Project import ensured for compiler.generated.props");

        return result;
    }

    private static void WriteBrandingJson(
        string outputPath,
        string templateDisplayName,
        CompilerRequest request)
    {
        var payload = new
        {
            Template = templateDisplayName,
            AppName = request.Branding.AppName,
            Publisher = request.Branding.Publisher,
            Version = request.Branding.Version,
            IconRelativePath = request.Branding.IconRelativePath,
            SetupWindowTitle = request.Branding.SetupWindowTitle,
            SetupSidebarAppName = request.Branding.SetupSidebarAppName,
            SetupSidebarVersion = request.Branding.SetupSidebarVersion,
            SetupWelcomeSummary = request.Branding.SetupWelcomeSummary,
            UninstallWindowTitle = request.Branding.UninstallWindowTitle,
            UninstallSidebarAppName = request.Branding.UninstallSidebarAppName,
            UninstallSidebarVersion = request.Branding.UninstallSidebarVersion,
            UninstallWelcomeSummary = request.Branding.UninstallWelcomeSummary
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    private static void WriteGeneratedBrandingCode(
        string outputPath,
        string templateDisplayName,
        CompilerRequest request)
    {
        var className = templateDisplayName.Equals("Setup", StringComparison.OrdinalIgnoreCase)
            ? "SetupGeneratedCompilerBranding"
            : "UninstallGeneratedCompilerBranding";

        var source =
$@"namespace InstallBunker.Compiler.Generated;

internal static class {className}
{{
    public const string Template = {ToCSharpStringLiteral(templateDisplayName)};
    public const string PackagePassword = {ToCSharpStringLiteral(request.PackagePassword)};
    public const string AppName = {ToCSharpStringLiteral(request.Branding.AppName)};
    public const string Publisher = {ToCSharpStringLiteral(request.Branding.Publisher)};
    public const string Version = {ToCSharpStringLiteral(request.Branding.Version)};
    public const string IconRelativePath = {ToCSharpStringLiteral(request.Branding.IconRelativePath)};
    public const string SetupWindowTitle = {ToCSharpStringLiteral(request.Branding.SetupWindowTitle)};
    public const string SetupSidebarAppName = {ToCSharpStringLiteral(request.Branding.SetupSidebarAppName)};
    public const string SetupSidebarVersion = {ToCSharpStringLiteral(request.Branding.SetupSidebarVersion)};
    public const string SetupWelcomeSummary = {ToCSharpStringLiteral(request.Branding.SetupWelcomeSummary)};
    public const string UninstallWindowTitle = {ToCSharpStringLiteral(request.Branding.UninstallWindowTitle)};
    public const string UninstallSidebarAppName = {ToCSharpStringLiteral(request.Branding.UninstallSidebarAppName)};
    public const string UninstallSidebarVersion = {ToCSharpStringLiteral(request.Branding.UninstallSidebarVersion)};
    public const string UninstallWelcomeSummary = {ToCSharpStringLiteral(request.Branding.UninstallWelcomeSummary)};
}}";

        File.WriteAllText(outputPath, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteGeneratedProps(
        string outputPath,
        string templateDisplayName,
        CompilerRequest request)
    {
        var description = templateDisplayName.Equals("Setup", StringComparison.OrdinalIgnoreCase)
            ? $"{request.Branding.AppName} setup bootstrapper"
            : $"{request.Branding.AppName} uninstall bootstrapper";

        var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Project>
                      <PropertyGroup>
                        <Product>{EscapeXml(request.Branding.AppName)}</Product>
                        <Company>{EscapeXml(request.Branding.Publisher)}</Company>
                        <Authors>{EscapeXml(request.Branding.Publisher)}</Authors>
                        <Description>{EscapeXml(description)}</Description>
                        <AssemblyVersion>{EscapeXml(NormalizeVersion(request.Branding.Version))}</AssemblyVersion>
                        <FileVersion>{EscapeXml(NormalizeVersion(request.Branding.Version))}</FileVersion>
                        <Version>{EscapeXml(NormalizeVersion(request.Branding.Version))}</Version>
                        <ApplicationIcon>compiler.icon.ico</ApplicationIcon>
                      </PropertyGroup>

                      <ItemGroup>
                        <None Include=""compiler.branding.json"">
                          <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                        </None>
                      </ItemGroup>
                    </Project>";

        File.WriteAllText(outputPath, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void EnsureProjectImportsGeneratedProps(string projectFilePath)
    {
        var contents = File.ReadAllText(projectFilePath, Encoding.UTF8);

        if (contents.Contains("compiler.generated.props", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var importLine = @"  <Import Project=""compiler.generated.props"" Condition=""Exists('compiler.generated.props')"" />";

        var projectCloseTagIndex = contents.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);

        if (projectCloseTagIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not locate </Project> inside template project file: {projectFilePath}");
        }

        contents = contents.Insert(projectCloseTagIndex, importLine + Environment.NewLine);
        File.WriteAllText(projectFilePath, contents, Encoding.UTF8);
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "1.0.0.0";
        }

        var parts = value
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => int.TryParse(p, out var n) ? n.ToString() : "0")
            .Take(4)
            .ToList();

        while (parts.Count < 4)
        {
            parts.Add("0");
        }

        return string.Join(".", parts);
    }

    private static string EscapeXml(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }

    private static string ToCSharpStringLiteral(string value)
    {
        return "@\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
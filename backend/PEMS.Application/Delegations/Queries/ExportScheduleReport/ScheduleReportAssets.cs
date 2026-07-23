using System.Reflection;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>Loads the static FPT logo bundled as an embedded resource, so schedule-report PDF
/// generation never depends on a filesystem path being present at deploy time.</summary>
public static class ScheduleReportAssets
{
    private const string ResourceName = "PEMS.Application.Common.Assets.fpt-logo.png";

    private static byte[]? _fptLogoBytes;

    public static byte[] FptLogoBytes => _fptLogoBytes ??= LoadFptLogo();

    private static byte[] LoadFptLogo()
    {
        var assembly = typeof(ScheduleReportAssets).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {ResourceName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}

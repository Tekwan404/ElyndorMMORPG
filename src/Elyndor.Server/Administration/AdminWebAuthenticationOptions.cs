using System.Text;

namespace Elyndor.Server.Administration;

public sealed class AdminWebAuthenticationOptions
{
    public const string SectionName = "Administration:WebAuthentication";
    public const int MinimumEmergencyPasswordBytes = 20;

    public bool EmergencyPasswordEnabled { get; init; }

    public string EmergencyPassword { get; init; } = string.Empty;

    public bool IsConfigured =>
        !EmergencyPasswordEnabled
        || Encoding.UTF8.GetByteCount(EmergencyPassword)
            >= MinimumEmergencyPasswordBytes;
}

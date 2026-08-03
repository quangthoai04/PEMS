using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Application.ApiIntegrations.Common;

public static class ResendEmailConstants
{
    public const string ApiCode = "RESEND_EMAIL_DELIVERY";
    public const string ProviderName = "Resend";
    public const string Purpose = "EMAIL_DELIVERY";
    public const string BaseUrl = "https://api.resend.com";
    public const string AuthType = "BEARER_TOKEN";
}

public sealed class ResendProviderSettings
{
    [JsonPropertyName("from_email")]
    public string FromEmail { get; set; } = string.Empty;

    [JsonPropertyName("from_name")]
    public string FromName { get; set; } = "PEMS";

    [JsonPropertyName("reply_to_email")]
    public string? ReplyToEmail { get; set; }

    [JsonPropertyName("reply_to_name")]
    public string? ReplyToName { get; set; }

    public static ResendProviderSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ResendProviderSettings();
        try
        {
            return JsonSerializer.Deserialize<ResendProviderSettings>(json) ?? new ResendProviderSettings();
        }
        catch
        {
            return new ResendProviderSettings();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this);
}

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

    /// <summary>
    /// The HTTP header Resend uses to de-duplicate a request at the provider: two POSTs carrying the same
    /// key and the same body are treated as one send. This is a TRANSPORT header sent to Resend itself —
    /// unrelated to PEMS's own client-facing <c>Idempotency-Key</c> (see
    /// <c>PEMS.Application.Emails.Idempotency.IdempotencyKey</c>), which protects a report/invoice send
    /// endpoint against a duplicate HTTP request from the browser.
    /// </summary>
    public const string IdempotencyHeaderName = "Idempotency-Key";
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

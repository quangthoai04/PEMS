namespace PEMS.Application.Authentication.Models;

/// <summary>Generic single-message response (logout, forgot/reset/change password).</summary>
public sealed class MessageResponse
{
    public string Message { get; init; } = null!;

    public MessageResponse() { }

    public MessageResponse(string message) => Message = message;
}

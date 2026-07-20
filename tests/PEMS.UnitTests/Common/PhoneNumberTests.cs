using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.Common;

/// <summary>
/// The backend phone rule. This exists because the frontend validated with libphonenumber while the
/// backend only checked MaximumLength(50), so a direct API call could store "090abc123" or a
/// 40-character string. Every case here is something the UI already rejected and the API did not.
/// </summary>
public class PhoneNumberTests
{
    [Theory]
    [InlineData("0912345678")]        // national VN form
    [InlineData("+84912345678")]      // E.164
    [InlineData(" 0912345678 ")]      // surrounding whitespace is trimmed, not rejected
    [InlineData("+1 202 555 0134")]   // another country, valid
    public void Accepts_real_numbers(string input) => Assert.True(PhoneNumber.IsValid(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]                 // too short
    [InlineData("090abc123")]           // letters
    [InlineData("09999999999999999")]   // too long for any region
    [InlineData("+84000000000")]        // parses, but cannot exist
    public void Rejects_everything_else(string? input) => Assert.False(PhoneNumber.IsValid(input));

    [Theory]
    [InlineData("0912345678", "+84912345678")]
    [InlineData("+84912345678", "+84912345678")]
    [InlineData(" 0912345678 ", "+84912345678")]
    [InlineData("091 234 5678", "+84912345678")]
    public void Normalizes_to_E164(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var e164));
        Assert.Equal(expected, e164);
    }

    [Fact]
    public void The_same_number_typed_two_ways_stores_identically()
    {
        // The point of normalising: one person is never persisted under two spellings.
        PhoneNumber.TryNormalize("0912345678", out var national);
        PhoneNumber.TryNormalize("+84912345678", out var international);
        Assert.Equal(national, international);
    }

    [Fact]
    public void An_invalid_number_yields_no_normalized_form()
    {
        Assert.False(PhoneNumber.TryNormalize("090abc123", out var e164));
        Assert.Null(e164);
    }

    [Fact]
    public void NormalizeOrOriginal_leaves_an_invalid_value_alone()
        // Persistence helpers run AFTER validation; they must not silently invent a number.
        => Assert.Equal("090abc123", PhoneNumber.NormalizeOrOriginal("090abc123"));
}

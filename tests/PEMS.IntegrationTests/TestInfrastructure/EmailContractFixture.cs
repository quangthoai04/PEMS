using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Infrastructure.Persistence;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Satisfies a template's trusted-block contract the way the product does, for fixtures that are not
/// about that contract.
///
/// <para>
/// Fourteen templates carry <c>{{contactInformationBlock}}</c> because their wording tells the recipient
/// to get in touch, and both halves of that are enforced: the body must keep the placeholder, and the
/// caller must supply a value for it. A fixture that writes its own body or drives the renderer directly
/// satisfies neither, so tests about encoding, language selection, concurrency tokens or transaction
/// boundaries began failing with an error about a contact block they never meant to have an opinion on.
/// </para>
///
/// <para>
/// The answer is this one helper rather than a literal per test. Two reasons. A hand-written
/// <c>&lt;div&gt;…&lt;/div&gt;</c> would be markup nobody maintains, drifting from whatever
/// <c>EmailContactHtmlRenderer</c> actually produces, and it would let a test pass while the real block
/// was broken. And spreading it across files would mean editing all of them the next time a template's
/// policy moves, which is exactly the coupling that made these thirty-seven failures look like
/// thirty-seven problems instead of two.
/// </para>
///
/// <para>
/// Nothing here relaxes the contract: no policy is downgraded, no block is stubbed out, and a template
/// with no required block still gets nothing.
/// </para>
/// </summary>
public static class EmailContractFixture
{
    /// <summary>
    /// <paramref name="body"/> plus a placeholder for every trusted block this template's contract
    /// requires. Already-present placeholders are left alone, so it is safe on canonical content.
    /// </summary>
    public static string BodyWithRequiredBlocks(string templateCode, string body)
    {
        var result = body ?? string.Empty;

        foreach (var (block, _) in EmailTemplateContracts.RequiredBlocksFor(templateCode))
        {
            var placeholder = "{{" + block + "}}";
            if (!result.Contains(placeholder, System.StringComparison.Ordinal)) result += placeholder;
        }

        return result;
    }

    /// <summary>
    /// The trusted-block values a direct render needs: the contact block as the REAL resolver builds it,
    /// merged over anything the caller already supplies.
    ///
    /// <para>
    /// Resolved rather than faked, because the value is what the recipient reads. A fixture that invented
    /// its own HTML here would be asserting against itself.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> TrustedBlocksAsync(
        ApplicationDbContext db,
        string templateCode,
        string language = EmailLanguages.Vi,
        IReadOnlyDictionary<string, string>? existing = null,
        ulong? visitInstanceId = null,
        ulong? campusId = null,
        ulong? departmentId = null,
        ulong? senderUserId = null,
        CancellationToken cancellationToken = default)
    {
        var blocks = existing is null
            ? new Dictionary<string, string>(System.StringComparer.Ordinal)
            : new Dictionary<string, string>(existing, System.StringComparer.Ordinal);

        var needsContact = false;
        foreach (var (block, _) in EmailTemplateContracts.RequiredBlocksFor(templateCode))
            if (block == EmailTrustedBlocks.ContactInformationBlock) needsContact = true;

        if (!needsContact || blocks.ContainsKey(EmailTrustedBlocks.ContactInformationBlock))
            return blocks;

        var resolution = await EmailEvidenceHarness.Contacts(db).ResolveAsync(
            new EmailContactRequest(templateCode, language, visitInstanceId, campusId, departmentId, senderUserId),
            cancellationToken);

        if (resolution is not null)
            blocks[EmailTrustedBlocks.ContactInformationBlock] = resolution.BlockHtml;

        return blocks;
    }
}

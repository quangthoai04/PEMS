using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// The single policy for what happens to an account's authentication bindings when its login email
/// moves. Every flow that can change an address (HO basic-info, HO's pending-email edit, a Staff
/// Leader's role+identity edit) applies exactly this — an account must never be left bound to an
/// external identity that was proven against a different address.
///
/// <para>
/// SSO/FEID rows are DELETED rather than blanked: <c>provider_subject</c> identifies the OLD external
/// identity, and a subject-less SSO/FEID row is rejected by the database outright (the auth-providers
/// validation trigger). Removing the row is also what "re-link on next login" actually means — the SSO
/// login handler creates a fresh row for a user who has none — and it frees the old subject from its
/// unique index. Local-password logins stay linked; only the address they point at changes.
/// </para>
///
/// <para>Nothing is saved here: the caller owns the transaction.</para>
/// </summary>
public static class AccountAuthProviderSync
{
    public static async Task RepointAsync(
        IApplicationDbContext db, ulong userId, string newEmail, CancellationToken cancellationToken)
    {
        // Read through the context rather than a navigation, so this works whether or not the caller
        // included AuthProviders. Already-tracked rows come back as the same instances.
        var providers = await db.UserAuthProviders
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        var external = providers
            .Where(p => p.ProviderType == ProviderTypes.GoogleSso || p.ProviderType == ProviderTypes.FeId)
            .ToList();
        if (external.Count > 0)
            db.UserAuthProviders.RemoveRange(external);

        foreach (var provider in providers)
        {
            if (provider.ProviderType == ProviderTypes.GoogleSso || provider.ProviderType == ProviderTypes.FeId)
                continue;
            provider.ProviderEmail = newEmail;
        }
    }
}

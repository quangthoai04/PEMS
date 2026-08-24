using System.Linq;
using System.Reflection;
using FluentValidation;
using Xunit;

namespace PEMS.ArchitectureTests;

/// <summary>
/// Patch 7 (P7.7) — architecture guard against a FUTURE email-accepting validator silently regressing
/// to a custom/ad-hoc syntax rule instead of FluentValidation's canonical <c>.EmailAddress()</c>
/// (Patch 5's own finding: the frontend alone had accumulated two independent copies of the same
/// hand-rolled regex before consolidation).
///
/// <para>
/// Same discovery-gate idiom as <see cref="PhoneValidatorDiscoveryTests"/> and
/// <see cref="AuthorizationTests"/>'s anonymous-action list, for the same reason: FluentValidation's
/// built-in <c>.EmailAddress()</c> is not reliably distinguishable BY TYPE from a custom
/// <c>.Must(...)</c> predicate via reflection alone, so this enumerates every validator with an
/// email-shaped property and requires it to be on an explicit, reviewed list — governed (covered by
/// <c>PEMS.UnitTests.Validation.EmailContractTests</c>'s behavior matrix) or exempt (a different,
/// reviewed rule: the read-only replay validator, or a domain Patch 5 never claimed to govern).
/// </para>
/// </summary>
public class EmailValidatorDiscoveryTests
{
    private static readonly Assembly ApplicationAssembly = Assembly.Load("PEMS.Application");

    /// <summary>Property names, on the validated type, that this scan treats as "an email field".</summary>
    private static readonly string[] EmailPropertyNames = { "Email", "RegistrantEmail" };

    /// <summary>
    /// Governed by the canonical rule — every one of these is exercised by
    /// <c>PEMS.UnitTests.Validation.EmailContractTests</c>'s behavior matrix.
    /// </summary>
    private static readonly string[] GovernedByEmailContract =
    {
        "RegistrantInputV2Validator",
        "OperationalContactV2Validator",
        "ReplaceOperationalContactCommandValidator",
        "UpdateOperationalContactProfileCommandValidator",
        "SaveOperationalContactCommandValidator",
        "InitiateOperationalContactTransferCommandValidator",
        "CreatePartnerCommandValidator",           // InitialContact.Email — nested, see note below
        "UpdateRegistrantInfoCommandValidator",
        "ResendVisitRequestOtpCommandValidator",
    };

    /// <summary>
    /// Explicitly NOT governed by the Visit email contract, with the reason each was reviewed and
    /// exempted rather than silently missed.
    /// </summary>
    private static readonly string[] ExemptWithReason =
    {
        // Structural rule only, by design — an EXISTING campus's contact snapshot being replayed
        // through edit/resubmit/amendment. No .EmailAddress() at all (its own doc comment): format
        // is not re-enforced on a read-only echo of data that may predate this rule. Whether the
        // snapshot actually CHANGED is enforced separately and unconditionally, in canonical space
        // (VisitRequestV2EditService.ApplyCommonFields / VisitSafeEditService), not here.
        "OperationalContactReplayV2Validator",

        // Auth (login/forgot/reset password) — the "login email" concept, per Patch 5's own audit a
        // materially stricter, SEPARATE rule (AccountIdentityRules: domain whitelist, no
        // +-aliasing) for a different business concept than a visit/contact email. These three
        // command validators carry no email-format rule of their own; the shape check for a login
        // email happens in AccountIdentityRules.HasValidEmailShape at the handler, not here — this
        // scan only sees the property, not that separate call, so they land here rather than in
        // GovernedByEmailContract.
        "LoginviaCredentialsCommandValidator",
        "ForgotPasswordCommandValidator",
        "ResetPasswordCommandValidator",

        // Account management (HO/Dept-Leader account provisioning) — same "login email" domain and
        // reasoning as the three above; AccountIdentityRules.ValidateEmail runs in each handler.
        "CreateAccountCommandValidator",
        "UpdateBasicAccountInfoCommandValidator",
        "UpdateAccountRoleCommandValidator",
        "ReplaceStaffLeaderCommandValidator",

        // Department Personnel — its own spec-backed rule set, not a Visit write path, same
        // reasoning Patch 3/7.5 already applied to this domain for phone.
        "AddDepartmentPersonnelCommandValidator",
        "CreateDepartmentPersonnelCommandValidator",
        "UpdateDepartmentPersonnelCommandValidator",

        // Campus master data (PERMISSION_MATRIX §5.14) — HO-only, its own spec-backed rule set,
        // same reasoning Patch 3/7.5 already applied to this domain for phone.
        "AddNewCampusCommandValidator",
        "UpdateCampusCommandValidator",

        // Partner Contact / Business Card capture (plan CanhIter3FixBug) — DELIBERATELY exempted, same
        // reasoning as PhoneValidatorDiscoveryTests' own Partner Contact exemption: external
        // business-card/partner-supplied data, never an authentication/identity email. .EmailAddress()
        // rejected real nonstandard values a card can print. Now only MaximumLength-bound to the actual
        // DB column (partner_contacts.email VARCHAR(150)) and stored as user-confirmed raw trimmed text
        // (never lowercased — EnsureEmailUniqueAsync normalizes for COMPARISON only). Covered by
        // PEMS.UnitTests.Validation.PartnerContactEmailContractTests, not EmailContractTests.
        "CreatePartnerContactCommandValidator",
        "UpdatePartnerContactCommandValidator",
        "ConfirmBusinessCardContactCommandValidator",
    };

    private static bool HasEmailProperty(System.Type validatedType)
        => validatedType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => EmailPropertyNames.Contains(p.Name))
        // The one known nested case: CreatePartnerCommand carries the email on
        // InitialContact.Email, not on the command itself.
        || validatedType.Name == "CreatePartnerCommand";

    [Fact]
    public void Every_email_accepting_validator_in_PEMS_Application_is_on_the_reviewed_list()
    {
        var validatorTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.BaseType is { IsGenericType: true }
                        && t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .ToList();
        Assert.True(validatorTypes.Count > 10, "Suspiciously few validators discovered — reflection scope is broken.");

        var emailValidators = validatorTypes
            .Where(t => HasEmailProperty(t.BaseType!.GetGenericArguments()[0]))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();
        Assert.True(emailValidators.Count > 0, "No email-accepting validators found at all — the discovery predicate itself is broken.");

        var reviewed = GovernedByEmailContract.Concat(ExemptWithReason).ToHashSet();
        var unreviewed = emailValidators.Where(n => !reviewed.Contains(n)).ToList();

        Assert.True(unreviewed.Count == 0,
            "New email-accepting validator(s) not on the reviewed list: " + string.Join(", ", unreviewed)
            + ". Either confirm it uses FluentValidation's EmailAddress() and add to EmailContractTests + "
            + "GovernedByEmailContract above, or add to ExemptWithReason with a documented reason "
            + "(a genuinely different, reviewed rule — not just an oversight).");

        var stale = reviewed.Where(n => !validatorTypes.Any(t => t.Name == n)).ToList();
        Assert.True(stale.Count == 0, "Reviewed-list entries that no longer match any validator class (renamed/removed?): "
            + string.Join(", ", stale));
    }
}

using System.Linq;
using System.Reflection;
using FluentValidation;
using Xunit;

namespace PEMS.ArchitectureTests;

/// <summary>
/// Patch 7 (P7.5) — architecture guard against a FUTURE phone-accepting validator silently regressing
/// to <c>MaximumLength(...)</c> alone (Patch 3's original bug: a direct API call could persist
/// "+821012340001123213sd" because nothing checked SHAPE, only length).
///
/// <para>
/// This cannot be a pure behavior test the way <c>PhoneContractTests</c> is — FluentValidation's
/// canonical rule (<see cref="PEMS.Application.Common.Validation.PhoneNumberRules.MustBeAPhoneNumber{T}"/>)
/// is wired as a <c>.Must(predicate)</c> lambda, which is indistinguishable BY TYPE from any other
/// <c>.Must(...)</c> call on the same property — reflection cannot tell "this predicate checks phone
/// shape" from "this predicate checks something else" without executing it.
/// </para>
/// <para>
/// So this is a DISCOVERY gate instead, the same idiom <see cref="AuthorizationTests"/> already uses
/// for anonymous actions: reflectively enumerate every FluentValidation validator in
/// <c>PEMS.Application</c> whose validated type has a property literally named <c>Phone</c>, and
/// require each one to appear on an explicit, reviewed list — either governed (covered by
/// <c>PhoneContractTests</c>'s behavior matrix) or exempt (a different, spec-backed rule set: Campus
/// master data, Department Personnel — see that class's own doc comment for why forcing the Visit
/// contract onto those would be wrong, not merely undone). A validator that shows up UNLISTED fails
/// the build immediately, forcing a conscious decision instead of a silent gap.
/// </para>
/// </summary>
public class PhoneValidatorDiscoveryTests
{
    private static readonly Assembly ApplicationAssembly = Assembly.Load("PEMS.Application");

    /// <summary>
    /// Governed by the canonical rule — every one of these is exercised by
    /// <c>PEMS.UnitTests.Validation.PhoneContractTests</c>'s behavior matrix (VN local / +84 / other
    /// international pass; letters / the exact regression value / too-short fail).
    /// </summary>
    private static readonly string[] GovernedByPhoneContract =
    {
        "UpdateRegistrantInfoCommandValidator",
        "ConfirmBusinessCardContactCommandValidator",
        "CreatePartnerCommandValidator",           // InitialContact.Phone — nested, see note below
        "UpdatePartnerContactCommandValidator",
        "CreatePartnerContactCommandValidator",
        "ReplaceOperationalContactCommandValidator",
        "UpdateOperationalContactProfileCommandValidator",
        "SaveOperationalContactCommandValidator",
        "InitiateOperationalContactTransferCommandValidator",
        "RegistrantInputV2Validator",
        "OperationalContactV2Validator",
    };

    /// <summary>
    /// Explicitly NOT governed by the Visit phone contract, with the reason each was reviewed and
    /// exempted rather than silently missed.
    /// </summary>
    private static readonly string[] ExemptWithReason =
    {
        // Structural rule only, by design: an EXISTING campus's contact snapshot being replayed
        // through edit/resubmit/amendment is read-only here — format is not re-enforced on a value
        // the caller cannot fix on this screen. See OperationalContactReplayV2Validator's own doc
        // comment. Whether the snapshot actually CHANGED is enforced separately and unconditionally.
        "OperationalContactReplayV2Validator",

        // Campus master data (PERMISSION_MATRIX §5.14) — a separate, HO-only, spec-backed rule set.
        // Patch 3 proved these are governed by their own contract, not the Visit domain's.
        "UpdateCampusCommandValidator",
        "AddNewCampusCommandValidator",

        // Department Personnel — same reasoning as Campus: its own spec-backed rule set, not a Visit
        // write path, and Patch 3 explicitly declined to fold it into the Visit phone contract.
        // AddDepartmentPersonnelCommandValidator is the legacy variant of this same domain.
        "UpdateDepartmentPersonnelCommandValidator",
        "CreateDepartmentPersonnelCommandValidator",
        "AddDepartmentPersonnelCommandValidator",

        // Account management (login account creation, Replace Staff Leader, self-profile) — a
        // separate domain Patch 3 never claimed to govern, same reasoning as Campus/Department
        // Personnel above. Found by THIS discovery test (its own point): none of these three
        // currently validate Phone's shape AT ALL, not even MaximumLength — a real gap, but out of
        // Patch 3/7.5's explicit scope ("Visit/Operational Contact/Partner/OCR paths" only) to fix
        // here. Documented as a known, deferred finding rather than silently left off this list or
        // silently fixed without being asked.
        "CreateAccountCommandValidator",
        "ReplaceStaffLeaderCommandValidator",
        "UpdateProfileCommandValidator",
    };

    private static bool HasPhoneProperty(System.Type validatedType)
        => validatedType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.Name == "Phone")
        // The one known nested case: CreatePartnerCommand carries the phone on
        // InitialContact.Phone, not on the command itself. Detected explicitly rather than via
        // general recursive property-graph reflection, which would be far more fragile than the
        // value of catching a second, still-hypothetical nested case is worth.
        || validatedType.Name == "CreatePartnerCommand";

    [Fact]
    public void Every_phone_accepting_validator_in_PEMS_Application_is_on_the_reviewed_list()
    {
        var validatorTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.BaseType is { IsGenericType: true }
                        && t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .ToList();
        Assert.True(validatorTypes.Count > 10, "Suspiciously few validators discovered — reflection scope is broken.");

        var phoneValidators = validatorTypes
            .Where(t => HasPhoneProperty(t.BaseType!.GetGenericArguments()[0]))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();
        Assert.True(phoneValidators.Count > 0, "No phone-accepting validators found at all — the discovery predicate itself is broken.");

        var reviewed = GovernedByPhoneContract.Concat(ExemptWithReason).ToHashSet();
        var unreviewed = phoneValidators.Where(n => !reviewed.Contains(n)).ToList();

        Assert.True(unreviewed.Count == 0,
            "New phone-accepting validator(s) not on the reviewed list: " + string.Join(", ", unreviewed)
            + ". Either wire PhoneNumberRules.MustBeAPhoneNumber and add to PhoneContractTests + "
            + "GovernedByPhoneContract above, or add to ExemptWithReason with a documented reason "
            + "(a genuinely different, spec-backed rule set — not just an oversight).");

        // The inverse also matters: an entry that no longer resolves to a real validator means the
        // list has drifted (a rename, a deletion) and is silently no longer testing what it claims to.
        var stale = reviewed.Where(n => !validatorTypes.Any(t => t.Name == n)).ToList();
        Assert.True(stale.Count == 0, "Reviewed-list entries that no longer match any validator class (renamed/removed?): "
            + string.Join(", ", stale));
    }
}

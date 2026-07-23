using System;
using System.Collections.Generic;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequestV2;
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequestV2;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// "A visit request has at least one campus" is the invariant the whole per-campus model rests on: with
/// no campus there is no form detail, so there is nothing to read and nothing to report on.
///
/// It was enforced at two layers but tested at neither. That mattered when the dead
/// <c>OrderBy(...).First()</c> compatibility projections were removed from the create and resubmit
/// services: those expressions would have thrown on an empty collection, so the question "is the real
/// guard somewhere else?" had to be answered from the code alone. These tests make the answer
/// executable, at each layer independently.
/// </summary>
public class VisitRequestV2CampusInvariantTests
{
    private static readonly DateTime Start = DateTime.Now.AddDays(20);

    private static RegistrantInputV2 Registrant()
        => new("Người ĐK", "VN", "ĐH X", "TP", "+84912345678", "reg@example.com");

    private static ContactPointDto PrimaryContact()
        => new("ĐM", "ĐH X", "+84987654321", "contact@example.com");

    // ── Layer 1: the create validator (boundary, before any transaction opens) ──

    private static readonly CreateVisitRequestV2CommandValidator CreateValidator = new();

    private static CreateVisitRequestV2Command CreateCommand(List<CampusVisitFormDto>? campuses)
        => new(new VisitRequestFormDataV2(
            "SUB-1", Registrant(), PrimaryContact(), null, campuses!));

    [Fact]
    public void Create_validator_rejects_an_empty_campus_list()
    {
        var result = CreateValidator.Validate(CreateCommand(new List<CampusVisitFormDto>()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CampusVisits", StringComparison.Ordinal));
    }

    /// <summary>The DTO property is a plain reference, so a hand-written caller can also send null.</summary>
    [Fact]
    public void Create_validator_rejects_a_null_campus_list()
    {
        var result = CreateValidator.Validate(CreateCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CampusVisits", StringComparison.Ordinal));
    }

    // The service-layer half of this invariant lives in PEMS.IntegrationTests
    // (VisitRequestV2ServiceCampusGuardTests): PEMS.UnitTests deliberately references only Domain and
    // Application, and the guard being tested is in Infrastructure.

    // ── Layer 2: the edit / resubmit validators ──

    private static readonly UpdatePendingVisitRequestV2CommandValidator PendingEditValidator = new();
    private static readonly ResubmitRejectedVisitRequestV2CommandValidator ResubmitValidator = new();

    private static VisitRequestEditV2Dto Edit(List<CampusVisitEditV2Dto>? campuses)
        => new(0, Registrant(), PrimaryContact(), null, campuses!);

    [Fact]
    public void Pending_edit_validator_rejects_an_empty_campus_list()
    {
        var result = PendingEditValidator.Validate(
            new UpdatePendingVisitRequestV2Command(1, Edit(new List<CampusVisitEditV2Dto>())));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CampusVisits", StringComparison.Ordinal));
    }

    /// <summary>
    /// Resubmit cannot change the campus set at all, so an empty list is doubly wrong — it must be
    /// refused for being empty, not merely for differing from the request's existing campuses.
    /// </summary>
    [Fact]
    public void Resubmit_validator_rejects_an_empty_campus_list()
    {
        var result = ResubmitValidator.Validate(
            new ResubmitRejectedVisitRequestV2Command(1, Edit(new List<CampusVisitEditV2Dto>())));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CampusVisits", StringComparison.Ordinal));
    }
}

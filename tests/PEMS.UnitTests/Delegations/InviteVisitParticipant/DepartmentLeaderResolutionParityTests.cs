using Moq;
using PEMS.Application.Delegations.Commands.InviteVisitParticipant;
using PEMS.Application.Delegations.Queries.GetSupportDepartments;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.InviteVisitParticipant;

/// <summary>
/// The list the host reads and the handler that sends must name the SAME department leader.
///
/// <para>
/// These are two independent queries in two files, and they answer the same question: who is this
/// department's active leader. The host picks a department from the list — which shows a name and, in
/// the preview, binds the approval to that person's id — and the send resolves the leader again for
/// itself. If the two ever disagreed, the host would approve a message addressed to the leader they
/// were shown while the send addressed someone else, and the scope check would reject a request that
/// is not the user's fault. Whichever leader is chosen matters less than the two ends choosing alike.
/// </para>
/// <para>
/// So these tests run BOTH sides over one fixture and compare their answers, rather than asserting a
/// hard-coded id against each separately — which is what let the orderings drift apart in the first
/// place. The business rule itself is unchanged: a valid configured head wins, otherwise the first
/// leader by name, with the user id breaking a tie between identical names.
/// </para>
/// </summary>
public class DepartmentLeaderResolutionParityTests
{
    private const ulong DeptId = 20;

    private static DelegationsTestDbContext SeedDept(ulong? headUserId, params (ulong Id, string Name)[] leaders)
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        db.Departments.Add(DelegationsTestData.CreateDepartment(DeptId, headUserId: headUserId));
        foreach (var (id, name) in leaders)
        {
            var u = DelegationsTestData.CreateUser(
                id, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, DeptId);
            u.FullName = name;
            db.Users.Add(u);
        }
        db.SaveChanges();
        return db;
    }

    /// <summary>Who the host is shown in the support-department picker.</summary>
    private static async Task<ulong?> ListedLeaderAsync(DelegationsTestDbContext db)
    {
        var handler = new GetSupportDepartmentsQueryHandler(db, new FakeDelegationsCurrentUser());
        var result = await handler.Handle(
            new GetSupportDepartmentsQuery(DelegationsTestData.VisitInstanceId, null), default);
        return result.Single(d => d.DepartmentId == DeptId).LeaderUserId;
    }

    /// <summary>Who the send actually invites — and therefore whose id the approval scope must carry.</summary>
    private static async Task<(ulong TargetUserId, string ScopeKey)> InvitedLeaderAsync(DelegationsTestDbContext db)
    {
        var mocks = new DelegationsHandlerMocks();
        var formRead = new Mock<PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService>();
        formRead
            .Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PEMS.Domain.Entities.Delegations.VisitRequest _, IReadOnlyList<ulong> ids, CancellationToken _) =>
                ids.ToDictionary(
                        id => id,
                        _ => new PEMS.Application.Delegations.Services.VisitFormRead.VisitCampusFormContent
                        {
                            DelegationName = "Đoàn khách kiểm thử",
                        })
                   as IReadOnlyDictionary<ulong, PEMS.Application.Delegations.Services.VisitFormRead.VisitCampusFormContent>);

        var approved = new StubApprovedEmailContentResolver(mocks.Sanitizer.Object);
        var handler = new InviteVisitParticipantCommandHandler(
            db, new FakeDelegationsCurrentUser(), mocks.Clock, mocks.DispatcherFor(db), mocks.Tokens.Object,
            mocks.Sanitizer.Object, mocks.Storage.Object, mocks.Normalizer.Object, mocks.Notifications.Object,
            formRead.Object, new RecordingUserMutationLockService(), approved);

        await handler.Handle(
            new InviteVisitParticipantCommand(DelegationsTestData.VisitInstanceId, "DEPT_SUPPORT", null, DeptId, null),
            default);

        var participant = db.VisitParticipants.Single(p => p.ParticipantRole == ParticipantRoles.DeptSupport);
        return (participant.UserId, approved.Calls.Single().ScopeKey);
    }

    /// <summary>Both sides, one fixture, one answer — plus the scope that answer implies.</summary>
    private static async Task AssertBothSidesAgreeAsync(DelegationsTestDbContext db, ulong expected)
    {
        var listed = await ListedLeaderAsync(db);
        var (invited, scopeKey) = await InvitedLeaderAsync(db);

        Assert.Equal(expected, listed);
        Assert.Equal(expected, invited);
        Assert.Equal(listed, invited);
        // The preview binds to the LISTED leader, the send recomputes from the INVITED one. Equal ids
        // are only useful if they produce the string the verifier compares.
        Assert.Equal($"visitInstance:{DelegationsTestData.VisitInstanceId}|participant:{expected}", scopeKey);
    }

    /// <summary>
    /// Name order and id order disagree, and no configured head decides it. Alphabetical order wins on
    /// both sides — the point being that they agree, not which one it is.
    /// </summary>
    [Fact]
    public async Task NameOrderAgainstIdOrder_BothSidesPickTheSameLeader()
    {
        // Ids avoid 100 (the fixture host) and 900 (its IC department).
        using var db = SeedDept(headUserId: null, (150, "Z Leader"), (250, "A Leader"));

        await AssertBothSidesAgreeAsync(db, expected: 250);
    }

    /// <summary>The reverse arrangement, so neither "lowest id" nor "highest id" can pass by accident.</summary>
    [Fact]
    public async Task NameOrderMatchingIdOrder_BothSidesPickTheSameLeader()
    {
        using var db = SeedDept(headUserId: null, (150, "A Leader"), (250, "Z Leader"));

        await AssertBothSidesAgreeAsync(db, expected: 150);
    }

    /// <summary>
    /// Two active leaders with the SAME full name — the case that has no answer without a second key.
    ///
    /// <para>
    /// Honest limit of this test: it cannot FAIL for a missing tie-break. The two sides sort the same
    /// in-memory list, and LINQ's OrderBy is stable, so under the InMemory provider they agree by
    /// accident even with the id key removed (verified by removing it). The real risk lives on MySQL,
    /// where `ORDER BY full_name` leaves tied rows in an unspecified order and the two sides take
    /// different execution paths — one sorts in SQL, the other in memory after fetching. The
    /// `ThenBy(UserId)` on both is what removes the ambiguity there; what this test pins is the
    /// agreement itself and the expected winner, so a future change that reorders one side alone is
    /// caught by a named expectation rather than by nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TwoLeadersSharingAFullName_ResolveDeterministicallyOnBothSides()
    {
        using var db = SeedDept(headUserId: null, (300, "Nguyễn Văn Trưởng"), (150, "Nguyễn Văn Trưởng"));

        await AssertBothSidesAgreeAsync(db, expected: 150);
    }

    /// <summary>
    /// A valid configured head still outranks name order — the tie-break added here decides only who is
    /// chosen when nothing else does, and must not quietly become the primary rule.
    /// </summary>
    [Fact]
    public async Task AConfiguredHead_StillOutranksNameAndIdOrder()
    {
        using var db = SeedDept(headUserId: 300, (150, "A Leader"), (300, "Z Leader"));

        await AssertBothSidesAgreeAsync(db, expected: 300);
    }
}

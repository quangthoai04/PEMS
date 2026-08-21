using Moq;
using PEMS.Application.Delegations.Commands.SignVisitLogisticsHandover;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.SignVisitLogisticsHandover;

/// <summary>
/// The item must close ("Hoàn thành") only once BOTH sides have signed the RETURN ("nghiệm thu")
/// handover — not the moment either one signs alone. The host signs the borrower side through this
/// handler; the department signs the provider side through the sibling
/// SignLogisticsHandoverCommand (DepartmentReceptionTasks). Before this fix, the host's signature
/// alone flipped the item to DONE regardless of whether the department had signed RETURN at all.
/// </summary>
public class SignVisitLogisticsHandoverCommandHandlerTests
{
    private const ulong ItemId = 800;

    private static (DelegationsTestDbContext Db, SignVisitLogisticsHandoverCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        db.VisitLogisticsItems.Add(new VisitLogisticsItem
        {
            LogisticsItemId = ItemId,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            ItemType = "TRANSPORT",
            Title = "Xe điện đón đoàn",
            Status = LogisticsItemStatus.InProgress,
            CoordinationMode = "SYSTEM_REQUEST",
            RequestedToDepartmentId = 900,
            CreatedAt = new DateTime(2026, 6, 1),
        });
        // BORROW must be fully signed before RETURN is accepted (existing rule, unchanged).
        db.LogisticsHandovers.Add(new VisitLogisticsItemHandover
        {
            HandoverId = 1,
            LogisticsItemId = ItemId,
            HandoverType = LogisticsHandoverTypes.Borrow,
            BorrowerSignedBy = DelegationsTestData.HostUserId,
            BorrowerSignedAt = new DateTime(2026, 7, 31, 8, 0, 0),
            ProviderSignedBy = 901,
            ProviderSignedAt = new DateTime(2026, 7, 31, 8, 5, 0),
            CreatedAt = new DateTime(2026, 7, 31, 8, 0, 0),
        });
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser(); // defaults to the seeded instance host
        var mocks = new DelegationsHandlerMocks();
        var handler = new SignVisitLogisticsHandoverCommandHandler(db, user, mocks.Clock, mocks.Notifications.Object, mocks.Locks);
        return (db, handler, user, mocks);
    }

    [Fact]
    public async Task HostSigningReturnAlone_LeavesItemInProgress_NotDone()
    {
        var (db, handler, _, _) = CreateSut();

        var response = await handler.Handle(
            new PEMS.Application.Delegations.Commands.SignVisitLogisticsHandover.SignVisitLogisticsHandoverCommand(
                DelegationsTestData.VisitInstanceId, ItemId, LogisticsHandoverTypes.Return, "GOOD", null),
            default);

        Assert.Equal(LogisticsItemStatus.InProgress, response.Status);
        var item = Assert.Single(db.VisitLogisticsItems);
        Assert.Equal(LogisticsItemStatus.InProgress, item.Status);
        Assert.Null(item.CompletedAt);

        var returnRow = db.LogisticsHandovers.Single(h => h.HandoverType == LogisticsHandoverTypes.Return);
        Assert.NotNull(returnRow.BorrowerSignedAt);
        Assert.Null(returnRow.ProviderSignedAt);
    }

    [Fact]
    public async Task HostSigningReturn_AfterDepartmentAlreadySignedReturn_ClosesTheItem()
    {
        var (db, handler, _, _) = CreateSut();
        // Department (provider side) already signed RETURN first.
        db.LogisticsHandovers.Add(new VisitLogisticsItemHandover
        {
            HandoverId = 2,
            LogisticsItemId = ItemId,
            HandoverType = LogisticsHandoverTypes.Return,
            ProviderSignedBy = 901,
            ProviderSignedAt = new DateTime(2026, 8, 1, 11, 5, 0),
            CreatedAt = new DateTime(2026, 8, 1, 11, 5, 0),
        });
        db.SaveChanges();

        var response = await handler.Handle(
            new PEMS.Application.Delegations.Commands.SignVisitLogisticsHandover.SignVisitLogisticsHandoverCommand(
                DelegationsTestData.VisitInstanceId, ItemId, LogisticsHandoverTypes.Return, "GOOD", null),
            default);

        Assert.Equal(LogisticsItemStatus.Done, response.Status);
        var item = Assert.Single(db.VisitLogisticsItems);
        Assert.Equal(LogisticsItemStatus.Done, item.Status);
        Assert.NotNull(item.CompletedAt);
    }

    // Producer contract (stabilization round 2 §3/§23): this producer and its sibling
    // SignLogisticsHandoverCommand (DepartmentReceptionTasks) both emit the SAME eventKey
    // (LogisticsHandoverSigned) — an ActionType here that outranked the eventKey with a DIFFERENT
    // classification than the sibling's would make the same real-world event route two different
    // ways depending on which side signed. Pins the fix directly rather than relying only on the
    // frontend's static coverage table (which cannot see which producer emits which ActionType).
    [Fact]
    public async Task NotifiesTheDepartmentAssignee_WithActionTypeMatchingItsSiblingProducer()
    {
        const ulong AssigneeId = 950;
        var (db, handler, _, mocks) = CreateSut();
        db.VisitLogisticsItems.Single().AssignedToUserId = AssigneeId;
        db.SaveChanges();

        await handler.Handle(
            new PEMS.Application.Delegations.Commands.SignVisitLogisticsHandover.SignVisitLogisticsHandoverCommand(
                DelegationsTestData.VisitInstanceId, ItemId, LogisticsHandoverTypes.Return, "GOOD", null),
            default);

        mocks.Notifications.Verify(
            n => n.CreateAsync(
                It.Is<PEMS.Application.Notifications.Common.CreateNotificationRequest>(r =>
                    r.RecipientUserId == AssigneeId
                    && r.ActionType == PEMS.Application.Notifications.Common.NotificationActionTypes.OpenHandoverDetail),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

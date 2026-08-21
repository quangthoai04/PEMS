using PEMS.Application.DepartmentReceptionTasks.Commands.SignLogisticsHandover;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.DepartmentReceptionTasks;

/// <summary>
/// The item must close ("Hoàn thành") only once BOTH sides have signed the RETURN ("nghiệm thu")
/// handover — not the moment either one signs alone. The department signs the provider side through
/// this handler; the host signs the borrower side through the sibling
/// SignVisitLogisticsHandoverCommand (Delegations). Before this fix, the department's RETURN
/// signature never even updated the item's status (only Return+Borrower was handled), which masked
/// the same underlying bug from the other direction.
/// </summary>
public class SignLogisticsHandoverCommandHandlerTests
{
    private const ulong DeptId = 40;
    private const ulong DeptStaffId = 400;
    private const ulong ItemId = 810;

    private static (DelegationsTestDbContext Db, SignLogisticsHandoverCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        db.Departments.Add(DelegationsTestData.CreateDepartment(DeptId));
        db.Users.Add(DelegationsTestData.CreateUser(DeptStaffId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Staff, DeptId));
        db.VisitLogisticsItems.Add(new VisitLogisticsItem
        {
            LogisticsItemId = ItemId,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            ItemType = "TRANSPORT",
            Title = "Xe điện đón đoàn",
            Status = LogisticsItemStatus.InProgress,
            CoordinationMode = "SYSTEM_REQUEST",
            RequestedToDepartmentId = DeptId,
            AssignedToUserId = DeptStaffId,
            CreatedAt = new DateTime(2026, 6, 1),
        });
        db.LogisticsHandovers.Add(new VisitLogisticsItemHandover
        {
            HandoverId = 1,
            LogisticsItemId = ItemId,
            HandoverType = LogisticsHandoverTypes.Borrow,
            BorrowerSignedBy = DelegationsTestData.HostUserId,
            BorrowerSignedAt = new DateTime(2026, 7, 31, 8, 0, 0),
            ProviderSignedBy = DeptStaffId,
            ProviderSignedAt = new DateTime(2026, 7, 31, 8, 5, 0),
            CreatedAt = new DateTime(2026, 7, 31, 8, 0, 0),
        });
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser
        {
            UserId = DeptStaffId,
            RoleId = DelegationsTestData.DepartmentRoleId,
            RoleCode = RoleCodes.Department,
            SubRole = UserSubRoles.Staff,
            DepartmentId = DeptId,
        };
        var mocks = new DelegationsHandlerMocks();
        var handler = new SignLogisticsHandoverCommandHandler(db, user, mocks.Notifications.Object, mocks.Locks);
        return (db, handler, user, mocks);
    }

    [Fact]
    public async Task DepartmentSigningReturnAlone_LeavesItemInProgress_NotDone()
    {
        var (db, handler, _, _) = CreateSut();

        var response = await handler.Handle(new SignLogisticsHandoverCommand
        {
            LogisticsItemId = ItemId,
            HandoverType = LogisticsHandoverTypes.Return,
            SignerSide = HandoverSignerSides.Provider,
        }, default);

        Assert.Equal(LogisticsItemStatus.InProgress, response.Status);
        var item = Assert.Single(db.VisitLogisticsItems);
        Assert.Equal(LogisticsItemStatus.InProgress, item.Status);
        Assert.Null(item.CompletedAt);

        var returnRow = db.LogisticsHandovers.Single(h => h.HandoverType == LogisticsHandoverTypes.Return);
        Assert.NotNull(returnRow.ProviderSignedAt);
        Assert.Null(returnRow.BorrowerSignedAt);
    }

    [Fact]
    public async Task DepartmentSigningReturn_AfterHostAlreadySignedReturn_ClosesTheItem()
    {
        var (db, handler, _, _) = CreateSut();
        // Host (borrower side) already signed RETURN first.
        db.LogisticsHandovers.Add(new VisitLogisticsItemHandover
        {
            HandoverId = 2,
            LogisticsItemId = ItemId,
            HandoverType = LogisticsHandoverTypes.Return,
            BorrowerSignedBy = DelegationsTestData.HostUserId,
            BorrowerSignedAt = new DateTime(2026, 8, 1, 11, 0, 0),
            CreatedAt = new DateTime(2026, 8, 1, 11, 0, 0),
        });
        db.SaveChanges();

        var response = await handler.Handle(new SignLogisticsHandoverCommand
        {
            LogisticsItemId = ItemId,
            HandoverType = LogisticsHandoverTypes.Return,
            SignerSide = HandoverSignerSides.Provider,
        }, default);

        Assert.Equal(LogisticsItemStatus.Done, response.Status);
        var item = Assert.Single(db.VisitLogisticsItems);
        Assert.Equal(LogisticsItemStatus.Done, item.Status);
        Assert.NotNull(item.CompletedAt);
    }
}

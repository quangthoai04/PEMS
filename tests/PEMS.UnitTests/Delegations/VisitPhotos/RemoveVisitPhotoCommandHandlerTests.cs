using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.VisitPhotos.Commands.RemoveVisitPhoto;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// Soft delete: only the uploading Student (still an ACCEPTED participant of the photo's OWN
/// instance) may remove; the row flips to REMOVED with the full removed_* audit trio; the files row
/// and Drive binary stay (soft-delete policy). IDOR by photo id resolves scope from the DB, never
/// from anything the client claims.
/// </summary>
public class RemoveVisitPhotoCommandHandlerTests
{
    private static (DelegationsTestDbContext Db, RemoveVisitPhotoCommandHandler Handler) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        VisitPhotoTestSeed.SeedAcceptedStudent(db);
        var handler = new RemoveVisitPhotoCommandHandler(db, VisitPhotoTestSeed.StudentCurrentUser());
        return (db, handler);
    }

    [Fact]
    public async Task OwnPhoto_IsSoftDeleted_WithReasonAndAudit()
    {
        var (db, handler) = CreateSut();
        var folder = VisitPhotoTestSeed.AddFolder(db);
        var photo = VisitPhotoTestSeed.AddPhoto(db, folder, 900, VisitPhotoTestSeed.StudentUserId);

        await handler.Handle(new RemoveVisitPhotoCommand
        {
            VisitPhotoId = photo.VisitPhotoId,
            Reason = "Ảnh bị mờ",
        }, default);

        var row = db.VisitPhotos.Single();
        Assert.Equal("REMOVED", row.Status);
        Assert.Equal(VisitPhotoTestSeed.StudentUserId, row.RemovedBy);
        Assert.NotNull(row.RemovedAt);
        Assert.Equal("Ảnh bị mờ", row.RemovalReason);
        // Soft delete: binary metadata stays for the FK/uniqueness invariants.
        Assert.Single(db.Files.ToList());
        Assert.Single(db.AuditLogs.Where(a => a.Action == "REMOVE_VISIT_PHOTO"));
    }

    [Fact]
    public async Task SomeoneElsesPhoto_IsForbidden()
    {
        var (db, handler) = CreateSut();
        var folder = VisitPhotoTestSeed.AddFolder(db);
        var photo = VisitPhotoTestSeed.AddPhoto(db, folder, 901, VisitPhotoTestSeed.OtherStudentUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new RemoveVisitPhotoCommand { VisitPhotoId = photo.VisitPhotoId, Reason = "x" }, default));

        Assert.Equal("ACTIVE", db.VisitPhotos.Single().Status);
    }

    [Fact]
    public async Task AlreadyRemovedPhoto_IsRejected()
    {
        var (db, handler) = CreateSut();
        var folder = VisitPhotoTestSeed.AddFolder(db);
        var photo = VisitPhotoTestSeed.AddPhoto(
            db, folder, 902, VisitPhotoTestSeed.StudentUserId, status: "REMOVED");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new RemoveVisitPhotoCommand { VisitPhotoId = photo.VisitPhotoId, Reason = "x" }, default));
        Assert.Equal("VISIT_PHOTO_ALREADY_REMOVED", ex.ErrorCode);
    }

    [Fact]
    public async Task PhotoInInstanceOutsideStudentScope_IsForbidden()
    {
        var (db, handler) = CreateSut();
        // A sibling campus instance of the same request the Student was NEVER accepted into.
        var sibling = DelegationsTestData.CreateVisitInstance(visitInstanceId: 11, campusId: DelegationsTestData.OtherCampusId);
        db.VisitRequestCampuses.Add(sibling);
        db.SaveChanges();
        var folder = VisitPhotoTestSeed.AddFolder(db);
        var photo = VisitPhotoTestSeed.AddPhoto(
            db, folder, 903, VisitPhotoTestSeed.StudentUserId, visitInstanceId: 11);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new RemoveVisitPhotoCommand { VisitPhotoId = photo.VisitPhotoId, Reason = "x" }, default));
    }

    [Fact]
    public async Task UnknownPhoto_IsNotFound()
    {
        var (_, handler) = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new RemoveVisitPhotoCommand { VisitPhotoId = 9999, Reason = "x" }, default));
    }

    [Fact]
    public void Validator_RequiresReason()
    {
        var validator = new RemoveVisitPhotoCommandValidator();

        Assert.False(validator.Validate(new RemoveVisitPhotoCommand { VisitPhotoId = 1, Reason = " " }).IsValid);
        Assert.False(validator.Validate(new RemoveVisitPhotoCommand { VisitPhotoId = 1, Reason = new string('a', 501) }).IsValid);
        Assert.True(validator.Validate(new RemoveVisitPhotoCommand { VisitPhotoId = 1, Reason = "Lý do hợp lệ" }).IsValid);
    }
}

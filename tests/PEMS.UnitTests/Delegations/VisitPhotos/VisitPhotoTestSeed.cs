using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Documents;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// Shared fixtures for the Student visit-photo slice: the base Delegations skeleton plus an ACTIVE
/// Student (id 200) with an ACCEPTED STUDENT participation in instance 10 of request 10.
/// </summary>
public static class VisitPhotoTestSeed
{
    public const ulong StudentUserId = 200;
    public const ulong OtherStudentUserId = 201;

    public static void SeedAcceptedStudent(
        DelegationsTestDbContext db,
        ulong userId = StudentUserId,
        string participantStatus = ParticipantStatuses.Accepted,
        string instanceStatus = "AFTER_VISIT")
    {
        DelegationsTestData.SeedBase(db, instanceStatus);
        db.Users.Add(DelegationsTestData.CreateUser(userId, DelegationsTestData.StudentRoleId, null, null));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            500 + userId, userId, ParticipantRoles.Student, participantStatus));
        db.SaveChanges();
    }

    public static FakeDelegationsCurrentUser StudentCurrentUser(ulong userId = StudentUserId) => new()
    {
        UserId = userId,
        RoleId = DelegationsTestData.StudentRoleId,
        RoleCode = RoleCodes.Student,
        SubRole = null,
        Email = $"user{userId}@test.local",
    };

    public static VisitPhotoFolder AddFolder(
        DelegationsTestDbContext db, ulong visitRequestId = DelegationsTestData.VisitRequestId)
    {
        var folder = new VisitPhotoFolder
        {
            VisitRequestId = visitRequestId,
            ExternalFolderId = $"drv-vr-{visitRequestId}",
            FolderName = $"VR-{visitRequestId}",
            WebViewUrl = $"https://drive.google.com/drive/folders/drv-vr-{visitRequestId}",
            Status = "ACTIVE",
            CreatedAt = new DateTime(2026, 7, 1),
            CreatedBy = StudentUserId,
        };
        db.VisitPhotoFolders.Add(folder);
        db.SaveChanges();
        return folder;
    }

    public static VisitPhoto AddPhoto(
        DelegationsTestDbContext db, VisitPhotoFolder folder, ulong fileId, ulong uploadedBy,
        ulong visitInstanceId = DelegationsTestData.VisitInstanceId, string status = "ACTIVE")
    {
        db.Files.Add(new UploadedFile
        {
            FileId = fileId,
            StorageProvider = "GOOGLE_DRIVE",
            ObjectKey = $"visit-photos/{fileId}.jpg",
            OriginalFilename = $"photo-{fileId}.jpg",
            MimeType = "image/jpeg",
            FilePurpose = "VISIT_REQUEST_PHOTO",
            UploadedBy = uploadedBy,
            UploadedAt = new DateTime(2026, 7, 2),
        });
        var photo = new VisitPhoto
        {
            VisitRequestId = folder.VisitRequestId,
            VisitInstanceId = visitInstanceId,
            VisitPhotoFolderId = folder.VisitPhotoFolderId,
            FileId = fileId,
            Status = status,
            UploadedBy = uploadedBy,
            UploadedAt = new DateTime(2026, 7, 2),
            RemovedAt = status == "REMOVED" ? new DateTime(2026, 7, 3) : null,
            RemovedBy = status == "REMOVED" ? uploadedBy : null,
            RemovalReason = status == "REMOVED" ? "seed" : null,
        };
        db.VisitPhotos.Add(photo);
        db.SaveChanges();
        return photo;
    }

    /// <summary>Minimal valid JPEG payload (magic bytes FF D8 FF) accepted by the image sniffer.</summary>
    public static byte[] JpegBytes(int length = 32)
    {
        var bytes = new byte[length];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;
        return bytes;
    }

    /// <summary>Valid PNG payload — used with a .jpg name to simulate a spoofed/fake-MIME upload.</summary>
    public static byte[] PngBytes()
        => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
}

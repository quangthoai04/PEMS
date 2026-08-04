using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Commands.RestoreEmailTemplate;
using PEMS.Application.Emails.Commands.UpdateEmailTemplate;
using PEMS.Infrastructure.Persistence;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Builds the two template-write handlers the way the application container does, so every suite that
/// exercises them agrees about their dependencies.
///
/// <para>
/// It used to build a good deal more. The save and the restore were ATOMIC over content AND contact
/// settings, which gave both handlers a policy store and an <c>IMediator</c> — the latter used for
/// exactly one thing: reading the settings back after the commit so the response carried what the
/// database held rather than what the handler believed it wrote. A stub mediator had to be written to
/// route that one query and refuse everything else.
/// </para>
/// <para>
/// All of it is gone with the contact feature. Both handlers now write content and nothing else, so they
/// take a context and a user — and this class is a shorthand rather than a scaffold.
/// </para>
/// </summary>
public static class EmailTemplateHandlers
{
    /// <summary>An HO operator, which is the only role these commands accept.</summary>
    public sealed class HoOperator : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 1;
        public string? Email => "ho-operator@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => "HO";
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? SessionId => null;
        public ulong? DepartmentId => null;
        public string? LoginPortal => null;
    }

    public static UpdateEmailTemplateCommandHandler Update(ApplicationDbContext db)
        => new(db, new HoOperator());

    public static RestoreEmailTemplateCommandHandler Restore(ApplicationDbContext db)
        => new(db, new HoOperator());
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Commands.RestoreEmailTemplate;
using PEMS.Application.Emails.Commands.UpdateEmailTemplate;
using PEMS.Application.Emails.Contact;
using PEMS.Infrastructure.Persistence;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Builds the two template-write handlers the way the application container does, so every suite that
/// exercises them agrees about their dependencies.
///
/// <para>
/// It exists because the save and the restore became ATOMIC over content AND contact settings, which gave
/// both handlers a fourth dependency — an <see cref="IMediator"/> — used for exactly one thing: reading
/// the settings back after the commit, so the response carries what the database holds rather than what
/// the handler believes it wrote. Four suites constructed these handlers by hand and each would otherwise
/// have grown its own copy of the same stub, with its own idea of which requests it routes.
/// </para>
/// <para>
/// The policy store is the REAL one, never a stub. Both handlers judge a body against the CONFIGURED
/// contact requirement, and a test fed the shipped default would be asserting the very drift these
/// handlers were changed to remove.
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

    /// <summary>
    /// Routes the one query these handlers send, and refuses everything else.
    ///
    /// <para>
    /// Refusing loudly rather than returning null is deliberate: a handler that started sending some other
    /// request would otherwise get a silent default back and the suite would keep passing while the
    /// behaviour it was meant to pin had changed.
    /// </para>
    /// </summary>
    public sealed class ContactSettingsMediator : IMediator
    {
        private readonly GetEmailContactSettingsQueryHandler _handler;

        public ContactSettingsMediator(ApplicationDbContext db)
            => _handler = new GetEmailContactSettingsQueryHandler(db, new EmailContactPolicyStore(db));

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request is GetEmailContactSettingsQuery q
                ? (Task<TResponse>)(object)_handler.Handle(q, ct)
                : throw new NotSupportedException($"Unexpected request {request.GetType().Name}.");

        public Task<object?> Send(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    public static UpdateEmailTemplateCommandHandler Update(ApplicationDbContext db)
        => new(db, new HoOperator(), new EmailContactPolicyStore(db), new ContactSettingsMediator(db));

    public static RestoreEmailTemplateCommandHandler Restore(ApplicationDbContext db)
        => new(db, new HoOperator(), new EmailContactPolicyStore(db), new ContactSettingsMediator(db));

    public static GetEmailContactSettingsQueryHandler ContactSettings(ApplicationDbContext db)
        => new(db, new EmailContactPolicyStore(db));
}

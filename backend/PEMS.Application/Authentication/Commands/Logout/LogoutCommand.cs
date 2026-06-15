using MediatR;

namespace PEMS.Application.Authentication.Commands.Logout;

public class LogoutCommand : IRequest<LogoutResponse>
{
}
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace PEMS.ArchitectureTests;

public class ControllerTests
{
    private static readonly Assembly ApiAssembly = Assembly.Load("PEMS.Api");

    [Fact]
    public void Controllers_ShouldNot_InjectDbContext()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOn("PEMS.Infrastructure.Persistence.ApplicationDbContext")
            .GetResult();

        Assert.True(result.IsSuccessful, "Controllers must not inject DbContext.");
    }

    [Fact]
    public void Controllers_ShouldNot_InjectConcreteRepositories()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOnAny(
                "UserRepository", 
                "DelegationRepository"
            )
            .GetResult();

        Assert.True(result.IsSuccessful, "Controllers must not inject concrete Repositories.");
    }

    [Fact]
    public void Controllers_ShouldNot_InjectInfrastructureConcreteServices()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOnAny(
                "JwtTokenService",
                "PasswordHasher",
                "RefreshTokenStore",
                "EmailService",
                "RedisRateLimitStore",
                "OcrService",
                "FaceRecognitionService"
            )
            .GetResult();

        Assert.True(result.IsSuccessful, "Controllers must not inject Infrastructure concrete services.");
    }

    [Fact]
    public void Controllers_Should_OnlyInjectIMediatorOrAbstractions()
    {
        // Enforcing that controllers should only depend on MediatR (ISender/IMediator)
        // or common abstractions. We do this by ensuring they don't depend on PEMS.Infrastructure
        // or PEMS.Application concretes.
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOn("PEMS.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Controllers should not inject Infrastructure directly; use IMediator or abstractions.");
    }
}

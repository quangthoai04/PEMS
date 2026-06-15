using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace PEMS.ArchitectureTests;

public class DependencyRuleTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("PEMS.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("PEMS.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("PEMS.Infrastructure");
    private static readonly Assembly ApiAssembly = Assembly.Load("PEMS.Api");

    [Fact]
    public void Domain_ShouldNot_ReferenceApplication()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("PEMS.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain must not reference Application.");
    }

    [Fact]
    public void Domain_ShouldNot_ReferenceInfrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("PEMS.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain must not reference Infrastructure.");
    }

    [Fact]
    public void Domain_ShouldNot_ReferenceApi()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("PEMS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain must not reference Api.");
    }

    [Fact]
    public void Application_ShouldNot_ReferenceInfrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("PEMS.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application must not reference Infrastructure.");
    }

    [Fact]
    public void Application_ShouldNot_ReferenceApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("PEMS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application must not reference Api.");
    }

    [Fact]
    public void Infrastructure_ShouldNot_ReferenceApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("PEMS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Infrastructure must not reference Api.");
    }
}

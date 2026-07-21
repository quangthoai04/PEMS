using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PEMS.IntegrationTests.Reports;

/// <summary>
/// Proves the disposable MySQL fixture actually stands up before any behavioural assertion relies
/// on it, so a provider/config failure can never be mistaken for a passing regression suite.
/// </summary>
[Collection(CanonicalV2ReaderCollection.Name)]
public sealed class CanonicalV2ReaderSmokeTests
{
    private readonly CanonicalV2ReaderFixture _fx;

    public CanonicalV2ReaderSmokeTests(CanonicalV2ReaderFixture fx) => _fx = fx;

    [Fact]
    public void Fixture_targets_the_disposable_database_on_a_real_provider()
    {
        Assert.Equal(CanonicalV2ReaderFixture.DisposableDbName, _fx.Db.Database.GetDbConnection().Database);
        Assert.Equal("Pomelo.EntityFrameworkCore.MySql", _fx.Db.Database.ProviderName);
        Assert.True(_fx.Db.Database.CanConnect());
    }
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Settings;
using MyRemoteControll.Agent.Infrastructure.Tls;

namespace MyRemoteControll.Agent.Tests.Tls;

/// <summary>
/// Usa o repositório real <c>CurrentUser\My</c>, com nome exclusivo por teste; certificados e chaves criados são
/// removidos ao final.
/// </summary>
public sealed class StoreCertificateProviderTests : IAsyncLifetime
{
    private const string NistP256Oid = "1.2.840.10045.3.1.7";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "myremotecontroll-tests", Guid.NewGuid().ToString("N"));
    private readonly string _friendlyName = $"MyRemoteControll Teste {Guid.NewGuid():N}";
    private readonly List<X509Certificate2> _created = [];
    private string _connectionString = "";

    public async ValueTask InitializeAsync()
    {
        var databasePath = Path.Combine(_directory, "data.db");
        await new DatabaseInitializer(TimeProvider.System, NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync(databasePath, TestContext.Current.CancellationToken);
        _connectionString = SqliteDatabase.ConnectionString(databasePath);
    }

    [Fact]
    public async Task AC10_Certificate_IsEcdsaP256NonExportableValidForFiveYears()
    {
        var (provider, configuration) = CreateProvider();

        var result = await provider.GetOrCreateAsync(TestContext.Current.CancellationToken);
        var certificate = Track(result.Certificate);

        Assert.False(result.Regenerated);
        using (var publicKey = certificate.GetECDsaPublicKey())
        {
            Assert.NotNull(publicKey);
            Assert.Equal(NistP256Oid, publicKey.ExportParameters(includePrivateParameters: false).Curve.Oid.Value);
        }

        Assert.InRange((certificate.NotAfter - certificate.NotBefore).TotalDays, 5 * 365, 5 * 366 + 1);

        using (var privateKey = certificate.GetECDsaPrivateKey())
        {
            Assert.NotNull(privateKey);
            Assert.ThrowsAny<CryptographicException>(() => privateKey.ExportParameters(includePrivateParameters: true));
        }

        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        Assert.Single(store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false));
        Assert.Equal(certificate.Thumbprint, configuration[StoreCertificateProvider.ThumbprintKey]);
    }

    [Fact]
    public async Task SecondCall_ReusesTheStoredCertificate()
    {
        var (provider, _) = CreateProvider();
        var ct = TestContext.Current.CancellationToken;

        var first = Track((await provider.GetOrCreateAsync(ct)).Certificate);
        var second = await provider.GetOrCreateAsync(ct);
        Track(second.Certificate);

        Assert.False(second.Regenerated);
        Assert.Equal(first.Thumbprint, second.Certificate.Thumbprint);
        Assert.Equal(CertificatePin.Compute(first), CertificatePin.Compute(second.Certificate));
    }

    [Fact]
    public async Task ConfiguredCertificateMissing_IsRegeneratedAndFlagged()
    {
        var (provider, _, settings) = CreateProviderWithSettings();
        var ct = TestContext.Current.CancellationToken;
        await settings.SetAsync(StoreCertificateProvider.ThumbprintKey, new string('A', 40), ct);

        var result = await provider.GetOrCreateAsync(ct);
        Track(result.Certificate);

        Assert.True(result.Regenerated);
    }

    public ValueTask DisposeAsync()
    {
        using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadWrite);

            // Instâncias com a mesma impressão digital compartilham a chave persistida: apaga uma vez só.
            foreach (var certificate in _created.DistinctBy(c => c.Thumbprint))
            {
                store.Remove(certificate);
                using var key = certificate.GetECDsaPrivateKey() as ECDsaCng;
                key?.Key.Delete();
            }
        }

        foreach (var certificate in _created)
        {
            certificate.Dispose();
        }

        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }

    private (StoreCertificateProvider Provider, IConfiguration Configuration) CreateProvider()
    {
        var (provider, configuration, _) = CreateProviderWithSettings();
        return (provider, configuration);
    }

    private (StoreCertificateProvider Provider, IConfiguration Configuration, AgentSettingsStore Settings) CreateProviderWithSettings()
    {
        var configuration = new ConfigurationBuilder().Add(new AgentSettingsConfigurationSource(_connectionString)).Build();
        var settingsProvider = configuration.Providers.OfType<AgentSettingsConfigurationProvider>().Single();
        var settings = new AgentSettingsStore(_connectionString, settingsProvider, TimeProvider.System);
        var provider = new StoreCertificateProvider(
            configuration, settings, TimeProvider.System, NullLogger<StoreCertificateProvider>.Instance, _friendlyName);
        return (provider, configuration, settings);
    }

    private X509Certificate2 Track(X509Certificate2 certificate)
    {
        _created.Add(certificate);
        return certificate;
    }
}

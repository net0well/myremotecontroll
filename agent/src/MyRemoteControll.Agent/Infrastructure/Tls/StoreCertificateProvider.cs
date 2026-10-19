using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MyRemoteControll.Agent.Infrastructure.Settings;

namespace MyRemoteControll.Agent.Infrastructure.Tls;

/// <summary>
/// Mantém o certificado no repositório <c>CurrentUser\My</c> com chave não exportável e guarda a impressão digital
/// em <c>AgentSettings</c>.
/// </summary>
internal sealed partial class StoreCertificateProvider(
    IConfiguration configuration,
    AgentSettingsStore settingsStore,
    TimeProvider timeProvider,
    ILogger<StoreCertificateProvider> logger,
    string friendlyName = StoreCertificateProvider.DefaultFriendlyName) : ICertificateProvider
{
    public const string ThumbprintKey = "Tls:CertificateThumbprint";
    public const string DefaultFriendlyName = "MyRemoteControll Agent";

    public async Task<CertificateResult> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var configuredThumbprint = configuration[ThumbprintKey];

        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        if (!string.IsNullOrWhiteSpace(configuredThumbprint))
        {
            var existing = FindUsable(store, configuredThumbprint);
            if (existing is not null)
            {
                return new CertificateResult(existing, Regenerated: false);
            }

            LogConfiguredCertificateUnusable(configuredThumbprint);
        }

        var created = CreateAndStore(store);
        await settingsStore.SetAsync(ThumbprintKey, created.Thumbprint, cancellationToken).ConfigureAwait(false);
        LogCertificateCreated(created.Thumbprint);

        return new CertificateResult(created, Regenerated: !string.IsNullOrWhiteSpace(configuredThumbprint));
    }

    private X509Certificate2? FindUsable(X509Store store, string thumbprint)
    {
        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        X509Certificate2? usable = null;
        foreach (var candidate in matches)
        {
            if (usable is null && candidate.HasPrivateKey && candidate.NotAfter > timeProvider.GetLocalNow().DateTime)
            {
                usable = candidate;
            }
            else
            {
                candidate.Dispose();
            }
        }

        return usable;
    }

    private X509Certificate2 CreateAndStore(X509Store store)
    {
        using var temporary = SelfSignedCertificateFactory.Create(Environment.MachineName, timeProvider.GetUtcNow());

        // A chave criada em memória é efêmera e o SChannel não a aceita; a importação persiste a chave no perfil do
        // usuário, sem o sinalizador Exportable, para que ela não possa ser exportada depois.
        var pkcs12 = temporary.Export(X509ContentType.Pkcs12);
        try
        {
            var persisted = X509CertificateLoader.LoadPkcs12(
                pkcs12, password: null, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
            persisted.FriendlyName = friendlyName;
            store.Add(persisted);
            return persisted;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs12);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Certificado configurado {Thumbprint} não encontrado ou inválido; um novo será gerado")]
    private partial void LogConfiguredCertificateUnusable(string thumbprint);

    [LoggerMessage(Level = LogLevel.Information, Message = "Certificado autoassinado {Thumbprint} criado no repositório do usuário")]
    private partial void LogCertificateCreated(string thumbprint);
}

using System.Security.Cryptography.X509Certificates;

namespace MyRemoteControll.Agent.Infrastructure.Tls;

internal interface ICertificateProvider
{
    Task<CertificateResult> GetOrCreateAsync(CancellationToken cancellationToken);
}

/// <param name="Regenerated">
/// Havia um certificado configurado que não pôde ser usado e outro foi gerado: os aparelhos precisam reparear.
/// </param>
internal sealed record CertificateResult(X509Certificate2 Certificate, bool Regenerated);

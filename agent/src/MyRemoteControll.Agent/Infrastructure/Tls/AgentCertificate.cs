using System.Security.Cryptography.X509Certificates;

namespace MyRemoteControll.Agent.Infrastructure.Tls;

/// <summary>Certificado em uso no listener público e o pin que vai no QR de pareamento.</summary>
internal sealed record AgentCertificate(X509Certificate2 Certificate)
{
    public string Pin { get; } = CertificatePin.Compute(Certificate);
}

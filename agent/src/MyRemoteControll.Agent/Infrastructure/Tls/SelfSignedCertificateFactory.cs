using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MyRemoteControll.Agent.Infrastructure.Tls;

/// <summary>Certificado do listener público: ECDSA P-256, validade de 5 anos (ADR-0006).</summary>
internal static class SelfSignedCertificateFactory
{
    public const int ValidityYears = 5;

    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    public static X509Certificate2 Create(string hostName, DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={hostName}, O=MyRemoteControll", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid(ServerAuthenticationOid)], critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName(hostName);
        alternativeNames.AddDnsName("localhost");
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(alternativeNames.Build());

        // Alguns minutos de folga para relógios levemente adiantados; o app valida pelo pin, não pela data.
        return request.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(ValidityYears));
    }
}

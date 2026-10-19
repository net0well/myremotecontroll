using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MyRemoteControll.Agent.Infrastructure.Tls;

/// <summary>
/// Pin = SHA-256 da chave pública (SPKI) em base64url. Fixar a chave em vez do certificado permite reemitir o
/// certificado com a mesma chave sem reparear os aparelhos (ADR-0006).
/// </summary>
internal static class CertificatePin
{
    public static string Compute(X509Certificate2 certificate) =>
        Base64Url.EncodeToString(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo()));
}

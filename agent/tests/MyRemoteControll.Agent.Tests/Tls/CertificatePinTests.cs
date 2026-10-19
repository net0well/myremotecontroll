using System.Security.Cryptography.X509Certificates;
using MyRemoteControll.Agent.Infrastructure.Tls;

namespace MyRemoteControll.Agent.Tests.Tls;

/// <summary>
/// O app (Dart) e o agente (C#) precisam calcular o mesmo pin. Este certificado e o pin esperado (gerado com OpenSSL:
/// SHA-256 da SPKI em base64url sem preenchimento) são os mesmos de app/test/fixtures/pin_fixture.*.
/// </summary>
public sealed class CertificatePinTests
{
    private const string FixturePem = """
        -----BEGIN CERTIFICATE-----
        MIIBrzCCAVWgAwIBAgIUPt8cVgSI1qapOvdaAUjm3wQF0lUwCgYIKoZIzj0EAwIw
        LTEQMA4GA1UEAwwHZml4dHVyZTEZMBcGA1UECgwQTXlSZW1vdGVDb250cm9sbDAe
        Fw0yNjEwMTgyMjAwMTBaFw0zNjEwMTUyMjAwMTBaMC0xEDAOBgNVBAMMB2ZpeHR1
        cmUxGTAXBgNVBAoMEE15UmVtb3RlQ29udHJvbGwwWTATBgcqhkjOPQIBBggqhkjO
        PQMBBwNCAASDV7kz+8+w3qB3RUf9gxn0ynTdjPcdglAho2qzL3rr4pructSodGnb
        my4M4Zec8BcVnYMwXPZxU76vGhBmZM2co1MwUTAdBgNVHQ4EFgQU6S5bJTxE75JB
        uQ3F6kfvl3JVuxswHwYDVR0jBBgwFoAU6S5bJTxE75JBuQ3F6kfvl3JVuxswDwYD
        VR0TAQH/BAUwAwEB/zAKBggqhkjOPQQDAgNIADBFAiEA77wtTfDNY4cZ36fBIwyu
        rzyR2ws5iOv1763bSQLyWnkCIH3Z2cbpXnwsCf5K1FfaDtwaM0o/aWhjsOBDgovA
        Ii4w
        -----END CERTIFICATE-----
        """;

    private const string ExpectedPin = "q2V1CbPFlOaouT0Ez2evXTKt7GWz25n7UvP3lWn5PXw";

    [Fact]
    public void Pin_MatchesTheOpenSslReferenceSharedWithTheApp()
    {
        using var certificate = X509Certificate2.CreateFromPem(FixturePem);

        Assert.Equal(ExpectedPin, CertificatePin.Compute(certificate));
    }
}

using System.Security.Cryptography;
using System.Text;

namespace MyRemoteControll.Agent.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 com um pepper que fica fora do banco (ADR-0008): quem copiar só o banco não consegue nem verificar
/// palpites de token.
/// </summary>
internal sealed class TokenHasher(byte[] pepper)
{
    public const int HashLength = 32;

    public byte[] Hash(string token) => HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes(token));

    public static bool AreEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
        CryptographicOperations.FixedTimeEquals(left, right);
}

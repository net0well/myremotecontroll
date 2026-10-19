using System.Buffers.Text;
using System.Security.Cryptography;

namespace MyRemoteControll.Agent.Infrastructure.Security;

internal static class SecureTokens
{
    /// <summary>Token de aparelho: 256 bits aleatórios em base64url (ADR-0008).</summary>
    public const int DeviceTokenBytes = 32;

    /// <summary>Código de pareamento: 128 bits, curto o bastante para o QR e impossível de adivinhar em 5 tentativas.</summary>
    public const int PairingCodeBytes = 16;

    private const int MaxEncodedLength = 64;

    public static string Create(int bytes) => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>Rejeita cedo valores que não podem ser um token emitido pelo agente.</summary>
    public static bool HasValidFormat(string? value) =>
        value is { Length: > 0 and <= MaxEncodedLength } && Base64Url.IsValid(value);
}

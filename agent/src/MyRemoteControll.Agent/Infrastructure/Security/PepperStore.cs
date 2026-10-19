using System.Security.Cryptography;
using System.Text;

namespace MyRemoteControll.Agent.Infrastructure.Security;

/// <summary>
/// Guarda o pepper dos tokens em arquivo protegido por DPAPI no escopo do usuário: só esta conta do Windows, neste PC,
/// consegue lê-lo.
/// </summary>
internal static class PepperStore
{
    public const string FileName = "pepper.dat";

    private const int PepperBytes = 32;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MyRemoteControll.TokenPepper.v1");

    /// <param name="regenerated">
    /// Verdadeiro quando havia um pepper ilegível (outro usuário ou outro PC) e um novo foi criado: todos os tokens
    /// existentes deixam de valer.
    /// </param>
    public static byte[] LoadOrCreate(string dataDirectory, out bool regenerated)
    {
        var path = Path.Combine(dataDirectory, FileName);
        regenerated = false;
        if (File.Exists(path))
        {
            try
            {
                return ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                regenerated = true;
            }
        }

        var pepper = RandomNumberGenerator.GetBytes(PepperBytes);
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllBytes(path, ProtectedData.Protect(pepper, Entropy, DataProtectionScope.CurrentUser));
        return pepper;
    }
}

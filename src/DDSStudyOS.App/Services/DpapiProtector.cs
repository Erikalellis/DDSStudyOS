using System;
using System.Security.Cryptography;
using System.Text;

namespace DDSStudyOS.App.Services;

/// <summary>
/// Proteção local usando DPAPI (Windows). Escopo: CurrentUser.
/// </summary>
public static class DpapiProtector
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("DDSStudyOS::Secrets::v1");

    public static byte[] ProtectString(string plainText)
    {
        var data = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(data, optionalEntropy: OptionalEntropy, scope: DataProtectionScope.CurrentUser);
    }

    public static string UnprotectToString(byte[] protectedData)
    {
        try
        {
            var data = ProtectedData.Unprotect(protectedData, optionalEntropy: OptionalEntropy, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (CryptographicException)
        {
            // Compatibilidade com dados antigos salvos sem entropy.
            var legacyData = ProtectedData.Unprotect(protectedData, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(legacyData);
        }
    }
}

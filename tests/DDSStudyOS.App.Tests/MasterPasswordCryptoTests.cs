using DDSStudyOS.App.Services;
using System;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class MasterPasswordCryptoTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrip_Works()
    {
        const string plainText = "backup-payload-123";
        const string password = "SenhaForte#2026";

        var blob = MasterPasswordCrypto.Encrypt(plainText, password);
        var decrypted = MasterPasswordCrypto.Decrypt(blob, password);

        Assert.Equal(plainText, decrypted);
        Assert.True(MasterPasswordCrypto.IsEncryptedBackupBlob(blob));
    }

    [Fact]
    public void Encrypt_WithWeakPassword_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MasterPasswordCrypto.Encrypt("abc", "123"));
        Assert.Contains("pelo menos 8 caracteres", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decrypt_WithWrongPassword_Throws()
    {
        const string password = "SenhaForte#2026";
        var blob = MasterPasswordCrypto.Encrypt("secret", password);

        var ex = Assert.Throws<InvalidOperationException>(() => MasterPasswordCrypto.Decrypt(blob, "SenhaErrada#2026"));
        Assert.Contains("Senha mestra inválida", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

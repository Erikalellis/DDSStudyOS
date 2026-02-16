using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DDSStudyOS.App.Services;

public static class MasterPasswordCrypto
{
    // Formato v2:
    // DDSOSV2 | kdf_iter(int32) | salt(16) | nonce(12) | tag(16) | ciphertext(n)
    //
    // Formato legado (compatível para leitura):
    // DDSHUB1 | salt(16) | nonce(12) | tag(16) | ciphertext(n)
    private static readonly byte[] MagicV2 = Encoding.ASCII.GetBytes("DDSOSV2");
    private static readonly byte[] LegacyMagic = Encoding.ASCII.GetBytes("DDSHUB1");

    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private const int LegacyIterations = 200_000;
    private const int DefaultIterations = 350_000;
    private const int MinIterations = 100_000;
    private const int MaxIterations = 2_000_000;

    private enum BlobFormat
    {
        V2,
        Legacy
    }

    public static byte[] Encrypt(string plainText, string password)
    {
        ValidatePasswordForEncryption(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var pt = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
        var ct = new byte[pt.Length];
        var tag = new byte[TagSize];
        var key = DeriveKey(password, salt, DefaultIterations);

        try
        {
            using (var aes = new AesGcm(key, TagSize))
            {
                aes.Encrypt(nonce, pt, ct, tag);
            }

            var totalSize = MagicV2.Length + sizeof(int) + SaltSize + NonceSize + TagSize + ct.Length;
            var blob = new byte[totalSize];
            var offset = 0;

            MagicV2.CopyTo(blob, offset);
            offset += MagicV2.Length;

            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(offset, sizeof(int)), DefaultIterations);
            offset += sizeof(int);

            salt.CopyTo(blob, offset);
            offset += SaltSize;

            nonce.CopyTo(blob, offset);
            offset += NonceSize;

            tag.CopyTo(blob, offset);
            offset += TagSize;

            ct.CopyTo(blob, offset);
            return blob;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(pt);
        }
    }

    public static string Decrypt(byte[] blob, string password)
    {
        if (blob is null || blob.Length == 0)
            throw new InvalidOperationException("Arquivo de backup vazio.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Informe a senha mestra para abrir o backup criptografado.");

        var format = DetectFormat(blob);
        var offset = format == BlobFormat.V2 ? MagicV2.Length : LegacyMagic.Length;
        var iterations = LegacyIterations;

        if (format == BlobFormat.V2)
        {
            EnsureMinimumLength(blob, offset + sizeof(int));
            iterations = BinaryPrimitives.ReadInt32LittleEndian(blob.AsSpan(offset, sizeof(int)));
            if (iterations < MinIterations || iterations > MaxIterations)
                throw new InvalidOperationException("Arquivo de backup inválido (KDF).");
            offset += sizeof(int);
        }

        EnsureMinimumLength(blob, offset + SaltSize + NonceSize + TagSize + 1);

        var salt = blob.AsSpan(offset, SaltSize).ToArray();
        offset += SaltSize;

        var nonce = blob.AsSpan(offset, NonceSize).ToArray();
        offset += NonceSize;

        var tag = blob.AsSpan(offset, TagSize).ToArray();
        offset += TagSize;

        var ct = blob.AsSpan(offset).ToArray();
        var pt = new byte[ct.Length];
        var key = DeriveKey(password, salt, iterations);

        try
        {
            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, ct, tag, pt);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("Senha mestra inválida ou backup corrompido.");
            }

            return Encoding.UTF8.GetString(pt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(pt);
        }
    }

    public static bool IsEncryptedBackupBlob(ReadOnlySpan<byte> blob)
        => StartsWith(blob, MagicV2) || StartsWith(blob, LegacyMagic);

    private static BlobFormat DetectFormat(ReadOnlySpan<byte> blob)
    {
        if (StartsWith(blob, MagicV2)) return BlobFormat.V2;
        if (StartsWith(blob, LegacyMagic)) return BlobFormat.Legacy;
        throw new InvalidOperationException("Arquivo não é um backup criptografado do DDS StudyOS.");
    }

    private static void ValidatePasswordForEncryption(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Senha mestra obrigatória para exportação criptografada.");
        if (password.Length < 8)
            throw new InvalidOperationException("Use uma senha mestra com pelo menos 8 caracteres.");
    }

    private static void EnsureMinimumLength(ReadOnlySpan<byte> blob, int minLength)
    {
        if (blob.Length < minLength)
            throw new InvalidOperationException("Arquivo de backup inválido.");
    }

    private static bool StartsWith(ReadOnlySpan<byte> source, ReadOnlySpan<byte> prefix)
        => source.Length >= prefix.Length && source.Slice(0, prefix.Length).SequenceEqual(prefix);

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return kdf.GetBytes(KeySize);
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Security;

/// <summary>
/// AES-256-GCM at-rest protection for credential columns. Key comes from
/// Security:SecretProtectionKey (base64, 32 bytes); when absent it is derived
/// (SHA-256) from JwtSettings:SecretKey so local/dev works out of the box.
/// Payload layout: base64( nonce(12) | tag(16) | ciphertext ).
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmSecretProtector(IConfiguration configuration)
    {
        var configured = configuration["Security:SecretProtectionKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var raw = Convert.FromBase64String(configured);
            if (raw.Length != 32)
                throw new InvalidOperationException("Security:SecretProtectionKey phải là base64 của 32 bytes.");
            _key = raw;
        }
        else
        {
            var seed = configuration["JwtSettings:SecretKey"]
                       ?? throw new InvalidOperationException(
                           "Thiếu Security:SecretProtectionKey và JwtSettings:SecretKey để bảo vệ credential.");
            _key = SHA256.HashData(Encoding.UTF8.GetBytes("PEMS_SECRET_PROTECTOR::" + seed));
        }
    }

    public string Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plainBytes, cipher, tag);

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, payload, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string ciphertext)
    {
        var payload = Convert.FromBase64String(ciphertext);
        if (payload.Length < NonceSize + TagSize)
            throw new InvalidOperationException("Ciphertext không hợp lệ.");

        var nonce = payload.AsSpan(0, NonceSize).ToArray();
        var tag = payload.AsSpan(NonceSize, TagSize).ToArray();
        var cipher = payload.AsSpan(NonceSize + TagSize).ToArray();
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}

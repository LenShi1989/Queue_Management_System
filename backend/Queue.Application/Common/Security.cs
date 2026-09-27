using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Queue.Application.Abstractions;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Exceptions;

namespace Queue.Application.Common;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "QueueManagement";

    public string Audience { get; set; } = "QueueWeb";

    public string Secret { get; set; } = string.Empty;

    public int ExpireMinutes { get; set; } = 480;

    public int RefreshExpireDays { get; set; } = 7;
}

public class QrOptions
{
    public const string SectionName = "Qr";

    /// <summary>QR Token 簽章密鑰（§24 QR Token 防偽）。</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeHours { get; set; } = 72;

    /// <summary>手機查詢前端網址，用於組出 QR Url。</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";
}

public class QueueOptions
{
    public const string SectionName = "Queue";

    /// <summary>全站預設平均服務分鐘數（§18 第一階段公式）。</summary>
    public double DefaultAverageServiceMinutes { get; set; } = 5;

    /// <summary>是否啟用老化（§17.2）。</summary>
    public bool AgingEnabled { get; set; }

    /// <summary>等待多久分鐘 +1 有效優先權。</summary>
    public int AgingFactorMinutes { get; set; } = 10;

    /// <summary>同一票據最大再叫次數，超過自動視為過號。</summary>
    public int MaxRecallCount { get; set; } = 2;
}

/// <summary>
/// 密碼雜湊：PBKDF2-SHA256，210,000 iterations（OWASP 建議），格式 iterations.salt.hash
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 210_000;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return string.Create(CultureInfo.InvariantCulture, $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}");
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

/// <summary>
/// QR Code Token（§24）：不暴露資料庫 ID，具備有效期限，HMAC-SHA256 防偽
/// 內容格式：base64url(ticketId|expUnix) + "." + base64url(hmac)
/// 資料庫僅儲存 HMAC，避免明文外流。
/// </summary>
public class QrTokenService
{
    private readonly IClock _clock;
    private readonly QrOptions _options;

    public QrTokenService(IClock clock, IOptions<QrOptions> options)
    {
        _clock = clock;
        _options = options.Value;
    }

    public string Create(long ticketId, out string hash)
    {
        var exp = _clock.UtcNow.AddHours(_options.TokenLifetimeHours <= 0 ? 72 : _options.TokenLifetimeHours).ToUnixTimeSeconds();
        var payload = $"{ticketId}|{exp}";
        var encoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signature = SignatureBytes(encoded);
        hash = Convert.ToHexString(SignatureBytes(encoded));
        return $"{encoded}.{Base64UrlEncode(signature)}";
    }

    public string CreateUrl(string token) => $"{_options.PublicBaseUrl.TrimEnd('/')}/q/{token}";

    public bool TryValidate(string token, out long ticketId)
    {
        ticketId = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] signature;
        byte[] payloadBytes;
        try
        {
            signature = FromBase64Url(parts[1]);
            payloadBytes = FromBase64Url(parts[0]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = SignatureBytes(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(signature, expected))
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var segments = payload.Split('|');
        if (segments.Length != 2
            || !long.TryParse(segments[0], CultureInfo.InvariantCulture, out ticketId)
            || !long.TryParse(segments[1], CultureInfo.InvariantCulture, out var exp))
        {
            ticketId = 0;
            return false;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(exp) < _clock.UtcNow)
        {
            ticketId = 0;
            return false;
        }

        return true;
    }

    private byte[] SignatureBytes(string encoded)
    {
        var key = string.IsNullOrWhiteSpace(_options.SigningKey) ? "QueueManagement-Default-Qr-Key" : _options.SigningKey;
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(encoded));
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }
}

public static class OptionGuard
{
    public static void EnsureJwtSecret(IOptions<JwtOptions> jwt)
    {
        var secret = jwt.Value.Secret;
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Secret 未設定或長度不足 32 字元，無法啟動（§40 Jwt__Secret）。");
        }
    }
}

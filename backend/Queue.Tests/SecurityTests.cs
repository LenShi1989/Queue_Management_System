using Microsoft.Extensions.Options;
using Queue.Application.Common;
using Queue.Domain.Common;

namespace Queue.Tests;

/// <summary>
/// 驗證 §21 密碼處理（PBKDF2-SHA256）與 §24 QR Token 防偽。
/// </summary>
public class SecurityTests
{
    // ---------- PasswordHasher ----------

    [Fact]
    public void Hash_相同密碼每次結果不同_因隨機salt()
    {
        var a = PasswordHasher.Hash("a12345678", 1000);
        var b = PasswordHasher.Hash("a12345678", 1000);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Hash_格式應為iterations_salt_hash()
    {
        var stored = PasswordHasher.Hash("secret123", 1000);
        var parts = stored.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.Equal(1000, int.Parse(parts[0]));
        Assert.NotEmpty(Convert.FromBase64String(parts[1]));
        Assert.NotEmpty(Convert.FromBase64String(parts[2]));
    }

    [Fact]
    public void Verify_正確密碼應回傳true()
        => Assert.True(PasswordHasher.Verify("a12345678", PasswordHasher.Hash("a12345678", 1000)));

    [Theory]
    [InlineData("wrongpass")]
    [InlineData("A12345678")]  // 大小寫敏感
    [InlineData("a12345678 ")] // 尾端空白
    public void Verify_錯誤密碼應回傳false(string wrong)
        => Assert.False(PasswordHasher.Verify(wrong, PasswordHasher.Hash("a12345678", 1000)));

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("1.notbase64!")]
    [InlineData("abc.def.ghi")]
    [InlineData("0.aaaa.bbbb")] // iterations 必須 > 0
    public void Verify_格式非法的雜湊應回傳false而非例外(string stored)
        => Assert.False(PasswordHasher.Verify("a12345678", stored));

    [Fact]
    public void Verify_空密碼或空雜湊應回傳false()
    {
        Assert.False(PasswordHasher.Verify("", PasswordHasher.Hash("a12345678", 1000)));
        Assert.False(PasswordHasher.Verify("a12345678", ""));
    }

    // ---------- QrTokenService ----------

    private static QrTokenService CreateQrService(DateTimeOffset now, string key = "unit-test-qr-signing-key", int lifetimeHours = 72)
        => new(new FixedClock(now), Options.Create(new QrOptions
        {
            SigningKey = key,
            TokenLifetimeHours = lifetimeHours,
            PublicBaseUrl = "https://queue.example.com"
        }));

    [Fact]
    public void QrToken_建立後可通過驗證並還原ticketId()
    {
        var svc = CreateQrService(DateTimeOffset.UnixEpoch);
        var token = svc.Create(4242, out _);

        Assert.True(svc.TryValidate(token, out var ticketId));
        Assert.Equal(4242, ticketId);
    }

    [Fact]
    public void QrToken_不應洩漏ticketId明文()
    {
        var svc = CreateQrService(DateTimeOffset.UnixEpoch);
        var token = svc.Create(987654, out _);
        Assert.DoesNotContain("987654", token);
    }

    [Fact]
    public void CreateUrl_應產生公開查詢網址()
    {
        var svc = CreateQrService(DateTimeOffset.UnixEpoch);
        var token = svc.Create(1, out _);
        Assert.Equal($"https://queue.example.com/q/{token}", svc.CreateUrl(token));
    }

    [Fact]
    public void TryValidate_過期Token應失效()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var svc = CreateQrService(now, lifetimeHours: 1);
        var token = svc.Create(555, out _);

        // 推進 2 小時後
        var later = CreateQrService(now.AddHours(2), lifetimeHours: 1);
        Assert.False(later.TryValidate(token, out var id));
        Assert.Equal(0, id);
    }

    [Fact]
    public void TryValidate_竄改payload應被簽章擋下()
    {
        var a = CreateQrService(DateTimeOffset.UnixEpoch, "key-a");
        var b = CreateQrService(DateTimeOffset.UnixEpoch, "key-a");

        var tokenA = a.Create(100, out _);
        var tokenB = b.Create(999, out _);

        // 取 A 的簽章 + B 的內容 → 驗證必須失敗
        var forged = tokenB.Split('.')[0] + "." + tokenA.Split('.')[1];
        Assert.False(a.TryValidate(forged, out _));
    }

    [Fact]
    public void TryValidate_不同SigningKey產生的Token互不相容()
    {
        var signer = CreateQrService(DateTimeOffset.UnixEpoch, "secret-one");
        var verifier = CreateQrService(DateTimeOffset.UnixEpoch, "secret-two");

        var token = signer.Create(77, out _);
        Assert.False(verifier.TryValidate(token, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-dot-separator")]
    [InlineData("a.b.c")]
    [InlineData("!!!.!!!")]
    public void TryValidate_格式非法的Token應回傳false(string token)
    {
        var svc = CreateQrService(DateTimeOffset.UnixEpoch);
        Assert.False(svc.TryValidate(token, out var id));
        Assert.Equal(0, id);
    }

    [Fact]
    public void Create_產生的hash應可重現簽章()
    {
        var svc = CreateQrService(DateTimeOffset.UnixEpoch);
        svc.Create(31337, out var hash);
        Assert.Equal(64, hash.Length); // SHA256 → 32 bytes → 64 hex chars
    }

    // ---------- OptionGuard ----------

    [Fact]
    public void OptionGuard_空Secret應阻擋啟動()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            OptionGuard.EnsureJwtSecret(Options.Create(new JwtOptions { Secret = "" })));
        Assert.Contains("Jwt", ex.Message);
    }

    [Fact]
    public void OptionGuard_過短Secret應阻擋啟動()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OptionGuard.EnsureJwtSecret(Options.Create(new JwtOptions { Secret = "short" })));
    }

    [Fact]
    public void OptionGuard_長度足夠的Secret應通過()
        => OptionGuard.EnsureJwtSecret(Options.Create(
            new JwtOptions { Secret = new string('x', 32) }));

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
        public DateTimeOffset Now => now.ToLocalTime();
        public DateOnly Today => DateOnly.FromDateTime(now.DateTime);
    }
}

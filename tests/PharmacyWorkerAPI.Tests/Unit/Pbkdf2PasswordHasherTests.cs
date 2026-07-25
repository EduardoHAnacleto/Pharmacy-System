using PharmacyWorkerAPI.Services;
using Xunit;

namespace PharmacyWorkerAPI.Tests.Unit;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ProducesTheSelfDescribingFormat()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        var parts = hash.Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 600_000);
    }

    [Fact]
    public void Hash_NeverContainsThePassword()
    {
        const string password = "correct horse battery staple";

        Assert.DoesNotContain(password, _hasher.Hash(password), StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_IsSaltedSoIdenticalPasswordsDiffer()
    {
        const string password = "same password";

        Assert.NotEqual(_hasher.Hash(password), _hasher.Hash(password));
    }

    [Fact]
    public void Verify_AcceptsTheOriginalPassword()
    {
        const string password = "sen#a com acentuação e símbolos ✓";

        Assert.True(_hasher.Verify(password, _hasher.Hash(password)));
    }

    [Fact]
    public void Verify_RejectsAWrongPassword()
    {
        var hash = _hasher.Hash("right");

        Assert.False(_hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Verify_RejectsAPasswordDifferingOnlyInCase()
    {
        var hash = _hasher.Hash("Password");

        Assert.False(_hasher.Verify("password", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256$notanumber$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$600000$!!!notbase64!!!$aGFzaA==")]
    [InlineData("bcrypt$600000$c2FsdA==$aGFzaA==")]
    public void Verify_RejectsMalformedStoredHashesWithoutThrowing(string stored)
    {
        // The login path verifies against whatever the database holds, so a
        // corrupt or foreign-format digest must return false rather than throw a
        // 500 that distinguishes it from a wrong password.
        Assert.False(_hasher.Verify("anything", stored));
    }

    [Fact]
    public void Verify_RejectsAnEmptyPassword()
    {
        var hash = _hasher.Hash("real password");

        Assert.False(_hasher.Verify("", hash));
    }

    [Fact]
    public void Verify_HonoursTheIterationCountRecordedInTheHash()
    {
        // A digest written with a lower work factor must keep verifying after the
        // default is raised; that is the whole point of storing the count.
        var hash = _hasher.Hash("password");
        var parts = hash.Split('$');
        var rewritten = string.Join('$', parts[0], "1000", parts[2], parts[3]);

        // Same salt, fewer iterations: the derived key differs, so this is false —
        // but it must fail as a mismatch, not as an exception.
        Assert.False(_hasher.Verify("password", rewritten));
    }
}

using System.Security.Claims;

[TestFixture]
public class AuthFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static ClaimsPrincipal Signed(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Bearer"));

    private static ClaimsPrincipal SignedInAgo(TimeSpan ago) =>
        Signed(("sub", "user_1"), ("auth_time", (Now - ago).ToUnixTimeSeconds().ToString()));

    [TestCase(0)]
    [TestCase(60)]
    [TestCase(299)]
    [TestCase(300)]
    public void SignedInWithinFiveMinutes_IsFresh(int secondsAgo)
    {
        Assert.That(AuthFreshness.IsFresh(SignedInAgo(TimeSpan.FromSeconds(secondsAgo)), Now), Is.True);
    }

    [TestCase(301)]
    [TestCase(3600)]
    [TestCase(2 * 24 * 3600)]   // a days-old session that is still being refreshed
    public void SignedInLongerAgo_IsStale(int secondsAgo)
    {
        Assert.That(AuthFreshness.IsFresh(SignedInAgo(TimeSpan.FromSeconds(secondsAgo)), Now), Is.False);
    }

    [Test]
    public void MissingAuthTime_IsStale()
    {
        Assert.That(AuthFreshness.IsFresh(Signed(("sub", "user_1")), Now), Is.False);
    }

    [TestCase("")]
    [TestCase("not-a-number")]
    [TestCase("2026-10-14T12:00:00Z")]
    public void UnreadableAuthTime_IsStale(string value)
    {
        Assert.That(AuthFreshness.IsFresh(Signed(("auth_time", value)), Now), Is.False);
    }

    [Test]
    public void Unauthenticated_IsStale()
    {
        Assert.That(AuthFreshness.IsFresh(new ClaimsPrincipal(new ClaimsIdentity()), Now), Is.False);
    }
}

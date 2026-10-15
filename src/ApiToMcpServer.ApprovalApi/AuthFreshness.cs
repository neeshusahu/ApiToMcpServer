using System.Security.Claims;

public static class AuthFreshness
{
    // Same window as approval-ui's RequireAuth (MAX_AUTH_AGE = 300)
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    public static bool IsFresh(ClaimsPrincipal user, DateTimeOffset now)
    {
        // Missing or unreadable claim counts as stale: fail closed
        if (!long.TryParse(user.FindFirst("auth_time")?.Value, out var authTimeUnix))
            return false;

        var age = now - DateTimeOffset.FromUnixTimeSeconds(authTimeUnix);
        return age <= MaxAge;
    }
}

using TenderScope.Domain.Entities;
using Xunit;

namespace TenderScope.Domain.Tests;

public sealed class AppUserSecurityTests
{
    private static AppUser CreateUser() => AppUser.Create("user@example.com", "Test User", "hash");

    [Fact]
    public void Five_failed_logins_lock_the_account()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 5; index++) user.RegisterFailedLogin(now);
        Assert.True(user.IsLocked(now));
        Assert.Equal(5, user.FailedLoginCount);
        Assert.NotNull(user.LockedUntil);
    }

    [Fact]
    public void Successful_login_clears_lockout_state()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 5; index++) user.RegisterFailedLogin(now);
        user.MarkLogin(now.AddMinutes(1));
        Assert.False(user.IsLocked(now.AddMinutes(1)));
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void Password_change_clears_failed_attempts()
    {
        var user = CreateUser();
        user.RegisterFailedLogin(DateTimeOffset.UtcNow);
        user.ChangePassword("new-hash");
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
        Assert.Equal("new-hash", user.PasswordHash);
    }
}

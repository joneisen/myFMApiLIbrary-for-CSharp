using FileMakerDataApi.Models;
using FileMakerDataApi.Tests.Helpers;

namespace FileMakerDataApi.Tests;

/// <summary>
/// Tests for token-management methods that don't require HTTP calls.
/// </summary>
public class DataApiTokenTests
{
    private static DataApi MakeApi()
    {
        var handler = new MockHttpMessageHandler();
        return DataApiFactory.WithoutToken(handler);
    }

    // -----------------------------------------------------------------------
    // GetApiToken
    // -----------------------------------------------------------------------

    [Fact]
    public void GetApiToken_Initially_ReturnsNull()
    {
        using var api = MakeApi();
        Assert.Null(api.GetApiToken());
    }

    [Fact]
    public void GetApiToken_AfterSet_ReturnsToken()
    {
        using var api = MakeApi();
        api.SetApiToken("tok123");
        Assert.Equal("tok123", api.GetApiToken());
    }

    // -----------------------------------------------------------------------
    // SetApiToken
    // -----------------------------------------------------------------------

    [Fact]
    public void SetApiToken_ReturnsTrue()
    {
        using var api = MakeApi();
        Assert.True(api.SetApiToken("abc"));
    }

    [Fact]
    public void SetApiToken_WithoutDate_SetsDateToUtcNow()
    {
        using var api = MakeApi();
        var before = DateTime.UtcNow;
        api.SetApiToken("abc");
        var after  = DateTime.UtcNow;

        var date = api.GetApiTokenDate();
        Assert.NotNull(date);
        Assert.InRange(date!.Value, before, after);
    }

    [Fact]
    public void SetApiToken_WithCustomDate_UsesProvidedDate()
    {
        using var api = MakeApi();
        var customDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        api.SetApiToken("abc", customDate);
        Assert.Equal(customDate, api.GetApiTokenDate());
    }

    // -----------------------------------------------------------------------
    // GetApiTokenDate
    // -----------------------------------------------------------------------

    [Fact]
    public void GetApiTokenDate_Initially_ReturnsNull()
    {
        using var api = MakeApi();
        Assert.Null(api.GetApiTokenDate());
    }

    // -----------------------------------------------------------------------
    // SetApiTokenDate
    // -----------------------------------------------------------------------

    [Fact]
    public void SetApiTokenDate_UpdatesTimestampToNow()
    {
        using var api = MakeApi();
        api.SetApiToken("abc", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var before = DateTime.UtcNow;
        api.SetApiTokenDate();
        var after = DateTime.UtcNow;

        var date = api.GetApiTokenDate();
        Assert.NotNull(date);
        Assert.InRange(date!.Value, before, after);
    }

    [Fact]
    public void SetApiTokenDate_ReturnsTrue()
    {
        using var api = MakeApi();
        api.SetApiToken("abc");
        Assert.True(api.SetApiTokenDate());
    }

    // -----------------------------------------------------------------------
    // IsApiTokenExpired
    // -----------------------------------------------------------------------

    [Fact]
    public void IsApiTokenExpired_WithNoToken_ReturnsTrue()
    {
        using var api = MakeApi();
        Assert.True(api.IsApiTokenExpired());
    }

    [Fact]
    public void IsApiTokenExpired_WithFreshToken_ReturnsFalse()
    {
        using var api = MakeApi();
        api.SetApiToken("fresh");
        Assert.False(api.IsApiTokenExpired());
    }

    [Fact]
    public void IsApiTokenExpired_WithOldToken_ReturnsTrue()
    {
        using var api = MakeApi();
        // Set token date to 15 minutes ago (beyond the 14-minute lifetime)
        var oldDate = DateTime.UtcNow.AddMinutes(-15);
        api.SetApiToken("old-token", oldDate);
        Assert.True(api.IsApiTokenExpired());
    }

    [Fact]
    public void IsApiTokenExpired_WithTokenJustUnderLimit_ReturnsFalse()
    {
        using var api = MakeApi();
        // 13 minutes old - within the 14-minute window
        var recentDate = DateTime.UtcNow.AddMinutes(-13);
        api.SetApiToken("recent", recentDate);
        Assert.False(api.IsApiTokenExpired());
    }

    // -----------------------------------------------------------------------
    // SetDapiVersion
    // -----------------------------------------------------------------------

    [Fact]
    public void SetDapiVersion_DoesNotThrow()
    {
        using var api = MakeApi();
        api.SetDapiVersion(DapiVersion.V2);
        api.SetDapiVersion(DapiVersion.VLatest);
        api.SetDapiVersion(DapiVersion.V1);
    }

    // -----------------------------------------------------------------------
    // RefreshTokenAsync – no stored credentials
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_WithNoCredentials_ReturnsFalse()
    {
        using var api = MakeApi();
        var result = await api.RefreshTokenAsync();
        Assert.False(result);
    }
}

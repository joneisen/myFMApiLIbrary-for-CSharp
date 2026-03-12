namespace FileMakerDataApi.Tests.Helpers;

/// <summary>
/// Creates <see cref="DataApi"/> instances backed by a <see cref="MockHttpMessageHandler"/>
/// for unit testing without network calls.
/// </summary>
public static class DataApiFactory
{
    private const string BaseUrl  = "https://fms.example.com/fmi/data";
    private const string Database = "TestDB";

    /// <summary>
    /// Returns a DataApi wired to the given handler.
    /// The returned instance already has a valid (non-expired) token set so
    /// <c>EnsureTokenAsync</c> won't attempt an automatic login.
    /// </summary>
    public static DataApi WithToken(MockHttpMessageHandler handler, string token = "test-token")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl + "/") };
        var api  = new DataApi(Database, http);
        api.SetApiToken(token);
        return api;
    }

    /// <summary>
    /// Returns a DataApi without a pre-set token. Useful for testing auth flows
    /// where the first HTTP call should be a login.
    /// </summary>
    public static DataApi WithoutToken(MockHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl + "/") };
        return new DataApi(Database, http);
    }
}

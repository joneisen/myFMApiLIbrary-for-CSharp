using FileMakerDataApi.Tests.Helpers;

namespace FileMakerDataApi.Tests;

public class DataApiMetadataTests
{
    // -----------------------------------------------------------------------
    // GetProductInfoAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetProductInfoAsync_SendsGet_ToProductInfoEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ProductInfo());
        using var api = DataApiFactory.WithoutToken(handler);

        await api.GetProductInfoAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("productInfo", req.UriStr);
    }

    [Fact]
    public async Task GetProductInfoAsync_DoesNotRequireAuth()
    {
        // No token set - should not throw and should not try to login
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ProductInfo());
        using var api = DataApiFactory.WithoutToken(handler);

        var result = await api.GetProductInfoAsync();

        // One request only (no auto-login)
        Assert.Single(handler.Requests);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetProductInfoAsync_ReturnsResponseObject()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ProductInfo());
        using var api = DataApiFactory.WithoutToken(handler);

        var result = await api.GetProductInfoAsync();

        Assert.NotNull(result);
        Assert.NotNull(result!["productInfo"]);
    }

    [Fact]
    public async Task GetProductInfoAsync_UsesVersionPrefixedPath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ProductInfo());
        using var api = DataApiFactory.WithoutToken(handler);

        await api.GetProductInfoAsync();

        // Should be a server-scoped path: v1/productInfo (no database segment)
        Assert.Contains("v1/productInfo", handler.Requests[0].UriStr);
        Assert.DoesNotContain("databases", handler.Requests[0].UriStr);
    }

    // -----------------------------------------------------------------------
    // GetDatabaseNamesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDatabaseNamesAsync_SendsGet_ToDatabasesEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("databases", """[{"name":"TestDB"}]"""));
        using var api = DataApiFactory.WithToken(handler);

        await api.GetDatabaseNamesAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("databases", req.UriStr);
    }

    [Fact]
    public async Task GetDatabaseNamesAsync_ReturnsResponseObject()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("databases", """[{"name":"TestDB"}]"""));
        using var api = DataApiFactory.WithToken(handler);

        var result = await api.GetDatabaseNamesAsync();

        Assert.NotNull(result);
    }

    // -----------------------------------------------------------------------
    // GetLayoutNamesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetLayoutNamesAsync_SendsGet_ToLayoutsEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("layouts", """[{"name":"Contacts"}]"""));
        using var api = DataApiFactory.WithToken(handler);

        await api.GetLayoutNamesAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("layouts", req.UriStr);
    }

    [Fact]
    public async Task GetLayoutNamesAsync_UsesDatabaseScopedPath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("layouts", "[]"));
        using var api = DataApiFactory.WithToken(handler);

        await api.GetLayoutNamesAsync();

        // Should include the database name in the path
        Assert.Contains("databases/TestDB/layouts", handler.Requests[0].UriStr);
    }

    // -----------------------------------------------------------------------
    // GetScriptNamesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScriptNamesAsync_SendsGet_ToScriptsEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("scripts", "[]"));
        using var api = DataApiFactory.WithToken(handler);

        await api.GetScriptNamesAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("scripts", req.UriStr);
    }

    [Fact]
    public async Task GetScriptNamesAsync_ReturnsResponseObject()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("scripts", """[{"name":"SendEmail"}]"""));
        using var api = DataApiFactory.WithToken(handler);

        var result = await api.GetScriptNamesAsync();

        Assert.NotNull(result);
    }

    // -----------------------------------------------------------------------
    // GetLayoutMetadataAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetLayoutMetadataAsync_WithoutRecordId_UsesMetadataPath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("fieldMetaData", "[]"));
        using var api = DataApiFactory.WithToken(handler);

        await api.GetLayoutMetadataAsync("Contacts");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("/layouts/Contacts/metadata", req.UriStr);
    }

    [Fact]
    public async Task GetLayoutMetadataAsync_WithRecordId_UsesRecordPath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.SingleRecord());
        using var api = DataApiFactory.WithToken(handler);

        await api.GetLayoutMetadataAsync("Contacts", "42");

        Assert.Contains("/layouts/Contacts/records/42", handler.Requests[0].UriStr);
        Assert.DoesNotContain("metadata", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task GetLayoutMetadataAsync_ReturnsResponseObject()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Metadata("fieldMetaData", "[]"));
        using var api = DataApiFactory.WithToken(handler);

        var result = await api.GetLayoutMetadataAsync("Layout");

        Assert.NotNull(result);
    }
}

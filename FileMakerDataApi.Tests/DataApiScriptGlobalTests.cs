using System.Text.Json.Nodes;
using FileMakerDataApi.Models;
using FileMakerDataApi.Tests.Helpers;

namespace FileMakerDataApi.Tests;

public class DataApiScriptGlobalTests
{
    // -----------------------------------------------------------------------
    // ExecuteScriptAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteScriptAsync_SendsGet_ToCorrectEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ScriptResult("done"));
        using var api = DataApiFactory.WithToken(handler);

        await api.ExecuteScriptAsync("Contacts", "SendEmail");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("/layouts/Contacts/script/SendEmail", req.UriStr);
    }

    [Fact]
    public async Task ExecuteScriptAsync_ReturnsScriptResult()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ScriptResult("hello world"));
        using var api = DataApiFactory.WithToken(handler);

        var result = await api.ExecuteScriptAsync("Layout", "MyScript");

        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task ExecuteScriptAsync_ReturnsNull_WhenNoResult()
    {
        var handler = new MockHttpMessageHandler();
        // scriptResult is absent or empty
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        var result = await api.ExecuteScriptAsync("Layout", "NoReturnScript");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteScriptAsync_AppendsScriptParam_AsQueryParam()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ScriptResult("ok"));
        using var api = DataApiFactory.WithToken(handler);

        await api.ExecuteScriptAsync("Layout", "MyScript", scriptParam: "arg1");

        Assert.Contains("script.param=arg1", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task ExecuteScriptAsync_DoesNotAppendParam_WhenNullOrEmpty()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ScriptResult("ok"));
        using var api = DataApiFactory.WithToken(handler);

        await api.ExecuteScriptAsync("Layout", "NoParamScript", scriptParam: null);

        Assert.DoesNotContain("script.param", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task ExecuteScriptAsync_UrlEncodesScriptName()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ScriptResult("ok"));
        using var api = DataApiFactory.WithToken(handler);

        await api.ExecuteScriptAsync("Layout", "Send Email");

        Assert.Contains("Send%20Email", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task ExecuteScriptAsync_Throws_WhenScriptReturnsNonZeroError()
    {
        var handler = new MockHttpMessageHandler();
        // Script executed but reported an error
        handler.EnqueueJson(
            """{"response":{"scriptResult":"","scriptError":"5"},"messages":[{"code":"0","message":"OK"}]}""");
        using var api = DataApiFactory.WithToken(handler);

        var ex = await Assert.ThrowsAsync<FileMakerException>(
            () => api.ExecuteScriptAsync("Layout", "BuggyScript"));
        Assert.Equal(5, ex.FileMakerErrorCode);
    }

    [Fact]
    public async Task ExecuteScriptAsync_DoesNotThrow_WhenScriptErrorIsZero()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.ScriptResult("result", "0"));
        using var api = DataApiFactory.WithToken(handler);

        // Should not throw
        var result = await api.ExecuteScriptAsync("Layout", "GoodScript");
        Assert.Equal("result", result);
    }

    // -----------------------------------------------------------------------
    // SetGlobalFieldsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetGlobalFieldsAsync_SendsPatch_ToGlobalsEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        await api.SetGlobalFieldsAsync(new() { ["Globals::UserName"] = "Alice" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, req.Method);
        Assert.Contains("/globals", req.UriStr);
    }

    [Fact]
    public async Task SetGlobalFieldsAsync_SendsGlobalFieldsInBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        await api.SetGlobalFieldsAsync(new()
        {
            ["Globals::UserName"] = "Alice",
            ["Globals::Company"]  = "Acme"
        });

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.NotNull(body!["globalFields"]);
        Assert.Equal("Alice", body["globalFields"]!["Globals::UserName"]?.GetValue<string>());
        Assert.Equal("Acme",  body["globalFields"]!["Globals::Company"]?.GetValue<string>());
    }

    [Fact]
    public async Task SetGlobalFieldsAsync_UsesDatabaseScopedPath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        await api.SetGlobalFieldsAsync(new() { ["G::F"] = "val" });

        Assert.Contains("databases/TestDB/globals", handler.Requests[0].UriStr);
    }

    // -----------------------------------------------------------------------
    // UploadToContainerFromBytesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UploadToContainerFromBytesAsync_SendsPost_ToContainerEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        await api.UploadToContainerFromBytesAsync(
            "Contacts", "42", "Photo", 1,
            new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "photo.png");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/layouts/Contacts/records/42/containers/Photo/1", req.UriStr);
    }

    [Fact]
    public async Task UploadToContainerFromBytesAsync_UsesMultipartFormData()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        await api.UploadToContainerFromBytesAsync(
            "Layout", "1", "File", 1,
            new byte[] { 1, 2, 3 }, "test.bin");

        var req = handler.Requests[0];
        var contentType = req.Message.Content?.Headers.ContentType?.MediaType;
        Assert.Equal("multipart/form-data", contentType);
    }
}

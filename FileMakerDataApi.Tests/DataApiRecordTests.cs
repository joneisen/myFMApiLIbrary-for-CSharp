using System.Text.Json.Nodes;
using FileMakerDataApi.Models;
using FileMakerDataApi.Tests.Helpers;

namespace FileMakerDataApi.Tests;

public class DataApiRecordTests
{
    // -----------------------------------------------------------------------
    // CreateRecordAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateRecordAsync_SendsPost_ToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("42"));
        using var api = DataApiFactory.WithToken(handler);

        await api.CreateRecordAsync("Contacts", new() { ["Name"] = "Alice" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/layouts/Contacts/records", req.UriStr);
    }

    [Fact]
    public async Task CreateRecordAsync_SendsFieldData_InBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("42"));
        using var api = DataApiFactory.WithToken(handler);

        await api.CreateRecordAsync("Contacts", new() { ["Name"] = "Alice", ["Age"] = "30" });

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.Equal("Alice", body!["fieldData"]!["Name"]?.GetValue<string>());
        Assert.Equal("30",    body["fieldData"]!["Age"]?.GetValue<string>());
    }

    [Fact]
    public async Task CreateRecordAsync_ReturnsRecordId()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("99"));
        using var api = DataApiFactory.WithToken(handler);

        var id = await api.CreateRecordAsync("Layout", new());
        Assert.Equal("99", id);
    }

    [Fact]
    public async Task CreateRecordAsync_Throws_WhenNoRecordIdInResponse()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok()); // no recordId
        using var api = DataApiFactory.WithToken(handler);

        await Assert.ThrowsAsync<FileMakerException>(
            () => api.CreateRecordAsync("Layout", new()));
    }

    [Fact]
    public async Task CreateRecordAsync_IncludesScript_InBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("1"));
        using var api = DataApiFactory.WithToken(handler);

        var scripts = new[] { new ScriptDefinition { Name = "SendEmail", Param = "user@test.com" } };
        await api.CreateRecordAsync("Layout", new(), scripts);

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.Equal("SendEmail",     body!["script"]?.GetValue<string>());
        Assert.Equal("user@test.com", body["script.param"]?.GetValue<string>());
    }

    [Fact]
    public async Task CreateRecordAsync_IncludesPortalData_InBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("7"));
        using var api = DataApiFactory.WithToken(handler);

        var portalData = new Dictionary<string, object?> { ["LineItems::Qty"] = "3" };
        await api.CreateRecordAsync("Orders", new(), null, portalData);

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.NotNull(body!["portalData"]);
    }

    [Fact]
    public async Task CreateRecordAsync_NullValueField_SerializesAsNull()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("5"));
        using var api = DataApiFactory.WithToken(handler);

        await api.CreateRecordAsync("Layout", new() { ["Notes"] = null });

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.True(body!["fieldData"]!["Notes"] is null or JsonValue);
    }

    // -----------------------------------------------------------------------
    // EditRecordAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EditRecordAsync_SendsPatch_ToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("42", "2"));
        using var api = DataApiFactory.WithToken(handler);

        await api.EditRecordAsync("Contacts", "42", new() { ["Name"] = "Bob" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, req.Method);
        Assert.Contains("/layouts/Contacts/records/42", req.UriStr);
    }

    [Fact]
    public async Task EditRecordAsync_ReturnsModId()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("42", "5"));
        using var api = DataApiFactory.WithToken(handler);

        var modId = await api.EditRecordAsync("Layout", "42", new());
        Assert.Equal("5", modId);
    }

    [Fact]
    public async Task EditRecordAsync_ReturnsRecordId_WhenModIdAbsent()
    {
        var handler = new MockHttpMessageHandler();
        // Response with no modId
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        var result = await api.EditRecordAsync("Layout", "42", new());
        Assert.Equal("42", result);
    }

    [Fact]
    public async Task EditRecordAsync_IncludesModId_InBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("1", "3"));
        using var api = DataApiFactory.WithToken(handler);

        await api.EditRecordAsync("Layout", "1", new(), lastModificationId: "2");

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.Equal("2", body!["modId"]?.GetValue<string>());
    }

    [Fact]
    public async Task EditRecordAsync_DoesNotIncludeModId_WhenNotProvided()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("1", "1"));
        using var api = DataApiFactory.WithToken(handler);

        await api.EditRecordAsync("Layout", "1", new());

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.Null(body!["modId"]);
    }

    // -----------------------------------------------------------------------
    // DuplicateRecordAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DuplicateRecordAsync_SendsPost_ToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("100"));
        using var api = DataApiFactory.WithToken(handler);

        await api.DuplicateRecordAsync("Contacts", "42");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/layouts/Contacts/records/42", req.UriStr);
    }

    [Fact]
    public async Task DuplicateRecordAsync_ReturnsNewRecordId()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("100"));
        using var api = DataApiFactory.WithToken(handler);

        var id = await api.DuplicateRecordAsync("Contacts", "42");
        Assert.Equal("100", id);
    }

    [Fact]
    public async Task DuplicateRecordAsync_Throws_WhenNoRecordIdInResponse()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        await Assert.ThrowsAsync<FileMakerException>(
            () => api.DuplicateRecordAsync("Layout", "1"));
    }

    // -----------------------------------------------------------------------
    // DeleteRecordAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteRecordAsync_SendsDelete_ToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        await api.DeleteRecordAsync("Contacts", "42");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.Contains("/layouts/Contacts/records/42", req.UriStr);
    }

    [Fact]
    public async Task DeleteRecordAsync_AppendsScript_AsQueryParam()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        var scripts = new[] { new ScriptDefinition { Name = "Cleanup", Type = ScriptType.PostRequest } };
        await api.DeleteRecordAsync("Layout", "5", scripts);

        Assert.Contains("script=Cleanup", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task DeleteRecordAsync_AppendsPrerequestScript_AsQueryParam()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Ok());
        using var api = DataApiFactory.WithToken(handler);

        var scripts = new[] { new ScriptDefinition { Name = "Pre", Type = ScriptType.PreRequest } };
        await api.DeleteRecordAsync("Layout", "5", scripts);

        Assert.Contains("script.prerequest=Pre", handler.Requests[0].UriStr);
    }

    // -----------------------------------------------------------------------
    // URL encoding
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateRecordAsync_UrlEncodesLayoutName()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("1"));
        using var api = DataApiFactory.WithToken(handler);

        await api.CreateRecordAsync("My Layout", new());

        Assert.Contains("My%20Layout", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task EditRecordAsync_UrlEncodesRecordId()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.RecordId("1", "2"));
        using var api = DataApiFactory.WithToken(handler);

        // Record IDs are numeric in FileMaker but the API accepts strings
        await api.EditRecordAsync("Layout", "42", new());

        Assert.Contains("/records/42", handler.Requests[0].UriStr);
    }
}

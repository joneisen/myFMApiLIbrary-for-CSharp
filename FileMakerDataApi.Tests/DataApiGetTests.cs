using System.Text.Json.Nodes;
using FileMakerDataApi.Models;
using FileMakerDataApi.Tests.Helpers;

namespace FileMakerDataApi.Tests;

public class DataApiGetTests
{
    // -----------------------------------------------------------------------
    // GetRecordAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetRecordAsync_SendsGet_ToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.SingleRecord());
        using var api = DataApiFactory.WithToken(handler);

        await api.GetRecordAsync("Contacts", "42");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("/layouts/Contacts/records/42", req.UriStr);
    }

    [Fact]
    public async Task GetRecordAsync_ReturnsFirstRecordFromData()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.SingleRecord("""{"Name":"Alice","Age":"30"}"""));
        using var api = DataApiFactory.WithToken(handler);

        var record = await api.GetRecordAsync("Contacts", "42");

        Assert.NotNull(record);
        Assert.Equal("Alice", record["fieldData"]!["Name"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetRecordAsync_Throws_FileMakerException_WhenRecordNotFound()
    {
        var handler = new MockHttpMessageHandler();
        // FileMaker returns error 401 (no records) when the record ID doesn't exist
        handler.Enqueue(FmJson.ErrorResponse(401, 404));
        using var api = DataApiFactory.WithToken(handler);

        var ex = await Assert.ThrowsAsync<FileMakerException>(
            () => api.GetRecordAsync("Layout", "99"));
        Assert.Equal(401, ex.FileMakerErrorCode);
    }

    [Fact]
    public async Task GetRecordAsync_AppendsPortal_AsQueryParam()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.SingleRecord());
        using var api = DataApiFactory.WithToken(handler);

        var portal = new PortalOptions { Name = "LineItems", Limit = 5, Offset = 1 };
        await api.GetRecordAsync("Orders", "1", portalOptions: portal);

        var url = handler.Requests[0].UriStr;
        Assert.Contains("portal=", url);
        Assert.Contains("_limit.LineItems=5",  url);
        Assert.Contains("_offset.LineItems=1", url);
    }

    [Fact]
    public async Task GetRecordAsync_AppendsResponseLayout()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.SingleRecord());
        using var api = DataApiFactory.WithToken(handler);

        await api.GetRecordAsync("Layout", "1", responseLayout: "AltLayout");

        Assert.Contains("layout.response=AltLayout", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task GetRecordAsync_AppendsDateFormat()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.SingleRecord());
        using var api = DataApiFactory.WithToken(handler);

        await api.GetRecordAsync("Layout", "1", dateFormat: DateFormat.Iso8601);

        Assert.Contains("dateformats=", handler.Requests[0].UriStr);
    }

    // -----------------------------------------------------------------------
    // GetRecordsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetRecordsAsync_SendsGet_ToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        await api.GetRecordsAsync("Contacts");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Contains("/layouts/Contacts/records", req.UriStr);
        Assert.DoesNotContain("_find", req.UriStr);
    }

    [Fact]
    public async Task GetRecordsAsync_ReturnsRecordsArray()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records(new { Name = "Alice" }, new { Name = "Bob" }));
        using var api = DataApiFactory.WithToken(handler);

        var records = await api.GetRecordsAsync("Contacts");

        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task GetRecordsAsync_AppendsOffset_AndLimit()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        await api.GetRecordsAsync("Layout", offset: 10, limit: 25);

        var url = handler.Requests[0].UriStr;
        Assert.Contains("_offset=10", url);
        Assert.Contains("_limit=25",  url);
    }

    [Fact]
    public async Task GetRecordsAsync_AppendsSort_AsQueryParam()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        var sort = new[]
        {
            new SortField { FieldName = "LastName",  SortOrder = SortOrder.Ascend  },
            new SortField { FieldName = "FirstName", SortOrder = SortOrder.Descend }
        };
        await api.GetRecordsAsync("Layout", sort: sort);

        var url = handler.Requests[0].UriStr;
        Assert.Contains("_sort=", url);
        Assert.Contains("LastName", url);
        Assert.Contains("descend",  url);
    }

    [Fact]
    public async Task GetRecordsAsync_NoQueryParams_WhenNoneProvided()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        await api.GetRecordsAsync("Layout");

        Assert.DoesNotContain("?", handler.Requests[0].UriStr);
    }

    [Fact]
    public async Task GetRecordsAsync_AppendsPortals()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        var portals = new[] { new PortalOptions { Name = "LineItems", Limit = 10 } };
        await api.GetRecordsAsync("Orders", portals: portals);

        var url = handler.Requests[0].UriStr;
        Assert.Contains("portal=",            url);
        Assert.Contains("_limit.LineItems=10", url);
    }

    // -----------------------------------------------------------------------
    // FindRecordsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FindRecordsAsync_SendsPost_ToFindEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records(new { Name = "Alice" }));
        using var api = DataApiFactory.WithToken(handler);

        var query = new[] { new FindRequest { Fields = new() { ["Name"] = "Alice" } } };
        await api.FindRecordsAsync("Contacts", query);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/_find", req.UriStr);
    }

    [Fact]
    public async Task FindRecordsAsync_SendsQueryInBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records(new { Name = "Alice" }));
        using var api = DataApiFactory.WithToken(handler);

        var query = new[] { new FindRequest { Fields = new() { ["Name"] = "Alice" } } };
        await api.FindRecordsAsync("Contacts", query);

        var body  = JsonNode.Parse(handler.Requests[0].Body!);
        var first = body!["query"]!.AsArray()[0];
        Assert.Equal("Alice", first!["Name"]?.GetValue<string>());
        Assert.Equal("false", first["omit"]?.GetValue<string>());
    }

    [Fact]
    public async Task FindRecordsAsync_SendsOmitFlag_InQuery()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        var query = new[] { new FindRequest { Fields = new() { ["Status"] = "Inactive" }, Omit = true } };
        await api.FindRecordsAsync("Contacts", query);

        var body  = JsonNode.Parse(handler.Requests[0].Body!);
        var first = body!["query"]!.AsArray()[0];
        Assert.Equal("true", first!["omit"]?.GetValue<string>());
    }

    [Fact]
    public async Task FindRecordsAsync_ReturnsEmptyArray_OnFileMaker401()
    {
        var handler = new MockHttpMessageHandler();
        // FM error 401 = no records found
        handler.Enqueue(FmJson.ErrorResponse(401, 200));
        using var api = DataApiFactory.WithToken(handler);

        var query   = new[] { new FindRequest { Fields = new() { ["Name"] = "NoSuchPerson" } } };
        var results = await api.FindRecordsAsync("Contacts", query);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindRecordsAsync_Throws_OnOtherFileMakerErrors()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(FmJson.ErrorResponse(500, 500)); // unexpected server error
        using var api = DataApiFactory.WithToken(handler);

        var query = new[] { new FindRequest { Fields = new() { ["Name"] = "X" } } };
        await Assert.ThrowsAsync<FileMakerException>(
            () => api.FindRecordsAsync("Layout", query));
    }

    [Fact]
    public async Task FindRecordsAsync_ReturnsRecords_OnSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records(
            new { Name = "Alice" },
            new { Name = "Bob" }));
        using var api = DataApiFactory.WithToken(handler);

        var query   = new[] { new FindRequest { Fields = new() { ["Department"] = "Engineering" } } };
        var results = await api.FindRecordsAsync("Employees", query);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task FindRecordsAsync_AppliesSort_InBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        var sort  = new[] { new SortField { FieldName = "Name", SortOrder = SortOrder.Descend } };
        var query = new[] { new FindRequest { Fields = new() { ["Status"] = "Active" } } };
        await api.FindRecordsAsync("Layout", query, sort: sort);

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        var sortArray = body!["sort"]!.AsArray();
        Assert.Single(sortArray);
        Assert.Equal("Name",    sortArray[0]!["fieldName"]?.GetValue<string>());
        Assert.Equal("descend", sortArray[0]!["sortOrder"]?.GetValue<string>());
    }

    [Fact]
    public async Task FindRecordsAsync_AppliesOffsetAndLimit_InBody()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        var query = new[] { new FindRequest { Fields = new() { ["X"] = "Y" } } };
        await api.FindRecordsAsync("Layout", query, offset: 5, limit: 10);

        var body = JsonNode.Parse(handler.Requests[0].Body!);
        Assert.Equal(5,  body!["offset"]?.GetValue<int>());
        Assert.Equal(10, body["limit"]?.GetValue<int>());
    }

    [Fact]
    public async Task FindRecordsAsync_MultipleOrBlocks_AllSentInQuery()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(FmJson.Records());
        using var api = DataApiFactory.WithToken(handler);

        var query = new[]
        {
            new FindRequest { Fields = new() { ["Status"] = "Active" } },
            new FindRequest { Fields = new() { ["Status"] = "Pending" } }
        };
        await api.FindRecordsAsync("Layout", query);

        var body      = JsonNode.Parse(handler.Requests[0].Body!);
        var queryArr  = body!["query"]!.AsArray();
        Assert.Equal(2, queryArr.Count);
    }
}

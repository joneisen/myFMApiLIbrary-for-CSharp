using System.Text.Json.Nodes;
using FileMakerDataApi.Models;

namespace FileMakerDataApi.Tests.Models;

public class FileMakerResponseTests
{
    private static FileMakerResponse Parse(string json) =>
        new(JsonNode.Parse(json)!);

    // -----------------------------------------------------------------------
    // MessageCode
    // -----------------------------------------------------------------------

    [Fact]
    public void MessageCode_Zero_OnOkResponse()
    {
        var r = Parse("""{"response":{},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Equal(0, r.MessageCode);
    }

    [Fact]
    public void MessageCode_ParsesNonZeroCode()
    {
        var r = Parse("""{"response":{},"messages":[{"code":"401","message":"No records found"}]}""");
        Assert.Equal(401, r.MessageCode);
    }

    [Fact]
    public void MessageCode_ReturnsZero_WhenMessagesAbsent()
    {
        var r = Parse("""{"response":{}}""");
        Assert.Equal(0, r.MessageCode);
    }

    // -----------------------------------------------------------------------
    // MessageText
    // -----------------------------------------------------------------------

    [Fact]
    public void MessageText_ReturnsText()
    {
        var r = Parse("""{"response":{},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Equal("OK", r.MessageText);
    }

    [Fact]
    public void MessageText_ReturnsEmpty_WhenAbsent()
    {
        var r = Parse("""{"response":{}}""");
        Assert.Equal(string.Empty, r.MessageText);
    }

    // -----------------------------------------------------------------------
    // Records
    // -----------------------------------------------------------------------

    [Fact]
    public void Records_ReturnsDataArray()
    {
        var r = Parse("""{"response":{"data":[{"fieldData":{},"recordId":"1","modId":"0"}]},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Single(r.Records);
    }

    [Fact]
    public void Records_ReturnsEmptyArray_WhenNoData()
    {
        var r = Parse("""{"response":{},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Empty(r.Records);
    }

    [Fact]
    public void Records_ReturnsEmptyArray_WhenResponseAbsent()
    {
        var r = Parse("""{"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Empty(r.Records);
    }

    // -----------------------------------------------------------------------
    // RawResponse
    // -----------------------------------------------------------------------

    [Fact]
    public void RawResponse_ReturnsResponseObject()
    {
        var r = Parse("""{"response":{"token":"abc123"},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.NotNull(r.RawResponse);
        Assert.Equal("abc123", r.RawResponse!["token"]?.GetValue<string>());
    }

    [Fact]
    public void RawResponse_ReturnsNull_WhenResponseAbsent()
    {
        var r = Parse("""{"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Null(r.RawResponse);
    }

    // -----------------------------------------------------------------------
    // Root
    // -----------------------------------------------------------------------

    [Fact]
    public void Root_ReturnsFullJsonDocument()
    {
        var r = Parse("""{"response":{},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.NotNull(r.Root);
        Assert.NotNull(r.Root["response"]);
        Assert.NotNull(r.Root["messages"]);
    }

    // -----------------------------------------------------------------------
    // ScriptResult / ScriptError
    // -----------------------------------------------------------------------

    [Fact]
    public void ScriptResult_ReturnsValue()
    {
        var r = Parse("""{"response":{"scriptResult":"hello"},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Equal("hello", r.ScriptResult);
    }

    [Fact]
    public void ScriptResult_ReturnsEmpty_WhenAbsent()
    {
        var r = Parse("""{"response":{},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Equal(string.Empty, r.ScriptResult);
    }

    [Fact]
    public void ScriptError_ReturnsValue()
    {
        var r = Parse("""{"response":{"scriptError":"5"},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Equal("5", r.ScriptError);
    }

    [Fact]
    public void ScriptError_ReturnsEmpty_WhenAbsent()
    {
        var r = Parse("""{"response":{},"messages":[{"code":"0","message":"OK"}]}""");
        Assert.Equal(string.Empty, r.ScriptError);
    }
}

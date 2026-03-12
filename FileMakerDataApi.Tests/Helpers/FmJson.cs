using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FileMakerDataApi.Tests.Helpers;

/// <summary>
/// Builds JSON strings and <see cref="HttpResponseMessage"/> objects that mimic
/// real FileMaker Data API responses.
/// </summary>
public static class FmJson
{
    // -----------------------------------------------------------------------
    // Raw JSON builders
    // -----------------------------------------------------------------------

    public static string Ok() =>
        """{"response":{},"messages":[{"code":"0","message":"OK"}]}""";

    public static string Login(string token) =>
        $$"""{"response":{"token":"{{token}}"},"messages":[{"code":"0","message":"OK"}]}""";

    public static string RecordId(string recordId, string modId = "1") =>
        $$"""{"response":{"recordId":"{{recordId}}","modId":"{{modId}}"},"messages":[{"code":"0","message":"OK"}]}""";

    public static string Records(params object[] fieldMaps)
    {
        var records = fieldMaps.Select(fm =>
            $$"""{"fieldData":{{JsonSerializer.Serialize(fm)}},"recordId":"1","modId":"0"}""");
        var data = string.Join(",", records);
        return $$"""{"response":{"data":[{{data}}]},"messages":[{"code":"0","message":"OK"}]}""";
    }

    public static string SingleRecord(string fieldDataJson = "{}") =>
        $$"""{"response":{"data":[{"fieldData":{{fieldDataJson}},"recordId":"42","modId":"0"}]},"messages":[{"code":"0","message":"OK"}]}""";

    public static string Error(int code, string message = "Error") =>
        $$"""{"response":{},"messages":[{"code":"{{code}}","message":"{{message}}"}]}""";

    public static string ScriptResult(string result, string error = "0") =>
        $$"""{"response":{"scriptResult":"{{result}}","scriptError":"{{error}}"},"messages":[{"code":"0","message":"OK"}]}""";

    public static string Metadata(string key = "layouts", string valueJson = "[]") =>
        $$"""{"response":{"{{key}}":{{valueJson}}},"messages":[{"code":"0","message":"OK"}]}""";

    public static string ProductInfo() =>
        """{"response":{"productInfo":{"name":"FileMaker","buildDate":"01/01/2024","version":"21.0.0.200","dateFormat":"MM/dd/yyyy","timeFormat":"HH:mm:ss","timeStampFormat":"MM/dd/yyyy HH:mm:ss"}},"messages":[{"code":"0","message":"OK"}]}""";

    // -----------------------------------------------------------------------
    // HttpResponseMessage helpers
    // -----------------------------------------------------------------------

    public static HttpResponseMessage OkResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    public static HttpResponseMessage ErrorResponse(int fmCode, int httpStatus = 500) =>
        new((HttpStatusCode)httpStatus)
        {
            Content = new StringContent(Error(fmCode), Encoding.UTF8, "application/json")
        };

    // -----------------------------------------------------------------------
    // JSON parsing helpers for assertions
    // -----------------------------------------------------------------------

    public static JsonNode? Parse(string? json) =>
        json is null ? null : JsonNode.Parse(json);

    public static string? GetString(string? json, string path)
    {
        if (json is null) return null;
        var node = JsonNode.Parse(json);
        foreach (var part in path.Split('.'))
            node = node?[part];
        return node?.GetValue<string>();
    }
}

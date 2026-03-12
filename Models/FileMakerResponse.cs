using System.Text.Json.Nodes;

namespace FileMakerDataApi.Models;

/// <summary>
/// Wraps the parsed JSON body returned by the FileMaker Data API.
/// Mirrors the PHP Response class, exposing the response envelope sections
/// as typed accessors.
/// </summary>
public class FileMakerResponse
{
    private readonly JsonNode _root;

    public FileMakerResponse(JsonNode root)
    {
        _root = root;
    }

    /// <summary>The full parsed response body.</summary>
    public JsonNode Root => _root;

    /// <summary>The "response" envelope from the API body.</summary>
    public JsonNode? Response => _root["response"];

    /// <summary>
    /// The data array from the response envelope.
    /// Returns an empty array when no data is present (e.g. login responses).
    /// </summary>
    public JsonArray Records => Response?["data"]?.AsArray() ?? new JsonArray();

    /// <summary>The raw response envelope as a JsonObject.</summary>
    public JsonObject? RawResponse => Response?.AsObject();

    /// <summary>The result returned by a script execution, or empty string if none.</summary>
    public string ScriptResult => Response?["scriptResult"]?.GetValue<string>() ?? string.Empty;

    /// <summary>The error code returned by a script execution, or empty string if none.</summary>
    public string ScriptError => Response?["scriptError"]?.GetValue<string>() ?? string.Empty;

    /// <summary>The FileMaker error code from the messages array, or zero on success.</summary>
    public int MessageCode
    {
        get
        {
            var code = _root["messages"]?[0]?["code"]?.GetValue<string>();
            return int.TryParse(code, out var n) ? n : 0;
        }
    }

    /// <summary>The message text from the messages array.</summary>
    public string MessageText => _root["messages"]?[0]?["message"]?.GetValue<string>() ?? string.Empty;
}

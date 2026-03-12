namespace FileMakerDataApi.Models;

/// <summary>
/// Determines when a FileMaker script runs relative to the Data API request.
/// </summary>
public enum ScriptType
{
    /// <summary>Runs before the request is processed.</summary>
    PreRequest,

    /// <summary>Runs after the request but before any sort is applied.</summary>
    PreSort,

    /// <summary>Runs after the request and sort have completed.</summary>
    PostRequest
}

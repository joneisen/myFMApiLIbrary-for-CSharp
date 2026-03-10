namespace FileMakerDataApi.Models;

/// <summary>
/// Thrown when the FileMaker Data API returns an error, either at the HTTP level
/// or as a non-zero FileMaker error code in the response body.
/// </summary>
public class FileMakerException : Exception
{
    /// <summary>
    /// The FileMaker error code from the response messages array.
    /// Zero means no FileMaker-level error was reported.
    /// Common codes: 401 = no records found, 952 = session token expired.
    /// </summary>
    public int FileMakerErrorCode { get; }

    /// <summary>The HTTP status code from the response.</summary>
    public int HttpStatusCode { get; }

    public FileMakerException(string message, int fileMakerErrorCode = 0, int httpStatusCode = 0)
        : base(message)
    {
        FileMakerErrorCode = fileMakerErrorCode;
        HttpStatusCode = httpStatusCode;
    }
}

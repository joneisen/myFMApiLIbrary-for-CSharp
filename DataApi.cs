using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FileMakerDataApi.Models;

namespace FileMakerDataApi;

/// <summary>
/// C# wrapper for the Claris FileMaker Data API.
/// Supports FileMaker Server 17 through 22 and Data API versions v1, v2, and vLatest.
///
/// Ported from rcconsulting/myFMApiLibrary-for-PHP (BSD-3-Clause).
/// All async methods throw <see cref="FileMakerException"/> on FileMaker-level or HTTP errors.
/// </summary>
public sealed class DataApi : IDataApi, IAsyncDisposable
{
    // FileMaker error codes that have special handling
    private const int FileMakerNoRecords    = 401;
    private const int FileMakerTokenExpired = 952;

    // Token lifetime is 15 minutes on the server side. 14 minutes is used here
    // to avoid sending a request right as the token is about to expire.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(14);

    private readonly HttpClient _http;
    private readonly string _apiDatabase;

    // Guards EnsureTokenAsync so concurrent callers don't race to issue multiple logins
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _apiUser;
    private string? _apiPassword;
    private string? _oAuthRequestId;
    private string? _oAuthIdentifier;

    private string? _apiToken;
    private DateTime? _apiTokenDate;
    private bool _hasToken;

    private DapiVersion _dapiVersion;

    private string VersionString => _dapiVersion switch
    {
        DapiVersion.V2      => "v2",
        DapiVersion.VLatest => "vLatest",
        _                   => "v1"
    };

    /// <summary>
    /// Creates a new DataApi instance.
    /// </summary>
    /// <param name="apiUrl">
    /// Base URL for the FileMaker Data API, e.g. https://server.example.com/fmi/data.
    /// Do not include the version segment - the library appends that automatically.
    /// </param>
    /// <param name="apiDatabase">Database name as it appears on FileMaker Server.</param>
    /// <param name="apiUser">
    /// Optional username. When provided alongside <paramref name="apiPassword"/>, credentials
    /// are stored so the library can log in automatically on the first request and re-authenticate
    /// when a token expires. Call <see cref="LoginAsync"/> explicitly if you prefer to control
    /// when authentication happens.
    /// </param>
    /// <param name="apiPassword">Optional password paired with <paramref name="apiUser"/>.</param>
    /// <param name="sslVerify">
    /// Whether to validate the server's SSL certificate. Should only be set to false in
    /// development environments with self-signed certificates.
    /// </param>
    /// <param name="dapiVersion">Data API version to target. Defaults to V1.</param>
    public DataApi(
        string apiUrl,
        string apiDatabase,
        string? apiUser         = null,
        string? apiPassword     = null,
        bool sslVerify          = true,
        DapiVersion dapiVersion = DapiVersion.V1)
    {
        _apiDatabase = PrepareUrlPart(apiDatabase);
        _dapiVersion = dapiVersion;

        var handler = new HttpClientHandler();
        if (!sslVerify)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        _http = new HttpClient(handler) { BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/") };

        if (apiUser is not null)
        {
            _apiUser     = apiUser;
            _apiPassword = apiPassword;
        }
    }

    /// <summary>
    /// Internal constructor for unit testing. Accepts a pre-configured <see cref="HttpClient"/>
    /// so tests can inject a mock <see cref="HttpMessageHandler"/> without network calls.
    /// </summary>
    internal DataApi(string apiDatabase, HttpClient httpClient, DapiVersion dapiVersion = DapiVersion.V1)
    {
        _apiDatabase = PrepareUrlPart(apiDatabase);
        _dapiVersion = dapiVersion;
        _http        = httpClient;
    }

    // -------------------------------------------------------------------------
    // Authentication
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens a session with FileMaker Server using username and password credentials.
    /// Stores the token and credentials for automatic token refresh on expiry.
    /// </summary>
    public async Task LoginAsync(string apiUsername, string apiPassword, CancellationToken cancellationToken = default)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiUsername}:{apiPassword}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, DbPath("sessions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = EmptyJsonContent();

        var response = await SendAsync(request, requiresAuth: false, cancellationToken);
        var token = response.RawResponse?["token"]?.GetValue<string>()
            ?? throw new FileMakerException("Login response did not contain a token.");

        SetApiToken(token);
        StoreCredentials(apiUsername, apiPassword);
    }

    /// <summary>
    /// Opens a session using OAuth credentials.
    /// </summary>
    public async Task LoginOauthAsync(string oAuthRequestId, string oAuthIdentifier, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DbPath("sessions"));
        request.Headers.Add("X-FM-Data-Login-Type", "oauth");
        request.Headers.Add("X-FM-Data-OAuth-Request-Id", oAuthRequestId);
        request.Headers.Add("X-FM-Data-OAuth-Identifier", oAuthIdentifier);
        request.Content = EmptyJsonContent();

        var response = await SendAsync(request, requiresAuth: false, cancellationToken);
        var token = response.RawResponse?["token"]?.GetValue<string>()
            ?? throw new FileMakerException("OAuth login response did not contain a token.");

        SetApiToken(token);
        StoreOAuth(oAuthRequestId, oAuthIdentifier);
    }

    /// <summary>
    /// Closes the current FileMaker session and clears the stored token.
    /// Tokens remain active on the server for 15 minutes after last use if logout is not called,
    /// which can exhaust the server session limit. Calling this at the end of each script or
    /// long-running process is strongly recommended.
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_apiToken is null) return;

        using var request = new HttpRequestMessage(HttpMethod.Delete, DbPath($"sessions/{_apiToken}"));
        await SendAsync(request, cancellationToken: cancellationToken);

        _apiToken     = null;
        _apiTokenDate = null;
        _hasToken     = false;
    }

    // -------------------------------------------------------------------------
    // Record operations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a new record on the specified layout.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="data">Field name to value mapping. Values are converted to strings.</param>
    /// <param name="scripts">Optional scripts to run with this request.</param>
    /// <param name="portalData">Optional portal row data keyed by portal occurrence name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The recordId of the newly created record.</returns>
    public async Task<string> CreateRecordAsync(
        string layout,
        Dictionary<string, object?> data,
        IEnumerable<ScriptDefinition>? scripts = null,
        Dictionary<string, object?>? portalData = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout = PrepareUrlPart(layout);

        var body = new JsonObject();
        body["fieldData"] = BuildFieldData(data);

        if (portalData is not null)
            body["portalData"] = BuildFieldData(portalData);

        MergeScriptOptions(body, scripts);

        using var request = new HttpRequestMessage(HttpMethod.Post, DbPath($"layouts/{layout}/records"));
        request.Content = JsonContent(body);

        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.RawResponse?["recordId"]?.GetValue<string>()
            ?? throw new FileMakerException("Create record response did not contain a recordId.");
    }

    /// <summary>
    /// Updates fields on an existing record.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="recordId">Target record ID.</param>
    /// <param name="data">Field name to value mapping for the fields being changed.</param>
    /// <param name="lastModificationId">Optional modId for conflict detection.</param>
    /// <param name="portalData">Optional portal row data to update.</param>
    /// <param name="scripts">Optional scripts to run with this request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The modId of the updated record.</returns>
    public async Task<string> EditRecordAsync(
        string layout,
        string recordId,
        Dictionary<string, object?> data,
        string? lastModificationId              = null,
        Dictionary<string, object?>? portalData = null,
        IEnumerable<ScriptDefinition>? scripts  = null,
        CancellationToken cancellationToken     = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout   = PrepareUrlPart(layout);
        recordId = PrepareUrlPart(recordId);

        var body = new JsonObject();
        body["fieldData"] = BuildFieldData(data);

        if (lastModificationId is not null)
            body["modId"] = lastModificationId;

        if (portalData is not null)
            body["portalData"] = BuildFieldData(portalData);

        MergeScriptOptions(body, scripts);

        using var request = new HttpRequestMessage(HttpMethod.Patch, DbPath($"layouts/{layout}/records/{recordId}"));
        request.Content = JsonContent(body);

        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.RawResponse?["modId"]?.GetValue<string>() ?? recordId;
    }

    /// <summary>
    /// Duplicates an existing record.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="recordId">Target record ID.</param>
    /// <param name="scripts">Optional scripts to run with this request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The recordId of the new duplicate record.</returns>
    public async Task<string> DuplicateRecordAsync(
        string layout,
        string recordId,
        IEnumerable<ScriptDefinition>? scripts = null,
        CancellationToken cancellationToken    = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout   = PrepareUrlPart(layout);
        recordId = PrepareUrlPart(recordId);

        var body = new JsonObject();
        MergeScriptOptions(body, scripts);

        using var request = new HttpRequestMessage(HttpMethod.Post, DbPath($"layouts/{layout}/records/{recordId}"));
        request.Content = JsonContent(body);

        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.RawResponse?["recordId"]?.GetValue<string>()
            ?? throw new FileMakerException("Duplicate record response did not contain a recordId.");
    }

    /// <summary>
    /// Deletes a record by its recordId.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="recordId">Target record ID.</param>
    /// <param name="scripts">Optional scripts to run with this request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task DeleteRecordAsync(
        string layout,
        string recordId,
        IEnumerable<ScriptDefinition>? scripts = null,
        CancellationToken cancellationToken    = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout   = PrepareUrlPart(layout);
        recordId = PrepareUrlPart(recordId);

        var url = AppendScriptQueryParams(DbPath($"layouts/{layout}/records/{recordId}"), scripts);

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        await SendAsync(request, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Retrieves a single record by its recordId.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="recordId">Target record ID.</param>
    /// <param name="portalOptions">Optional portal row pagination for a single portal.</param>
    /// <param name="scripts">Optional scripts to run with this request.</param>
    /// <param name="responseLayout">Optional alternate layout to use for the response.</param>
    /// <param name="dateFormat">Optional date format for field values in the response.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The first record node from the response data array.</returns>
    public async Task<JsonNode> GetRecordAsync(
        string layout,
        string recordId,
        PortalOptions? portalOptions           = null,
        IEnumerable<ScriptDefinition>? scripts = null,
        string? responseLayout                 = null,
        DateFormat? dateFormat                 = null,
        CancellationToken cancellationToken    = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout   = PrepareUrlPart(layout);
        recordId = PrepareUrlPart(recordId);

        var query = new Dictionary<string, string>();

        if (portalOptions is not null)
        {
            // The Claris Data API requires portal to be a JSON array even for a single portal.
            // Passing a bare string (portal=PortalName) is incorrect per the official docs.
            query["portal"] = JsonSerializer.Serialize(new[] { portalOptions.Name });
            if (portalOptions.Limit.HasValue)  query[$"_limit.{portalOptions.Name}"]  = portalOptions.Limit.Value.ToString();
            if (portalOptions.Offset.HasValue) query[$"_offset.{portalOptions.Name}"] = portalOptions.Offset.Value.ToString();
        }

        if (responseLayout is not null) query["layout.response"] = responseLayout;
        if (dateFormat.HasValue)        query["dateformats"]      = ((int)dateFormat.Value).ToString();

        AppendScriptQueryDict(query, scripts);

        var url = DbPath($"layouts/{layout}/records/{recordId}");
        if (query.Count > 0) url += "?" + BuildQueryString(query);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await SendAsync(request, cancellationToken: cancellationToken);

        return response.Records[0]
            ?? throw new FileMakerException("Record not found.", FileMakerNoRecords);
    }

    /// <summary>
    /// Retrieves a list of records from the specified layout.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="sort">Optional sort fields applied in order.</param>
    /// <param name="offset">Starting record offset (1-based).</param>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="portals">Optional portal row pagination for one or more portals.</param>
    /// <param name="scripts">Optional scripts to run with this request.</param>
    /// <param name="responseLayout">Optional alternate layout for the response.</param>
    /// <param name="dateFormat">Optional date format for field values.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Array of record nodes.</returns>
    public async Task<JsonArray> GetRecordsAsync(
        string layout,
        IEnumerable<SortField>? sort           = null,
        int? offset                            = null,
        int? limit                             = null,
        IEnumerable<PortalOptions>? portals    = null,
        IEnumerable<ScriptDefinition>? scripts = null,
        string? responseLayout                 = null,
        DateFormat? dateFormat                 = null,
        CancellationToken cancellationToken    = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout = PrepareUrlPart(layout);

        var query = new Dictionary<string, string>();

        if (offset.HasValue)            query["_offset"]         = offset.Value.ToString();
        if (limit.HasValue)             query["_limit"]          = limit.Value.ToString();
        if (responseLayout is not null) query["layout.response"] = responseLayout;
        if (dateFormat.HasValue)        query["dateformats"]     = ((int)dateFormat.Value).ToString();

        if (sort is not null)
        {
            var sortList = sort.Select(s => new
            {
                fieldName = s.FieldName,
                sortOrder = s.SortOrder == SortOrder.Descend ? "descend" : "ascend"
            });
            query["_sort"] = JsonSerializer.Serialize(sortList);
        }

        AppendPortalQueryDict(query, portals);
        AppendScriptQueryDict(query, scripts);

        var url = DbPath($"layouts/{layout}/records");
        if (query.Count > 0) url += "?" + BuildQueryString(query);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.Records;
    }

    /// <summary>
    /// Performs a find request using FileMaker find criteria.
    /// Returns an empty array when no records match (FileMaker error 401) rather than throwing.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="query">
    /// One or more find criterion blocks. Multiple blocks are OR'd together.
    /// Fields within a single block are AND'd together.
    /// Values support FileMaker find operators, e.g. "==Exact", ">100", "10...20".
    /// </param>
    /// <param name="sort">Optional sort applied to the result set.</param>
    /// <param name="offset">Starting record offset (1-based).</param>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="portals">Optional portal row pagination.</param>
    /// <param name="scripts">Optional scripts to run with this request.</param>
    /// <param name="responseLayout">Optional alternate layout for the response.</param>
    /// <param name="dateFormat">Optional date format for field values.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Array of matching record nodes, or an empty array when nothing matches.</returns>
    public async Task<JsonArray> FindRecordsAsync(
        string layout,
        IEnumerable<FindRequest> query,
        IEnumerable<SortField>? sort           = null,
        int? offset                            = null,
        int? limit                             = null,
        IEnumerable<PortalOptions>? portals    = null,
        IEnumerable<ScriptDefinition>? scripts = null,
        string? responseLayout                 = null,
        DateFormat? dateFormat                 = null,
        CancellationToken cancellationToken    = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout = PrepareUrlPart(layout);

        var body = new JsonObject();

        var queryArray = new JsonArray();
        foreach (var req in query)
        {
            var block = new JsonObject();
            foreach (var kv in req.Fields)
                block[kv.Key] = kv.Value;
            block["omit"] = req.Omit ? "true" : "false";
            queryArray.Add(block);
        }
        body["query"] = queryArray;

        if (offset.HasValue)            body["offset"]          = offset.Value;
        if (limit.HasValue)             body["limit"]           = limit.Value;
        if (responseLayout is not null) body["layout.response"] = responseLayout;
        if (dateFormat.HasValue)        body["dateformats"]     = (int)dateFormat.Value;

        if (sort is not null)
        {
            var sortArray = new JsonArray();
            foreach (var s in sort)
                sortArray.Add(new JsonObject
                {
                    ["fieldName"] = s.FieldName,
                    ["sortOrder"] = s.SortOrder == SortOrder.Descend ? "descend" : "ascend"
                });
            body["sort"] = sortArray;
        }

        MergePortalOptions(body, portals);
        MergeScriptOptions(body, scripts);

        using var request = new HttpRequestMessage(HttpMethod.Post, DbPath($"layouts/{layout}/_find"));
        request.Content = JsonContent(body);

        try
        {
            var response = await SendAsync(request, cancellationToken: cancellationToken);
            return response.Records;
        }
        catch (FileMakerException ex) when (ex.FileMakerErrorCode == FileMakerNoRecords)
        {
            // No records found is a normal outcome for a find, not an error
            return new JsonArray();
        }
    }

    // -------------------------------------------------------------------------
    // Container uploads
    // -------------------------------------------------------------------------

    /// <summary>
    /// Uploads a file from disk to a container field.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="recordId">Target record ID.</param>
    /// <param name="containerFieldName">Container field name.</param>
    /// <param name="containerFieldRepetition">Field repetition number (1-based).</param>
    /// <param name="filePath">Absolute path to the file on disk.</param>
    /// <param name="fileName">Optional filename override. Defaults to the file's actual name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task UploadToContainerAsync(
        string layout,
        string recordId,
        string containerFieldName,
        int containerFieldRepetition,
        string filePath,
        string? fileName                    = null,
        CancellationToken cancellationToken = default)
    {
        fileName ??= Path.GetFileName(filePath);
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        await UploadToContainerFromBytesAsync(layout, recordId, containerFieldName, containerFieldRepetition, bytes, fileName, cancellationToken);
    }

    /// <summary>
    /// Uploads raw bytes to a container field without requiring a file on disk.
    /// Useful when file content is already in memory, such as from a web form upload.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="recordId">Target record ID.</param>
    /// <param name="containerFieldName">Container field name.</param>
    /// <param name="containerFieldRepetition">Field repetition number (1-based).</param>
    /// <param name="fileBytes">Raw file content to upload.</param>
    /// <param name="fileName">Filename sent to FileMaker Server, including extension.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task UploadToContainerFromBytesAsync(
        string layout,
        string recordId,
        string containerFieldName,
        int containerFieldRepetition,
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout             = PrepareUrlPart(layout);
        recordId           = PrepareUrlPart(recordId);
        containerFieldName = PrepareUrlPart(containerFieldName);

        using var content = new MultipartFormDataContent();
        var fileContent   = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "upload", fileName);

        var url = DbPath($"layouts/{layout}/records/{recordId}/containers/{containerFieldName}/{containerFieldRepetition}");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;
        await SendAsync(request, cancellationToken: cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Scripts
    // -------------------------------------------------------------------------

    /// <summary>
    /// Executes a named script on a layout without creating, editing, or returning records.
    /// Returns the script result string when the script sets one, otherwise null.
    /// Throws <see cref="FileMakerException"/> when the script itself returns a non-zero error code.
    /// </summary>
    /// <param name="layout">Layout name the script is associated with.</param>
    /// <param name="scriptName">Name of the script to execute.</param>
    /// <param name="scriptParam">Optional parameter passed to the script.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<string?> ExecuteScriptAsync(
        string layout,
        string scriptName,
        string? scriptParam                 = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout     = PrepareUrlPart(layout);
        scriptName = PrepareUrlPart(scriptName);

        var url = DbPath($"layouts/{layout}/script/{scriptName}");
        if (!string.IsNullOrEmpty(scriptParam))
            url += $"?script.param={Uri.EscapeDataString(scriptParam)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await SendAsync(request, cancellationToken: cancellationToken);

        if (!string.IsNullOrEmpty(response.ScriptError) && response.ScriptError != "0")
            throw new FileMakerException(
                $"Script returned error: {response.ScriptError}",
                int.TryParse(response.ScriptError, out var c) ? c : 0);

        return string.IsNullOrEmpty(response.ScriptResult) ? null : response.ScriptResult;
    }

    // -------------------------------------------------------------------------
    // Global fields
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets one or more global field values for the current session.
    /// Global field names must be fully qualified: "TableName::FieldName".
    /// Global fields are scoped to the session, not to a specific layout.
    /// </summary>
    /// <param name="globalFields">Fully qualified field name to value mapping.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task SetGlobalFieldsAsync(
        Dictionary<string, string> globalFields,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);

        var globals = new JsonObject();
        foreach (var kv in globalFields)
            globals[kv.Key] = kv.Value;

        var body = new JsonObject { ["globalFields"] = globals };

        using var request = new HttpRequestMessage(HttpMethod.Patch, DbPath("globals"));
        request.Content = JsonContent(body);
        await SendAsync(request, cancellationToken: cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns product information for the FileMaker Server.
    /// This is a server-level endpoint that does not require authentication.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<JsonNode?> GetProductInfoAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ServerPath("productInfo"));
        var response = await SendAsync(request, requiresAuth: false, cancellationToken);
        return response.RawResponse;
    }

    /// <summary>
    /// Returns the list of databases available on the server.
    /// This is a server-level endpoint but requires a valid session token.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<JsonNode?> GetDatabaseNamesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, ServerPath("databases"));
        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.RawResponse;
    }

    /// <summary>Returns the list of layouts in the current database.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<JsonNode?> GetLayoutNamesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, DbPath("layouts"));
        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.RawResponse;
    }

    /// <summary>Returns the list of scripts in the current database.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<JsonNode?> GetScriptNamesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, DbPath("scripts"));
        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.RawResponse;
    }

    /// <summary>
    /// Returns layout metadata.
    /// When <paramref name="recordId"/> is provided, returns field values for that record.
    /// Without a recordId, returns field and portal metadata for the layout.
    /// </summary>
    /// <param name="layout">Layout name.</param>
    /// <param name="recordId">Optional record ID. When provided, returns field values for that record.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<JsonNode?> GetLayoutMetadataAsync(
        string layout,
        string? recordId                    = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);
        layout = PrepareUrlPart(layout);

        var path = string.IsNullOrEmpty(recordId)
            ? DbPath($"layouts/{layout}/metadata")
            : DbPath($"layouts/{layout}/records/{PrepareUrlPart(recordId!)}");

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var response = await SendAsync(request, cancellationToken: cancellationToken);
        return response.RawResponse;
    }

    // -------------------------------------------------------------------------
    // Token management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the current API session token without checking whether it is still valid.
    /// </summary>
    public string? GetApiToken() => _apiToken;

    /// <summary>
    /// Manually sets a session token, typically when reusing a token obtained elsewhere.
    /// Setting <paramref name="tokenDate"/> to a past timestamp lets the expiry check
    /// reflect the token's actual remaining lifetime.
    /// </summary>
    public bool SetApiToken(string token, DateTime? tokenDate = null)
    {
        _apiToken     = token;
        _apiTokenDate = tokenDate ?? DateTime.UtcNow;
        _hasToken     = true;
        return true;
    }

    /// <summary>Returns the UTC timestamp of the last token use, or null if no token has been set.</summary>
    public DateTime? GetApiTokenDate() => _apiTokenDate;

    /// <summary>
    /// Updates the token last-used timestamp to now.
    /// Called automatically after every successful authenticated request.
    /// </summary>
    public bool SetApiTokenDate()
    {
        _apiTokenDate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Returns true when the locally tracked token age exceeds 14 minutes.
    /// This is a local check only and does not contact FileMaker Server.
    /// For a live check, use <see cref="ValidateTokenWithServerAsync"/>.
    /// </summary>
    public bool IsApiTokenExpired()
    {
        if (_apiTokenDate is null) return true;
        return DateTime.UtcNow - _apiTokenDate.Value > TokenLifetime;
    }

    /// <summary>
    /// Re-authenticates using stored credentials.
    /// Works for both username/password and OAuth sessions.
    /// Returns false when no credentials are stored or the login attempt fails.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_apiUser is not null)
            {
                await LoginAsync(_apiUser, _apiPassword ?? string.Empty, cancellationToken);
                return true;
            }
            if (_oAuthRequestId is not null)
            {
                await LoginOauthAsync(_oAuthRequestId, _oAuthIdentifier ?? string.Empty, cancellationToken);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates the current token by making a live request to FileMaker Server.
    /// Returns false when the token has expired or is otherwise invalid.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<bool> ValidateTokenWithServerAsync(CancellationToken cancellationToken = default)
    {
        if (_apiToken is null) return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ServerPath("validateSession"));
            var response = await SendAsync(request, cancellationToken: cancellationToken);
            return response.MessageCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Changes the Data API version used for subsequent requests.
    /// Can also be set via the constructor parameter.
    /// </summary>
    public void SetDapiVersion(DapiVersion version) => _dapiVersion = version;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    // Database-scoped path: {version}/databases/{db}/{endpoint}
    private string DbPath(string endpoint) =>
        $"{VersionString}/databases/{_apiDatabase}/{endpoint}";

    // Server-scoped path: {version}/{endpoint}
    // Used for productInfo, databases list, and validateSession
    private string ServerPath(string endpoint) =>
        $"{VersionString}/{endpoint}";

    private static string PrepareUrlPart(string value) =>
        Uri.EscapeDataString(value.Trim());

    private void StoreCredentials(string user, string pass)
    {
        _apiUser     = user;
        _apiPassword = pass;
    }

    private void StoreOAuth(string requestId, string identifier)
    {
        _oAuthRequestId  = requestId;
        _oAuthIdentifier = identifier;
    }

    private async Task EnsureTokenAsync(CancellationToken cancellationToken)
    {
        // The semaphore prevents two concurrent callers from both detecting an expired
        // token and each issuing their own login, wasting a session slot and racing on _apiToken.
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!_hasToken)
            {
                if (_apiUser is not null)
                    await LoginAsync(_apiUser, _apiPassword ?? string.Empty, cancellationToken);
                else
                    throw new FileMakerException("Not authenticated. Call LoginAsync() before making Data API requests.");
                return;
            }

            if (IsApiTokenExpired())
            {
                if (!await RefreshTokenAsync(cancellationToken))
                    throw new FileMakerException("Session token has expired and could not be refreshed. Call LoginAsync() again.");
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<FileMakerResponse> SendAsync(
        HttpRequestMessage request,
        bool requiresAuth               = true,
        CancellationToken cancellationToken = default)
    {
        if (requiresAuth && _apiToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new FileMakerException($"HTTP request failed: {ex.Message}");
        }

        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        JsonNode? root;
        try { root = JsonNode.Parse(body); }
        catch { throw new FileMakerException($"Invalid JSON in response (HTTP {(int)httpResponse.StatusCode}): {body}"); }

        var wrapped = new FileMakerResponse(root ?? new JsonObject());

        // Non-zero FileMaker error codes are always surfaced as exceptions.
        // Callers like FindRecordsAsync catch specific codes (e.g. 401) when
        // that code represents a normal outcome rather than a failure.
        if (wrapped.MessageCode != 0)
            throw new FileMakerException(wrapped.MessageText, wrapped.MessageCode, (int)httpResponse.StatusCode);

        if (!httpResponse.IsSuccessStatusCode)
            throw new FileMakerException(
                $"HTTP {(int)httpResponse.StatusCode}: {httpResponse.ReasonPhrase}",
                httpStatusCode: (int)httpResponse.StatusCode);

        // Refresh the token last-used timestamp after every successful authenticated call
        if (_hasToken) SetApiTokenDate();

        return wrapped;
    }

    private static HttpContent JsonContent(JsonObject body) =>
        new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

    private static HttpContent EmptyJsonContent() =>
        new ByteArrayContent("{}"u8.ToArray()) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };

    private static JsonObject BuildFieldData(Dictionary<string, object?> data)
    {
        var obj = new JsonObject();
        foreach (var kv in data)
            obj[kv.Key] = kv.Value is null ? null : JsonValue.Create(kv.Value.ToString());
        return obj;
    }

    // Merges script options into a JSON body object (POST/PATCH requests)
    private static void MergeScriptOptions(JsonObject body, IEnumerable<ScriptDefinition>? scripts)
    {
        if (scripts is null) return;

        foreach (var script in scripts)
        {
            var (nameKey, paramKey) = GetScriptKeys(script.Type);
            body[nameKey] = script.Name;

            // FileMaker Server 17 DAPI returns an error when script.param is present but empty,
            // so blank params are intentionally skipped here.
            if (!string.IsNullOrEmpty(script.Param))
                body[paramKey] = script.Param;
        }
    }

    // Appends script options as query string parameters (GET/DELETE requests)
    private static string AppendScriptQueryParams(string url, IEnumerable<ScriptDefinition>? scripts)
    {
        if (scripts is null) return url;

        var parts = new List<string>();
        foreach (var script in scripts)
        {
            var (nameKey, paramKey) = GetScriptKeys(script.Type);
            parts.Add($"{nameKey}={Uri.EscapeDataString(script.Name)}");
            if (!string.IsNullOrEmpty(script.Param))
                parts.Add($"{paramKey}={Uri.EscapeDataString(script.Param!)}");
        }

        return parts.Count > 0 ? url + "?" + string.Join("&", parts) : url;
    }

    private static void AppendScriptQueryDict(Dictionary<string, string> query, IEnumerable<ScriptDefinition>? scripts)
    {
        if (scripts is null) return;

        foreach (var script in scripts)
        {
            var (nameKey, paramKey) = GetScriptKeys(script.Type);
            query[nameKey] = script.Name;
            if (!string.IsNullOrEmpty(script.Param))
                query[paramKey] = script.Param!;
        }
    }

    private static (string nameKey, string paramKey) GetScriptKeys(ScriptType type) => type switch
    {
        ScriptType.PreRequest => ("script.prerequest", "script.prerequest.param"),
        ScriptType.PreSort    => ("script.presort",    "script.presort.param"),
        _                     => ("script",             "script.param")
    };

    private static void MergePortalOptions(JsonObject body, IEnumerable<PortalOptions>? portals)
    {
        if (portals is null) return;
        var list = portals.ToList();
        if (list.Count == 0) return;

        var names = new JsonArray();
        foreach (var p in list)
        {
            names.Add(p.Name);
            if (p.Offset.HasValue) body[$"offset.{p.Name}"] = p.Offset.Value;
            if (p.Limit.HasValue)  body[$"limit.{p.Name}"]  = p.Limit.Value;
        }
        body["portal"] = names;
    }

    private static void AppendPortalQueryDict(Dictionary<string, string> query, IEnumerable<PortalOptions>? portals)
    {
        if (portals is null) return;
        var list = portals.ToList();
        if (list.Count == 0) return;

        query["portal"] = JsonSerializer.Serialize(list.Select(p => p.Name));
        foreach (var p in list)
        {
            if (p.Offset.HasValue) query[$"_offset.{p.Name}"] = p.Offset.Value.ToString();
            if (p.Limit.HasValue)  query[$"_limit.{p.Name}"]  = p.Limit.Value.ToString();
        }
    }

    private static string BuildQueryString(Dictionary<string, string> query) =>
        string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    // -------------------------------------------------------------------------
    // IDisposable / IAsyncDisposable
    // -------------------------------------------------------------------------

    /// <summary>
    /// Logs out of the current FileMaker session before releasing resources.
    /// Prefer <c>await using</c> over <c>using</c> so the logout call is awaited properly.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try { await LogoutAsync(); } catch { /* best-effort logout on dispose */ }
        _http.Dispose();
        _tokenLock.Dispose();
    }

    /// <summary>
    /// Synchronous dispose. Does not log out - use <c>await using</c> / <see cref="DisposeAsync"/>
    /// when a clean session teardown matters.
    /// </summary>
    public void Dispose()
    {
        _http.Dispose();
        _tokenLock.Dispose();
    }
}

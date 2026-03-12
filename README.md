# FileMaker 17/18/19/20/21/22 Data API wrapper - myFMApiLibrary for C#

A C# port of [rcconsulting/myFMApiLibrary-for-PHP](https://github.com/rcconsulting/myFMApiLibrary-for-PHP),
providing a clean async wrapper around the Claris FileMaker Data API.

Targets .NET 8 with zero external dependencies.

---

## Requirements

- .NET 8 or later
- A FileMaker Server with the Data API enabled

## Installation

Add the project reference or copy the source files directly into your solution.
NuGet package support is planned for a future release.

```xml
<ProjectReference Include="..\FileMakerDataApi\FileMakerDataApi.csproj" />
```

---

## Prepare your FileMaker solution

1. Enable the FileMaker Data API in the FileMaker Server Admin Console.
2. Create a user account in your FileMaker database with a custom privilege set
   that has the `fmrest` extended privilege enabled.
3. Define record and layout access for that account as appropriate.

---

## Usage

### Login

Login with credentials:

```csharp
var dataApi = new DataApi("https://your-server.example.com/fmi/data", "MyDatabase");
await dataApi.LoginAsync("filemaker_user", "filemaker_password");
```

One-line login with all options:

```csharp
// Arguments: API URL, Database, Username, Password, SSL verification, DapiVersion
var dataApi = new DataApi(
    "https://your-server.example.com/fmi/data",
    "MyDatabase",
    apiUser:     "filemaker_user",
    apiPassword: "filemaker_password",
    sslVerify:   true,
    dapiVersion: DapiVersion.V1
);
// The first Data API call will log in automatically when credentials are supplied.
```

Login with OAuth:

```csharp
// Note: OAuth logins have not been tested. Pull requests are welcome.
var dataApi = new DataApi("https://your-server.example.com/fmi/data", "MyDatabase");
await dataApi.LoginOauthAsync("oAuthRequestId", "oAuthIdentifier");
```

### Logout

```csharp
// Strongly recommended at the end of every script or long-running process.
// Data API tokens remain active on the server for 15 minutes after last use.
// Not calling logout holds a session slot open for the remainder of that window.
// Logout does not dispose the DataApi object - it only clears the session token.
await dataApi.LogoutAsync();
```

---

### Create record

```csharp
var fieldData = new Dictionary<string, object?>
{
    ["FirstName"]         = "John",
    ["LastName"]          = "Doe",
    ["email"]             = "johndoe@acme.inc",
    ["RepeatingField(1)"] = "Test"
};

var scripts = new[]
{
    new ScriptDefinition("ScriptBeforeRequest",      "johndoe@acme.inc", ScriptType.PreRequest),
    new ScriptDefinition("ScriptBeforeSort",         "johndoe@acme.inc", ScriptType.PreSort),
    new ScriptDefinition("ScriptAfterRequestAfterSort", "johndoe@acme.inc", ScriptType.PostRequest)
};

var portalData = new Dictionary<string, object?>
{
    ["lunchDate"]  = "04/17/2013",
    ["lunchPlace"] = "Acme Inc."
};

string recordId = await dataApi.CreateRecordAsync("layout name", fieldData, scripts, portalData);
```

### Delete record

```csharp
await dataApi.DeleteRecordAsync("layout name", recordId, scripts);
```

### Edit record

```csharp
// Returns the new modId of the updated record
string modId = await dataApi.EditRecordAsync(
    "layout name",
    recordId,
    fieldData,
    lastModificationId: null,
    portalData:         null,
    scripts:            scripts
);
```

### Get record

```csharp
var portal = new PortalOptions("Portal1", limit: 10, offset: null);

JsonNode record = await dataApi.GetRecordAsync(
    "layout name",
    recordId,
    portalOptions:  portal,
    scripts:        null,
    responseLayout: null,
    dateFormat:     null
);
```

### Get records

```csharp
var sort = new[]
{
    new SortField("FirstName", SortOrder.Ascend),
    new SortField("City",      SortOrder.Descend)
};

JsonArray records = await dataApi.GetRecordsAsync(
    "layout name",
    sort:    sort,
    offset:  1,
    limit:   50
);
```

### Find records

```csharp
// Multiple FindRequest objects are OR'd together.
// Fields within a single FindRequest are AND'd.
// Values support FileMaker find operators: ==Exact, >100, 10...20, etc.
var query = new[]
{
    new FindRequest(
        fields: new Dictionary<string, string>
        {
            ["FirstName"] = "==John",
            ["LastName"]  = "==Doe"
        },
        omit: false
    )
};

// Returns an empty array instead of throwing when no records match
JsonArray results = await dataApi.FindRecordsAsync(
    "layout name",
    query,
    sort:   sort,
    offset: 1,
    limit:  50
);
```

### Execute script (blind fire)

```csharp
// Returns the script result string, or null if the script sets no result.
// Throws FileMakerException if the script returns a non-zero error code.
string? result = await dataApi.ExecuteScriptAsync("layout name", "ScriptName", "optional param");
```

### Set global fields

```csharp
// Field names must be fully qualified: "TableName::FieldName"
// Global fields are session-scoped - no layout parameter is needed.
var globals = new Dictionary<string, string>
{
    ["TableName::FieldName1"] = "value1",
    ["TableName::FieldName2"] = "value2"
};

await dataApi.SetGlobalFieldsAsync(globals);
```

### Upload file to container

From a file path on disk:

```csharp
await dataApi.UploadToContainerAsync(
    "layout name",
    recordId,
    containerFieldName:       "Picture",
    containerFieldRepetition: 1,
    filePath:                 "/path/to/photo.jpg"
);
```

From bytes already in memory (e.g. from an ASP.NET form upload):

```csharp
byte[] fileBytes = await formFile.GetAllBytesAsync();

await dataApi.UploadToContainerFromBytesAsync(
    "layout name",
    recordId,
    containerFieldName:       "Picture",
    containerFieldRepetition: 1,
    fileBytes:                fileBytes,
    fileName:                 "photo.jpg"
);
```

---

## Disposal and session cleanup

`DataApi` implements both `IDisposable` and `IAsyncDisposable`. Use `await using` so the
logout call is awaited before the `HttpClient` is released:

```csharp
await using var dataApi = new DataApi("https://your-server.example.com/fmi/data", "MyDatabase");
await dataApi.LoginAsync("user", "password");

// ... do work ...

// DisposeAsync calls LogoutAsync then disposes the HttpClient
```

If you use `using` (synchronous), the `HttpClient` is disposed but logout is **not** called.
The session will expire naturally on the server after 15 minutes of inactivity.

---

## Cancellation

Every async method accepts an optional `CancellationToken` as its last parameter:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

JsonArray records = await dataApi.GetRecordsAsync(
    "layout name",
    cancellationToken: cts.Token
);
```

---

## Token management

FileMaker Data API session tokens expire 15 minutes after last use. By default, the
library manages tokens automatically. When credentials are supplied at construction
or via `LoginAsync`, the library will log in on the first request and re-authenticate
transparently when the token expires.

For cases where you need direct control over the session token:

```csharp
// Set a token obtained elsewhere.
// Pass tokenDate to let the expiry check reflect the token's actual remaining lifetime.
dataApi.SetApiToken(token);
dataApi.SetApiToken(token, tokenDate: DateTime.UtcNow.AddMinutes(-10));

// Get the current token without checking validity
string? token = dataApi.GetApiToken();

// Local expiry check (does not contact FileMaker Server)
if (dataApi.IsApiTokenExpired())
{
    // token is locally considered expired
}

// Re-authenticate using stored credentials. Only useful after expiry;
// calling this while the token is still valid creates a new unnecessary session.
bool success = await dataApi.RefreshTokenAsync();

// Live check with FileMaker Server
bool valid = await dataApi.ValidateTokenWithServerAsync();
```

---

## Version

```csharp
// Use v1, v2, or vLatest. Defaults to V1.
// v2 and metadata commands will fail on FileMaker Server 17.
dataApi.SetDapiVersion(DapiVersion.V2);
```

---

## Differences from the PHP library

| PHP | C# |
|---|---|
| All methods are synchronous | All methods are `async Task` |
| PHP arrays for script/portal options | Typed `ScriptDefinition`, `PortalOptions`, `SortField` classes |
| `SCRIPT_PREREQUEST`, `SCRIPT_PRESORT`, `SCRIPT_POSTREQUEST` string constants | `ScriptType.PreRequest`, `ScriptType.PreSort`, `ScriptType.PostRequest` enum |
| `DapiVersion::V1`, `DapiVersion::VLATEST` | `DapiVersion.V1`, `DapiVersion.VLatest` |
| `returnResponseObject` constructor option | Raw `JsonNode` returned from all methods |
| cURL / Guzzle HTTP client selection | Single `HttpClient` implementation (no external dependencies) |
| `forceLegacyHTTP` constructor option | Not applicable - `HttpClient` negotiates automatically |

---

## Error handling

All methods throw `FileMakerException` on FileMaker-level or HTTP errors.
`FindRecordsAsync` is the exception: it returns an empty array rather than throwing
when FileMaker returns error 401 (no records found), since that is a normal find outcome.

```csharp
try
{
    string recordId = await dataApi.CreateRecordAsync("Contacts", fieldData);
}
catch (FileMakerException ex)
{
    Console.WriteLine($"FileMaker error {ex.FileMakerErrorCode}: {ex.Message}");
    Console.WriteLine($"HTTP status: {ex.HttpStatusCode}");
}
```

---

## License

BSD 3-Clause License. See [LICENSE](LICENSE) for details.

This library is a C# port of [rcconsulting/myFMApiLibrary-for-PHP](https://github.com/rcconsulting/myFMApiLibrary-for-PHP),
originally developed by [Lesterius](https://www.lesterius.com) and maintained by
[Richard Carlton Consulting](https://rcconsulting.com). All credit to the original authors.

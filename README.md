# NikSBO

A C# SDK for the **SAP Business One Service Layer**, multi-targeted for **.NET 8 / .NET 6 / .NET Standard 2.0** (so it also runs on .NET Framework 4.6.2+ — handy for SAP B1 add-ons). Built so you don't have to fight SAP's OData API by hand: login with auto-renewal, typed CRUD, a fluent query builder (with LINQ), document actions (`Close`, `Cancel`, …), transactional batches, parameterized raw SQL, UDF support, `IAsyncDisposable` (auto-logout via `await using`), a logging hook and `CancellationToken` on every async method.

---

## Requirements

- .NET 8, .NET 6, or any platform compatible with .NET Standard 2.0 (this includes **.NET Framework 4.6.2+**, Mono 5.4+, Unity, Xamarin, etc.)
- Access to a SAP B1 Service Layer (HANA or SQL Server) — typically exposed at `https://<host>:50000/`
- A SAP user with permissions on the target company database

---

## Installation

Install from NuGet:

```bash
dotnet add package NikSBO
```

Or pin a specific version:

```bash
dotnet add package NikSBO --version 0.1.0
```

To work from source instead, clone the repo and open `NikSBO.slnx`, or reference the project directly:

```bash
dotnet add reference ../NikSBO/NikSBO.csproj
```

---

## Configuration

The connection is fully described by `B1Options`:

```csharp
using NikSBO.http;
using NikSBO.models;

var client = new B1Client(new B1Options
{
    ServerUrl = "https://sap.mycompany.local:50000/",
    CompanyDb = "SBODemoUS",
    Username  = "manager",
    Password  = "********"
});
```

To avoid hardcoding credentials, use `appsettings.json`:

```json
{
  "B1": {
    "ServerUrl": "https://sap.mycompany.local:50000/",
    "CompanyDb": "SBODemoUS",
    "Username":  "manager",
    "Password":  "********"
  }
}
```

```csharp
var config = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText("appsettings.json"));
var b1 = config.GetProperty("B1");
var client = new B1Client(new B1Options
{
    ServerUrl = b1.GetProperty("ServerUrl").GetString()!,
    CompanyDb = b1.GetProperty("CompanyDb").GetString()!,
    Username  = b1.GetProperty("Username").GetString()!,
    Password  = b1.GetProperty("Password").GetString()!
});
```

### TLS certificate validation

By default the client validates the Service Layer's TLS certificate against the standard chain of trust. SAP B1 on-prem usually ships a **self-signed certificate**, so you'll get connection errors unless you either:

- Install the SL certificate in the machine's trust store, **or**
- Opt into accepting any certificate via `B1Options.AcceptAnyServerCertificate`:

```csharp
var client = new B1Client(new B1Options
{
    ServerUrl = "https://sap.mycompany.local:50000/",
    CompanyDb = "SBODemoUS",
    Username  = "manager",
    Password  = "********",
    AcceptAnyServerCertificate = true   // self-signed cert on internal network
});
```

> Only enable this flag over a trusted network (LAN, VPN). Over Internet, public WiFi or unsegmented networks it opens the door to MITM attacks: an attacker can present their own certificate, read credentials and tamper with data in transit.

---

## Login / Logout

```csharp
await client.Login();   // stores the B1SESSION cookie
// ... operations ...
await client.Logout();
```

Service Layer sessions expire after 30 minutes. `B1Client` tracks when the login happened and **renews the session automatically**:

- Before every request, if `IsExpired()`, it re-logs in.
- If the SL still answers `401 Unauthorized`, the client re-logs in transparently and retries the request once.

If you ever need to check it yourself:

```csharp
if (client.IsExpired(TimeSpan.FromMinutes(1)))
    await client.Login();

DateTimeOffset? expiresAt = client.ExpiresAt;
```

---

## Logging

The SDK doesn't ship a logger — instead, set `B1Options.LogTrace` to a delegate that receives a pre-formatted string per event. No external dependency, no log levels, no structured logging: just a hook.

```csharp
var client = new B1Client(new B1Options
{
    ServerUrl = "...",
    Username = "...",
    Password = "...",
    LogTrace = msg => Console.WriteLine($"[NikSBO] {msg}")
});
```

What you'll see in the console:

```
[NikSBO] Login -> https://sap:50000/b1s/v1/Login (user: manager, db: SBODemoUS)
[NikSBO] Login OK (234 ms, sesión hasta 2026-05-20 13:42:10Z)
[NikSBO] GET /b1s/v1/BusinessPartners?$filter=... -> 200 OK (87 ms)
[NikSBO] Sesión caducada, re-login automático
[NikSBO] Login -> https://sap:50000/b1s/v1/Login (user: manager, db: SBODemoUS)
[NikSBO] Login OK (198 ms, ...)
[NikSBO] POST /b1s/v1/Orders(123)/Close -> 204 No Content (156 ms)
[NikSBO] Auto-logout via DisposeAsync
[NikSBO] Logout -> https://sap:50000/b1s/v1/Logout
[NikSBO] Logout OK (89 ms)
```

### Common adaptations

**To a file:**

```csharp
LogTrace = msg => File.AppendAllText("niksbo.log",
    $"{DateTime.Now:O} {msg}{Environment.NewLine}")
```

**Bridge to `ILogger` (ASP.NET Core / Microsoft.Extensions.Logging):**

```csharp
LogTrace = msg => _logger.LogDebug(msg)
```

**Add timestamps and PID:**

```csharp
LogTrace = msg => Console.WriteLine(
    $"{DateTime.UtcNow:HH:mm:ss.fff} [pid {Environment.ProcessId}] {msg}")
```

If `LogTrace` is `null` (the default), nothing is emitted and there's no overhead beyond a `Stopwatch` per request (negligible).

---

## Disposing the client

`B1Client` implements `IAsyncDisposable`, so you can let `await using` handle the Logout for you:

```csharp
await using var client = new B1Client(new B1Options { ... });
await client.Login();

// ... your operations ...

// On scope exit: Logout against the SL + HttpClient cleanup, automatically.
```

If the Logout fails during disposal (server down, network blip, …), the exception is **swallowed silently** — `Dispose` must never throw. If you need to know whether the Logout succeeded, call `await client.Logout()` explicitly before exiting the scope.

> Only `IAsyncDisposable` is implemented (not synchronous `IDisposable`). Synchronous dispose would have to block on `Logout`, which can deadlock in UI/ASP.NET sync contexts. If you can't use `await using` (legacy sync code on .NET Framework), call `await client.Logout()` manually.

---

## Cancellation

Every async method (`Login`, `Logout`, `GetAsync`, `PostAsync`, `Query<T>.GetAsync`, `B1Batch.SubmitAsync`, `SqlAsync`, …) accepts an optional `CancellationToken` as the last parameter:

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(5));   // hard timeout

try
{
    var customers = await client
        .Query<BusinessPartner>()
        .Where(bp => bp.CardType == "cCustomer")
        .GetAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // cancelled by timeout or by user
}
```

In ASP.NET Core, propagate the request's token so a disconnected client aborts the SAP call instead of leaving a thread waiting:

```csharp
public async Task<IActionResult> Get(CancellationToken ct)
{
    var bps = await _client.Query<BusinessPartner>().GetAsync(ct);
    return Ok(bps);
}
```

`GetAllAsync` also checks the token **between pages**, so a long pagination walk stops promptly.

For `ExecuteAsync` (raw HTTP escape hatch), the delegate now receives the token so you can pass it to your own call:

```csharp
var response = await client.ExecuteAsync(
    (http, ct) => http.GetAsync("b1s/v1/SomeEndpoint", ct),
    cancellationToken: ct);
```

---

## CRUD

Two flavors: **manual endpoint** (handy for UDOs or unmodeled resources) and **typed model** (the endpoint is resolved from the `[B1Entity]` attribute).

### With a typed model

```csharp
// GET by key
var customer = await client.GetAsync<BusinessPartner>("C30000");
var order    = await client.GetAsync<SalesOrder>(123);     // numeric key

// POST (create)
var created = await client.PostAsync<BusinessPartner>(new BusinessPartner
{
    CardCode = "C99999",
    CardName = "Test Customer",
    CardType = "cCustomer"
});

// PATCH (update)
await client.PatchAsync<BusinessPartner>("C99999", new
{
    EmailAddress = "new@email.com"
});

// DELETE
await client.DeleteAsync<BusinessPartner>("C99999");
```

### With a manual endpoint

Useful for UDOs (user-defined objects) or any resource you don't have a model for:

```csharp
// GET against a UDO
var route = await client.GetByEndpointAsync<JsonElement>("MY_UDO('R001')");

// POST
await client.PostByEndpointAsync<JsonElement>("MY_UDO", new
{
    Code = "R002",
    Name = "North Route"
});

// PATCH
await client.PatchByEndpointAsync("MY_UDO('R002')", new { Name = "North Route (edited)" });

// DELETE
await client.DeleteByEndpointAsync("MY_UDO('R002')");
```

The `b1s/v1/` prefix is added automatically: `"BusinessPartners"` and `"b1s/v1/BusinessPartners"` both work.

---

## Querying (query builder)

Chain filters and projections, then execute with `GetAsync()`, `GetAllAsync()` or `CountAsync()`.

### Against a model

```csharp
var activeCustomers = await client
    .Query<BusinessPartner>()
    .Where(bp => bp.CardType == "cCustomer")
    .OrderBy("CardCode")
    .Top(50)
    .GetAsync();
```

### Against an endpoint (UDOs or unmodeled resources)

```csharp
var routes = await client
    .Query<JsonElement>("MY_UDO")
    .Where("Code", "R001")
    .GetAsync();
```

### Filters: two styles

**Typed LINQ (recommended).** Translates `Expression<Func<T, bool>>` to an OData `$filter`, escaping quotes and formatting dates/decimals under invariant culture:

```csharp
var today = DateTime.Today;

var recentOrders = await client.Query<SalesOrder>()
    .Where(o => o.CardCode == "C30000" && o.DocDate >= today.AddDays(-30))
    .GetAllAsync();

// Supports:
//   ==, !=, <, >, <=, >=
//   &&, ||, !
//   string.Contains / StartsWith / EndsWith
//   captured variables (closures)
var name = "Demo";
var hits = await client.Query<BusinessPartner>()
    .Where(bp => bp.CardName.Contains(name))
    .GetAsync();
```

**String-based (field / operator / value).** Handy when the field name is dynamic, or when filtering by a UDF:

```csharp
using NikSBO.Enums;

var bps = await client.Query<BusinessPartner>()
    .Where("CardType", BOCondition.Equals, "cCustomer")
    .Where("U_MY_FIELD", BOCondition.Equals, "R001") // UDF
    .GetAsync();
```

Available operators (`BOCondition`): `Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `GreaterOrEqual`, `LessOrEqual`. Shortcut: `Where("Field", "value")` is equivalent to `Equals`. Multiple `Where` calls are combined with `and`.

### Select, Top, OrderBy

`Select` supports three styles. Mix and match as you prefer:

```csharp
// 1) Strings — handy for dynamic field names, UDFs, or unmodeled endpoints
var a = await client.Query<Item>()
    .Select("ItemCode", "ItemName", "PriceList")
    .Top(100).GetAsync();

// 2) Individual lambdas
var b = await client.Query<Item>()
    .Select(i => i.ItemCode, i => i.ItemName)
    .Top(100).GetAsync();

// 3) Anonymous type — most idiomatic when projecting several fields
var c = await client.Query<Item>()
    .Select(i => new { i.ItemCode, i.ItemName, i.PriceList })
    .OrderByDesc("ItemCode")
    .Top(100).GetAsync();
```

The lambda variants give you **compile-time safety**: rename a property in the model and the call won't compile. The string variant only fails at runtime against SAP.

> Tuple literals (`i => (i.A, i.B)`) are **not** valid here — expression trees in C# don't support them (CS8143). Use the anonymous type form instead.

### Automatic pagination

By default the Service Layer returns 20 rows plus an `odata.nextLink`. `GetAllAsync()` walks every page and concatenates the results:

```csharp
var all = await client.Query<Item>()
    .Where(it => it.Valid == "tYES")
    .GetAllAsync();
```

> For large collections (tens of thousands of rows), `GetAllAsync()` loads everything into memory — check the size before reaching for it.

### Count

```csharp
int total = await client.Query<BusinessPartner>()
    .Where(bp => bp.CardType == "cCustomer")
    .CountAsync();
```

---

## Document actions (Close, Cancel, …)

SAP B1 exposes operations like closing or cancelling a document as **actions** — `POST /Endpoint(key)/ActionName` with no body. These aren't CRUD, they change the document's state.

### Generic — works with any action

```csharp
// Close a sales order
await client.InvokeActionAsync<SalesOrder>(123, "Close");

// Cancel an invoice
await client.InvokeActionAsync<Invoice>(456, "Cancel");

// Any future SAP action without SDK updates
await client.InvokeActionAsync<DeliveryNote>(789, "MarkAsClosed");
```

For documents with string keys, or for UDOs:

```csharp
// String key
await client.InvokeActionAsync<MyDoc>("ABC", "Close");

// Manual endpoint (UDO with custom action)
await client.InvokeActionByEndpointAsync("MY_UDO('R001')", "MyCustomAction");
```

### Typed shortcut for `Close`

`Close` is the most common action, so there's a typed shortcut:

```csharp
await client.CloseAsync<SalesOrder>(123);
await client.CloseAsync<Quotation>(456);
```

Internally it's just `InvokeActionAsync<T>(key, "Close")` — same call, less typing.

> Actions that return a payload (rare) aren't supported by these helpers yet. For those, use `ExecuteAsync` and read the response manually.

---

## Batch (atomic transactions)

The Service Layer batch endpoint groups operations into an **atomic** changeset: if any of them fails, the rest are rolled back.

```csharp
var batch = client.CreateBatch();

batch.Post("BusinessPartners", new BusinessPartner { CardCode = "C00001", CardName = "One" });
batch.Post("BusinessPartners", new BusinessPartner { CardCode = "C00002", CardName = "Two" });
batch.Patch("BusinessPartners('C30000')", new { CardName = "Renamed" });
batch.Delete("BusinessPartners('C99999')");

var results = await batch.SubmitAsync();

for (int i = 0; i < results.Count; i++)
{
    var r = results[i];
    if (r.IsSuccess)
        Console.WriteLine($"Op {i}: OK ({r.StatusCode}) -> {r.Body}");
    else
        Console.WriteLine($"Op {i}: FAILED ({r.StatusCode}) -> {r.Body}");
}
```

Each `BatchResult` carries:
- `StatusCode` — the HTTP code SAP returned for that operation
- `Body` — the raw JSON body (the created entity with its `DocEntry`, or the SAP error)
- `IsSuccess` — shortcut for `200 <= StatusCode < 300`

If the outer envelope itself fails (auth expired, server down, etc.) a `B1Exception` is thrown — there's no per-operation result in that case.

---

## Raw SQL (SqlAsync)

SAP doesn't expose `SqlQueries` results over a direct GET, so the SDK **creates, executes and deletes** the query behind the scenes (three round trips).

```csharp
var result = await client.SqlAsync(@"
    SELECT TOP 10 ""CardCode"", ""CardName""
    FROM OCRD
    WHERE ""CardType"" = 'C'
");
```

### Parameters (avoid SQL injection)

When values come from user input, **never concatenate** them into the SQL string. Use named parameters with `:name` placeholders and pass the values as an anonymous type (or a dictionary):

```csharp
var rows = await client.SqlAsync(
    @"SELECT ""CardCode"", ""CardName""
      FROM OCRD
      WHERE ""CardType"" = :type
        AND ""CardCode"" LIKE :prefix",
    new { type = "C", prefix = userInput + "%" });
```

The SDK formats each value safely:

| C# type            | URL formatting                            |
| ------------------ | ------------------------------------------ |
| `string`           | `'value'` with `'` doubled (`O'Brien` → `'O''Brien'`) |
| `int`, `long`, …   | `42` (no quotes)                           |
| `bool`             | `true` / `false`                           |
| `decimal`, `double`, `float` | `12.5` (invariant culture, dot)   |
| `DateTime`         | `'2024-01-15T10:30:00'` (ISO 8601)         |
| `null`             | `null`                                     |

For dynamic parameter sets, use a dictionary:

```csharp
var filter = new Dictionary<string, object>
{
    ["type"] = "C",
    ["minBalance"] = 1000m
};

var rows = await client.SqlAsync(
    @"SELECT * FROM OCRD WHERE ""CardType"" = :type AND ""Balance"" >= :minBalance",
    filter);
```

If the SQL fails to execute, the `SQLQueries` entry that the SDK created is still cleaned up afterwards — no orphan queries left in SAP.

> Result type is still `object` (raw JSON). Typed deserialization would be a separate feature.

---

## Bundled models

Under `NikSBO.models`:

- `BusinessPartner` → `BusinessPartners`
- `Item` → `Items`
- `ContactEmployee` → `ContactEmployees`
- `SalesPerson` → `SalesPersons`
- `BusinessPlace` → `BusinessPlaces`
- `Warehouse` → `Warehouses`
- `ItemGroup` → `ItemGroups`
- `VatGroup` → `VatGroups`
- Marketing documents (subclasses of `MarketingDocument`):
  - Sales: `SalesOrder` (`Orders`), `Invoice`, `Quotation`, `DeliveryNote`, `CreditNote`, `Return`, `DownPaymentRequest`, `Draft`, `DownPayment`
  - Purchasing: `PurchaseOrder`, `PurchaseInvoice`, `PurchaseRequest`, `PurchaseDeliveryNote`, `PurchaseReturn`, `PurchaseCreditNote`, `PurchaseDownPayment`
- `DocumentLine` for document lines

All of them inherit from `B1Model`, so they support **UDFs**.

### UDFs (user-defined fields)

Any JSON property that isn't bound to a strong-typed property falls into `ExtensionData` automatically:

```csharp
var bp = await client.GetAsync<BusinessPartner>("C30000");
string?  route   = bp.GetUDF<string>("U_MY_FIELD");
decimal? balance = bp.GetUDF<decimal>("U_AVAILABLE_BALANCE");
```

To write UDFs in POST/PATCH, just include them in the body (an anonymous type works fine):

```csharp
await client.PatchAsync<BusinessPartner>("C30000", new
{
    CardName   = "Demo Customer",
    U_MY_FIELD = "R002"
});
```

### Defining your own models

Annotate with `[B1Entity("ServiceLayerEndpoint")]` and inherit from `B1Model` if you want UDF support:

```csharp
using NikSBO.models;

[B1Entity("MY_UDO")]
public class Route : B1Model
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

// then use Query<Route>(), GetAsync<Route>(...), etc.
var routes = await client.Query<Route>().Where(r => r.Code == "R001").GetAsync();
```

---

## Error handling

Every call throws `B1Exception` when the Service Layer answers with a non-success status:

```csharp
using NikSBO.Exceptions;

try
{
    await client.PostAsync<BusinessPartner>(new BusinessPartner { CardCode = "C30000" });
}
catch (B1Exception ex)
{
    Console.WriteLine($"HTTP: {(int)ex.HttpStatusCode}");
    Console.WriteLine($"SAP code: {ex.SapErrorCode}");
    Console.WriteLine($"Message: {ex.Message}");
    // or simply:
    Console.WriteLine(ex);
    // → SAP B1 Error [-2035] (HTTP 400): BusinessPartner with this code already exists
}
```

If the error body doesn't follow the standard OData shape, the exception still returns the raw body (`SapErrorCode = 0`) so no information is lost.

---

## Full example

```csharp
using NikSBO.Exceptions;
using NikSBO.http;
using NikSBO.models;

var client = new B1Client(new B1Options
{
    ServerUrl = "https://sap.mycompany.local:50000/",
    CompanyDb = "SBODemoUS",
    Username  = "manager",
    Password  = "******"
});

await client.Login();

try
{
    // Create a customer
    var customer = await client.PostAsync<BusinessPartner>(new BusinessPartner
    {
        CardCode = "C12345",
        CardName = "Demo Customer",
        CardType = "cCustomer"
    });

    // Create a sales order for that customer
    var order = await client.PostAsync<SalesOrder>(new SalesOrder
    {
        CardCode   = customer.CardCode,
        DocDate    = DateTime.Today,
        DocDueDate = DateTime.Today.AddDays(30),
        DocumentLines = new List<DocumentLine>
        {
            new() { ItemCode = "A00001", Quantity = 2, Price = 100 },
            new() { ItemCode = "A00002", Quantity = 1, Price = 250 }
        }
    });

    Console.WriteLine($"Order created: DocEntry={order.DocEntry}, DocNum={order.DocNum}");

    // List the customer's open orders
    var open = await client.Query<SalesOrder>()
        .Where(o => o.CardCode == customer.CardCode && o.DocumentStatus == "bost_Open")
        .OrderByDesc("DocDate")
        .GetAllAsync();

    Console.WriteLine($"{open.Count} open orders for {customer.CardCode}");
}
catch (B1Exception ex)
{
    Console.WriteLine($"Failed: {ex}");
}
finally
{
    await client.Logout();
}
```

---

## Running the tests

The repo ships a `NikSBO.Tests` project (xUnit) covering the OData expression translator and the query builder's URL generation. They run **offline** in milliseconds — no Service Layer needed:

```bash
dotnet test
```

In Visual Studio, open `NikSBO.slnx` and run **Test → Test Explorer**.

---

## Known limitations

- Amounts on marketing documents are `double`, not `decimal` — watch out for precision loss
- The query builder doesn't have `Skip` / `$expand` / `$search`
- `SqlAsync` returns `object` (raw JSON), not typed `List<T>`
- Document actions that return a payload aren't covered by the typed helpers — use `ExecuteAsync` for those
- No `IServiceCollection` integration for DI (intentional — target audience is SAP B1 add-on development on .NET Framework where DI isn't the norm)

---

## License

TBD.

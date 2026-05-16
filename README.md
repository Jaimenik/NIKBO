# NikSBO

A C# / .NET 8 SDK for the **SAP Business One Service Layer**. Built so you don't have to fight SAP's OData API by hand: login, typed CRUD, a fluent query builder (with LINQ), transactional batches, raw SQL and UDF support out of the box.

---

## Requirements

- .NET 8 SDK
- Access to a SAP B1 Service Layer (HANA or SQL Server) — typically exposed at `https://<host>:50000/`
- A SAP user with permissions on the target company database

---

## Installation

There's no NuGet package yet. Add the project as a reference:

```bash
dotnet add reference ../NikSBO/NikSBO.csproj
```

Or clone the repo and open `NikSBO.slnx`.

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

> Heads up: the client **does not validate the Service Layer's TLS certificate** (SAP usually ships a self-signed one). For production with a trusted certificate this will need to be made configurable.

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
var route = await client.GetByEndpointAsync<JsonElement>("LSI_RUTAS('R001')");

// POST
await client.PostByEndpointAsync<JsonElement>("LSI_RUTAS", new
{
    Code = "R002",
    Name = "North Route"
});

// PATCH
await client.PatchByEndpointAsync("LSI_RUTAS('R002')", new { Name = "North Route (edited)" });

// DELETE
await client.DeleteByEndpointAsync("LSI_RUTAS('R002')");
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
    .Query<JsonElement>("LSI_RUTAS")
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
var name = "Acme";
var hits = await client.Query<BusinessPartner>()
    .Where(bp => bp.CardName.Contains(name))
    .GetAsync();
```

**String-based (field / operator / value).** Handy when the field name is dynamic, or when filtering by a UDF:

```csharp
using NikSBO.Enums;

var bps = await client.Query<BusinessPartner>()
    .Where("CardType", BOCondition.Equals, "cCustomer")
    .Where("U_LSI_RUTA", BOCondition.Equals, "R001") // UDF
    .GetAsync();
```

Available operators (`BOCondition`): `Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `GreaterOrEqual`, `LessOrEqual`. Shortcut: `Where("Field", "value")` is equivalent to `Equals`. Multiple `Where` calls are combined with `and`.

### Select, Top, OrderBy

```csharp
var list = await client.Query<Item>()
    .Select("ItemCode", "ItemName", "PriceList")
    .OrderByDesc("ItemCode")
    .Top(100)
    .GetAsync();
```

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

> Today it returns `object` (untyped JSON) and **concatenates the SQL directly** — don't use it with user-controlled input until we add parameter support (`SqlParams`).

---

## Bundled models

Under `NikSBO.models`:

- `BusinessPartner` → `BusinessPartners`
- `Item` → `Items`
- Marketing documents (subclasses of `MarketingDocument`):
  - Sales: `SalesOrder` (`Orders`), `Invoice`, `Quotation`, `DeliveryNote`, `CreditNote`, `Return`, `DownPaymentRequest`, `Draft`, `DownPayment`
  - Purchasing: `PurchaseOrder`, `PurchaseInvoice`, `PurchaseRequest`, `PurchaseDeliveryNote`, `PurchaseReturn`, `PurchaseCreditNote`, `PurchaseDownPayment`
- `DocumentLine` for document lines

All of them inherit from `B1Model`, so they support **UDFs**.

### UDFs (user-defined fields)

Any JSON property that isn't bound to a strong-typed property falls into `ExtensionData` automatically:

```csharp
var bp = await client.GetAsync<BusinessPartner>("C30000");
string?  route   = bp.GetUDF<string>("U_LSI_RUTA");
decimal? balance = bp.GetUDF<decimal>("U_AVAILABLE_BALANCE");
```

To write UDFs in POST/PATCH, just include them in the body (an anonymous type works fine):

```csharp
await client.PatchAsync<BusinessPartner>("C30000", new
{
    CardName   = "Acme LLC",
    U_LSI_RUTA = "R002"
});
```

### Defining your own models

Annotate with `[B1Entity("ServiceLayerEndpoint")]` and inherit from `B1Model` if you want UDF support:

```csharp
using NikSBO.models;

[B1Entity("LSI_RUTAS")]
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
        CardName = "Acme LLC",
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

## Known limitations

See [`TODO.md`](TODO.md) for the full backlog. The highlights:

- No `CancellationToken` on `*Async` methods yet
- Amounts on marketing documents are `double`, not `decimal` — watch out for precision loss
- The query builder doesn't have `Skip` / `$expand` / `$search`
- `SqlAsync` doesn't parameterize values (injection risk if you concatenate untrusted input)
- No NuGet package, no `IServiceCollection` integration for DI

---

## License

TBD.

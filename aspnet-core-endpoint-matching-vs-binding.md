# Endpoint matching vs model binding — what routing actually consults

The short answer: **routing looks at very little, model binding looks at everything else, and they happen in that order.** Two actions on the same path with different *signatures* are NOT collapsed into one endpoint — they become two endpoints, and matching them both → an ambiguity error.

## 1. The two phases — keep them separate in your head

```
Request
  │
  ▼
┌───────────────────────────────┐
│ ENDPOINT SELECTION            │  ← routing's job (UseRouting)
│  inputs the matcher considers:│
│   - path                      │
│   - HTTP method               │
│   - route constraints (:int…) │
│   - host (if RequireHost set) │
│   - [Consumes] content-type   │
│   - other MatcherPolicy metadata
│                               │
│  output: ONE endpoint chosen  │
│          (or ambiguous → 500) │
└───────────────────────────────┘
  │
  ▼
┌───────────────────────────────┐
│ MODEL BINDING                 │  ← MVC ActionInvoker's job
│  now we know WHICH action;    │
│  per-parameter sources read   │
│  from:                        │
│   - route values              │
│   - query string              │
│   - body (JSON / form)        │
│   - headers, cookies, form    │
│                               │
│  output: parameter values     │
│          for the action call  │
└───────────────────────────────┘
  │
  ▼
Action method runs
```

**Routing never inspects the query string or the body.** Those exist for *binding* parameter values after the action has already been selected.

## 2. One controller method = one endpoint (always)

`MapControllers()` walks `ApplicationModel`, finds each action method, and registers **one `RouteEndpoint` per `ActionDescriptor`**. Two methods with the same path are still two endpoints in the routing table — they don't merge.

```csharp
public class UsersController : Controller
{
    [HttpGet("/users")]
    public IActionResult GetAll() => Ok(...);

    [HttpGet("/users")]
    public IActionResult Search(string name) => Ok(...);
}
```

After startup, the endpoint table looks like:

```
Endpoint A: GET /users  →  UsersController.GetAll
Endpoint B: GET /users  →  UsersController.Search
```

A request `GET /users?name=alice` arrives. The matcher walks the DFA, considers both endpoints' route templates and HTTP methods. Both match. **Ambiguous match → `AmbiguousMatchException`** at request time.

The matcher does **not** say "well, B has a `name` parameter and the request has `?name=alice`, so B wins." It has no idea what `name` is — that's a binding concern, and binding hasn't happened yet.

## 3. What the matcher actually consults — the complete list

Every matching decision comes from one of these:

| Input | Where it lives | Example |
|---|---|---|
| Path template | `RoutePattern` on the endpoint | `/users/{id:int}` |
| HTTP method | `HttpMethodMetadata` | `[HttpGet]`, `[HttpPost]` |
| Route value constraints | inline `{id:int}`, `{slug:regex(...)}` | `:int`, `:guid`, `:length(5)` |
| Host | `HostAttribute` / `RequireHost(...)` | `admin.example.com` |
| Content-Type of request | `ConsumesAttribute` via `ConsumesMatcherPolicy` | `[Consumes("application/json")]` |
| CORS preflight | `CorsMatcherPolicy` | OPTIONS preflights routed specially |
| Custom `MatcherPolicy` you write | yours | rare |

**Not in the list:** query string keys, query string values, request body, most headers, cookies, claims.

## 4. Why ambiguity is the right behavior

If matching consulted "does action B's parameter `name` exist in the query?", you'd get:

- `GET /users` → only A matches (no `?name`)
- `GET /users?name=alice` → both A and B match (A's `GetAll()` happily ignores `name`)
- The framework would have to invent precedence rules ("more matched parameters wins"?), and those rules would be invisible at the call site.

Instead, the framework demands you make the difference **explicit and visible**. You have five clean ways to do it:

| Differentiator | Mechanism |
|---|---|
| **Different path** | `[HttpGet("/users")]` vs `[HttpGet("/users/search")]` |
| **Different HTTP method** | `[HttpGet]` vs `[HttpPost]` |
| **Route constraint** | `[HttpGet("/users/{id:int}")]` vs `[HttpGet("/users/{name:alpha}")]` |
| **Required value in route template** | `[HttpGet("/users/{action=GetAll}")]` (action token routing) |
| **`[Consumes]` content-type** | `[HttpPost("/users"), Consumes("application/json")]` vs `[HttpPost("/users"), Consumes("application/xml")]` |

Anything that's *not* one of these will collide.

## 5. The `[Consumes]` case — the closest thing to "matching on body"

`[Consumes]` is a real exception worth knowing. It plugs into the matcher via `ConsumesMatcherPolicy`:

```csharp
[HttpPost("/api/users")]
[Consumes("application/json")]
public IActionResult CreateJson([FromBody] UserDto dto) { ... }

[HttpPost("/api/users")]
[Consumes("application/xml")]
public IActionResult CreateXml([FromBody] UserDto dto) { ... }
```

Here `POST /api/users` with `Content-Type: application/json` selects the first endpoint; with `Content-Type: application/xml` selects the second. The matcher reads the **request's `Content-Type` header** (not the body itself!) and uses it as a discriminator.

But notice: it's still a *header* check, not body parsing. The matcher never touches the body.

## 6. What the matcher CAN'T do — concrete examples of attempted differentiation that fail

```csharp
// AMBIGUOUS — both match GET /users
[HttpGet("/users")]
public IActionResult List() { ... }

[HttpGet("/users")]
public IActionResult Search(string q) { ... }
```

```csharp
// AMBIGUOUS — body shape differs but matcher doesn't read body
[HttpPost("/items")]
public IActionResult AddOne([FromBody] Item item) { ... }

[HttpPost("/items")]
public IActionResult AddMany([FromBody] List<Item> items) { ... }
```

```csharp
// AMBIGUOUS — query param presence isn't a routing signal
[HttpGet("/products")]
public IActionResult AllProducts() { ... }

[HttpGet("/products")]
public IActionResult ProductsByCategory([FromQuery] int categoryId) { ... }
```

In every case, both endpoints share path + method + content-type, so the matcher sees them as equivalent → throws.

## 7. The legacy escape hatch — `IActionConstraint`

MVC has an older mechanism, `IActionConstraint`, that lets you write custom action selection logic *after* routing has shortlisted candidates. You can implement one that checks query keys, headers, etc., and tie-break between same-path actions. Examples in the wild: `[NonAction]`, `[ActionMethodSelector]`-style attributes from older MVC.

In endpoint routing, action constraints are wrapped by `ActionConstraintMatcherPolicy`. So they DO run during matching, but they're an extension point, not the default. **Do not lean on them for normal differentiation** — the explicit five from §4 are clearer.

## 8. Endpoint registration — the data flow at startup

```
[HttpGet("/users")]            ┐
public IActionResult GetAll()  │
                               │
[HttpGet("/users/{id:int}")]   │ ApplicationModel discovers
public IActionResult ById(...) ├─ each method as an
                               │  ActionDescriptor
[HttpPost("/users")]            │
public IActionResult Create()  ┘
        │
        ▼
ControllerActionEndpointDataSource
        │
        ▼
For each ActionDescriptor:
    new RouteEndpoint(
        requestDelegate: ControllerActionInvoker.Invoke,
        routePattern:    "/users" or "/users/{id:int}",
        order:           …,
        metadata:        [HttpMethodMetadata, ConsumesMetadata,
                          AuthorizeAttribute, RouteNameMetadata, …,
                          ControllerActionDescriptor itself])
        │
        ▼
Endpoint table fed to DfaMatcher
        │
        ▼
Matcher build picks up duplicates → at request time, ambiguity throws.
```

Two same-path actions = two distinct `RouteEndpoint`s in the table. The matcher doesn't deduplicate or pick one; it surfaces both as candidates and gives up.

## 9. Mental-model summary

| Question | Answer |
|---|---|
| Do two actions with same path & method share an endpoint? | **No.** Each action = one endpoint. Both are registered. |
| Does the matcher look at query strings? | **No.** |
| Does the matcher look at the request body? | **No** — but it looks at `Content-Type` if `[Consumes]` is present. |
| What happens when two endpoints both match? | `AmbiguousMatchException` at request time. |
| How do I differentiate two actions on the same path? | Different path / method / route constraint / `[Consumes]` content-type / action token. |
| Where do `[FromQuery]`, `[FromBody]` etc. come in? | After endpoint selection — they drive **model binding**, not routing. |

## 10. One-sentence rule

> **Routing chooses the endpoint using only what the request line and a few headers reveal — path, method, route constraints, `[Consumes]`. Anything else (query keys, body shape, parameter names) is a binding concern that runs strictly later, after the endpoint is already chosen.**

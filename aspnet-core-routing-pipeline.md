# ASP.NET Core routing pipeline — concentrated walkthrough

## 1. The historical pivot — why the term changed

| Era | What ran routing | Problem |
|---|---|---|
| Pre-3.0 (`UseMvc()`) | A **single terminal middleware** that did matching + dispatch in one shot | Anything before it couldn't know *which* action would run. Auth, CORS, caching had to make do with URL strings. |
| 3.0+ (Endpoint Routing) | **Two middlewares**: one matches, one executes — with a gap in between | Match-then-execute lets every middleware in the gap inspect the chosen endpoint's metadata. |
| 6.0+ (Minimal APIs) | Same endpoint routing; `WebApplication` wires the two middlewares automatically | `app.MapGet(...)` and `[ApiController] class Foo { [HttpGet(...)] ... }` both land in the same endpoint table. |

So when you read "routing", "endpoints", "endpoint routing" — they're all the same system. The word "endpoint" replaced "route" because a route is just a *URL pattern*, while an **endpoint = URL pattern + delegate to call + metadata**.

## 2. The two-middleware split — this is the whole concept

Routing is **two** middlewares with a deliberate gap between them:

```
HTTP request
    │
    ▼
┌────────────────────────────────────┐
│ UseRouting (EndpointRoutingMW)     │  ← runs the matcher
│   match request → Endpoint object  │     stores it on HttpContext
│   HttpContext.SetEndpoint(ep)      │
│   route values → RouteValueDict    │
└────────────────────────────────────┘
    │
    │   ← the GAP. Other middleware can inspect HttpContext.GetEndpoint()
    │     and read its Metadata to make decisions.
    │     (UseAuthentication, UseAuthorization, UseCors, RateLimiter…)
    │
    ▼
┌────────────────────────────────────┐
│ UseEndpoints (EndpointMW)          │  ← actually executes the endpoint
│   ep = HttpContext.GetEndpoint()   │
│   await ep.RequestDelegate(ctx)    │
└────────────────────────────────────┘
```

**`UseRouting` does not call your action.** It only fills in `HttpContext.GetEndpoint()`. **`UseEndpoints` (or the implicit terminal middleware in `WebApplication`) is what calls the delegate.**

This is why pipeline order matters — and why `UseAuthorization` *must* come after `UseRouting` and before `UseEndpoints`. It needs the endpoint set, and it must run before the endpoint executes.

## 3. What lives in the gap, and why each one needs the endpoint

```
UseRouting
    │
    ├── UseAuthentication   reads cookie/JWT → sets HttpContext.User
    │                       (doesn't need endpoint, but conventionally placed here)
    │
    ├── UseCors             reads endpoint.Metadata for [EnableCors]/[DisableCors]
    │
    ├── UseAuthorization    reads endpoint.Metadata for [Authorize]/[AllowAnonymous]
    │                       evaluates policy → Challenge / Forbid / pass
    │
    ├── UseRateLimiter      reads endpoint.Metadata for [EnableRateLimiting]
    │
    ├── UseResponseCaching  reads endpoint.Metadata for [ResponseCache]
    │
    ▼
UseEndpoints
```

Every one of these consults the endpoint's `Metadata` bag. That's the whole point of the gap — the middleware doesn't have to know about controllers or minimal APIs; it just reads metadata attached to *whichever* endpoint matched.

If you put `UseAuthorization` **before** `UseRouting`, `HttpContext.GetEndpoint()` is null → it has nothing to authorize against → either silently passes or throws.

## 4. What an `Endpoint` actually is

It's a tiny object:

```csharp
public class Endpoint
{
    public RequestDelegate? RequestDelegate { get; }   // the thing to call
    public string?          DisplayName     { get; }
    public EndpointMetadataCollection Metadata { get; } // attributes, route, etc.
}
```

A `RouteEndpoint` adds:

```csharp
public RoutePattern RoutePattern { get; }   // /api/users/{id:int}
public int          Order        { get; }
```

That's it. The endpoint **doesn't know** whether it came from a controller, a minimal API, a Razor Page, a SignalR hub, or Blazor. It's just a `RequestDelegate` plus metadata.

## 5. Where endpoints come from — `EndpointDataSource`

Every `Map…` call you write registers entries in an `EndpointDataSource`. The matcher reads from these.

| Call you write | What it registers |
|---|---|
| `app.MapGet("/x", () => "hi")` | One `RouteEndpoint`. RequestDelegate generated from the lambda by `RequestDelegateFactory`. |
| `app.MapControllers()` | One `RouteEndpoint` **per controller action** discovered via reflection. RequestDelegate is the MVC `ControllerActionInvoker`. |
| `app.MapRazorPages()` | One per page. |
| `app.MapHub<ChatHub>("/chat")` | SignalR endpoint. |
| `app.MapBlazorHub()` | Blazor Server endpoint. |
| `app.MapFallback(...)` | A low-priority catch-all endpoint. |

When `UseRouting` boots, it asks all registered `EndpointDataSource`s for their endpoints, and builds the matcher from the union.

## 6. The matcher — what `UseRouting` actually does on each request

The default matcher is `DfaMatcher` — a Deterministic Finite Automaton built once from all route templates. On each request:

1. Tokenize the request path: `/api/users/42` → `["api", "users", "42"]`
2. Walk the DFA segment by segment
3. At leaves, apply constraints (HTTP method, route constraints like `:int`, content type)
4. Resolve ambiguity via:
   - **Order** (lower wins)
   - **Specificity** (literal segments beat parameter segments beat catch-alls)
   - **HTTP method match** (`MapGet` won't be picked for a POST)
5. Output: a chosen `Endpoint` + a `RouteValueDictionary` of extracted parameters

If nothing matches → `HttpContext.GetEndpoint()` stays null → request falls through to the next middleware after `UseEndpoints`. (That's why `app.UseStaticFiles()` after Routing still works for unmatched paths, and why a final 404 gets emitted by the host's terminal middleware.)

## 7. Execution side — how the delegate dispatches to controllers vs minimal APIs

Once `UseEndpoints` invokes `endpoint.RequestDelegate(ctx)`, what runs depends on the endpoint's source.

### (a) Minimal API (`MapGet("/x", handler)`)

`RequestDelegateFactory` analyzes the lambda's signature at startup and generates a `RequestDelegate` that:

```
1. Read route values / query / headers / body → bind to parameters
2. Resolve services from DI for [FromServices]
3. Invoke the lambda
4. Convert return value to HTTP response
   (string → text/plain, object → JSON, IResult → calls IResult.ExecuteAsync)
```

No filter pipeline by default (filters are opt-in in .NET 7+ with `AddEndpointFilter`).

### (b) MVC controller action

The `RequestDelegate` is `ControllerActionInvoker.Invoke`. It runs MVC's full pipeline:

```
ResourceFilter
  ├─ AuthorizationFilter (additional, on top of UseAuthorization)
  ├─ ActionFilter
  │     ├─ ModelBinding → param values from route / query / body
  │     ├─ ModelValidation
  │     ├─ Action method invocation
  │     └─ IActionResult.ExecuteResultAsync
  └─ ResultFilter
ExceptionFilter (wraps all of the above)
```

So **the same `EndpointMiddleware` line** ends up either invoking your minimal-API lambda directly or trampolining into MVC's filter machinery — depending on which factory built the delegate.

## 8. End-to-end pipeline — sequence diagram

```
Browser            Kestrel           Middleware pipeline
   │ GET /api/users/42 (cookie)        │
   │──────────────────────────────────>│
   │                                   │
   │                          ┌────────────────────┐
   │                          │ UseRouting         │
   │                          │  DfaMatcher walks  │
   │                          │  /api/users/42     │
   │                          │  → matches the     │
   │                          │    UsersController │
   │                          │    .Get(int id)    │
   │                          │    endpoint        │
   │                          │  HttpContext.SetEndpoint(ep)
   │                          │  RouteValues = { id = 42 }
   │                          └────────────────────┘
   │                          ┌────────────────────┐
   │                          │ UseAuthentication  │
   │                          │  reads ".AspNetCore.Cookies"
   │                          │  HttpContext.User = principal
   │                          └────────────────────┘
   │                          ┌────────────────────┐
   │                          │ UseAuthorization   │
   │                          │  reads ep.Metadata │
   │                          │  → finds [Authorize]
   │                          │  policy passes     │
   │                          └────────────────────┘
   │                          ┌────────────────────┐
   │                          │ UseEndpoints       │
   │                          │  ep = GetEndpoint()│
   │                          │  await ep.RequestDelegate(ctx)
   │                          │    │               │
   │                          │    ▼               │
   │                          │  ControllerActionInvoker:
   │                          │   - resolves UsersController from DI
   │                          │   - model binds id=42
   │                          │   - runs filters
   │                          │   - calls Get(42)
   │                          │   - serializes return → JSON
   │                          └────────────────────┘
   │ 200 OK { "id":42, ... }              │
   │<─────────────────────────────────────│
```

For minimal API, only the **last box** changes — `RequestDelegateFactory`-generated delegate runs instead of `ControllerActionInvoker`. Everything before is identical.

## 9. Where the magic of `WebApplication` hides

In .NET 6+ you write:

```csharp
var app = builder.Build();
app.MapGet("/x", () => "hi");
app.Run();
```

…and don't see `UseRouting` or `UseEndpoints` anywhere. They're added implicitly:

- The first `Map…` call after `Build()` lazily inserts `UseRouting` at the *start* of the pipeline.
- The terminal `EndpointMiddleware` is added at the *end* automatically when `app.Run()` finalizes the pipeline.
- Anything you `Use…` between sits in "the gap."

If you call `app.UseRouting()` or `app.UseEndpoints()` explicitly, the framework respects your placement and skips the implicit insertion. So you can override the position when needed (rare, but useful when you want middleware *before* routing).

## 10. Mental-model summary

| Question | Answer |
|---|---|
| Which middleware decides the route? | `UseRouting` (specifically `EndpointRoutingMiddleware` running `DfaMatcher`). |
| Where is the decision stored? | `HttpContext.SetEndpoint(ep)` — accessible via `HttpContext.GetEndpoint()` everywhere downstream. |
| Which middleware *executes* the action? | `UseEndpoints` / the implicit `EndpointMiddleware`. |
| What do auth, CORS, etc. do? | They sit in the gap and read `endpoint.Metadata`. |
| What unifies controllers and minimal APIs? | Both register `Endpoint` objects in `EndpointDataSource`s; both are dispatched by `EndpointMiddleware`. The only difference is who built the `RequestDelegate`: MVC's invoker vs `RequestDelegateFactory`. |
| Why does pipeline order matter? | The endpoint must be *matched* before middleware can read its metadata, and *not yet executed* if those middlewares want to short-circuit (auth fail → challenge/forbid). |

## 11. One-sentence rule

> **`UseRouting` matches; `UseEndpoints` executes; everything between them inspects `HttpContext.GetEndpoint().Metadata` to make policy decisions before the action runs.**

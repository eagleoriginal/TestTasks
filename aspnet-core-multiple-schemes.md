# Multiple schemes — who challenges?

Short answer: **schemes don't compete.** The framework picks one (or, if a policy lists several, runs them in sequence — and that's where the mess starts). There's no "race." Let me unfold it.

## 1. There are six default-scheme slots, not one

`AuthenticationOptions` has six knobs. Most apps only set the first:

| Option | Used when |
|---|---|
| `DefaultScheme` | Fallback for all the others below if they're not set |
| `DefaultAuthenticateScheme` | `context.AuthenticateAsync()` with no scheme name |
| `DefaultChallengeScheme` | `context.ChallengeAsync()` with no scheme name — **this is the one for your question** |
| `DefaultForbidScheme` | `context.ForbidAsync()` with no name |
| `DefaultSignInScheme` | `context.SignInAsync()` with no name |
| `DefaultSignOutScheme` | `context.SignOutAsync()` with no name |

```csharp
builder.Services
    .AddAuthentication("Cookies")            // sets DefaultScheme = "Cookies"
    .AddCookie("Cookies", ...)
    .AddOpenIdConnect("Yandex", ...);
```

Here `DefaultChallengeScheme` is **also "Cookies"** (because it falls back to `DefaultScheme`). That's almost never what you want for an OIDC app — you want challenge to go to Yandex. So you set it explicitly:

```csharp
.AddAuthentication(o =>
{
    o.DefaultScheme          = "Cookies";   // who reads the cookie on every request
    o.DefaultChallengeScheme = "Yandex";    // who handles "user is anonymous"
})
```

## 2. The simple case — no policy schemes, just defaults

```
[Authorize]                         ← no AuthenticationSchemes set
public IActionResult Secret() => …
```

Anonymous request flow:

```
Authenticate phase:
   AuthorizationMiddleware → context.AuthenticateAsync(DefaultAuthenticateScheme)
                              ↓
                              ONE scheme runs.   "Cookies" reads the cookie.
                              No principal.

Challenge phase:
   Authorization fails → context.ChallengeAsync()    // no name passed
                              ↓
                              ONE scheme runs.   DefaultChallengeScheme = "Yandex".
                              OIDC handler writes 302 to oauth.yandex.ru/authorize.
```

**No competition.** Exactly one scheme handles authenticate; exactly one handles challenge. They can be different schemes — and usually are, in a BFF.

## 3. The interesting case — policy lists multiple schemes

```csharp
[Authorize(AuthenticationSchemes = "Cookies,Bearer")]
public IActionResult Mixed() => …
```

Now both schemes are in play. Two phases, two different behaviors:

### Authenticate phase — schemes are *merged*

```
foreach scheme in ["Cookies", "Bearer"]:
    result = AuthenticateAsync(scheme)
    if result.Succeeded:
        principal = MergeUserPrincipal(principal, result.Principal)
```

Each scheme produces a `ClaimsPrincipal`; successful ones get **merged** into one principal carrying multiple `ClaimsIdentity` objects (one per scheme). It's union, not race. If `Cookies` says "this is Alice with role X" and `Bearer` says "this is Alice with scope Y", the action sees both.

### Challenge phase — schemes run *in sequence*, and the response gets messy

```
foreach scheme in ["Cookies", "Bearer"]:
    await context.ChallengeAsync(scheme)
```

Each handler's `HandleChallengeAsync` writes to the *same* `HttpResponse`. There is no negotiation:

```
Iteration 1: Cookies handler
   Response.StatusCode = 302
   Response.Headers["Location"] = "/Account/Login?ReturnUrl=..."

Iteration 2: Bearer handler
   Response.StatusCode = 401
   Response.Headers.Append("WWW-Authenticate", "Bearer")
```

The wire-level reality:

- **Status code** is a single integer on the response. Last writer wins (until headers flush).
- **Headers** can stack — `WWW-Authenticate` is explicitly multi-valued, so multiple schemes adding it is fine.
- **Body writes / `Response.Redirect`** — once a handler calls `Response.Redirect`, it's committed; later handlers fighting it produce broken responses (302 with a `WWW-Authenticate: Bearer` header — neither browser nor SDK knows what to do).

So mixing a redirecting scheme (Cookies/OIDC) with a header-only scheme (Bearer) in the same `[Authorize]` policy is **almost always a bug**. They don't compete cleanly; they collide.

## 4. The pattern people actually use — pick a scheme per request

The right move is to *select* a scheme based on the request, not challenge them all. Two common ways:

### (a) `AddPolicyScheme` + `ForwardDefaultSelector`

A "policy scheme" is a virtual scheme that forwards to a real one based on a predicate:

```csharp
.AddAuthentication("Smart")
.AddPolicyScheme("Smart", "Smart", o =>
{
    o.ForwardDefaultSelector = ctx =>
        ctx.Request.Headers.Authorization
            .ToString().StartsWith("Bearer ")
        ? "Bearer"
        : "Cookies";
})
.AddCookie("Cookies", …)
.AddJwtBearer("Bearer", …);
```

Now `DefaultScheme = "Smart"`. On every Authenticate / Challenge / etc., the selector decides which real scheme handles it. **One scheme per request, chosen by inspection.**

### (b) Split by route / endpoint

API endpoints take `[Authorize(AuthenticationSchemes = "Bearer")]`; UI controllers take `[Authorize(AuthenticationSchemes = "Cookies")]` (or rely on defaults). No mixing in a single policy.

## 5. The forwarding chain — how scheme selection actually resolves

`ForwardDefaultSelector` is not alone. Every `AuthenticationSchemeOptions` has a **chain** of forwarding knobs that go from most specific to most general. Understanding the priority is what lets you express things like "Bearer reads the JWT itself, but for Challenge fall back to Cookies."

### The seven knobs

| Property | Type | Purpose |
|---|---|---|
| `ForwardAuthenticate` | `string?` | Forward **only** Authenticate to this named scheme |
| `ForwardChallenge` | `string?` | Forward **only** Challenge to this named scheme |
| `ForwardForbid` | `string?` | Forward **only** Forbid to this named scheme |
| `ForwardSignIn` | `string?` | Forward **only** SignIn to this named scheme |
| `ForwardSignOut` | `string?` | Forward **only** SignOut to this named scheme |
| `ForwardDefault` | `string?` | Static fallback — any operation not explicitly forwarded above |
| `ForwardDefaultSelector` | `Func<HttpContext, string?>` | Dynamic fallback — decides per request |

The first five are **per-operation overrides**. The last two are **catch-alls**.

### Resolution order

The base `AuthenticationHandler<TOptions>` does, e.g., for Challenge:

```csharp
public virtual Task ChallengeAsync(AuthenticationProperties? properties)
{
    var target = ResolveTarget(Options.ForwardChallenge);     //  step 1
    return (target != null)
        ? Context.ChallengeAsync(target, properties)
        : HandleChallengeAsync(properties);                   //  step 4 (own logic)
}

protected virtual string? ResolveTarget(string? scheme)
{
    var target = scheme                                       //  step 1: ForwardChallenge
              ?? Options.ForwardDefaultSelector?.Invoke(Context) //  step 2: dynamic
              ?? Options.ForwardDefault;                      //  step 3: static
    return target == Scheme.Name ? null : target;             //  prevent self-loop
}
```

So the priority chain, from most specific to most general:

```
ChallengeAsync called on scheme X
        │
        ▼
┌────────────────────────────────────────────────┐
│ 1. Options.ForwardChallenge (string?)          │  per-op override — most specific
│    set?  → target = that scheme                │
└────────────────────────────────────────────────┘
        │ no
        ▼
┌────────────────────────────────────────────────┐
│ 2. Options.ForwardDefaultSelector(ctx)         │  dynamic per-request
│    returns non-null? → target = that scheme    │
└────────────────────────────────────────────────┘
        │ no
        ▼
┌────────────────────────────────────────────────┐
│ 3. Options.ForwardDefault (string?)            │  static fallback
│    set?  → target = that scheme                │
└────────────────────────────────────────────────┘
        │ no
        ▼
┌────────────────────────────────────────────────┐
│ 4. X.HandleChallengeAsync()                    │  the scheme's own logic
└────────────────────────────────────────────────┘
```

Same chain for Authenticate (`ForwardAuthenticate` → selector → default → own), Forbid, SignIn, SignOut. **Each operation has its own step 1; steps 2 and 3 are shared across all operations.**

### What this lets you express

The granularity matters. Examples:

```csharp
.AddJwtBearer("Bearer", o => {
    o.ForwardChallenge = "Cookies";   // only Challenge forwards
    // Authenticate stays on Bearer (read the JWT itself)
})
```

Hybrid behavior: **read the JWT on every request, but if the user is anonymous, fall back to cookie-based login.** Without per-operation forwarding you couldn't.

```csharp
.AddPolicyScheme("Smart", "Smart", o => {
    o.ForwardDefaultSelector = ctx =>
        ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ")
            ? "Bearer"
            : "Cookies";
    o.ForwardDefault = "Cookies";   // belt-and-suspenders if selector returns null
});
```

Selector wins; `ForwardDefault` is the safety net if the selector hands back `null`.

### How `AddPolicyScheme` actually works under the hood

A "policy scheme" is just a scheme whose own `HandleChallengeAsync` / `HandleAuthenticateAsync` etc. are **never reached** — because step 1, 2, or 3 always resolves to a real scheme first. The "virtual" scheme lives entirely in the forwarding layer.

## 6. Mental-model summary

| Situation | What runs on Authenticate | What runs on Challenge |
|---|---|---|
| Single scheme registered | That one | That one |
| Multiple schemes, default set, no policy schemes | `DefaultAuthenticateScheme` only | `DefaultChallengeScheme` only |
| Policy lists `AuthenticationSchemes = "A,B"` | A then B; results **merged** into one principal | A then B; **last writer wins on status code, headers stack** — usually a bug if A and B disagree on response shape |
| `AddPolicyScheme` with selector | The forwarded-to scheme | The forwarded-to scheme |

## 7. Wire-level: BFF default case (your setup)

```
Browser                 BFF (Cookies + Yandex registered)
   │                       │
   │ GET /api/me           │
   │ (no cookie)           │
   │──────────────────────>│ Authenticate("Cookies") → no principal
   │                       │ [Authorize] → Challenge()
   │                       │ DefaultChallengeScheme = "Yandex"
   │                       │ OIDC.HandleChallengeAsync → builds /authorize URL
   │                       │
   │ 302 Found             │
   │ Location: oauth.yandex.ru/authorize?...
   │<──────────────────────│
```

Cookies authenticates; Yandex challenges. They're cooperating, not competing — because the *defaults* told them their roles.

## 8. One-sentence rule

> **One scheme authenticates a request; one scheme challenges it.** If you ever find yourself listing multiple schemes in a single `[Authorize]` policy and they have different response shapes (302 vs 401), use `AddPolicyScheme` to select between them instead.

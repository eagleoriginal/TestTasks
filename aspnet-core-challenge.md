# Challenge in ASP.NET Core auth — concentrated article

## 1. The word comes from HTTP, not from Microsoft

RFC 7235 defines an **HTTP challenge**: when a server demands authentication, it answers with `401 Unauthorized` plus a `WWW-Authenticate` header. That header *is* the challenge — it tells the client *which* authentication mechanism to use.

```
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Basic realm="api"
                  ^^^^^^^^^^^^^^^^^^
                  this is "the challenge"
```

Original meaning: **"server's demand: prove who you are, here's how."**

ASP.NET Core inherits this word and **generalizes** it.

## 2. The ASP.NET Core meaning

> **Challenge = "tell the user to authenticate via the configured scheme."**
>
> What that *physically* means on the wire depends entirely on which scheme is doing the challenging.

The handler decides. Each `IAuthenticationHandler` implements `HandleChallengeAsync()`, and that method writes whatever response makes sense for that scheme.

| Scheme | What "challenge" does on the wire |
|---|---|
| `Cookie` (interactive) | **302** redirect to `/Account/Login?ReturnUrl=...` |
| `OpenIdConnect` / `OAuth` (e.g. Yandex) | **302** redirect to the OP's `/authorize` URL with `code_challenge`, `state`, `nonce` |
| `JwtBearer` (API) | **401** with `WWW-Authenticate: Bearer` |
| `Negotiate` (Windows) | **401** with `WWW-Authenticate: Negotiate` |

So *"challenge = 302"* is a half-truth that comes from spending all your time on cookie/OIDC schemes. JWT challenge is 401.

## 3. The three verbs (this is what untangles everything)

ASP.NET Core's auth pipeline has **three** distinct verbs. Confusing them is what makes people drown.

| Verb | When called | Meaning | Side effects |
|---|---|---|---|
| **Authenticate** | Every request | "Read the credentials, tell me who this is" — decode cookie / validate JWT / etc. | None. Returns `ClaimsPrincipal` or `AuthenticateResult.NoResult/Fail`. |
| **Challenge** | User is **anonymous** but needs to be authenticated | "Force them to identify themselves" | Writes 401 / 302 / OP redirect, depending on scheme |
| **Forbid** | User **is** authenticated but lacks permission | "They proved who they are; they're still not allowed" | Writes 403 / redirect to access-denied page |

Decision tree:

```
Request comes in
   │
   │── Authenticate runs (always; cheap; no I/O on response)
   │     ├── ok        → request has User.Identity.IsAuthenticated == true
   │     └── no creds  → User is anonymous (not an error yet)
   │
   ├── [Authorize] checks the User
   │     ├── anonymous       →  Challenge   (302/401 — go log in)
   │     ├── authed, no role →  Forbid      (403/redirect — you're not allowed)
   │     └── authed + ok     →  pass to action
```

`[Authorize]` is just the policy that picks Challenge vs Forbid vs pass-through.

## 4. Where the term shows up in code

### (a) Implicit — via `[Authorize]`

You almost never call Challenge yourself for the "user hit a protected page while anonymous" case. `[Authorize]` triggers it for you.

```csharp
[Authorize]                 // anonymous request → framework calls Challenge() automatically
public IActionResult Secret() => View();
```

### (b) Explicit — `ChallengeResult` from a controller

You **do** write it explicitly when *you* want to kick off a flow. The classic case is a "Sign in with Yandex" button:

```csharp
public IActionResult LoginWithYandex(string returnUrl = "/")
    => Challenge(
        new AuthenticationProperties { RedirectUri = returnUrl },
        "Yandex");                                 // ← scheme name
```

`Challenge(...)` returns a `ChallengeResult`. When MVC executes it, this happens:

```
ChallengeResult.ExecuteResultAsync
  → HttpContext.ChallengeAsync(scheme, properties)
    → IAuthenticationService.ChallengeAsync
      → IAuthenticationHandler.HandleChallengeAsync     ← the scheme writes the response
```

`ChallengeResult` is a thin wrapper that says *"invoke the named scheme's challenge logic."* It contains no auth logic itself.

### (c) Lower-level — `HttpContext.ChallengeAsync(scheme)`

Same effect; used in middleware or Razor pages where you don't have an MVC `IActionResult`.

## 5. Wire-level: cookie scheme challenge

```
Browser                       Your BFF
   │                             │
   │  GET /api/me                │
   │  (no cookie, or expired)    │
   │────────────────────────────>│
   │                             │ Authenticate → no principal
   │                             │ [Authorize]  → Challenge
   │                             │ Cookie handler.HandleChallengeAsync:
   │                             │   "redirect to LoginPath"
   │                             │
   │  302 Found                  │
   │  Location: /Account/Login?ReturnUrl=/api/me
   │<────────────────────────────│
```

## 6. Wire-level: OIDC scheme challenge (your Yandex case)

This is the *exact* moment your BFF kicks off the auth-code + PKCE flow.

```
Browser              BFF                       Yandex
   │                  │                          │
   │ GET /login       │                          │
   │ (action calls Challenge("Yandex"))          │
   │─────────────────>│                          │
   │                  │ OIDC handler.HandleChallengeAsync:
   │                  │   - generate code_verifier
   │                  │   - compute code_challenge = S256(verifier)
   │                  │   - generate state, nonce
   │                  │   - stash them (correlation cookie)
   │                  │   - build authorize URL
   │                  │                          │
   │ 302 Found        │                          │
   │ Location: oauth.yandex.ru/authorize?        │
   │   client_id=...&redirect_uri=...&           │
   │   response_type=code&scope=openid+...&      │
   │   code_challenge=...&code_challenge_method=S256&
   │   state=...&nonce=...                       │
   │<─────────────────│                          │
   │                                             │
   │ GET oauth.yandex.ru/authorize?...           │
   │────────────────────────────────────────────>│
```

So when you read OIDC handler source and see "HandleChallengeAsync" — **that's the box that builds the authorize redirect**. PKCE values are born inside that method.

## 7. When you write Challenge yourself

Almost always one of these:

| Scenario | Code |
|---|---|
| User clicks "Login with Yandex" | `return Challenge(props, "Yandex");` |
| Force re-prompt (step-up auth) | `props.Items["prompt"] = "login";` then `Challenge` |
| Default scheme login button | `return Challenge();` (no scheme = use default) |
| Mix schemes (cookie app, JWT API) on same site | `Challenge` with explicit scheme name |

## 8. Mental model — one sentence

> A **challenge** is the framework asking the configured authentication scheme: *"please write the response that demands login."* The scheme owns the verb; the controller / `[Authorize]` only triggers it.

## 9. Quick anti-confusion checklist

- "I see 302 — is every challenge a 302?" → **No**, only cookie/OIDC. JWT bearer challenge is 401.
- "Why does `Challenge()` not return a token?" → It's not authentication; it's the *demand* for authentication. Tokens come back later through the scheme's callback (`/signin-yandex` → handler exchanges code → cookie issued).
- "What's the difference between `Challenge` and `SignIn`?" → `Challenge` *asks* for credentials. `SignIn` *issues* them (writes the cookie / sets the principal). They're opposite ends of the flow.
- "What's the difference between `Challenge` and `Forbid`?" → Anonymous → Challenge. Authed-but-not-allowed → Forbid. Different HTTP outcomes (login redirect vs 403).
- "Where is the scheme name registered?" → `builder.Services.AddAuthentication().AddOpenIdConnect("Yandex", ...)` — the string `"Yandex"` is what `Challenge("Yandex")` looks up.

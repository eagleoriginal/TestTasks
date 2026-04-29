# ClaimsPrincipal, ClaimsIdentity, Claim — fancy words demystified

Three nouns, one user. They look like layered abstractions invented to feel important, but each has a real reason to exist. Let me unpack the **lineage** of the words and the **factual** structure they describe.

## 1. Where these words come from

| Word | Source | What it meant there |
|---|---|---|
| **Principal** | Kerberos / POSIX security (1980s) | A uniquely-identified actor in an authentication system — a user, a service, a host. *"alice@EXAMPLE.COM"* is a Kerberos principal. |
| **Identity** | Same lineage — a representation of a principal in a particular auth context | An ID card. The principal *has* one or more identities depending on which system issued the card. |
| **Claim** | Claims-based identity (SAML, WS-Trust, ~2005; later JWT/OIDC) | A statement made by an issuer about a subject. *"Yandex says alice's email is alice@y.ru."* |

.NET inherited "principal" / "identity" in v1.0 (`IPrincipal`, `IIdentity`) — simple, single-identity, name + roles. In .NET 4.5, the framework pivoted to **claims-based** identity universally: `ClaimsPrincipal` became the base class for everything, and roles/names became just specific *kinds of claims*. ASP.NET Core inherited that model and never looked back.

## 2. The three boxes — structural relationship

```
ClaimsPrincipal                        ← THE USER (the actor)
   │
   ├── ClaimsIdentity #1               ← an "ID card" issued by some scheme
   │     ├── Claim { Type, Value, Issuer, … }
   │     ├── Claim { Type, Value, Issuer, … }
   │     └── Claim { Type, Value, Issuer, … }
   │
   ├── ClaimsIdentity #2               ← another card from a different scheme
   │     ├── Claim …
   │     └── Claim …
   │
   └── ClaimsIdentity #N
```

A **principal** can hold *multiple* identities. Each **identity** is a bag of **claims** plus the name of the scheme that produced it. Each **claim** is a typed statement plus an issuer.

The "fancy" words map to plain meaning like this:

| Fancy word | Plain reading |
|---|---|
| Principal | "the user, with all the cards in their wallet" |
| Identity | "one card in the wallet — issued by one scheme" |
| Claim | "one fact written on that card, signed by an issuer" |

## 3. `ClaimsPrincipal` — what it actually is in code

```csharp
public class ClaimsPrincipal : IPrincipal
{
    public IEnumerable<ClaimsIdentity> Identities { get; }   // the wallet
    public IIdentity?                  Identity   { get; }   // the "primary" card
    public IEnumerable<Claim>          Claims     { get; }   // ALL claims, flattened across identities
    public bool IsInRole(string role);
    public Claim? FindFirst(string type);
    public IEnumerable<Claim> FindAll(string type);
}
```

Two things to know:

**(a) `Identity` is a derived view, not a stored field.** It returns the first authenticated identity (or the first identity at all if none is authenticated). When you have multiple identities, `Identity.Name` is the "primary" name, but `Claims` iterates over **all** of them.

**(b) `IsInRole(role)`** scans claims of type `ClaimTypes.Role` across **all** identities. Roles are not a separate concept — they're claims with a specific type, and the principal knows to look for that type.

This is why the multi-scheme article said "Authenticate phase merges principals." It actually merges *identities into one principal*. After merge:

```
ClaimsPrincipal
  ├── ClaimsIdentity (AuthenticationType = "Cookies")  → roles, name from cookie
  └── ClaimsIdentity (AuthenticationType = "Bearer")   → scope, sub from JWT
```

`User.IsInRole("admin")` checks both. `User.Claims` flattens both.

## 4. `ClaimsIdentity` — what it actually is

```csharp
public class ClaimsIdentity : IIdentity
{
    public string?              AuthenticationType { get; }   // "Cookies", "Bearer", "Yandex"…
    public bool                 IsAuthenticated    { get; }   // == (AuthenticationType != null)
    public string?              Name               { get; }   // value of the NameClaimType claim
    public IEnumerable<Claim>   Claims             { get; }   // claims on this card
    public string               NameClaimType      { get; }   // which claim type is "the name"
    public string               RoleClaimType      { get; }   // which claim type is "the role"
    public ClaimsIdentity?      Actor              { get; }   // for impersonation/delegation
    public object?              BootstrapContext   { get; }   // raw token (JWT string, SAML XML)
}
```

The non-obvious bits:

- **`IsAuthenticated` is just `AuthenticationType != null`.** If you `new ClaimsIdentity()` with no auth type, it's anonymous regardless of how many claims you add. Schemes always pass their name in.
- **`NameClaimType` / `RoleClaimType`** are configurable per-identity. JWT bearer often points `RoleClaimType` at `"role"` (short form) instead of the long `ClaimTypes.Role` URI. Mismatched config is a common source of "why isn't `IsInRole` working?" bugs.
- **`Actor`** supports the delegation case: a service principal acting *on behalf of* a user. Rarely used outside enterprise scenarios.
- **`BootstrapContext`** can hold the raw token (JWT string) so you can forward it on to downstream services.

## 5. `Claim` — anatomy

```csharp
public class Claim
{
    public string  Type           { get; }   // e.g. "email" or ClaimTypes.Email URI
    public string  Value          { get; }   // e.g. "alice@yandex.ru"
    public string  ValueType      { get; }   // XSD type — usually xs:string
    public string  Issuer         { get; }   // who said it now (e.g. "LOCAL AUTHORITY", "https://oauth.yandex.ru")
    public string  OriginalIssuer { get; }   // who originally said it (when re-issued through a chain)
    public ClaimsIdentity? Subject { get; }  // back-pointer to the identity carrying this claim
    public IDictionary<string,string> Properties { get; }  // arbitrary metadata
}
```

### `Issuer` vs `OriginalIssuer` — the federated-trust subtlety

Imagine the BFF flow:

```
Yandex          BFF                       Browser
  │  id_token (claims signed by Yandex)
  │────────────────────>│
  │                     │ extracts claims
  │                     │ packages them in a NEW ClaimsIdentity("Cookies")
  │                     │ writes encrypted cookie
  │                     │            cookie ─────────────────────>│
  │                     │
  │                     │  next request: cookie comes back
  │                     │  ClaimsIdentity is rehydrated
  │                     │
  │                     │  for an email claim:
  │                     │    Issuer         = "LOCAL AUTHORITY"        ← who's vouching now
  │                     │    OriginalIssuer = "https://oauth.yandex.ru" ← original speaker
```

When you write authz like *"trust the `email_verified` claim only if it came from Yandex,"* `OriginalIssuer` is the field you check.

### Common claim types

| Long URI form (`ClaimTypes.*`) | Short form (JWT/OIDC) | Meaning |
|---|---|---|
| `ClaimTypes.NameIdentifier` | `sub` | Subject — stable user ID |
| `ClaimTypes.Name` | `name` / `preferred_username` | Display name |
| `ClaimTypes.Email` | `email` | Email |
| `ClaimTypes.Role` | `role` | Role |
| `ClaimTypes.GivenName` / `Surname` | `given_name` / `family_name` | First/last |
| (no equivalent) | `scope` | OAuth scopes (space-separated) |
| (no equivalent) | `aud` / `iss` / `exp` | JWT envelope claims |

The `JwtBearer` handler by default **rewrites** short-form JWT claims into the long URI form via `JwtSecurityTokenHandler.InboundClaimTypeMap`. That's why your action sees `ClaimTypes.Name` even though the JWT had `"name"`. You can clear the map to get short forms if you prefer.

## 6. Why multiple identities? Concrete example

`[Authorize(AuthenticationSchemes = "Cookies,Bearer")]` runs both schemes. Each contributes its own card:

```
ClaimsPrincipal (HttpContext.User)
  │
  ├── ClaimsIdentity { AuthenticationType="Cookies",
  │                    Claims = [
  │                      Claim(Name,  "alice"),
  │                      Claim(Role,  "user"),
  │                      Claim(Email, "alice@x.com",   Issuer="LOCAL AUTHORITY",
  │                                                    OriginalIssuer="https://oauth.yandex.ru")
  │                    ] }
  │
  └── ClaimsIdentity { AuthenticationType="Bearer",
                       Claims = [
                         Claim("sub",   "alice"),
                         Claim("scope", "api:read api:write",
                               Issuer="https://my-issuer/")
                       ] }
```

Now:

- `User.Identity` → the first authenticated identity (here: Cookies).
- `User.Identity.Name` → `"alice"`.
- `User.IsInRole("user")` → true (found in the Cookies card).
- `User.Claims` → six claims, both cards flattened.
- `User.FindFirst("scope")?.Value` → `"api:read api:write"` (from the Bearer card).

Both *cards* are present; the principal is the wallet.

## 7. How `HttpContext.User` gets populated — sequence

```
Browser                         BFF
  │ GET /api/me                  │
  │ Cookie: .AspNetCore.Cookies=...│
  │─────────────────────────────>│
  │                              │
  │                  ┌───────────────────────────────┐
  │                  │ UseAuthentication             │
  │                  │  cookie handler.HandleAuthenticate:
  │                  │   - decrypt cookie            │
  │                  │   - deserialize ClaimsPrincipal
  │                  │   - validate expiration       │
  │                  │   - HttpContext.User = principal
  │                  └───────────────────────────────┘
  │                              │
  │                              │ action runs:
  │                              │   var email = User.FindFirst(ClaimTypes.Email)?.Value;
  │                              │   if (User.IsInRole("admin")) ...
```

The cookie is **the serialized `ClaimsPrincipal`**, encrypted with a server key. `User` on every request is the rehydrated principal. There's no DB lookup per request unless you opted into one (event hooks like `OnValidatePrincipal`).

## 8. The legacy `IPrincipal` / `IIdentity` interfaces — still here

Old code expects:

```csharp
IPrincipal user = HttpContext.User;
string? name = user.Identity?.Name;
bool isAdmin = user.IsInRole("admin");
```

It still works because **`ClaimsPrincipal : IPrincipal`** and **`ClaimsIdentity : IIdentity`**. The interfaces are vestigial — they predate claims by a decade — but the framework keeps them so old libraries compile. Any new code should use the claims-based API directly.

## 9. The "fancy → factual" cheat sheet

| Fancy word | Concrete meaning | Code anchor |
|---|---|---|
| Principal | The whole user, possibly carrying multiple ID cards | `ClaimsPrincipal`; lives at `HttpContext.User` |
| Identity | One ID card, issued by one auth scheme | `ClaimsIdentity`; one per scheme that succeeded |
| Claim | One fact on that card | `Claim` (Type + Value + Issuer) |
| AuthenticationType | The scheme that issued the card | string ("Cookies", "Bearer", "Yandex") |
| Issuer | Who's vouching for the claim *right now* | string on `Claim.Issuer` |
| OriginalIssuer | Who *originally* asserted the claim | string on `Claim.OriginalIssuer` |
| Role | A claim with `Type == identity.RoleClaimType` | nothing special — it's just a claim |
| Name | A claim with `Type == identity.NameClaimType` | nothing special — it's just a claim |
| `IsAuthenticated` | "did *any* scheme succeed for this identity?" | `AuthenticationType != null` |
| BootstrapContext | The raw token the claims came from (JWT string, SAML XML) | `ClaimsIdentity.BootstrapContext` |
| Actor | The principal *acting on behalf of* this one (delegation) | `ClaimsIdentity.Actor` |

## 10. The mental model — three lines

> 1. **A principal is a wallet.** It can hold one card or many.
> 2. **An identity is a card.** It says which scheme issued it and carries a list of facts.
> 3. **A claim is a fact on that card,** signed (logically) by an issuer who can be the local app or a federated IDP.

## 11. One-sentence rule

> **`HttpContext.User` is a `ClaimsPrincipal` — a wallet of `ClaimsIdentity` cards, each holding `Claim` facts. Roles and names aren't first-class concepts; they're just claims with conventional types.**

## PS. Why every claim need the issuer
❯ briefely ansfer. why every claim need the issuer, it is not intuitive. more common when the whole identity card is issued by the one issuer, how in one identity card can be facts that was issued by
  different issuers?

● A single card almost always does have a primary issuer — but its claims often don't all come from the same place. Two common reasons:

  1. Local enrichment after federation

  You log in via Yandex. The BFF takes Yandex's claims and adds local facts that Yandex doesn't know about (your subscription tier, internal user ID, app-specific roles). They all live on the same
  ClaimsIdentity ("Cookies") because that's the card the cookie carries — but their origins differ:

  ClaimsIdentity (AuthenticationType = "Cookies")
    ├── Claim(Email, "alice@y.ru",      Issuer = "https://oauth.yandex.ru")
    ├── Claim(Sub,   "yandex|12345",    Issuer = "https://oauth.yandex.ru")
    ├── Claim(Role,  "premium",         Issuer = "LOCAL AUTHORITY")        ← from your DB
    └── Claim("tenant_id", "acme",      Issuer = "LOCAL AUTHORITY")        ← from your DB

  IClaimsTransformation is the standard hook for adding these post-login. The card is one wallet entry; the claims on it have heterogeneous provenance.

  2. Audit / authorization decisions need provenance

  Some checks legitimately depend on who said it, not just what was said. Examples:

  - "Trust email_verified=true only if Yandex asserted it." — your local DB could store email_verified too, but for security decisions you only want the IDP-asserted one.
  - "Allow admin actions only if the role claim came from our internal SSO, not from a federated partner."

  Without per-claim issuer, you couldn't distinguish "Yandex said you have role admin" from "our DB said you have role admin" — and those have very different trust levels.

  So the rule

  The card has one AuthenticationType (the scheme that built it). Its claims can each have their own issuer because real systems mix authoritative sources: federated tokens + local DB + transformations +
  delegated identity. Per-claim issuer is the seam that lets you reason about trust at the right granularity.

  ▎ Identity = where the card came from. Issuer = where each fact on the card came from. They're often the same, but the framework refuses to assume.
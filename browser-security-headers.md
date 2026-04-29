# Browser security headers & attack defenses — concentrated walkthrough

## 1. The mental framework

Every browser security mechanism is one of two flavors:

| Flavor | Purpose | Examples |
|---|---|---|
| **Restrictions baked in** | Default-deny rules the browser always enforces | Same-Origin Policy (SOP) |
| **Opt-in relaxations / extra hardening** | Headers/cookie attributes that *loosen* SOP for legitimate cases or *tighten* default behavior | CORS, CSP, COOP, SameSite, X-Frame-Options, HSTS… |

So the right way to read the topic: **start with SOP**, then for each header/attribute ask *"is this loosening SOP or tightening it further, and against which attack?"*

## 2. Same-Origin Policy (SOP) — the floor

**Origin = scheme + host + port.** `https://app.example.com` and `https://app.example.com:8443` are different origins. `https://example.com` and `https://app.example.com` are different origins.

What SOP **blocks**:
- JS in origin A reading the *response body* of a fetch to origin B (the response leaves the server, but the browser hides it from the script).
- JS in origin A reading the DOM / cookies / localStorage of a window on origin B (`window.opener.document.body` → throws).
- Reading pixels of an `<img src=otherOrigin>` (canvas tainting).

What SOP **does NOT** block:
- *Sending* a request to origin B. Forms can POST anywhere; `<img src=...>`, `<script src=...>`, `<link href=...>` can fetch from anywhere.
- The browser **attaching cookies for origin B** to those requests (the foundation of CSRF).

That second bullet is the entire reason CSRF exists. Memorize it.

## 3. CORS — opt-in to cross-origin **reads**

### The problem CORS solves

You have an API at `api.example.com` and a SPA at `app.example.com`. SOP forbids the SPA's JS from reading the API's response. CORS lets the API say "*I authorize app.example.com to read me*."

### Wire-level: simple request

```
Browser                 api.example.com
   │ GET /users          │
   │ Origin: https://app.example.com
   │────────────────────>│
   │ 200 OK              │
   │ Access-Control-Allow-Origin: https://app.example.com
   │ Content-Type: application/json
   │ {…}                 │
   │<────────────────────│
   │
   │ Browser sees ACAO matches Origin → JS gets to read body.
   │ If header missing or doesn't match → fetch().then(r => r.json()) throws.
```

### Wire-level: preflighted request

Triggered when the request is *not simple* (e.g. method is `PUT`/`DELETE`, or `Content-Type: application/json`, or custom headers, or credentials are involved).

```
Browser                 api.example.com
   │ OPTIONS /users        ← the preflight
   │ Origin: https://app.example.com
   │ Access-Control-Request-Method: PUT
   │ Access-Control-Request-Headers: Content-Type, X-CSRF-Token
   │────────────────────────────────────>│
   │                                     │
   │ 204 No Content                      │
   │ Access-Control-Allow-Origin: https://app.example.com
   │ Access-Control-Allow-Methods: PUT, POST
   │ Access-Control-Allow-Headers: Content-Type, X-CSRF-Token
   │ Access-Control-Allow-Credentials: true
   │ Access-Control-Max-Age: 3600
   │<────────────────────────────────────│
   │
   │ Browser caches the policy for Max-Age. Then the real request:
   │
   │ PUT /users
   │ Cookie: session=...   (because credentials=include)
   │────────────────────────────────────>│
```

### What CORS does NOT do — read this twice

- **CORS does not prevent the request from being sent.** Side effects on the server already happen by the time the browser refuses to expose the response to JS.
- **CORS does not protect non-JS requests** (form POST, `<img>`). Those have always been allowed cross-origin and CORS doesn't apply.
- **Therefore CORS is not CSRF protection.** People conflate them constantly. CORS is a read-permission system; CSRF defenses are about preventing *writes* triggered by the attacker.

### Cookies + CORS

- Server must set `Access-Control-Allow-Credentials: true`
- `Access-Control-Allow-Origin` must NOT be `*` — must be the literal origin
- Client must opt in: `fetch(url, { credentials: 'include' })` or `xhr.withCredentials = true`

## 4. CSRF / XSRF — what CORS doesn't fix

### The attack

```
User is logged in to bank.com (cookie set).
Visits attacker.com which serves:

   <form action="https://bank.com/transfer" method="POST">
       <input name="to"     value="attacker">
       <input name="amount" value="1000000">
   </form>
   <script>document.forms[0].submit()</script>

Browser POSTs to bank.com WITH the user's bank cookie attached.
Server sees an authenticated request → transfers money.
```

CORS doesn't prevent this — form POST is allowed cross-origin and the response (which the attacker doesn't need) is hidden, but the *side effect* already happened.

### Three layered defenses

**(a) `SameSite` cookie attribute** (browser-side, automatic)

| Value | Behavior |
|---|---|
| `Strict` | Cookie sent **only** when the site of the request matches the cookie's site. Even top-level link clicks from another site arrive without it (so a logged-in user clicking a link to your site sees a logged-out page on first load — UX hit). |
| `Lax` (modern default) | Cookie sent on **top-level GET navigations** (clicking a link) but **not** on cross-site subresource requests (form POST, fetch, iframe, image). |
| `None` | Sent everywhere. **Must** be `Secure` (HTTPS only). |

`SameSite=Lax` (the modern default) defeats the CSRF form-POST attack outright because the cookie isn't attached.

**(b) CSRF / anti-forgery token** (app-side)

Server issues a per-session secret; client must echo it on writes. Two common shapes:

- **Hidden form field**: server renders `<input name="__RequestVerificationToken" value="...">`, validates on POST.
- **Double-submit cookie**: server sets a cookie with a random value; client JS reads it and sends it back in a custom header (`X-CSRF-Token`). Attacker's site can't read the cookie (SOP), can't set the custom header on a cross-origin form (only fetch can, which triggers preflight, which would fail).

ASP.NET Core: `services.AddAntiforgery()` + `[ValidateAntiForgeryToken]` (MVC) or `IAntiforgery` injected (minimal API).

**(c) Origin / Referer check**

On state-changing requests, server verifies `Origin` (or `Referer`) header matches expected. Cheap; useful as defense-in-depth.

### Layered defense diagram

```
Attacker site → POST bank.com/transfer
                            │
            ┌───────────────┴────────────────┐
            ▼                                ▼
  SameSite=Lax cookie           No SameSite (or =None)
  → cookie not attached         → cookie attached
  → server sees anonymous req   → server sees authed
  → reject as 401                  │
                                   ▼
                          CSRF token check
                          → token missing/invalid
                          → 403
                                   │
                                   ▼
                          Origin header check
                          → mismatch
                          → 403
```

## 5. Cookie attributes — the three knobs that matter

```
Set-Cookie: session=abc123; Secure; HttpOnly; SameSite=Lax; Path=/; Max-Age=3600
            ^^^^^^^^^^^^^^  ^^^^^^  ^^^^^^^^  ^^^^^^^^^^^^^
            value           HTTPS   no JS     CSRF defense
                            only    access
```

| Attribute | Defends against |
|---|---|
| `Secure` | Sniffing on plain HTTP, MITM downgrade |
| `HttpOnly` | XSS-driven cookie exfiltration (`document.cookie` returns "" for httponly cookies) |
| `SameSite=Lax\|Strict` | CSRF |
| `__Host-` / `__Secure-` prefix | Subdomain confusion (browser enforces strict rules on cookies with these prefixes) |

A session cookie with all three (`Secure; HttpOnly; SameSite=Lax`) is the modern baseline.

## 6. CSP — Content Security Policy

### What it defends

Primarily **XSS**. CSP doesn't prevent injection; it limits *what an injection can do*.

### Wire-level

```
HTTP/1.1 200 OK
Content-Security-Policy:
   default-src 'self';
   script-src 'self' 'nonce-r4nd0m' https://cdn.example.com;
   style-src  'self' 'unsafe-inline';
   img-src    'self' data: https:;
   connect-src 'self' https://api.example.com;
   frame-ancestors 'self';
   form-action 'self';
   base-uri 'self';
   object-src 'none';
```

### Key directives

| Directive | Controls |
|---|---|
| `default-src` | Fallback for unspecified resource types |
| `script-src` | What JS may load/execute |
| `style-src` | Stylesheets, inline `<style>` |
| `img-src`, `media-src`, `font-src` | Media |
| `connect-src` | `fetch`, `XHR`, `WebSocket`, `EventSource` targets |
| `frame-src` / `child-src` | What can be embedded in *your* iframes |
| `frame-ancestors` | Who can iframe *you* (replaces `X-Frame-Options`) |
| `form-action` | Where forms can submit to |
| `base-uri` | What `<base>` can be set to (defends a sneaky redirect attack) |
| `object-src 'none'` | Disable Flash/plugins (always include) |
| `report-uri` / `report-to` | Where the browser sends violation reports |

### Inline-script handling — the hard part

Default-deny inline scripts. To allow specific ones:

- **Nonce**: server emits `script-src 'nonce-RANDOM'`, only `<script nonce="RANDOM">` runs. New nonce per response.
- **Hash**: `'sha256-...'` for static inline scripts.
- **`'strict-dynamic'`**: any nonced script may load other scripts — useful for module loaders. Drops the host-allowlist requirement.
- **`'unsafe-inline'`**: opens the door wide; avoid except for styles when truly necessary.
- **`'unsafe-eval'`**: allows `eval()`, `new Function()`. Avoid.

### CSP in practice

CSP is the single highest-leverage XSS mitigation. A correct CSP turns most XSS bugs from "session theft" into "broken functionality." Pair with **Trusted Types** (`require-trusted-types-for 'script'`) to also block DOM-XSS sinks.

## 7. Clickjacking — `frame-ancestors` / `X-Frame-Options`

### The attack

```
Attacker page:
   <iframe src="bank.com/transfer-page" style="opacity:0.01"></iframe>
   <button style="position:absolute; ...">CLICK ME TO WIN</button>

User clicks "CLICK ME" but actually clicks the (transparent, on top) bank
button. Bank cookie is attached because it's a top-level navigation
inside the iframe — and (until SameSite=Lax was the default) the click
went through.
```

### Defense

Modern: **`Content-Security-Policy: frame-ancestors 'self'`** — only same-origin can iframe.

Legacy header (still respected, weaker): **`X-Frame-Options: DENY`** or `SAMEORIGIN`.

Use both for older-browser coverage. SameSite=Lax cookies *also* mitigate this for state-changing actions.

## 8. COOP / COEP / CORP — the cross-origin isolation trio

### COOP — Cross-Origin Opener Policy

Defends against cross-origin window-handle attacks and side-channel leaks via shared browsing-context groups.

```
HTTP/1.1 200 OK
Cross-Origin-Opener-Policy: same-origin
```

| Value | Effect |
|---|---|
| `unsafe-none` (default) | Any popup keeps a `window.opener` reference back to you |
| `same-origin-allow-popups` | Cross-origin popups you open lose the back-reference; same-origin ones keep it |
| `same-origin` | Severs the link to **all** cross-origin windows in either direction. Browser puts you in your own browsing context group (often: own process). |

### What COOP prevents

- **Tabnabbing**: a popup you opened (`window.open(badsite)`) doing `window.opener.location = "phishing-site"` — without COOP, your tab silently navigates to a phishing page.
- **Cross-origin reference leakage**: `window.opener.length`, frame counts, and similar side channels.
- **Spectre-class leaks**: when paired with COEP, you become "cross-origin isolated" and the browser puts you in a process not shared with cross-origin frames.

### COEP — Cross-Origin Embedder Policy

```
Cross-Origin-Embedder-Policy: require-corp
```

Forces every subresource (image, script, font, fetch) to either be:
- Same-origin, or
- Opted in via `Cross-Origin-Resource-Policy: cross-origin` on the response, or
- Served with `Access-Control-Allow-Origin` (CORS).

If a subresource doesn't opt in, the browser refuses to load it. This is strict and breaks many third-party embeds — but the payoff is **cross-origin isolation**.

### CORP — Cross-Origin Resource Policy

```
Cross-Origin-Resource-Policy: same-origin | same-site | cross-origin
```

A *server-side* declaration: "who is allowed to embed/load me?" CORP defends against **Spectre-style cross-origin reads** (the attacker page loads your resource as `<img>` and reads it via side channel) — set `same-origin` on private resources to block embedding entirely.

### Why you'd want all three

```
COOP (same-origin) + COEP (require-corp) ⇒ "cross-origin isolated"
                                            │
                                            ▼
                  Unlocks SharedArrayBuffer, performance.now() at high
                  resolution, and other features the platform disabled
                  after Spectre/Meltdown.
```

If you don't need those APIs, **COOP alone** still has real value (tabnabbing prevention). COEP-`require-corp` is much more disruptive — only adopt when you need isolation.

## 9. HSTS — Strict-Transport-Security

```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

Tells the browser: *"for the next year, never speak HTTP to me — always upgrade to HTTPS, even if the user types `http://`."* Defends against:

- First-visit downgrade MITM (with `preload`, registered in browsers' built-in lists)
- SSL-stripping proxies
- Cookie theft on misconfigured plain-HTTP endpoints

## 10. Smaller siblings — also worth having

| Header | Purpose |
|---|---|
| `Referrer-Policy: strict-origin-when-cross-origin` | Stops leaking full URLs (and any tokens in them) to third parties |
| `Permissions-Policy: camera=(), microphone=(), geolocation=()` | Disables sensitive APIs for the page and its iframes |
| `X-Content-Type-Options: nosniff` | Browser must respect declared `Content-Type` (defends MIME-confusion attacks) |
| Subresource Integrity (`<script integrity="sha384-...">`) | CDN-served file must hash to expected value or the browser refuses to execute |
| Trusted Types (`require-trusted-types-for 'script'` in CSP) | Eliminates DOM-XSS by forcing a typed object at every dangerous sink |

## 11. Attack → defense matrix

| Attack | Primary defense | Supporting defenses |
|---|---|---|
| **Cross-origin response read by JS** | SOP (always on); CORS (to opt in legitimately) | — |
| **CSRF (forged write)** | `SameSite=Lax\|Strict` cookie | CSRF token, Origin/Referer check |
| **XSS (script injection)** | CSP (`script-src` lockdown + nonces) | Output encoding, Trusted Types, `HttpOnly` cookies |
| **Clickjacking** | CSP `frame-ancestors 'self'` | `X-Frame-Options: DENY`, SameSite cookies |
| **Tabnabbing / opener attacks** | COOP `same-origin` | `rel="noopener noreferrer"` on `<a target=_blank>` |
| **Spectre cross-origin reads** | COOP + COEP (cross-origin isolation) | CORP on private resources |
| **MITM / SSL-strip** | HSTS + `Secure` cookies | Preload list |
| **Cookie theft via XSS** | `HttpOnly` cookies | CSP |
| **Compromised CDN serves bad JS** | SRI (`integrity=`) | CSP `script-src` allowlist |
| **Referer leak of session token in URL** | `Referrer-Policy` | Don't put tokens in URLs |
| **MIME confusion** | `X-Content-Type-Options: nosniff` | Strict `Content-Type` |
| **Unwanted device API access from iframe** | `Permissions-Policy` | — |

## 12. A reasonable baseline header set for a BFF + SPA

```
Strict-Transport-Security:        max-age=31536000; includeSubDomains; preload
Content-Security-Policy:          default-src 'self';
                                  script-src 'self' 'nonce-{N}';
                                  style-src 'self';
                                  img-src 'self' data:;
                                  connect-src 'self' https://api.example.com;
                                  frame-ancestors 'none';
                                  form-action 'self';
                                  base-uri 'self';
                                  object-src 'none';
                                  require-trusted-types-for 'script';
X-Frame-Options:                  DENY
X-Content-Type-Options:           nosniff
Referrer-Policy:                  strict-origin-when-cross-origin
Permissions-Policy:               camera=(), microphone=(), geolocation=()
Cross-Origin-Opener-Policy:       same-origin
Cross-Origin-Resource-Policy:     same-origin

Set-Cookie: .AspNetCore.Cookies=...; Secure; HttpOnly; SameSite=Lax; Path=/
```

Add `Cross-Origin-Embedder-Policy: require-corp` only if you actually need `SharedArrayBuffer` / high-res timers — it breaks third-party embeds aggressively.

## 13. Pipeline view — where each header sits in the request/response

```
                 Browser                                Your BFF
                    │                                      │
        ┌───────────┴─────────────┐                        │
        │ Same-Origin Policy:     │                        │
        │ enforced on every       │                        │
        │ cross-origin read.      │                        │
        │ COOP cuts opener links. │                        │
        └───────────┬─────────────┘                        │
                    │                                      │
                    │  fetch() with credentials:'include'  │
                    │  Cookie: session=...  (gated by      │
                    │     SameSite, Secure, HttpOnly       │
                    │     attached by browser based on     │
                    │     cookie attributes)               │
                    │                                      │
                    │  (preflight if non-simple)           │
                    │─────────────────────────────────────>│
                    │                                      │
                    │             Server checks:           │
                    │             - CORS: Origin allowed?  │
                    │             - CSRF token valid?      │
                    │             - Origin/Referer match?  │
                    │             - Auth cookie valid?     │
                    │                                      │
                    │  Response:                           │
                    │     Access-Control-Allow-*           │
                    │     CSP, COOP, COEP, CORP            │
                    │     HSTS, Referrer-Policy, etc.      │
                    │     Set-Cookie (Secure;HttpOnly;     │
                    │       SameSite=Lax)                  │
                    │<─────────────────────────────────────│
                    │                                      │
        ┌───────────┴─────────────┐                        │
        │ Browser enforces:       │                        │
        │  - CORS (gates JS read) │                        │
        │  - CSP  (gates exec/load)                        │
        │  - frame-ancestors      │                        │
        │  - cookie attributes    │                        │
        │  - HSTS upgrade memory  │                        │
        └─────────────────────────┘                        │
```

## 14. One-sentence rules

> 1. **SOP is the floor.** Every other mechanism is either an opt-in relaxation (CORS) or an opt-in tightening (CSP, COOP, SameSite, etc.).
>
> 2. **CORS gates *reading* responses; it does not stop *sending* requests.** Use SameSite + CSRF tokens for write protection, not CORS.
>
> 3. **Cookies are dangerous because they're attached automatically.** `Secure; HttpOnly; SameSite=Lax` on every session cookie, no exceptions.
>
> 4. **CSP is the highest-leverage anti-XSS measure** even when you have output encoding — it limits the blast radius when (not if) something slips through.
>
> 5. **COOP `same-origin` is cheap and worth setting** even without full cross-origin isolation; it kills tabnabbing for free.

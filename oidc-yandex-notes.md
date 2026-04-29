# OIDC, OAuth 2.0 + PKCE, and Yandex — protocol-level notes

Reference notes on how OpenID Connect, OAuth 2.0, and PKCE relate, using Yandex as a concrete provider, for a browser SPA + ASP.NET Core (BFF) target architecture. Focus is on **what is exchanged** at each step, not setup code.

---

## 1. Quick framing

These aren't competing flows — they stack:

- **OAuth 2.0** = authorization framework ("can this app do X on behalf of user").
- **PKCE** = a *security extension* to OAuth 2.0's Authorization Code Flow (not a separate flow).
- **OpenID Connect (OIDC)** = an *identity layer on top* of OAuth 2.0 — adds `id_token` (a signed JWT that proves "who" the user is) and a standardized `/userinfo` endpoint.

So the real question is "OAuth 2.0 (Authorization Code + PKCE) vs OIDC (Authorization Code + PKCE + id_token)". PKCE is used in **both**.

**Yandex note:** historically Yandex was OAuth 2.0 only — no `id_token`; you call `https://login.yandex.ru/info` to learn who the user is. Yandex ID has been adding OIDC (`id_token`, discovery doc), but the classic integration most tutorials show is plain OAuth 2.0 + userinfo call. The flow below works for both; OIDC just additionally returns an `id_token` you can validate locally.

---

## 2. Architecture choice for SPA + ASP.NET Core

Two options:

1. **SPA as public client** — SPA itself does PKCE, stores `access_token` in memory. ASP.NET Core is just an API that validates tokens. Simpler conceptually, but tokens live in the browser.
2. **BFF (Backend-for-Frontend)** — ASP.NET Core does the OAuth dance, exchanges code for tokens, stores tokens server-side, hands the SPA a plain HTTP-only **session cookie**. SPA never sees Yandex tokens. Modern recommendation for SPA + same-origin backend.

The diagram below shows the **BFF flow**. PKCE is still used even though the backend is "confidential" — belt-and-suspenders.

---

## 3. The full exchange (BFF + PKCE + Yandex)

```
┌──────┐     ┌─────┐     ┌──────────────┐                  ┌────────┐
│ User │     │ SPA │     │ ASP.NET BFF  │                  │ Yandex │
└──┬───┘     └──┬──┘     └──────┬───────┘                  └───┬────┘
   │ click login│               │                              │
   │───────────>│ GET /login    │                              │
   │            │──────────────>│                              │
   │            │               │ generate:                    │
   │            │               │  • state (CSRF nonce)        │
   │            │               │  • code_verifier (random 43+)│
   │            │               │  • code_challenge =          │
   │            │               │     BASE64URL(SHA256(verif)) │
   │            │               │ store verifier+state in      │
   │            │               │ server-side session          │
   │            │  302 Location:│                              │
   │            │<──────────────│                              │
   │                                                            │
   │     browser follows 302 to:                                │
   │     https://oauth.yandex.ru/authorize?                     │
   │        response_type=code                                  │
   │        client_id=YOUR_APP_ID                               │
   │        redirect_uri=https://your.app/signin-yandex         │
   │        scope=login:email login:info                        │
   │        state=XYZ                                           │
   │        code_challenge=ABC                                  │
   │        code_challenge_method=S256                          │
   │───────────────────────────────────────────────────────────>│
   │                                                            │
   │   Yandex login UI; user types Yandex creds, approves app  │
   │<══════════════════════════════════════════════════════════>│
   │                                                            │
   │   302 to https://your.app/signin-yandex?code=AUTH_CODE&    │
   │       state=XYZ                                            │
   │<───────────────────────────────────────────────────────────│
   │            │   browser hits /signin-yandex                 │
   │            │──────────────>│                              │
   │                            │ verify state matches stored  │
   │                            │                              │
   │                            │ POST oauth.yandex.ru/token   │
   │                            │  grant_type=authorization_code
   │                            │  code=AUTH_CODE              │
   │                            │  client_id=...               │
   │                            │  client_secret=...  ◄─ secret│
   │                            │  code_verifier=...  ◄─ PKCE  │
   │                            │  redirect_uri=...            │
   │                            │─────────────────────────────>│
   │                            │                              │
   │                            │ { access_token, expires_in,  │
   │                            │   refresh_token,             │
   │                            │   token_type:"bearer"        │
   │                            │   [, id_token if OIDC] }     │
   │                            │<─────────────────────────────│
   │                            │                              │
   │                            │ GET login.yandex.ru/info     │
   │                            │ Authorization: OAuth <token> │
   │                            │─────────────────────────────>│
   │                            │ { id, login, default_email,  │
   │                            │   real_name, ... }           │
   │                            │<─────────────────────────────│
   │                            │                              │
   │                            │ upsert local user by         │
   │                            │ external_provider="yandex" + │
   │                            │ external_id=id               │
   │                            │ issue ASP.NET auth cookie    │
   │   302 to / + Set-Cookie: .AspNetCore.Cookies=...; HttpOnly│
   │<───────────│<──────────────│                              │
   │            │                                              │
   │   subsequent SPA→BFF calls auto-send cookie; BFF can also │
   │   use stored access_token to call Yandex APIs on behalf   │
```

---

## 4. What you give vs. what you get — Yandex specifics

**One-time, at oauth.yandex.com (registration):**
- You give: app name, redirect URI, requested scopes
- Yandex gives: `client_id` (public), `client_secret` (server-only)

**On the authorize URL (browser → Yandex):**
- You give: `client_id`, `redirect_uri`, `response_type=code`, `scope`, `state`, `code_challenge`, `code_challenge_method=S256`
- Yandex gives back (via redirect to your callback): `code` (one-shot, ~10 min TTL), `state` (you must verify it matches)

**On the token endpoint (server → Yandex, back-channel POST):**
- You give: `grant_type=authorization_code`, `code`, `client_id`, `client_secret`, `code_verifier` (proves you started the flow), `redirect_uri`
- Yandex gives: `access_token` (long-lived for Yandex, ~1 yr), `refresh_token`, `expires_in`, `token_type=bearer`, optionally `id_token` if OIDC

**On /info (server → Yandex with access_token):**
- You give: `Authorization: OAuth <access_token>` header
- Yandex gives: user profile JSON — `id`, `login`, `default_email`, `real_name`, `display_name`, etc. The `id` is your **stable external user identifier**.

---

## 5. Why PKCE matters

Without PKCE, if an attacker intercepts the `code` (malicious browser extension, log leak, redirect-uri shenanigans), they could exchange it for tokens. With PKCE:

1. Client generates `code_verifier` (random secret, kept locally).
2. Sends only `code_challenge = SHA256(verifier)` in the authorize URL.
3. To redeem the code at `/token`, client must present the original `verifier`.

Attacker sees `code` and `code_challenge` but not `code_verifier`, so the stolen code is useless.

---

## 6. Mental model summary

| Concept | Purpose | Required? |
|---|---|---|
| OAuth 2.0 Authorization Code | Get a token that lets server act on user's behalf | Yes |
| PKCE | Protect the code from interception | Yes — recommended even with confidential client |
| OIDC `id_token` | Standardized "who is this user" JWT, locally verifiable | Optional with Yandex; the `/info` call replaces it |
| `state` parameter | CSRF protection on the redirect | Yes, always |
| Refresh token | Get new access tokens without re-prompting user | Optional, only if BFF needs to keep calling Yandex APIs later |

---

## 7. What happens when the BFF cookie expires? (silent re-auth)

Short answer: **Yandex doesn't actually need to re-ask consent because Yandex itself remembers two things in its own cookies on `*.yandex.ru`** — (1) "this browser is already logged into a Yandex account" and (2) "this Yandex account already granted these scopes to this `client_id`". Your BFF cookie expiring has no relationship to those Yandex-side cookies.

### The three cookie domains in play

```
┌──────────────────────────────────────────────────────────────────────┐
│ Browser cookie jar                                                   │
├──────────────────────────────────────────────────────────────────────┤
│  yourapp.com                                                         │
│    .AspNetCore.Cookies   ← BFF session (the one we worry about)      │
│                                                                      │
│  .yandex.ru / oauth.yandex.ru                                        │
│    Session_id, yandex_login, ...   ← Yandex's OWN session            │
│    (set when user originally logged into Yandex; lives for           │
│     weeks/months independent of your app)                            │
└──────────────────────────────────────────────────────────────────────┘
```

These are **completely separate**. Your BFF cookie expiring is a `yourapp.com` problem. Yandex still knows who the user is via its own `*.yandex.ru` cookies.

### Four scenarios when SPA reloads

#### Scenario 1: BFF cookie still valid → no Yandex involved at all

```
SPA → GET /api/me  (cookie sent)
BFF: cookie valid, return user
```

Done. Yandex isn't touched. Most page loads.

#### Scenario 2: BFF cookie expired, Yandex session alive, consent already granted → silent re-login

```
SPA → GET /api/me                                  → 401
SPA → window.location = "/login"
BFF: 302 to oauth.yandex.ru/authorize?...&prompt=none(*)
                                                      │
browser follows redirect to yandex.ru, AUTOMATICALLY  │
sending its Session_id cookie because it's on         │
the yandex.ru domain                                  ▼
Yandex checks:                              ┌─────────────────┐
  • is Session_id valid? YES                │  Yandex servers │
  • is this client_id pre-approved          │                 │
    for these scopes by this user? YES      │  decision: skip │
  • → no UI shown                           │  login & consent│
                                            └────────┬────────┘
302 back to https://yourapp.com/signin-yandex?code=… │
                                                      ▼
BFF: POST /token, GET /info, issue new .AspNetCore.Cookies
302 back to SPA → SPA reloads, /api/me works
```

User experience: a brief flash of redirects, maybe 300 ms. **No login screen, no consent screen.** Looks like a page refresh.

(*) `prompt=none` is the OIDC standard parameter that says "fail rather than show UI". Yandex's classic OAuth doesn't formally honor it, but the silent re-auth happens naturally whenever both conditions are met — Yandex just won't show UI when it doesn't have to.

#### Scenario 3: BFF cookie expired, Yandex session expired, but app was approved before

```
... same redirect to oauth.yandex.ru/authorize ...
Yandex sees: no Session_id cookie
  → shows Yandex login page (username/password or QR)
User logs into Yandex
  → Yandex sees client_id is pre-approved by this account
  → SKIPS consent screen
  → 302 back with code
```

User sees a Yandex login screen but **not** a consent screen.

#### Scenario 4: First time ever (or app was de-authorized in Yandex settings)

```
Yandex login → Yandex consent screen ("App Foo wants access to your
                email, name, ..." with [Allow] [Deny] buttons)
              → 302 back with code
```

The consent prompt is **per-(account, client_id, scope-set)** and is sticky on Yandex's side. Yandex stores it under "Connected apps" in the user's Yandex account settings; the user can revoke it, which forces scenario 4 again next time.

### Why this is safe and not "skipping security"

It feels like Yandex is "letting them in without asking", but every guardrail still holds:

- **Authentication** — proven by Yandex's own session cookie on its own domain. Only Yandex can read it. Your app never sees it.
- **Authorization** — proven by the user's previously-recorded consent for that exact `client_id` + scopes.
- **PKCE** — still happening. Your BFF still generates a fresh `code_verifier`/`code_challenge` per attempt. The redirect-back code is still single-use.
- **state** — still validated against your server-side session.

What's being remembered is *the user's prior decision*, not a credential. The user already said "yes, this app can have my email and name forever, until I revoke it." Re-prompting them every time would be UX noise without any security benefit.

### How to influence it deliberately

Sometimes you *want* to force the dialogs:

| Goal | How |
|---|---|
| Force the user to re-enter Yandex password | OIDC standard: `prompt=login`. Yandex equivalent: typically have the user log out of Yandex first; `force_confirm=yes` does not force password re-entry. |
| Force the consent screen again (e.g. you added a new scope) | Yandex: append `force_confirm=yes` to the authorize URL. Standard OIDC: `prompt=consent`. |
| Let user pick a different Yandex account | Yandex: send them to `https://passport.yandex.ru/auth/list` first, or `force_confirm=yes` shows the account picker. Standard OIDC: `prompt=select_account`. |
| Pure silent refresh, fail if any UI needed | Standard OIDC: `prompt=none`. Useful for hidden-iframe silent renewal in pure-SPA flows. Not really applicable to the BFF pattern since the BFF already has its own session lifecycle. |

---

## 8. A subtler BFF-pattern tip

In the BFF pattern you usually **don't** want the BFF cookie's lifetime tied to Yandex's `access_token` lifetime. Decouple them:

- **BFF cookie:** e.g. 8h sliding session in ASP.NET Core (`CookieAuthenticationOptions.ExpireTimeSpan` + `SlidingExpiration = true`). When it expires, scenario 2 above kicks in and the user re-enters silently.
- **`access_token` / `refresh_token`:** only relevant if your BFF actually calls Yandex APIs on behalf of the user later. If you only use Yandex for login (just to learn "who"), you can throw the tokens away after the `/info` call and rely purely on your own cookie afterwards.

If you don't need to call Yandex APIs after login, **the simplest mental model is: Yandex is used once to identify the user, then your app is on its own.** Subsequent re-logins just re-confirm identity through the silent flow above.

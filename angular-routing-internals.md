# Angular Routing Internals: URLs, UrlTree & ActivatedRoute

A framework-level refresher on *how Angular models a URL*. This is pure Angular — no
project specifics. It answers: what is a URL made of, how is it parsed into a `UrlTree`,
what does that tree actually look like in memory, and how does it become the
`ActivatedRoute` your components read.

> For how *this app* builds on these primitives (the tab registry, the custom reuse
> strategy), see [`routing-overhead.md`](./routing-overhead.md).

## Table of contents

1. [The three representations of a URL](#1-the-three-representations-of-a-url)
2. [Anatomy of a URL string](#2-anatomy-of-a-url-string)
3. [The UrlTree class family](#3-the-urltree-class-family)
4. [A worked example: string → tree](#4-a-worked-example-string--tree)
5. [How a URL is compiled (parse / serialize)](#5-how-a-url-is-compiled-parse--serialize)
6. [From UrlTree to RouterState (matching)](#6-from-urltree-to-routerstate-matching)
7. [RouterState & RouterStateSnapshot (the containers)](#7-routerstate--routerstatesnapshot-the-containers)
8. [ActivatedRoute & ActivatedRouteSnapshot](#8-activatedroute--activatedroutesnapshot)
9. [params vs queryParams vs matrix params vs data](#9-params-vs-queryparams-vs-matrix-params-vs-data)
10. [Param & data inheritance (componentless & empty-path routes)](#10-param--data-inheritance-componentless--empty-path-routes)
11. [Building URLs with createUrlTree](#11-building-urls-with-createurltree)
12. [Why it feels heavy (gotchas)](#12-why-it-feels-heavy-gotchas)
13. [Cheat-sheet](#13-cheat-sheet)

---

## 1. The three representations of a URL

The single most useful mental model: a "URL" in Angular exists in **three forms**, and the
router constantly converts between them.

```
  ┌─────────────────┐   parse / serialize   ┌──────────────┐   recognize / match   ┌────────────────────┐
  │  URL string     │  ◄─────────────────►  │   UrlTree    │  ──────────────────►  │   RouterState      │
  │  (address bar)  │   (UrlSerializer)     │ (parsed tree)│   (against Routes)    │ (ActivatedRoute …) │
  └─────────────────┘                       └──────────────┘                       └────────────────────┘
        text                              data structure (segments)              what components consume
```

- **URL string** — what's in the browser address bar: `"/users/42;tab=info?sort=name#top"`.
- **`UrlTree`** — the parsed, structured form. A tree of segment groups + query params +
  fragment. *No knowledge of your route config yet* — it's purely syntactic.
- **`RouterState` / `RouterStateSnapshot`** — the result of matching the `UrlTree` against
  your `Routes`. This is a tree of `ActivatedRoute(Snapshot)` nodes, one per matched route
  level, carrying the resolved `params`, `data`, resolved `component`, etc.

Navigation flows left→right; building a link (`createUrlTree`) flows right→left and ends
with serialization back to a string. Everything in the "overhead" is one of these
conversions.

---

## 2. Anatomy of a URL string

Angular's URL grammar is richer than "slash-separated path". A full URL can contain:

```
/segment1;mk=mv/segment2/42(outlet:aux/path//other:more)?q1=a&q2=b#fragment
 └──────┬─────┘ └────────┬───────┘ └──────────┬───────────┘ └────┬────┘ └───┬──┘
   path segment      path segment        named outlets        query params  fragment
   + matrix param                       (auxiliary routes)    (whole URL)
```

| Piece | Syntax | Scope | Becomes |
| --- | --- | --- | --- |
| **Path segment** | `users`, `42` | one level of the tree | `UrlSegment.path` |
| **Matrix parameters** | `;key=value` after a segment | that single segment | `UrlSegment.parameters` |
| **Auxiliary / named outlets** | `(name:path//name2:path2)` | child segment groups | `UrlSegmentGroup.children[name]` |
| **Query parameters** | `?k=v&k2=v2` | the entire URL | `UrlTree.queryParams` |
| **Fragment** | `#anchor` | the entire URL | `UrlTree.fragment` |

Two things that surprise people:

- **Matrix params** (`;k=v`) belong to *one segment* and live *inside the path*, unlike
  query params which belong to the whole URL. They survive across child navigations within
  that segment group.
- **Named outlets** in parentheses let one URL drive multiple `<router-outlet>`s at the
  same level. The unnamed outlet is called **`primary`** (`PRIMARY_OUTLET === 'primary'`).

---

## 3. The UrlTree class family

Three classes do all the structural work. Simplified signatures:

```ts
class UrlTree {
  root: UrlSegmentGroup;                 // the top of the tree (see note below)
  queryParams: Params;                   // { [k: string]: any } — whole-URL query params
  fragment: string | null;               // the part after '#'
  readonly queryParamMap: ParamMap;      // typed accessor over queryParams
  toString(): string;                    // serialize back to a URL string
}

class UrlSegmentGroup {
  segments: UrlSegment[];                          // path segments at THIS level
  children: { [outletName: string]: UrlSegmentGroup };  // child groups, keyed by outlet
  parent: UrlSegmentGroup | null;
  hasChildren(): boolean;
  readonly numberOfChildren: number;
}

class UrlSegment {
  path: string;                          // the segment text, e.g. 'users'
  parameters: { [name: string]: string };// matrix params on this segment (;k=v)
  readonly parameterMap: ParamMap;
}
```

And the param accessors:

```ts
type Params = { [key: string]: any };

interface ParamMap {            // safer, typed wrapper around a Params object
  has(name: string): boolean;
  get(name: string): string | null;     // single value (or null)
  getAll(name: string): string[];       // all values (arrays come from repeated keys)
  readonly keys: string[];
}
// convertToParamMap(params) builds one from a raw Params object.
```

> **Crucial subtlety — the root group is (almost) always empty.** For any non-empty URL,
> `UrlTree.root.segments` is `[]` and the real path lives one level down under
> `root.children[PRIMARY_OUTLET]`. So to read the first path segment you go
> `tree.root.children['primary'].segments[0]`, **not** `tree.root.segments[0]`. This trips
> up almost everyone the first time.

---

## 4. A worked example: string → tree

Take this URL:

```
/users;role=admin/42(sidebar:notes)?sort=name&dir=asc#section3
```

It parses into this object graph:

```
UrlTree
├─ root: UrlSegmentGroup           // root: empty segments, single 'primary' child
│    segments: []
│    children:
│      primary: UrlSegmentGroup
│         segments:
│           ├─ UrlSegment { path: 'users', parameters: { role: 'admin' } }
│           └─ UrlSegment { path: '42',    parameters: {} }
│         children:
│           sidebar: UrlSegmentGroup           // the (sidebar:notes) auxiliary outlet
│              segments:
│                └─ UrlSegment { path: 'notes', parameters: {} }
│              children: {}
├─ queryParams: { sort: 'name', dir: 'asc' }   // belong to the whole tree
└─ fragment: 'section3'
```

Reading it back:

- `tree.root.children['primary'].segments[0].path` → `'users'`
- `tree.root.children['primary'].segments[0].parameters['role']` → `'admin'`  (matrix param)
- `tree.root.children['primary'].children['sidebar'].segments[0].path` → `'notes'`
- `tree.queryParamMap.get('sort')` → `'name'`
- `tree.fragment` → `'section3'`

---

## 5. How a URL is compiled (parse / serialize)

The string ⇄ `UrlTree` conversion is owned by an injectable **`UrlSerializer`** (default
implementation `DefaultUrlSerializer`):

```ts
abstract class UrlSerializer {
  abstract parse(url: string): UrlTree;        // string → tree
  abstract serialize(tree: UrlTree): string;   // tree → string
}
```

You rarely call it directly; `Router` exposes convenience wrappers:

```ts
const tree = router.parseUrl('/users/42?x=1');   // → UrlTree
const url  = router.serializeUrl(tree);           // → '/users/42?x=1'
router.url;                                        // current URL as a string (post-redirects)
```

**Parsing rules** the default serializer applies (high level):

1. Split on `/` into segments; the leading `/` denotes an absolute URL from the root.
2. A `;key=value` suffix on a segment is peeled off into that `UrlSegment.parameters`.
3. `(...)` opens a set of **child segment groups**; inside, `outlet:path` names the outlet
   and `//` separates multiple outlets. An omitted name defaults to `primary`.
4. Everything after the first `?` is query params (`&`-separated, repeated keys → arrays).
5. Everything after `#` is the fragment.

**Serialization** is the exact inverse and is what `UrlTree.toString()` calls. Because it's
deterministic and canonical, a serialized `UrlTree` makes a stable identity/key for a
location (which is why app code often stringifies trees to compare or cache them).

---

## 6. From UrlTree to RouterState (matching)

A `UrlTree` is just syntax — it doesn't know your routes. The router's **Recognizer** walks
the `UrlTree`'s segment groups against your `Routes` config and produces a
**`RouterStateSnapshot`**: a tree of `ActivatedRouteSnapshot` nodes, one per matched route
level.

```ts
class RouterStateSnapshot {     // a Tree<ActivatedRouteSnapshot>
  url: string;                  // the matched URL
  root: ActivatedRouteSnapshot; // top of the activated tree
}
```

During matching, for each route the router:

- consumes the segments that the route's `path` matches (e.g. `users/:id` consumes
  `['users', '42']`);
- extracts **route params** from `:`-tokens (`:id` → `{ id: '42' }`) into that level's
  `params`;
- attaches the route's static `data`, resolved `resolve` data, and the route's `component`.

The resulting tree mirrors your nested `Routes`: a parent route's `ActivatedRouteSnapshot`
has the child route's snapshot as its `firstChild`, and so on. **`RouterState`** is the
live (observable) counterpart that the router keeps updated and hands to components as
`ActivatedRoute`.

---

## 7. RouterState & RouterStateSnapshot (the containers)

`RouterState` and `RouterStateSnapshot` are the **root containers** that hold the
`ActivatedRoute(Snapshot)` tree from [§8](#8-activatedroute--activatedroutesnapshot). You
rarely construct them — the router hands them to you. They're easy to miss because the
nodes (`ActivatedRoute`) get all the attention.

```ts
class RouterState extends Tree<ActivatedRoute> {     // the LIVE, observable tree
  snapshot: RouterStateSnapshot;                     // frozen view of itself
}                                                    // .root → root ActivatedRoute

class RouterStateSnapshot extends Tree<ActivatedRouteSnapshot> {  // FROZEN tree
  url: string;                                       // ← the current URL as a string
}                                                    // .root → root ActivatedRouteSnapshot
```

So:

- **`RouterState`** = the root wrapper of the **live** `ActivatedRoute` tree.
- **`RouterStateSnapshot`** = the root wrapper of the **frozen** `ActivatedRouteSnapshot`
  tree, **plus** the `url` string.

Note the `url` string lives on the **Snapshot**, not on `RouterState` itself:

```
RouterState ──.snapshot──► RouterStateSnapshot ──.url──► "/marketplace/orders/5"
   │                              │
 .root                          .root
   ▼                              ▼
ActivatedRoute (tree)     ActivatedRouteSnapshot (tree)
```

### How to access them

| You have | Live state | Snapshot | URL string |
| --- | --- | --- | --- |
| `Router` | `router.routerState` | `router.routerState.snapshot` | `router.url` *(shortcut)* or `router.routerState.snapshot.url` |
| any `ActivatedRoute` | `route.root` *(node, not wrapper)* | `route.snapshot.root` | — *(no `.url` string on a node)* |
| a **guard / resolver** | — | the **2nd argument** | `state.url` |
| a router **event** | — | `event.state` *(on some events)* | `event.urlAfterRedirects` *(on `NavigationEnd`)* |

### Where they're actually used

1. **Guards & resolvers** — the main place app code touches `RouterStateSnapshot` directly.
   The signature is `(route: ActivatedRouteSnapshot, state: RouterStateSnapshot)`, so
   `state.url` is the **target** URL of the pending navigation — handy for "remember where
   the user was heading, then redirect to login."
2. **Router events** — `RoutesRecognized`, `GuardsCheckStart/End`, `ResolveStart/End` carry
   `.state: RouterStateSnapshot`; `NavigationEnd` carries `.url` / `.urlAfterRedirects`
   strings.
3. **`RouteReuseStrategy`** — operates on `ActivatedRouteSnapshot` *nodes*, which are exactly
   the nodes of the recognized `RouterStateSnapshot` tree.
4. **`router.routerState` directly** — rarely needed; injecting `ActivatedRoute` (a node)
   plus `router.url` (the string) covers almost everything.

### "I just want the current URL string"

```ts
this.router.url                          // simplest — serialized current URL (post-redirects)
this.router.routerState.snapshot.url     // identical, the long form
// in a guard:    canActivate(route, state) { return state.url; }
// in NavigationEnd: e instanceof NavigationEnd && console.log(e.urlAfterRedirects)
```

`router.url` is just the convenience getter over `routerState.snapshot.url`, which is why
app code reaches for it instead of digging into `routerState`.

---

## 8. ActivatedRoute & ActivatedRouteSnapshot

`ActivatedRoute` is what a routed component injects to learn about *its own* route level.
It comes in two flavors:

### Reactive — `ActivatedRoute`

Values are **observables** that re-emit as you navigate (useful when the component is
*reused* across param changes, e.g. `/user/1` → `/user/2`):

```ts
class ActivatedRoute {
  snapshot: ActivatedRouteSnapshot;        // frozen "now" value (see below)

  url:          Observable<UrlSegment[]>;  // segments consumed at THIS level
  params:       Observable<Params>;        // route/matrix params at THIS level
  queryParams:  Observable<Params>;        // whole-URL query params (same on every node)
  fragment:     Observable<string | null>; // whole-URL fragment
  data:         Observable<Data>;          // static data + resolved data
  paramMap:     Observable<ParamMap>;      // typed view of params
  queryParamMap:Observable<ParamMap>;      // typed view of queryParams
  title:        Observable<string | undefined>;

  outlet: string;                          // 'primary' or a named outlet
  component: Type<any> | string | null;
  routeConfig: Route | null;               // the matched Route definition

  // navigation within the activated tree:
  readonly root:        ActivatedRoute;
  readonly parent:      ActivatedRoute | null;
  readonly firstChild:  ActivatedRoute | null;
  readonly children:    ActivatedRoute[];
  readonly pathFromRoot: ActivatedRoute[];  // [root, …, this]
}
```

### Frozen — `ActivatedRouteSnapshot`

Same shape, but every observable is replaced by its **plain current value**. Use it for
one-shot reads (e.g. in `ngOnInit` when the component won't be reused):

```ts
this.route.snapshot.paramMap.get('id');     // synchronous
this.route.snapshot.url;                     // UrlSegment[]
```

### Navigating the activated tree

These mirror the `UrlTree`/route nesting and are the backbone of most "walk the route"
code:

- **`firstChild`** — go *down* to the primary child route. Loop `while (r.firstChild)` to
  reach the deepest active route.
- **`children`** — all child routes (includes named outlets, not just primary).
- **`parent`** — go *up* one level.
- **`pathFromRoot`** — the full chain `[root, …, this]`; handy to aggregate params/segments
  from every ancestor.
- **`root`** — the top `ActivatedRoute`.

> **Reactive vs snapshot — when does it matter?** If Angular *reuses* a component instance
> across a navigation (same route config, only a param changed), `ngOnInit` runs once but
> the observables re-emit. Reading `snapshot` in `ngOnInit` then gives you a stale value on
> the second visit. Subscribe to `paramMap`/`params` instead when the component can be
> reused.

---

## 9. params vs queryParams vs matrix params vs data

A classic source of confusion. All four hang off `ActivatedRoute`, but they mean different
things:

| Name | Declared by | URL form | Scope | Read via |
| --- | --- | --- | --- | --- |
| **Route params** | `:token` in a route `path` (e.g. `user/:id`) | positional path segment | the matching route level | `params` / `paramMap` |
| **Matrix params** | ad-hoc on a segment | `;key=value` on a segment | that segment group | also `params` / `paramMap` |
| **Query params** | ad-hoc | `?key=value` | the **whole** URL | `queryParams` / `queryParamMap` |
| **Data** | route config `data` / `resolve` | not in the URL at all | the route level | `data` |

Key consequences:

- **Route + matrix params are per-level — *mostly*.** `route.snapshot.params` holds what
  *this* route matched **plus anything inherited from componentless / empty-path ancestors**
  (see [§10](#10-param--data-inheritance-componentless--empty-path-routes)). To collect
  params from the whole chain regardless, merge over `pathFromRoot`.
- **Query params and fragment are global** — they read the same on every node of the
  activated tree (they live on the `UrlTree`, not a segment group).
- **`data` never appears in the URL.** It's metadata attached in the `Routes` config (and
  the place to stash flags like a tab index, reuse marker, etc.).

---

## 10. Param & data inheritance (componentless & empty-path routes)

A subtlety that surprises *everyone*: a child route's `params`/`data` can contain values
declared on an **ancestor** route — so a leaf component sees an `:id` that was matched two
levels up. This is governed by the router's **`paramsInheritanceStrategy`** (default
`'emptyOnly'`).

### The rule (`emptyOnly`, the default)

A route inherits its ancestors' `params` and `data` **down through empty-path (`path: ''`)
and componentless ancestors**, stopping at the first ancestor that *has* a component.

- A **componentless route** is one with `children` but no `component` / `loadComponent`. It
  matches segments (and may run guards) but never instantiates a component.
- Without inheritance, params matched by a componentless route would be **unreachable** —
  there's no component at that level to read them. Inheritance pushes them down to the
  nearest real component.

### Worked example

```ts
{ path: 'orders', children: [                          // componentless (no component)
  { path: ':id', canActivate: [...], children: [       // componentless
    { path: 'detail', component: DetailComponent, data: { reuse: true } },
  ]},
]}
```

Navigating to `/orders/0/detail`:

```
orders   (componentless)  url: ['orders'], params: {}        ┐
  :id    (componentless)  url: ['0'],      params: {id:'0'}  ├─ merged downward (emptyOnly)
    detail (component)    url: ['detail'], params: {}        ┘ ← inherits → { id: '0' }
```

`DetailComponent`'s snapshot ends up with:

```ts
route.snapshot.paramMap.get('id');   // '0'  ← inherited from the componentless :id parent
route.snapshot.url;                  // [ {path:'detail'} ]   ← NOT '0' (see note below)
route.snapshot.data;                 // { reuse: true, …plus any data from orders / :id }
```

So **both** the `:id` snapshot (its own param) and the `detail` snapshot (inherited) carry
`id`. The same merge applies to `data`.

### The catch that ties back to URL rebuilding

The inherited `id` is a convenience copy in **`params`** — it is *not* a segment in the
leaf's own **`url`**. The `0` segment physically belongs to the `:id` level. So:

- **reading** `id` → just use the leaf's `paramMap`; inheritance already brought it down;
- **rebuilding** the URL from segments → still walk the **whole** chain (the `0` lives on
  the `:id` node's `url`, not the leaf's).

Inheritance duplicates the *value* into `params`, never the *segment* into `url`.

### Changing it

```ts
provideRouter(routes, withRouterConfig({
  paramsInheritanceStrategy: 'always',   // inherit ALL ancestor params/data,
}));                                      // even through routes that HAVE a component
```

- **`'emptyOnly'`** (default) — inherit only through empty-path + componentless ancestors.
  This is already the least-inheriting standard mode; you can't switch off the componentless
  flow without restructuring the routes.
- **`'always'`** — every route inherits all ancestor `params`/`data`, even across component
  routes (useful when a deep child needs a grandparent's `:id`).

---

## 11. Building URLs with createUrlTree

Going the other direction — code → URL — you assemble a `UrlTree` from a **command array**,
then either navigate to it or serialize it.

```ts
// Router (absolute & relative supported), or the standalone createUrlTreeFromSnapshot.
const tree: UrlTree = router.createUrlTree(commands, navigationExtras);
const url:  string   = tree.toString();         // or router.serializeUrl(tree)
router.navigateByUrl(tree);                       // navigate to it
```

### Command array semantics

```ts
router.createUrlTree(['/team', 33, 'user', 'victor']);   // → /team/33/user/victor (absolute)
router.createUrlTree(['user', 11], { relativeTo: route });// relative to a given ActivatedRoute
router.createUrlTree(['../sibling'], { relativeTo: route });// '..' goes up a level
router.createUrlTree(['./child'],    { relativeTo: route });// './' stays at current level

// An object literal sets MATRIX params on the preceding segment:
router.createUrlTree(['user', 11, { admin: true }]);     // → /user/11;admin=true

// Named outlets via the `outlets` command:
router.createUrlTree([{ outlets: { primary: ['main'], sidebar: ['notes'] } }]);
// → /main(sidebar:notes)
```

### `NavigationExtras` worth knowing

```ts
router.createUrlTree(['list'], {
  relativeTo: this.route,           // anchor for relative commands
  queryParams: { sort: 'name' },    // set query params
  queryParamsHandling: 'merge',     // 'merge' | 'preserve' | '' (replace) keep/merge existing
  fragment: 'top',                  // set #fragment
  preserveFragment: true,           // keep the existing fragment
});
```

- Without `relativeTo`, a relative command array is resolved against the **root**.
- `queryParamsHandling: 'merge'` is the usual way to add a query param without clobbering
  the others already in the URL.
- The template equivalent of all this is `routerLink` + `[queryParams]` + `[relativeTo]`;
  it calls `createUrlTree` under the hood.

---

## 12. Why it feels heavy (gotchas)

A "simple URI in the address bar" carries more machinery than it looks because:

1. **It's a tree, not a string.** Named outlets and per-level params mean the structure is
   genuinely hierarchical; the flat string is only its serialization.
2. **The empty root group.** `tree.root.segments` is empty; real segments are under
   `root.children['primary']`. Forgetting this yields confusing "no segments" bugs.
3. **Per-level params.** `route.params` is *not* "all params" — it's this level's only. You
   must walk `pathFromRoot` (or use a parent route's snapshot) to see ancestors' params.
4. **Snapshot vs observable.** Reading `snapshot` once in `ngOnInit` silently goes stale if
   the component is reused across a param-only navigation.
5. **Two param worlds.** Matrix (`;`) vs query (`?`) params behave differently in scope and
   persistence; mixing them up changes where the value lives in the tree.
6. **Strings are canonical, objects are convenient.** Code keeps round-tripping
   string ⇄ `UrlTree` (`parseUrl`/`toString`) because the string is a stable key while the
   tree is the workable structure.
7. **Params leak downward.** With the default `emptyOnly` strategy, a leaf component's
   `params` can include an `:id` matched by a componentless ancestor — values in `params`
   are *not* a 1:1 reflection of the leaf's own `url` segments
   (see [§10](#10-param--data-inheritance-componentless--empty-path-routes)).

---

## 13. Cheat-sheet

**Read the current location**

```ts
router.url;                              // current URL string (after redirects)
router.routerState.snapshot.url;         // identical — the long form
router.parseUrl(router.url);             // → UrlTree for the current URL
route.snapshot.paramMap.get('id');       // one route param, synchronously
route.snapshot.queryParamMap.get('q');   // one query param
route.snapshot.fragment;                 // the #fragment
// in a guard: canActivate(route, state) { return state.url; }   // target URL
```

**Walk the activated tree**

```ts
let r = route; while (r.firstChild) r = r.firstChild;   // deepest active route
route.pathFromRoot.flatMap(r => r.snapshot.url);        // all segments root→here
```

**Inspect a parsed UrlTree**

```ts
const t = router.parseUrl('/a/b;x=1(aux:c)?q=2#f');
t.root.children['primary'].segments.map(s => s.path);   // ['a', 'b']
t.root.children['primary'].segments[1].parameters;      // { x: '1' }
t.root.children['primary'].children['aux'].segments[0].path; // 'c'
t.queryParamMap.get('q');                                // '2'
t.fragment;                                              // 'f'
```

**Build & navigate**

```ts
const tree = router.createUrlTree(['user', id, { tab: 'info' }],
  { relativeTo: route, queryParams: { ref: 'x' }, queryParamsHandling: 'merge' });
router.navigateByUrl(tree);              // or: tree.toString() for a stable string key
```

**Glossary**

| Type | One-liner |
| --- | --- |
| `UrlTree` | Parsed URL: `root` group + `queryParams` + `fragment`. |
| `UrlSegmentGroup` | A tree node: `segments[]` + `children{}` keyed by outlet. |
| `UrlSegment` | One path piece: `path` + matrix `parameters`. |
| `UrlSerializer` | `parse(string) ⇄ serialize(tree)`; default is `DefaultUrlSerializer`. |
| `ActivatedRoute` | Observable route state for one level a component is on. |
| `ActivatedRouteSnapshot` | Frozen, synchronous version of the above. |
| `RouterState` | Live root container of the `ActivatedRoute` tree (`router.routerState`); has `.root` + `.snapshot`. |
| `RouterStateSnapshot` | Frozen root container of the `ActivatedRouteSnapshot` tree; has `.root` + the `url` string. |
| `ParamMap` | Safe `has`/`get`/`getAll`/`keys` accessor over a `Params` object. |
| `PRIMARY_OUTLET` | `'primary'` — the unnamed `<router-outlet>`. |
| `paramsInheritanceStrategy` | `'emptyOnly'` (default) inherits params/data through componentless + empty-path ancestors; `'always'` inherits through every ancestor. |
| Componentless route | A route with `children` but no `component`/`loadComponent`; matches segments + runs guards, but its params surface on the nearest descendant component. |

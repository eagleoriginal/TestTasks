# Route Reuse — Attach / Detach Lifecycle — tehnoinnovalk.client

How Angular's `RouteReuseStrategy` keeps a component alive across navigations, what fires (and what
does **not**) when it is detached/re-attached, and how to add an `attached` state so a component stops
doing layout work while it sits in a background tab slot.

> Paths are relative to `src/app/`. The live implementation is `app-route.reuse-startegy.ts`
> (`AppRouteReuseStrategy`), registered in `app-routing.module.ts`.

## 1. Why this exists

Reusable grids run `performInitialLayout()` → `setTimeout(() => mainGrid.autoFitColumns(), 100)` from
**async callbacks** — HTTP responses (`sendRequest` / `doPostSendRequestAction`) and SignalR
`bufferTime` updates. When such a callback lands while the component is **detached** in a background
tab, `autoFitColumns` measures a grid that is not in the live DOM: column widths come out wrong, the
work is wasted, and the `layoutPerformed = true` latch can mark layout "done" while hidden so it never
re-fits once the tab is shown. The fix is to know whether the component is currently attached.

## 2. The five-method contract

A `RouteReuseStrategy` is one object Angular asks five questions during every navigation. Order of the
columns below is the order Angular reaches them.

| Method | When Angular calls it | `true` / non-null means | `AppRouteReuseStrategy` does |
| --- | --- | --- | --- |
| `shouldReuseRoute(future, curr)` | While building the future snapshot tree, **per node**, very frequently | Same route → **reuse the component in place** (no detach/attach, no re-create; only `ActivatedRoute` subjects advance) | `future.routeConfig === curr.routeConfig`, but returns **false** when `data.recreateComponent` is set (force re-create) |
| `shouldDetach(route)` | When a route is being **left** (and was *not* reused in place) | Keep the component alive for later instead of destroying it | `data.reuse === true`, with a `uriRejectToDetach` escape hatch so closing a tab forces a real destroy |
| `store(route, handle \| null)` | Right after a detach (`handle`), **and** right after a successful retrieve (`null`, to empty the slot) | — | Stores `handle` in a `Map` keyed by `FullUriFromSnapshot(route.root)`; calls `ngCanDetach()` so the component can veto being kept |
| `shouldAttach(route)` | When a route is being **entered** | A stored component should be re-attached instead of created fresh | `data.reuse === true && handlers.has(key)` |
| `retrieve(route)` | After `shouldAttach` returned true | The handle to re-attach into the outlet | Returns the stored handle and calls `ngOnAttach()` on the instance |

Two of these already carry component hooks: **`store` → `ngCanDetach()`** (a veto) and
**`retrieve` → `ngOnAttach()`** (a notification). There is deliberately **no detach notification** yet —
that gap is what §7 closes.

These five methods are not one decision; they belong to **two separate phases** (§3). The first method,
`shouldReuseRoute`, runs in phase 1 and **never detaches anything** — it only decides whether an
existing route node is kept *in place*. The other four run in phase 2 and only get a vote on positions
that `shouldReuseRoute` already declared "changed."

## 3. Two phases: build the tree, then activate it

A navigation is not a single yes/no about the whole page. The router walks the future and current
route **trees in parallel from the root** and decides things position by position.

### Phase 1 — build the future state tree (`createRouterState`)

Uses **only** `shouldReuseRoute(future, curr)`, called once per paired tree node:

- `true` → keep the existing `ActivatedRoute` instance at that position, set its `_futureSnapshot`,
  recurse into children. The component stays put; later `advanceActivatedRoute` just pushes new
  param/data/url values onto its subjects.
- `false` → that position is a "different route"; a fresh `ActivatedRoute` is created there and the old
  one is *not* paired with it.

The default rule is one line — `future.routeConfig === curr.routeConfig` — i.e. "is this the same
`Route` config object from `app-routing.module.ts`?" Same entry reached with different params → same
object → `true` (param-only navigation, **no** re-create). Different entries (`orders` vs `licenses`) →
different objects → `false`. This app's override adds: even when configs match, return `false` if
`data.recreateComponent` is set, otherwise fall back to `super`.

`shouldReuseRoute` is what finds the **reused prefix** of shared ancestors so they aren't torn down on
every click. Walking `/marketplace/orders` → `/marketplace/licenses`:

```
        future                       curr
root ──────────────────  vs  root            → shouldReuseRoute = true   (reuse root)
 └ marketplace (host) ──  vs   └ marketplace  → shouldReuseRoute = true   (reuse tab host)
     └ licenses (B) ────  vs       └ orders (A) → shouldReuseRoute = false (swap this leaf only)
```

So even an "A → B" swap calls `shouldReuseRoute` several times; it returns `true` up the shared chain
and `false` only at the leaf that actually changed.

### Phase 2 — activate the tree (`ActivateRoutes`)

Now the detach/attach hooks fire, **driven by phase 1's verdict per node**:

| Phase 1 said | Phase 2 does |
| --- | --- |
| `shouldReuseRoute = true` | Reuse in place. `advanceActivatedRoute` emits new params/data/url. **No `shouldDetach`, no `shouldAttach`, no create/destroy.** |
| `shouldReuseRoute = false` | Old subtree **leaves** → `shouldDetach` → `store`; new subtree **enters** → `shouldAttach` → `retrieve` (or fresh create). |

For the one leaf that changed (both `reuse: true`, B visited earlier so its handle is stored):

```
leaving A
  ├─ shouldDetach(A) → true
  ├─ outlet.detach()            // A removed from DOM, NOT destroyed
  └─ store(A, handleA)          // → your ngCanDetach() veto point

entering B
  ├─ shouldAttach(B) → true
  ├─ retrieve(B) → handleB      // → your ngOnAttach() notification
  ├─ store(B, null)             // empties B's slot
  └─ outlet.attach(handleB)     // B back in DOM, same instance as before
```

The `marketplace` host above sits in phase-1 `true` territory, so **it** never sees `shouldDetach`/
`shouldAttach` when you switch tabs — only the leaf does.

**Contrast — `recreateComponent: true`** (the `marketplace` tab host, `app-routing.module.ts:176`):
`shouldReuseRoute` returns `false` even though the config matches, so phase 1 marks the node "changed"
→ phase 2 fully destroys and re-creates the component on every internal navigation. Those components
get a fresh `ngOnInit` each time and therefore **never** hit the background-layout problem. Only
`reuse: true` routes (`licenses`, `licensestasks`, `license/:serial`, the preorder card) keep a live
instance across navigations and need the attached-state guard.

## 4. What survives a detach — and what does NOT fire

This is the part that surprises people. A detached component is **alive but DOM-detached**:

- `ngOnDestroy` does **not** run. The instance, its fields, its `@ViewChild` refs, and its RxJS
  subscriptions all stay exactly as they were.
- Its host view is detached from `ApplicationRef`, so the **router no longer change-detects it** — but
  anything async keeps running in the background: open `subscribe`s, `setTimeout`, `setInterval`,
  `requestAnimationFrame`, SignalR handlers. They will execute and mutate component state; they just
  won't trigger automatic view updates.
- On **re-attach**, the constructor / `ngOnInit` / `ngAfterViewInit` do **not** run again. The only
  signal you receive is whatever you wired into `retrieve` — here, `ngOnAttach()`. View change
  detection resumes from then on.
- `@ViewChild` element refs (e.g. `mainGrid`) survive the round-trip and point at the same instance,
  **but** while detached that element is out of the live DOM, so any measurement or sizing
  (`autoFitColumns`, `getBoundingClientRect`, scroll math) is invalid until the element is re-attached.
- There is **no built-in detach lifecycle hook**. `store()` is the single place in the whole flow where
  you can learn that a detach just happened.

Net effect for the grids: an HTTP/SignalR callback that arrives while detached runs against a live
component whose DOM is gone. Calling layout there is the bug.

## 5. This app's hook surface today

Defined in `utils/routeUtils.ts`:

```ts
export interface Attachable        { ngOnAttach(): void; }
export interface DetachableWithCheck { ngCanDetach(): boolean; }

export function IsAttachable(obj: any): obj is Attachable { /* typeof obj.ngOnAttach === "function" */ }
export function IsDetachableWithCheck(obj: any): obj is DetachableWithCheck { /* typeof obj.ngCanDetach === "function" */ }
```

Consumers:

- `marketplace-preorder-card.component.ts:578,593` — `ngOnAttach()` restores scroll **inside a
  `requestAnimationFrame`** because the element is not back in the live DOM on the same tick that
  `retrieve` runs; `ngCanDetach()` returns `!restrictToDetach`. This rAF detail is the key timing
  lesson for any work that touches the DOM on attach.
- `marketplace-balance`, `marketplace-license`, `marketplace-balance-activate`,
  `marketplace-licenses-with-balance` — additional `ngOnAttach` implementors (some fan the call out to
  child components).

Note on keys: `getRouteKey` uses `FullUriFromSnapshot(route.root, false)`, i.e. the full URL from the
root. Whatever key scheme you pick for storage must be the same one used everywhere a stored route is
looked up — see the tab-URI canonicalization discussion (`marketplace-tabs.component.ts` stores tab
URIs via three different serializers; prefer routing all of them through `FullUriFromSnapshot`).

## 6. Alternative signal source: the outlet

`RouterOutlet` itself exposes the same events at the template level:

```html
<router-outlet (attach)="onOutletAttach($event)" (detach)="onOutletDetach($event)"></router-outlet>
```

plus `isActivated` / `activatedRoute`. This works, but the event gives you a `ComponentRef`/component
you then have to type-narrow, and it lives on the *parent* that hosts the outlet. The per-component
hook approach (`ngOnAttach` / `ngOnDetach` on the component being reused) keeps the knowledge where the
state lives and is already half-built in this codebase, so §7 extends that path rather than the outlet
events.

## 7. Recommended integration: an `attached` state to gate layout

Goal: each reusable component knows whether it is currently on screen, and layout work that arrives
while detached is **deferred** until the next attach instead of running on a hidden grid.

**Step 1 — add the missing detach hook** in `utils/routeUtils.ts`, mirroring `Attachable`:

```ts
export interface Detachable { ngOnDetach(): void; }

export function IsDetachable(obj: any): obj is Detachable {
  return !!obj && typeof (obj as any).ngOnDetach === "function";
}
```

**Step 2 — fire it from the strategy** in `app-route.reuse-startegy.ts`, inside `store()`, right after
the handle is actually kept (by that point `outlet.detach()` has already run, so "you are detached" is
true):

```ts
override store(route: ActivatedRouteSnapshot, handle: DetachedRouteHandle | null): void {
  const key = this.getRouteKey(route);
  if (!key) return;

  if (handle && route.data['reuse'] === true) {
    const instance = (handle as any).componentRef.instance;
    if (IsDetachableWithCheck(instance) && !instance.ngCanDetach()) {
      destroyDetachedRouteHandle(handle);
      return;
    }
    this.handlers.set(key, handle);
    if (IsDetachable(instance)) instance.ngOnDetach();   // <-- new: notify "you are now detached"
  } else {
    this.handlers.delete(key);
  }
}
```

> Drive the notification from `store()`, **not** from `shouldDetach()`. `shouldDetach` is a predicate
> Angular may call to make a decision; treating it as an event would fire at the wrong time. `store`
> with a non-null handle is the one moment a detach has definitely occurred.

**Step 3 — component state + deferred layout.** In a reusable grid component
(`marketplace-licenses-tasks.component.ts` is the canonical example; same shape in
`marketplace-licenses.component.ts`):

```ts
export class MarketplaceLicensesTasksComponent /* ... */ implements OnInit, Attachable, Detachable {
  private attached = true;       // a freshly created component is on screen
  private pendingLayout = false; // a layout was requested while detached

  ngOnDetach(): void {
    this.attached = false;
  }

  ngOnAttach(): void {
    this.attached = true;
    if (this.pendingLayout) {
      this.pendingLayout = false;
      this.layoutPerformed = false;     // reset the latch so the deferred fit can run
      this.performInitialLayout();      // its internal setTimeout gives the DOM a tick to settle
    }
    this.changeDetector.detectChanges(); // view was not CD'd while detached
  }

  public performInitialLayout() {
    if (this.layoutPerformed || !this.gridData.data?.length) return;

    if (!this.attached) {            // <-- guard: don't fit a hidden grid
      this.pendingLayout = true;
      return;
    }

    this.layoutPerformed = true;
    setTimeout(() => this.mainGrid?.autoFitColumns(), 100);
  }
}
```

Now an HTTP/SignalR callback that calls `performInitialLayout()` while the tab is in the background just
sets `pendingLayout` and returns; the actual `autoFitColumns` runs once, on re-attach, when the grid is
visible.

**Targets** = `reuse: true` components that own a `performInitialLayout` / `layoutPerformed` latch:
`marketplace-licenses-tasks` (`:297`) and `marketplace-licenses` (`:388`), plus the reusable licenses /
license views. `marketplace-orders` is `recreateComponent`, so it is recreated each visit and is
**exempt**.

If several components need this, factor `attached` / `pendingLayout` / `ngOnDetach` / the `ngOnAttach`
flush into the existing grid mixin (`mixins/mixin-common.ts`) rather than copy-pasting.

## 8. Gotchas / checklist

- **`shouldReuseRoute` is phase 1 and never detaches.** It only decides "keep this route node in place?"
  while the future tree is built; the detach/attach hooks run in phase 2 *because of* its per-node
  verdict. Returning `false` for a shared ancestor by mistake tears that whole subtree down on every
  navigation.
- **Veto ≠ notification.** `ngCanDetach()` answers "may I be kept?"; `ngOnDetach()` says "you were just
  detached." They are different hooks with different call sites.
- **Notify from `store`, not `shouldDetach`.** See Step 2.
- **Re-attach needs a frame.** The element is not in the live DOM during `retrieve`/`ngOnAttach`'s
  synchronous run. Defer any DOM read/write to `requestAnimationFrame` or a `setTimeout`
  (`performInitialLayout` already uses `setTimeout(…, 100)`; the scroll restore in the preorder card
  uses `requestAnimationFrame`).
- **Reset the `layoutPerformed` latch** on the deferred path, or the flushed layout will no-op.
- **Detached components keep computing.** If background CPU/network from a hidden tab is itself a
  problem, gate the *work* (skip the refresh while `!attached` and refresh on `ngOnAttach`), not just
  the layout.
- **Keep storage keys canonical.** `getRouteKey` uses `FullUriFromSnapshot(route.root)`; route every
  store/lookup/compare through the same serializer so a stored handle is always found again.

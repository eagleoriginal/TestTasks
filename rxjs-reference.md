# RxJS Quick Reference — tehnoinnovalk.client

A cheat-sheet of every RxJS symbol actually used in this Angular client: one line on
what it does, plus a representative file where it lives so you can copy a real pattern.
The last section (§8) covers this app's own recurring idioms — the things that get
forgotten a week after you write them.

> Paths are relative to `src/app/`. Pervasive symbols (`of`, `map`, `tap`,
> `catchError`, `Observable`) appear almost everywhere; only a sample is listed.

## 1. Subjects & multicast

| Symbol | What it does | Used in |
| --- | --- | --- |
| `Subject` | Multicast Observable with no initial/replayed value; you push with `.next()`. | `identity/auth-service.ts`, `tasks/task-add/formModel.ts` |
| `BehaviorSubject` | Subject that stores and replays the latest value to new subscribers (needs a seed). | `utils/formUtils.ts`, `util-components/reload-buton/reload-buton.component.ts` |
| `ReplaySubject` | Subject that replays the last N emitted values to new subscribers. | `marketplace/marketplace-preorder-card`, `subspaces-all/invites/invites` |

## 2. Creation functions

| Symbol | What it does | Used in |
| --- | --- | --- |
| `of` | Emits the given value(s) synchronously, then completes. | `lkservices/*State.ts` (pervasive) |
| `from` | Turns a Promise / array / iterable into an Observable. | `lkservices/devicesLkService.ts`, `utils/httpHelpers.ts` |
| `fromEvent` | Builds an Observable from DOM/EventTarget events. | `util-components/directives/overlay-click-directive.ts`, `services/connection-status-service.ts` |
| `interval` | Emits incrementing numbers on a fixed period. | `identity/auth-background-service.ts`, `lkservices/UserNotificationsState.ts` |
| `timer` | Emits once after a delay, optionally then periodically. | `services/notify/notify-service.ts`, `marketplace/marketplace-license` |
| `merge` | Subscribes to several sources concurrently, interleaving their emissions. | `app.component.ts`, `services/media-service.ts` |
| `combineLatest` | Emits an array of the latest value from each source whenever any of them emits. | `tasks/tasks-all`, `marketplace/marketplace-orders` |
| `concat` | Runs sources one after another, in order (each waits for the previous to complete). | `marketplace/marketplace-preorder-card` |
| `EMPTY` | Completes immediately without emitting anything. | `utils/httpHelpers.ts`, `lkservices/devicesLkState.ts` |
| `NEVER` | Never emits and never completes (a stream that just stays open). | `app.component.ts` |
| `throwError` | Returns an Observable that errors immediately. | `utils/helpers.ts`, `lkservices/*State.ts` |

## 3. Transformation operators

| Symbol | What it does | Used in |
| --- | --- | --- |
| `map` | Projects each value through a function. | nearly every `lkservices/*` |
| `tap` | Runs side-effects without altering the stream (logging, patchState). | pervasive |
| `switchMap` | Maps to an inner Observable, **cancelling the previous** inner one — good for typeahead/latest-wins. | `utils/helpers.ts`, `tasks/task-add`, `user-data-all/user-data-change-password` |
| `mergeMap` | Maps to inner Observables and runs them **concurrently** (no ordering guarantee). | `identity/auth-background-service.ts`, `identity/signin.component.ts` |
| `concatMap` | Maps to inner Observables run **one at a time, in order** (queues them). | `utils/httpHelpers.ts`, `cashboxes/cashboxes-main` |
| `exhaustMap` | Maps to an inner Observable but **ignores new source values while one is in flight** — the double-click guard. | `mixins/mixin-common.ts`, `util-components/captcha` |
| `mergeAll` | Flattens a higher-order Observable concurrently. | `app.component.ts`, `marketplace/marketplace-preorder-card` |
| `concatAll` | Flattens a higher-order Observable sequentially. | `marketplace/marketplace-preorder-card` |
| `combineLatestAll` | Applies `combineLatest` to a higher-order Observable once it completes. | `tasks/tasks-all`, `marketplace/marketplace-preorder-card` |
| `toArray` | Buffers all values and emits them as a single array on completion. | `marketplace/marketplace-preorder-card` |
| `buffer` / `bufferTime` | Collects values into arrays, flushed by a notifier (`buffer`) or on a time window (`bufferTime`). | `services/connection-status-service.ts`, `cashboxes/cashboxes-main` |

## 4. Combination operators (pipeable)

| Symbol | What it does | Used in |
| --- | --- | --- |
| `mergeWith` | Merges the source with additional Observables (pipeable `merge`). | `app.component.ts`, `cashboxes/cashboxes-table-rowscells` |
| `concatWith` | Appends other Observables after the source completes. | `marketplace/marketplace-preorder-card` |
| `combineLatestWith` | `combineLatest` as a pipeable operator on the source. | `subspaces-all/subspaces/subspaces`, `user-data-all/user-data-invites` |
| `startWith` | Emits seed value(s) before the source's own values (great for "loading" states). | `utils/helpers.ts`, `marketplace/marketplace-licenses` |

## 5. Filtering operators

| Symbol | What it does | Used in |
| --- | --- | --- |
| `filter` | Passes only values matching a predicate. | `utils/preloadFromStore.ts`, `cashboxes/utils/cashGroupWithDevs.ts` |
| `debounceTime` | Emits a value only after a quiet period (no new value for N ms). | `tasks/task-add`, `util-components/directives/debounceValueChangedDirective.ts` |
| `distinctUntilChanged` | Suppresses consecutive duplicate values. | `util-components/directives/debounceValueChangedDirective.ts` |
| `take` | Completes after the first N values. | `shell/shell-component`, `identity/store/reset-password-page.state.ts` |
| `takeUntil` | Completes when a notifier Observable emits — the teardown operator (see §8). | `app.component.ts`, `identity/auth-background-service.ts` |
| `takeWhile` | Emits while a predicate holds, then completes. | `identity/signin.component.ts` |
| `skip` | Ignores the first N values. | `subspaces-all/users/subspaces-users`, `cashboxes/cashboxes-main` |
| `first` | Emits the first (matching) value, then completes. | `services/notify/notify-service.ts` |
| `delay` | Time-shifts emissions by a duration. | `tasks/task-add`, `cashboxes/cashboxes-table-editfrom` |

## 6. Utility / error / interop

| Symbol | What it does | Used in |
| --- | --- | --- |
| `catchError` | Intercepts an error and returns a fallback Observable (often `of(...)` / `EMPTY`). | `lkservices/*State.ts` (pervasive) |
| `share` (+ `ShareConfig`) | Multicasts a source among multiple subscribers instead of re-running it. | `services/media-service.ts` |
| `timeout` | Errors if no emission arrives within the time limit. | `identity/auth-background-service.ts` |
| `throwIfEmpty` | Errors if the source completes without ever emitting. | `marketplace/marketplace-preorder-card` |
| `pipe` | Composes operators into a reusable standalone operator function. | `lkservices/devicesTablesShemaUpdateBackGroundService.ts` |
| `firstValueFrom` | Converts an Observable's first value into a Promise (`await`-able). | `identity/auth-service.ts` |

## 7. Types

| Symbol | What it does | Used in |
| --- | --- | --- |
| `Observable` | The core async stream type. | pervasive |
| `Subscription` | Handle returned by `.subscribe()`; call `.unsubscribe()` to tear down. | `app.component.ts`, `util-components/directives/debounceValueChangedDirective.ts` |
| `ObservableInput` / `ObservedValueOf` | Generic types describing inputs/outputs of flatten operators (used to type the grid mixin's `refreshFn`). | `mixins/mixin-common.ts` |
| `ShareConfig` | Config object passed to `share()`. | `services/media-service.ts` |

---

## 8. Project idioms (the easy-to-forget patterns)

### `takeUntil(destroy$)` teardown — long-lived streams
A `Subject` is fired in `ngOnDestroy`/`stopTimer` to auto-complete a stream so it
unsubscribes. Used in background services and reset-on-logout flows.

```ts
// identity/auth-background-service.ts
private destroyed$ = new Subject<any>();

interval(environment.timeoutCheckLogin).pipe(
  takeUntil(this.destroyed$),       // completes the stream on teardown
  filter(() => !this.connectionStatus.AppisOfflineStatus.value),
  mergeMap(() => this.store.selectOnce(AuthStatusState.getIsLogged)),
  exhaustMap(isLogged => isLogged ? this.store.dispatch(new Auth.LoginInfo()) : of()),
).subscribe();

stopTimer() { this.destroyed$.next(true); this.destroyed$.unsubscribe(); }
```

> In components, prefer Angular's `takeUntilDestroyed(this.destroyRef)` (from
> `@angular/core/rxjs-interop`) instead of a manual Subject — see the mixin below.

### `exhaustMap` action guard — `mixins/mixin-common.ts`
The shared grid/refresh mixins push reload requests through a `refreshState$` Subject
and use `exhaustMap` so repeated reload clicks are **ignored while a request is still
in flight** (no overlapping reloads).

```ts
// mixins/mixin-common.ts (gridHelpersMixinFunction)
this.refreshState$.pipe(
  exhaustMap((isRefreshBtn) => {
    if (isRefreshBtn) this.refreshStateTriggered$.next(true);
    this.gridLoading = true;
    return refreshFn();             // ObservableInput returned by the caller
  }),
  takeUntilDestroyed(this.destroyRef),
).subscribe();

onClickReload() { this.refreshState$.next(true); }
```

### Polling pattern — `*BackGroundService.ts`
`interval` / `timer` → `filter` (skip when offline) → `exhaustMap` (dispatch, skip
overlaps) → `takeUntil` (stop on destroy). See `auth-background-service.ts`,
`services/update-app-version-service.ts`, `lkservices/UserNotificationsBackGroundService.ts`.

### NGXS state action flow — `lkservices/*State.ts`
The canonical `@Action` returns the service Observable so NGXS waits for it, with
`tap` writing into the store and `catchError` swallowing the error after recording it.

```ts
// lkservices/devicesLkState.ts
return this.lkService.GetAll().pipe(
  tap({
    next:  val   => ctx.patchState({ data: ctx.getState().data.SetNewValue(val), lastError: null }),
    error: error => ctx.patchState({ lastError: ReadHttpErrorEssential(error), /* ... */ }),
  }),
  catchError(() => of()),           // error already handled in tap; keep the action stream alive
);
```

> Variations use `map` (transform + patch) then `catchError(() => EMPTY)` or
> `throwError(() => err)` when the error must propagate to the caller.

### `bufferTime` batching — coalescing bursts
Collects rapid emissions into one array over a time window, then handles them in a
single downstream step (here followed by `concatMap` to process sequentially).

```ts
// cashboxes/cashboxes-main.component.ts
this.viewChanges.pipe(
  takeUntilDestroyed(this.destroyRef),
  bufferTime(100),                  // batch all view changes within 100ms
  concatMap(v => this.restrictChangeView() ? EMPTY : /* ...apply batch... */),
).subscribe();
```

---

### Flatten-operator picker (switch / merge / concat / exhaust)

| You want… | Use |
| --- | --- |
| Only the latest, cancel the rest | `switchMap` |
| All in parallel, order doesn't matter | `mergeMap` |
| All, strictly in order, one at a time | `concatMap` |
| Ignore new ones until the current finishes | `exhaustMap` |

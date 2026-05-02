# JavaScript internal slots — the `[[*]]` notation

Every JavaScript object you touch has a public face (own properties, prototype chain) and a **hidden** spec-level face: a fixed set of named storage cells called **internal slots**, written `[[Name]]`. They are not properties — you cannot reach them from JS code, `Object.keys` does not see them, `Reflect.ownKeys` does not see them, the `in` operator does not see them. They exist only inside the engine.

Almost every "weird" behavior in the language — why `class Foo {}` cannot be called without `new`, why `Date` objects compare differently, why `Promise.resolve(promise) === promise`, why a Proxy's identity is hidden — bottoms out in "this kind of object has slot X, that kind doesn't." This article enumerates them.

## 1. Slots vs internal methods — keep them apart

| | Internal slot `[[X]]` | Internal method `[[X]]()` |
|---|---|---|
| What it is | A storage cell on the object | A behavior the object exposes |
| Examples | `[[Prototype]]`, `[[Extensible]]`, `[[PromiseState]]` | `[[Get]]`, `[[Set]]`, `[[GetPrototypeOf]]`, `[[Call]]`, `[[Construct]]` |
| Set when? | At object creation | Defined by the object's "kind" (ordinary, exotic) |

Some `[[X]]` are storage; some are operations. The notation is identical, which is confusing. When you read `o.[[Get]]("foo")`, that's an operation; when you read `f.[[Environment]]`, that's storage.

A second cut: **ordinary** objects share a default set of internal methods; **exotic** objects override one or more of them (arrays override `[[DefineOwnProperty]]` to keep `length` honest; proxies forward all of them to a handler). Slots are how exotics carry the state they need to do that overriding.

## 2. Ordinary-object slots — what every object has

| Slot | Holds | Observable via |
|---|---|---|
| `[[Prototype]]` | Reference to another object or `null` | `Object.getPrototypeOf(o)` / `o.__proto__` |
| `[[Extensible]]` | Boolean: can new own properties be added? | `Object.isExtensible(o)`; flipped by `Object.preventExtensions`, `seal`, `freeze` |

That's it for *every* ordinary object. Everything else (own properties, the methods you defined) lives in property storage, not slots.

## 3. Function objects — the slots that make functions callable

A "function" is just an ordinary object plus a few specific slots. Strip the slots and you have a regular `{}`.

| Slot | Holds | Why it exists |
|---|---|---|
| `[[Call]]` | The "invoke as `f(...)`" behavior | Without it, `f()` throws `TypeError: f is not a function` |
| `[[Construct]]` | The "invoke as `new f(...)`" behavior | Without it, `new f()` throws `TypeError: f is not a constructor` |
| `[[Environment]]` | The lexical environment captured at definition | Powers closures — variable lookup walks this chain |
| `[[FormalParameters]]` | The parsed parameter list | Used to bind arguments at call time |
| `[[ECMAScriptCode]]` | The parsed body (AST/bytecode) | What `[[Call]]` actually executes |
| `[[ThisMode]]` | `lexical` / `strict` / `global` | `lexical` is what makes arrows take `this` from `[[Environment]]` |
| `[[Strict]]` | Boolean | Strict-mode behavior of the body |
| `[[HomeObject]]` | The enclosing object literal / class — for `super` | `super.foo()` resolves through this object's `[[Prototype]]` |
| `[[Realm]]` | The Realm (intrinsics + global) the function belongs to | Cross-realm checks (`Array.isArray` works across iframes thanks to realms) |
| `[[ScriptOrModule]]` | The script/module record that defined the function | Used by `import.meta`, dynamic import resolution |
| `[[FunctionKind]]` | `normal` / `classConstructor` / `generator` / `async` / `asyncGenerator` | Drives what `[[Call]]` and `[[Construct]]` do |
| `[[ConstructorKind]]` | `base` / `derived` | Affects how `new` and `super()` initialize `this` |
| `[[Fields]]` | List of class field initializers | Run when an instance is constructed |
| `[[PrivateMethods]]` | List of `#private` methods to attach | Per-instance installation |
| `[[InitialName]]` | The `f.name` value at creation | Distinct from the public `name` property |

### Practical consequences

- **A function declaration** has both `[[Call]]` and `[[Construct]]` → callable AND constructable.
- **An arrow function** has `[[Call]]` only — no `[[Construct]]`. So `new (() => {})()` throws. Also `[[ThisMode]] = lexical`, so the arrow has no own `this` binding; reads fall through to `[[Environment]]`.
- **A class** has `[[Construct]]` only — no `[[Call]]`. So `class Animal {}; Animal()` throws `TypeError: Class constructor Animal cannot be invoked without 'new'`. **This is why a class is not "just sugar" over a function** — it's a function-shaped object minus one slot.
- **A method shorthand** (`{ foo() {} }`) has `[[HomeObject]]` set and **no `[[Construct]]`** — that's why `new obj.foo()` throws.
- **A generator function** has its own kind in `[[FunctionKind]]`; `[[Call]]` returns a generator object instead of executing the body.

| Function shape | `[[Call]]` | `[[Construct]]` | `[[ThisMode]]` |
|---|---|---|---|
| `function f() {}` | yes | yes | strict/global |
| `() => {}` | yes | **no** | **lexical** |
| `class C {}` | **no** | yes | strict |
| `{ m() {} }` (method shorthand) | yes | **no** | strict/global |
| `function* g() {}` | yes (returns iterator) | no | strict/global |
| `async function af() {}` | yes (returns Promise) | no | strict/global |

## 4. Bound functions — slot-only objects

`f.bind(thisArg, ...args)` returns a new function-like object whose `[[Call]]` and `[[Construct]]` simply delegate to the target.

| Slot | Holds |
|---|---|
| `[[BoundTargetFunction]]` | The original function |
| `[[BoundThis]]` | The `this` to use for `[[Call]]` |
| `[[BoundArguments]]` | Pre-supplied positional args |

`new (f.bind(x))()` ignores `[[BoundThis]]` because `[[Construct]]` makes a fresh `this`. That is the spec talking, not magic.

## 5. Primitive-wrapper objects

Boxed primitives keep the underlying value in a slot:

| Object | Slot | Holds |
|---|---|---|
| `new String("a")` | `[[StringData]]` | `"a"` |
| `new Number(1)` | `[[NumberData]]` | `1` |
| `new Boolean(true)` | `[[BooleanData]]` | `true` |
| `Object(1n)` | `[[BigIntData]]` | `1n` |
| `Object(Symbol())` | `[[SymbolData]]` | the symbol |

`String.prototype.toString` is specified as "throw if `this` doesn't have `[[StringData]]`" — that's how it brand-checks. You can't fake it with a regular object.

## 6. Array — exotic `[[DefineOwnProperty]]`, no extra slot

Arrays are an *exotic* object: they override the internal method `[[DefineOwnProperty]]` so that:

- writing a non-negative integer index updates `length` if it grows
- writing `length` to a smaller value deletes higher indices

There is no `[[ArrayData]]` slot. The data lives in ordinary indexed properties; the magic is in the overridden method.

## 7. Date

| Slot | Holds |
|---|---|
| `[[DateValue]]` | Number of milliseconds since the epoch (or `NaN`) |

`Date.prototype.getTime` brand-checks for this slot. `+new Date()` reads it. A plain object copy of a Date has no `[[DateValue]]` and every method throws `TypeError`.

## 8. Error

| Slot | Holds |
|---|---|
| `[[ErrorData]]` | (empty marker — its presence is a brand) |

Used by `Error.isError`-style checks and by hosts that decide whether to attach a stack trace.

## 9. RegExp

| Slot | Holds |
|---|---|
| `[[OriginalSource]]` | The source string (`"a+"`) |
| `[[OriginalFlags]]` | The flag string (`"gi"`) |
| `[[RegExpMatcher]]` | A compiled matcher closure |
| `[[RegExpRecord]]` | Parsed pattern info |

`lastIndex` is a regular own property, not a slot.

## 10. Promise

| Slot | Holds |
|---|---|
| `[[PromiseState]]` | `pending` / `fulfilled` / `rejected` |
| `[[PromiseResult]]` | The fulfillment value or rejection reason |
| `[[PromiseFulfillReactions]]` | List of reactions to run on fulfillment |
| `[[PromiseRejectReactions]]` | List of reactions to run on rejection |
| `[[PromiseIsHandled]]` | Boolean — drives the "unhandled rejection" warning |

`Promise.resolve(x)` checks: if `x` has these slots and the same constructor, return `x` itself — that's the spec line behind "you can't double-wrap a promise".

## 11. Generators / async generators

A generator object — what a `function*` returns — is its own kind:

| Slot | Holds |
|---|---|
| `[[GeneratorState]]` | `suspendedStart` / `suspendedYield` / `executing` / `completed` |
| `[[GeneratorContext]]` | The frozen execution context (locals, position, this binding) |
| `[[GeneratorBrand]]` | Marker for which kind of generator |

Async generators add `[[AsyncGeneratorQueue]]`. Async functions don't get a generator object directly, but the engine internally builds an `[[AsyncContext]]` to suspend on each `await`.

The `[[GeneratorContext]]` slot is what makes `yield` magical: the entire ExecutionContext (lexical env, this binding, instruction pointer) is stored, then restored on `next()`.

## 12. Map / Set / WeakMap / WeakSet

| Object | Slot | Holds |
|---|---|---|
| `Map` | `[[MapData]]` | List of `{ Key, Value }` records |
| `Set` | `[[SetData]]` | List of values |
| `WeakMap` | `[[WeakMapData]]` | List of `{ Key, Value }` (key held weakly) |
| `WeakSet` | `[[WeakSetData]]` | List of values (held weakly) |

These slots are *exotic* in the sense that the GC has special knowledge of weak ones — entries are eligible for collection when nothing else references the key.

## 13. Proxy — slots-as-redirection

| Slot | Holds |
|---|---|
| `[[ProxyTarget]]` | The wrapped object |
| `[[ProxyHandler]]` | The trap object |

A Proxy's *internal methods* (`[[Get]]`, `[[Set]]`, `[[OwnKeys]]`, …) are all overridden to consult `[[ProxyHandler]]`. Revoking a proxy sets both slots to `null`; every operation then throws.

You cannot detect a Proxy from outside (no `Proxy.isProxy`) — this is by design, the slots are sealed.

## 14. ArrayBuffer / SharedArrayBuffer / TypedArrays / DataView

| Object | Slots |
|---|---|
| `ArrayBuffer` | `[[ArrayBufferData]]` (the raw byte block), `[[ArrayBufferByteLength]]`, `[[ArrayBufferDetachKey]]` |
| `SharedArrayBuffer` | similar but cannot be detached |
| `DataView` | `[[ViewedArrayBuffer]]`, `[[ByteOffset]]`, `[[ByteLength]]` |
| `Uint8Array` etc. | `[[ViewedArrayBuffer]]`, `[[ByteOffset]]`, `[[ByteLength]]`, `[[ArrayLength]]`, `[[TypedArrayName]]`, `[[ContentType]]` |

`buf.transfer()` zeroes `[[ArrayBufferData]]` — that's "detached". Subsequent typed-array reads throw.

## 15. WeakRef / FinalizationRegistry

| Object | Slot |
|---|---|
| `WeakRef` | `[[WeakRefTarget]]` |
| `FinalizationRegistry` | `[[CleanupCallback]]`, `[[Cells]]` |

These are GC hooks; the slots are what the runtime walks when sweeping.

## 16. Module records

Modules aren't quite objects, but they have spec-level records with slots:

| Slot | Holds |
|---|---|
| `[[Module]]` | The Module Record |
| `[[Exports]]` | The export bindings |
| `[[Status]]` | `unlinked` / `linking` / `linked` / `evaluating` / `evaluated` |
| `[[Namespace]]` | The Module Namespace Object exposed to `import * as ns` |

`import.meta` is itself an object with host-defined slots (typically `[[URL]]`).

## 17. Mapping back to the questions we already covered

| Earlier question | Slot story |
|---|---|
| Why does `class Foo {}; Foo()` throw? | `Foo` has `[[Construct]]` only — no `[[Call]]` |
| Why does `new (() => {})()` throw? | Arrow has `[[Call]]` only — no `[[Construct]]` |
| Why do arrows close over `this`? | `[[ThisMode] = lexical` — no own this binding; lookup falls through `[[Environment]]` |
| Why do closures keep variables alive? | `[[Environment]]` pins the surrounding lexical environment record |
| Why does `super.foo()` work? | `[[HomeObject]]` says where to start the prototype walk |
| Why are class methods on the prototype but fields per-instance? | Methods → defined once on the constructor's `prototype` object; fields → entry in `[[Fields]]`, run per `new` call |
| Why can't `new obj.method()` work for shorthand? | Shorthand methods have no `[[Construct]]` |
| Why is `Promise.resolve(p) === p`? | `Promise.resolve` brand-checks `[[PromiseState]]` and same constructor |
| Why can `Date` instances compare with `<` but plain objects can't? | `Date.prototype[Symbol.toPrimitive]` reads `[[DateValue]]` |

## 18. How to peek at slots from JS

You can't read a slot directly. You read **what slots imply**:

```js
// [[Construct]] presence
function isConstructor(f) {
  try { Reflect.construct(String, [], f); return true; }
  catch { return false; }
}

// [[Prototype]]
Object.getPrototypeOf(o);

// [[Extensible]]
Object.isExtensible(o);

// [[PromiseState]] — only via async behavior or DevTools
// [[ProxyTarget]] — not reachable; this is intentional
```

DevTools (Chrome especially) shows many slots in the "Internal Properties" section when you expand an object — including `[[Prototype]]`, `[[PromiseState]]`, `[[ProxyTarget]]`, `[[BoundTargetFunction]]`, etc. That's the closest you get to reading them.

## 19. Mental-model summary

| Question | Answer |
|---|---|
| Are slots properties? | **No.** Properties live in ordinary storage; slots are spec-level cells the engine reads. |
| Can JS code read or write slots? | Not directly — only through methods and operators that consult them. |
| What makes "kinds" of objects different? | Which slots they have, plus which internal methods they override. |
| Why does a function differ from a class differ from an arrow? | Different combinations of `[[Call]]`, `[[Construct]]`, `[[ThisMode]]`, `[[ConstructorKind]]`. |
| Why is a Proxy invisible? | Its slots (`[[ProxyTarget]]`, `[[ProxyHandler]]`) are not exposed; identity checks see only the proxy. |
| Why are weak collections "weak"? | The GC treats `[[WeakMapData]]` / `[[WeakRefTarget]]` specially. |

## 20. One-sentence rule

> **An object's *behavior* is decided by which internal slots it has and which internal methods its kind overrides — properties are just the public face on top of that hidden machinery.**

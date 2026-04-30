# JavaScript — lexical scope, `this`, and memory captures

Most "JS is weird" pain comes from confusing two systems that look similar but operate at different times:

| System | Decided at | Driven by | Affects |
|---|---|---|---|
| **Lexical scope / closures** | *write time* (where the function is in the source) | source position | what variables a function can *see* |
| **`this` / call context** | *call time* (how the function is invoked) | call site | what `this` *points to* during execution |

They are **orthogonal**. A function written in one place but called from another has its variables resolved by the *first* and its `this` resolved by the *second*. Once you internalize this split, most of the "wat" examples stop being mysterious.

## 1. Lexical scope — what your function can see

When the parser sees a function, it captures a reference to the **environment record chain** that surrounded it. That chain is fixed forever; it doesn't matter who calls the function later.

```js
const x = 10;
function outer() {
  const y = 20;
  function inner() {
    console.log(x, y);   // resolves through the chain at DEFINITION
  }
  return inner;
}

const f = outer();   // outer returns; its scope COULD be GC'd…
f();                 // …but f still holds a reference → 10 20
```

### The chain

```
Global Environment
   ├── x = 10
   └── outer (function)
         ↑ captured by `outer.[[Environment]]`
         │
         outer's call:  outer Environment
                        ├── y = 20
                        └── inner (function)
                              ↑ captured by `inner.[[Environment]]`
```

Every function has an internal `[[Environment]]` slot pointing at the environment that existed where it was *written*. Variable lookups walk this chain. **Where you call the function from has no effect on this lookup.**

### `var` vs `let` / `const`

| Keyword | Scoped to | Hoisted? | Initialized? |
|---|---|---|---|
| `var` | nearest function or global | yes | `undefined` until line of declaration |
| `let` | nearest block (`{ … }`) | yes (in TDZ) | ReferenceError until line of declaration |
| `const` | nearest block | yes (in TDZ) | ReferenceError until line of declaration |

The classic loop bug shows the difference:

```js
// var — one binding shared by all iterations
const fns = [];
for (var i = 0; i < 3; i++) fns.push(() => i);
fns.map(f => f());   // [3, 3, 3]   ← all closed over the SAME i

// let — fresh binding per iteration
const fns2 = [];
for (let i = 0; i < 3; i++) fns2.push(() => i);
fns2.map(f => f()); // [0, 1, 2]
```

`let` in a `for` head creates a new binding each iteration; the closures capture *different* `i`s. `var` creates one binding for the whole function; all three closures see the same one — and by the time you call them, it's `3`.

## 2. Closures — captures and what stays alive

A closure is just a function value plus the environment it pinned via `[[Environment]]`. From a memory standpoint:

> A closure keeps alive **every variable in the captured environment record that the engine cannot prove is unused**.

In theory, the engine could GC variables the inner function never references. In practice, V8 / SpiderMonkey are conservative — they often keep the whole environment if any inner function exists. So:

```js
function setup(bigData) {
  // bigData captured even if log() never reads it
  return function log() { console.log("hello"); };
}
```

`bigData` may stay alive as long as `log` is reachable. The leak surfaces when you keep `log` in a long-lived collection (event handler, observer, cache).

### Common leak shapes

| Shape | What stays | Fix |
|---|---|---|
| `element.addEventListener("click", () => useCtx(ctx))` and the element is later detached but you never `removeEventListener` | element + ctx + ctx's whole capture chain | unregister, or store handler ref and use `AbortController` |
| `setInterval(cb, …)` where `cb` captures large state | the captured state lives forever | `clearInterval`, or store the id and clear on teardown |
| `const cache = new Map(); cache.set(key, () => use(big))` | every `big` per entry | `WeakMap`, or strip the closure into pure data |
| Subscribing to an event-emitter from a transient component | every closure across re-renders | `WeakRef`, or unsubscribe in cleanup |

### Shared mutable capture — same scope, multiple closures

Closures over the same environment record see *the same variables*. Mutations are visible across them:

```js
function make() {
  let counter = 0;
  return {
    inc:  () => ++counter,
    read: () => counter,
  };
}
const c = make();
c.inc(); c.inc();
c.read();   // 2
```

This is desirable when you mean it, painful when you don't. If you want isolation, give each closure its own variable.

## 3. `this` — the orthogonal system

`this` is **not** part of lexical scope. It's an *implicit parameter* that the engine sets up on every regular-function invocation, based on **how** the function is called.

### The five call patterns

Given `function f() { return this; }`:

| Call pattern | `this` becomes | Example |
|---|---|---|
| **Method call** | the object before the dot | `obj.f()` → `obj` |
| **Plain function call** | `undefined` (strict) / global (sloppy) | `f()` → `undefined` in modules / strict |
| **Constructor** | newly created object | `new f()` → `{}` (`f.prototype`-based) |
| **Explicit** | argument | `f.call(x)`, `f.apply(x)`, `f.bind(x)()` → `x` |
| **Arrow function** | **lexical** — `this` of enclosing scope, captured at definition | `() => this` returns whatever `this` was where the arrow was written |

The first four behave differently per call. The fifth — arrow — *doesn't have its own `this` at all*. It looks up `this` exactly like any captured variable.

### Why this is the source of every "lost `this`" bug

The dot before the name is what carries the binding:

```js
const obj = {
  name: "alice",
  greet() { return `hi ${this.name}`; }
};

obj.greet();             // "hi alice"

const g = obj.greet;     // detach: g points at the function, no dot
g();                     // TypeError: undefined has no .name

setTimeout(obj.greet);   // same: setTimeout calls f() not obj.f()
                          // → "hi undefined" (or TypeError in strict)
```

Once the dot is gone, the call pattern degrades to "plain function call" and `this` is `undefined`. Three repairs:

```js
setTimeout(() => obj.greet(), 100);          // arrow keeps the dot at call site
setTimeout(obj.greet.bind(obj), 100);        // pre-bind a permanent `this`
class Obj {                                  // class field with arrow → bound per instance
  greet = () => `hi ${this.name}`;
}
```

### Arrow function — lexical `this`, no `arguments`, no `new`

```js
const obj = {
  items: [1, 2, 3],
  log() {
    this.items.forEach(function (n) { console.log(this, n); });
    //                  ^^^^^^^^^^ regular function inside forEach
    //                  → forEach calls it without dot → this = undefined

    this.items.forEach((n) => console.log(this, n));
    //                  ^^^^^^^^^^^^^^^^^^^^^^^^
    //                  → arrow captures `this` from log() → this = obj
  }
};
```

That's why arrows became the "default" inside callbacks: they make `this` behave like every other variable — captured from where it was written.

Arrow functions also:
- Have no `arguments` object (use rest params: `(...args) =>`)
- Cannot be used with `new`
- Cannot be `bind`'d to a different `this` (the bound `this` is ignored)

### Constructors and classes

```js
class User {
  constructor(name) { this.name = name; }
  greet() { return `hi ${this.name}`; }
}
new User("alice").greet();     // "hi alice"
```

`new` creates the object, sets `this` to it, runs the constructor, returns the object. Class methods are regular functions on the prototype — they suffer the same "lost `this`" issue if detached.

## 4. The mental model — execution context vs lexical environment

Per the spec, every running call has an **Execution Context** with two distinct slots:

```
ExecutionContext
   │
   ├── LexicalEnvironment   ← variables (resolved by where written)
   │     └── chains up to the function's [[Environment]]
   │
   ├── VariableEnvironment  ← (var bindings, mostly the same as LexicalEnvironment)
   │
   └── ThisBinding          ← `this` (set by HOW the function was called)
```

When a function is called:

1. Engine creates a new Execution Context.
2. Sets `ThisBinding` based on the call pattern (method/plain/new/explicit) — **for arrows, it's not set; lookup falls through to the enclosing context**.
3. Sets `LexicalEnvironment` to a fresh record whose outer link is `[[Environment]]` (the captured chain) — **completely independent of how the call happened**.

```
Call:  obj.f(arg)
            │
            ▼
   ┌──────────────────────────────────────────────┐
   │ new ExecutionContext                          │
   │  ThisBinding       = obj                      │ ← from call pattern
   │  LexicalEnvironment = { arg, locals, … }      │
   │     outer ──────► f.[[Environment]]           │ ← from where f was defined
   └──────────────────────────────────────────────┘
```

That picture is the whole game.

## 5. Six classic gotchas — and why each one happens

| Code | What you expect | What you get | Why |
|---|---|---|---|
| `const f = obj.m; f()` | `this = obj` | `this = undefined` | Detached call — no dot, plain function |
| `setTimeout(obj.m, 1000)` | `this = obj` | `this = undefined` (or global) | Same — `setTimeout` invokes without dot |
| `[1,2,3].forEach(function (x) { this.log(x) })` inside a class | `this = instance` | `this = undefined` | `forEach` calls callback without `this` (unless you pass `thisArg`) |
| `for (var i = 0; i < 3; i++) setTimeout(() => log(i), 0)` | `0 1 2` | `3 3 3` | `var` is one binding; all timers see final `i` |
| `new f.bind(obj)()` | "f bound to obj, called as ctor" | bound `this` ignored | `new` overrides explicit binding |
| `class Foo { x = 1; m() { this.x } }` then `const m = new Foo().m; m()` | reads `1` | TypeError | Class methods aren't auto-bound; lost-`this` strikes again |

## 6. Capture diagram — closure + `this` at the same time

```js
class Counter {
  count = 0;
  start() {
    setInterval(function tick() {
      this.count++;             // this = undefined (timer calls without dot)
      console.log(this.count);
    }, 1000);
  }
}
```

```
Counter instance
   ├── count: 0
   └── start (method)

start() runs:
   ExecutionContext for start
      ThisBinding       = the instance        ← set by the dot
      LexicalEnvironment ─► start's locals ─► class scope ─► …

   creates `tick` (function expression)
      tick.[[Environment]] = start's LexicalEnvironment   ← captured
      (so tick CAN see `this` if it asked the right way…
       but `this` inside `tick` is the ThisBinding of TICK's context,
       not start's)

setInterval calls tick:
   ExecutionContext for tick
      ThisBinding       = undefined          ← plain call
      LexicalEnvironment ─► tick's locals ─► start's locals ─► …
                                    │
                                    └─ start's `this` was here lexically,
                                       but tick has its own ThisBinding slot
                                       that hides it.
```

Fix: use an arrow.

```js
setInterval(() => { this.count++; … }, 1000);
//          ^^ no own ThisBinding → falls through to start's
```

Now `this` inside the arrow is *looked up* in the surrounding lexical environment, finds start's `ThisBinding` (the instance), and works.

## 7. Practical heuristics

- **Default to arrow functions for callbacks** so `this` matches your reading of the code.
- **Default to `let` / `const`** so each iteration has its own binding.
- **Treat methods as if they're not bound** — if you're going to detach one, `bind` it or wrap it in an arrow.
- **In React class components / similar**: bind in the constructor or use class field arrows; never rely on auto-binding.
- **For long-lived references** (event listeners, intervals, observers): track them and tear them down. Closures are the #1 source of memory leaks in long-lived SPAs.
- **Use `WeakMap` / `WeakRef`** for caches keyed by objects you don't want to keep alive.
- **`new Function()` / `eval()`** break lexical scope — they don't capture surrounding variables. Prefer regular functions.

## 8. Mental-model summary

| Question | Lexical scope (vars) | `this` (call context) |
|---|---|---|
| When is it decided? | When the function is *written* | When the function is *called* |
| What controls it? | Source-code position | Call pattern (dot? `new`? `call`?) |
| Does the call site matter? | No | Yes |
| Does an arrow function differ? | No (same rules) | **Yes — captures `this` lexically** |
| Memory impact | Captured environment is kept alive while the function is reachable | Not retained — bound per call |

## 9. One-sentence rule

> **Variables come from where the function was *written*; `this` comes from how the function was *called*. Arrow functions break the second rule by treating `this` like a captured variable — and that's why they're the right default in callbacks.**

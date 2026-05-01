# JavaScript — function objects, prototypes, and the closure-vs-prototype trade-off

This is the companion to `javascript-this-and-closures.md`. That one covered *how variables are captured* (closures) and *how `this` gets bound* (call context). This one covers the **other thing** that confuses people about JS functions: that **functions are objects**, that **every regular function has a `.prototype` property** that's separate from its **own `[[Prototype]]`**, and **how to choose between closures and prototypes for organizing instances and methods**.

## 1. Functions are objects — own properties on the function itself

`typeof f === "function"` makes them feel like a separate species, but they are full-blown objects: you can put properties on them, store them in arrays, pass them as arguments. Every regular function has a few standard properties out of the box:

| Property | Meaning |
|---|---|
| `f.name` | Inferred or explicit name (`""` for anonymous) |
| `f.length` | Declared parameter count, ignoring rest/defaults |
| `f.prototype` | The `prototype` property — used by `new` (more on this below) |
| `f.[[Call]]`, `f.[[Construct]]` | Internal slots — what makes them callable / `new`-able |

You can attach your own properties:

```js
function tally() { tally.count++; }
tally.count = 0;
tally(); tally(); tally.count;   // 3
```

The function object lives independently of any specific call to it. Closures capture variables; **the function object itself is just a plain object** that happens to have call semantics.

## 2. Two different "prototype" things — the source of half the confusion

Every regular function `f` is involved with **two** prototype-related concepts that look the same but are completely different:

| The two things | Where it lives | What it is | Used for |
|---|---|---|---|
| `Object.getPrototypeOf(f)` (a.k.a. `f.__proto__`) | the function object's own `[[Prototype]]` slot | usually `Function.prototype` | gives `f` access to `.call`, `.apply`, `.bind`, etc. |
| `f.prototype` | a regular property on `f` | a plain object created automatically when `f` is declared | becomes the `[[Prototype]]` of objects created with `new f()` |

These point in **different directions**:

```
f                    Function.prototype           Object.prototype
│                          ▲                              ▲
│ [[Prototype]] ───────────┘                              │
│                                                         │
│ .prototype  ────►  { constructor: f, … }  ──[[Proto]]──┘
                              ▲
                              │
                              │ [[Prototype]]
                              │
                       (instance from `new f()`)
```

So `f.__proto__` is *what `f` inherits from* (the parent of the function itself).
`f.prototype` is *what instances created by `new f()` will inherit from* (the parent of future instances).

Easy to remember: the dotted property `f.prototype` is **for the kids**; `f.__proto__` is **for `f` itself**.

### Which functions have `.prototype`

- **Function declarations & expressions:** yes (auto-created `{ constructor: f }` object).
- **Class declarations:** yes — under the hood a class is a function with `.prototype`.
- **Arrow functions:** **no** `.prototype`. Arrows can't be used with `new`.
- **Methods in shorthand (`{ m() {} }` or `class { m() {} }`):** no `.prototype`.
- **`bind`-created bound functions:** no `.prototype`.

## 3. The prototype chain — how property lookup works

Every object has a `[[Prototype]]` (an object or `null`). When you read `obj.x`:

```
look on obj's own properties
        │ not found
        ▼
look on obj.[[Prototype]]
        │ not found
        ▼
look on (obj.[[Prototype]]).[[Prototype]]
        │ …
        ▼
       null  → result is `undefined`
```

Writing `obj.x = …` is **not** symmetric — it usually creates an own property on `obj`, shadowing the chain. (Unless there's a setter up the chain, in which case the setter is called.)

### A concrete chain

```js
function Animal(kind) { this.kind = kind; }
Animal.prototype.describe = function () { return `a ${this.kind}`; };

const a = new Animal("dog");
a.describe();   // "a dog"
```

The chain after `new`:

```
a  ──[[Proto]]──►  Animal.prototype  ──[[Proto]]──►  Object.prototype  ──[[Proto]]──►  null
│                       │
│ kind: "dog"           │ describe: function
│ (own property)        │ constructor: Animal
                        │ (these are shared by every dog)
```

`a.describe` isn't on `a` itself — it's looked up on `Animal.prototype`. That's the whole point: **the method is stored once on the prototype, not copied per instance.**

## 4. How `new` actually works — step by step

`new f(args)` is equivalent to:

```js
function callNew(f, args) {
  const obj = Object.create(f.prototype);    // 1. new object whose [[Proto]] is f.prototype
  const ret = f.apply(obj, args);             // 2. run f with `this` = obj
  return (ret !== null && typeof ret === "object") ? ret : obj;   // 3. if f returned an object, use that; else use obj
}
```

The four pieces:

1. **Create** an empty object whose prototype is `f.prototype`.
2. **Bind** `this` to that object.
3. **Run** the constructor body — it adds own properties to `this`.
4. **Return** the object (unless the constructor explicitly returns a non-null object — rare, but legal).

So the function's `.prototype` property is the *constructor's contract about what its products inherit*. Mutating it after instances exist also affects them, because the chain is live:

```js
function Animal(){}
const a = new Animal();
Animal.prototype.bark = () => "woof";
a.bark();   // "woof"  ← retroactively gained
```

## 5. Closures vs prototypes — two ways to pair state with behavior

This is the core trade-off. Both can model "an object with private state and methods." They differ in **memory** and **encapsulation**.

### (a) Closure-based — factory + captured state

```js
function makeCounter() {
  let count = 0;                           // private
  return {
    inc:  () => ++count,
    read: () => count,
  };
}

const c1 = makeCounter();
const c2 = makeCounter();

c1.inc !== c2.inc;       // true — each instance has its own closure
```

- ✅ `count` is **truly private** — unreachable from outside.
- ✅ No `this` shenanigans — methods are arrow functions over the closure.
- ❌ Each instance carries its own copy of every method (memory cost).
- ❌ No prototype chain to extend — composition over inheritance only.

### (b) Prototype-based — constructor + shared methods

```js
function Counter() { this.count = 0; }
Counter.prototype.inc  = function () { return ++this.count; };
Counter.prototype.read = function () { return this.count; };

const c1 = new Counter();
const c2 = new Counter();

c1.inc === c2.inc;      // true — ONE function shared across instances
```

- ✅ One copy of each method regardless of instance count (cheap).
- ✅ Easy inheritance: `Subtype.prototype = Object.create(Counter.prototype)`.
- ❌ `count` is **public** (`c1.count`) — pseudo-privacy by convention, or use `#private` fields / `WeakMap` / `Symbol`.
- ❌ `this` is needed inside methods → all the call-context pitfalls apply.

### Memory cost — concrete numbers

If you have `N` instances and `M` methods:

| Approach | Function objects in memory |
|---|---|
| Closure / factory | `N × M` |
| Prototype | `M` |

For a Counter, this is meaningless. For a UI component framework rendering 10,000 list items × 8 methods = 80,000 function objects vs 8 — it matters.

### Hybrid

Modern JS gives you both at once via class fields and `#private`:

```js
class Counter {
  #count = 0;                              // private slot, per-instance
  inc()  { return ++this.#count; }         // shared on Counter.prototype
  read() { return this.#count; }
}
```

`inc` and `read` live once on `Counter.prototype`; `#count` is per-instance, truly private. Best of both, at the cost of needing `this` (the closure pattern lets you forget `this` exists).

## 6. Class syntax — what it actually creates

```js
class Foo {
  constructor(x) { this.x = x; }
  greet() { return `hi ${this.x}`; }
  static create(x) { return new Foo(x); }
}
```

is roughly equivalent to:

```js
function Foo(x) { this.x = x; }
Foo.prototype.greet = function () { return `hi ${this.x}`; };
Foo.create = function (x) { return new Foo(x); };
```

So:

- **Instance methods** (`greet`) → on `Foo.prototype`.
- **Static methods** (`create`) → on `Foo` itself (the function object).
- **The class body's `constructor`** → the function `Foo` itself.
- **`extends Bar`** → sets `Foo.prototype.[[Proto]] = Bar.prototype` *and* `Foo.[[Proto]] = Bar` (for static inheritance).

`class` does **not** introduce new semantics — it's pure sugar over prototypes. But it does add small genuine differences: classes are *not callable without `new`*, the constructor body runs in strict mode, methods are non-enumerable.

## 7. The full chain when you use class + extends

```js
class Animal {
  constructor(name) { this.name = name; }
  greet() { return `hi ${this.name}`; }
}
class Dog extends Animal {
  bark() { return "woof"; }
}
const d = new Dog("rex");
```

Memory layout after `new Dog("rex")`:

```
d ─[[Proto]]─►  Dog.prototype ─[[Proto]]─►  Animal.prototype ─[[Proto]]─►  Object.prototype ─[[Proto]]─► null
│                  │                              │                              │
│ name: "rex"      │ bark, constructor: Dog       │ greet, constructor: Animal   │ toString, hasOwnProperty, …
```

Static side (less often discussed):

```
Dog ─[[Proto]]─►  Animal ─[[Proto]]─►  Function.prototype ─[[Proto]]─►  Object.prototype ─[[Proto]]─► null
```

So `Dog.someStaticOnAnimal()` works because of the **second** chain. That's why `extends` updates two `[[Proto]]` links.

## 8. `prototype.constructor` — the back-pointer

When JS auto-creates `f.prototype`, it sets `f.prototype.constructor = f`. So:

```js
function Foo() {}
new Foo().constructor === Foo;   // true
```

It's a courtesy back-link, not load-bearing. If you reassign the prototype wholesale you lose it; people often re-pin it manually:

```js
Sub.prototype = Object.create(Super.prototype);
Sub.prototype.constructor = Sub;       // restore the back-pointer
```

`class … extends` does this for you.

## 9. Common gotchas

| Code | Bug | Why |
|---|---|---|
| `Sub.prototype = Super.prototype` | mutating `Sub.prototype` mutates `Super.prototype` | Same object reference |
| `Sub.prototype = new Super()` | constructor side effects run at definition time | Use `Object.create(Super.prototype)` |
| `arrowFn.prototype.x = …` | TypeError | Arrow functions don't have `.prototype` |
| `obj.method = obj2.method; obj.method()` | wrong `this` | Methods aren't bound — see closures-and-`this` doc |
| Adding to `Array.prototype` | breaks `for…in`, third-party libs | Don't pollute built-in prototypes |
| `instance.constructor === Foo` after replacing prototype | false | `constructor` was lost, see §8 |
| Modifying `f.prototype` after instances exist | retroactively affects them | Live chain — usually you don't want this |

## 10. Mental model summary

| Question | Answer |
|---|---|
| Is `f` an object? | Yes — function objects are first-class. |
| What's `f.prototype`? | A property on `f`. Used by `new f()` to set the new instance's `[[Prototype]]`. |
| What's `f.__proto__`? | `f`'s own prototype (usually `Function.prototype`). What `f` itself inherits from. |
| What does `new f()` do? | Creates `Object.create(f.prototype)`, calls `f` with `this` = that object, returns it. |
| Where do prototype methods live? | On the constructor's `.prototype` object — shared by all instances. |
| Where do closure methods live? | In the closure environment — one copy per factory call. |
| When prefer prototypes? | Many instances, shared behavior, classical-OO style, low memory budget. |
| When prefer closures? | True privacy without `#fields`, factory style, no `this` headaches, few instances. |
| Is `class` magic? | No — it's sugar over function + prototype. With small extras (strict mode, non-callable, non-enumerable methods). |

## 11. One-sentence rule

> **A function is an object that owns a `.prototype` property used to seed instances created with `new`. Closures pair state with behavior per-instance via captured variables; prototypes pair state with behavior across-instances via a shared lookup chain. Class syntax is sugar over the prototype option; choose the closure option when privacy or per-instance independence matters more than memory.**

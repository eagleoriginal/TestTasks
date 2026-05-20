# Understanding Microsoft.OpenApi 2.x types in `AdvancedAnnotationsSchemaFilter`

## Context

You moved to .NET 10. That pulled in **Microsoft.OpenApi 2.x** (via Swashbuckle 10.x and Microsoft.AspNetCore.OpenApi 10.x). The OpenAPI object model was redesigned in that version — schemas are no longer a single concrete class, they are now a polymorphic hierarchy. Your filter
`TehnoInnovaLk.SharedServer/AspHelpers/Swagger/AdvancedAnnotationsSchemaFilter.cs:25` already reflects this — its signature is `Apply(IOpenApiSchema schema, ...)`, not `OpenApiSchema schema, ...`.

You are **still using Swashbuckle** (`ISchemaFilter`, `IOperationFilter`, `options.SchemaFilter<...>()`). What changed is *the types the filter receives*. `AddOpenApi()` in your `Program.cs` is from `Microsoft.AspNetCore.OpenApi` — a separate built-in generator — but you are not consuming its filters from this file. So forget `Microsoft.AspNetCore.OpenApi` for this question; the package that owns these types is **`Microsoft.OpenApi`**.

---

## The mental model: schema vs reference

In the JSON OpenAPI output a schema slot can appear in two structural forms:

**Inline schema** — the definition is written in place:
```json
"orderStatus": {
  "type": "string",
  "enum": ["Pending", "Done"],
  "description": "Order status"
}
```

**Reference** — a pointer into `components/schemas` (`$ref`):
```json
"orderStatus": { "$ref": "#/components/schemas/OrderStatus" }
```

In OpenAPI 3.0 anything that is *not* a `$ref` is a real schema; anything that *is* a `$ref` is only a pointer with no other content.

Microsoft.OpenApi 2.x models this with two concrete C# types behind one interface:

```
IOpenApiSchema                      // the slot — "something that resolves to a schema"
   ├─ OpenApiSchema                 // the inline definition (Type, Enum, Properties, AllOf, ...)
   └─ OpenApiSchemaReference        // wraps a $ref pointer (Reference.Id, ResolvedTarget)
```

Anywhere a schema can appear — a property type, a parameter type, an item in `AllOf` / `OneOf` / `AnyOf`, `Items` of an array, `AdditionalProperties` — the collection is typed as **`IList<IOpenApiSchema>`** (or `IOpenApiSchema`). At runtime each element is **either** an `OpenApiSchema` **or** an `OpenApiSchemaReference`. That is the polymorphism.

This is why your filter's setter sites have `if (schema is OpenApiSchema apiSchema) { ... }` casts (lines 58, 137): the interface `IOpenApiSchema` only exposes read-style members you can mutate on *both* shapes (`Description`, `Title` — properties common to both forms). To write `Extensions` you need the concrete inline class, because a `$ref` cannot carry siblings in OAS 3.0.

> Note: in OAS 3.1 a `$ref` *can* carry siblings — that is partly why the v2 library introduced this split (and why `OpenApiSchemaReference` itself can hold a few of those sibling properties). For OAS 3.0 you can treat the reference as opaque.

---

## Why the `AllOf` guard exists

The line you are asking about:

```csharp
if (schema.AllOf?.Any(apiSchema => apiSchema is OpenApiSchemaReference) ?? false)
{
    return;
}
```

It is interacting with this Swashbuckle option you have set in `Program.cs:265`:

```csharp
options.UseAllOfToExtendReferenceSchemas();
```

That option changes how Swashbuckle emits a **property whose type is a named (referenced) schema and which also has its own metadata** (description on the property, nullability, etc.). Without the option you get a bare `$ref` and any sibling description is lost. With the option Swashbuckle wraps it:

```json
"customerStatus": {
  "allOf": [ { "$ref": "#/components/schemas/CustomerStatus" } ],
  "description": "Status of the customer",
  "nullable": true
}
```

That wrapper is structurally **a real (inline) `OpenApiSchema`** whose `AllOf[0]` is an **`OpenApiSchemaReference`**. The schema filter is invoked on this wrapper just like on any other schema — and `context.Type` will be the wrapped enum/class type.

The guard says: *“if I am a wrapper that exists only to attach annotations to a `$ref`, bail out — do not apply my type-level decorations here.”*

The reason:

1. The referenced schema itself (`CustomerStatus`) is also visited by the filter, in its own call. Type-level annotations belong **there**, on the canonical schema, not on every wrapper site that points to it.
2. If you wrote, say, `apiSchema.Title = context.Type.Name + ...` on this wrapper, you would be putting type-level metadata onto a *property-level* schema slot. That pollutes one usage site and still leaves the actual component schema un-annotated.
3. `ApplyTypeAnnotations` (`line 125`) already short-circuits when `MemberInfo != null || ParameterInfo != null` — i.e. when the slot is clearly a property/parameter. The `AllOf+Reference` guard catches the **other** case Swashbuckle produces: a schema-position wrapper where MemberInfo is null, but the schema is still really just a decorated `$ref` and should be left alone.

So the guard is structural shorthand for: *"this isn't a real schema I should decorate; it's a $ref dressed up with extras."*

The same `allOf [ $ref ]` pattern shows up in inheritance: a derived class's schema is often emitted as `allOf: [ { $ref: BaseType }, { ...own properties... } ]`. The same logic applies — let the base's own filter call handle base-level annotations.

---

## Quick cheat sheet for the new types

| Old (Microsoft.OpenApi 1.x) | New (2.x) | Notes |
|-----------------------------|-----------|-------|
| `OpenApiSchema schema` in filter signatures | `IOpenApiSchema schema` | Interface; check concrete type to mutate |
| `schema.Reference != null` test for "is this a $ref?" | `schema is OpenApiSchemaReference` | Pattern-match instead of null check |
| `schema.Properties[name].Reference` | `schema.Properties[name] is OpenApiSchemaReference r ? r.Reference : null` | Properties values are `IOpenApiSchema` |
| Directly assign `schema.Extensions[...] = ...` | First `if (schema is OpenApiSchema concrete) concrete.Extensions ??= ...` | Reference shape can't always carry sibling extensions |
| `OpenApiReference` had `Type` enum (Schema, Response, ...) | Each reference shape is its own type: `OpenApiSchemaReference`, `OpenApiResponseReference`, `OpenApiParameterReference`, ... | Strongly typed per element kind |

Read-only properties (`Description`, `Title`, `Type`, `Enum`, `AllOf`, `OneOf`, `AnyOf`, `Properties`, ...) live on `IOpenApiSchema` and are safe to **read** without casting. To **mutate**, cast to the concrete `OpenApiSchema` first — exactly what your filter already does at lines 58 and 137.

---

## Practical decision tree when you receive an `IOpenApiSchema`

1. **Is it a `$ref` only?** → `schema is OpenApiSchemaReference` → leave it; decorate the target component instead.
2. **Is it a wrapper around a `$ref` via `allOf`?** → the guard you have (`AllOf.Any(... is OpenApiSchemaReference)`) → leave it.
3. **Is it a property/parameter slot?** → `context.MemberInfo != null || context.ParameterInfo != null` → apply member/param annotations only.
4. **Otherwise it's a real, owned schema** for `context.Type` → apply type-level annotations; cast to `OpenApiSchema` to write `Extensions`, etc.

That is the whole structural story behind the cryptic-looking guard.

---

## Critical files referenced

- `TehnoInnovaLk.SharedServer\AspHelpers\Swagger\AdvancedAnnotationsSchemaFilter.cs` — the filter under discussion
- `TehnoInnovaLk.Server\Program.cs:265` — where `UseAllOfToExtendReferenceSchemas()` is enabled (this is the option that makes the `allOf+$ref` wrappers appear)
- `TehnoInnovaLk.SharedServer\TehnoInnovaLk.SharedServer.csproj:33,88-91` — the package versions in play (Microsoft.AspNetCore.OpenApi 10.0.8, Swashbuckle.AspNetCore 10.1.7)

## Annotated walkthrough of your filter

`AdvancedAnnotationsSchemaFilter.cs`, top to bottom — what each block is doing in v2 terms.

### The signature: line 25

```csharp
public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
```

The first parameter is the new interface. The same method gets invoked for **every** schema Swashbuckle assembles: each DTO, each property's type-slot, each parameter, each `allOf`-wrapper, each component schema. The `context` tells you *what role* this particular call is playing:

- `context.Type` — the CLR type currently being mapped (always set).
- `context.MemberInfo` — non-null when the schema slot is a **property** on a class.
- `context.ParameterInfo` — non-null when the schema slot is an **action parameter**.
- `context.SchemaGenerator` + `context.SchemaRepository` — gateway to ask Swashbuckle to generate sub-schemas (used in `SwaggerConcreteTypeSchemaFilter.cs:30`).
- The `schema` you receive is *the slot in this role*. The same CLR type may visit your filter many times, in different roles.

The same type at, say, property position and at type position **does not share an `IOpenApiSchema` instance** when `UseAllOfToExtendReferenceSchemas` is on — the property position gets the inline wrapper, the type position gets the component schema. Two separate calls, two different `schema` objects, both for the same CLR type. Most v1→v2 confusion stems from not separating these two calls in your head.

### The enum block: lines 28–93

```csharp
if (context.Type.IsEnum) enumType = context.Type;
else if (Nullable<T> with T:enum) enumType = T;
```

Plain reflection; nothing OpenApi-specific. The interesting part for v2 is line 44:

```csharp
foreach (var eName in schema.Enum) { ... eName.AsValue().GetValueKind() ... }
```

In Microsoft.OpenApi 2.x `schema.Enum` is no longer `IList<IOpenApiAny>` (the v1 typed wrapper). It is `IList<JsonNode>` — raw `System.Text.Json.Nodes` values. That is why you call `AsValue().GetValueKind()` and `GetValue<string>()` / `GetValue<int>()`. The library aligned its "any value" representation to `JsonNode` to avoid maintaining a parallel type tree.

Then line 58:

```csharp
if (schema is OpenApiSchema apiSchema)
{
    apiSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
    apiSchema.Extensions.Add("EnumValues", new JsonNodeExtension(JsonValue.Create(enumStr)));
}
```

This is the **cast-to-write** pattern. `Extensions` is on the concrete `OpenApiSchema`, not on `IOpenApiSchema`, because in OAS 3.0 a pure `$ref` cannot carry siblings. The `if-pattern` makes the write a no-op when the slot happens to be a bare reference. `JsonNodeExtension` is the v2 wrapper that says "this extension value is a `JsonNode`" — it replaces the v1 `OpenApiString` / `OpenApiInteger` zoo.

### The guard you asked about: lines 95–98

Already covered in detail above. In one sentence: *“if this schema is an annotation-only wrapper around a `$ref`, stop — the canonical schema gets its own visit.”*

### `ApplyTypeAnnotations`: lines 125–154

```csharp
if (context.MemberInfo != null || context.ParameterInfo != null) return;
```

This is the **other half** of the role-disambiguation. Together with the `AllOf+$ref` guard, the two checks fence off "this is the canonical type-position visit, not a usage-site wrapper":

| `MemberInfo` set? | `ParameterInfo` set? | `AllOf` contains `$ref`? | What this call represents |
|---|---|---|---|
| yes | no | maybe | property slot — apply member-level annotations only |
| no | yes | maybe | parameter slot — apply parameter-level annotations only |
| no | no | **yes** | type slot **but** decorated `$ref` wrapper — skip |
| no | no | no | **canonical** schema for `context.Type` — decorate it |

Only the last row reaches `ApplyTypeAnnotations`, and that is the only place where a `DescriptionAttribute` *on the class itself* makes sense.

Then line 137:

```csharp
if (schema is OpenApiSchema schemaTyped)
{
    if (schema.Title != null) { ... schemaTyped.Title = ... }
}
```

Same cast-to-write pattern: read `Title` through `IOpenApiSchema`, mutate it through `OpenApiSchema`.

### Compare: `SwaggerConcreteTypeSchemaFilter.cs:23`

```csharp
if (context.MemberInfo == null || (schema.AllOf?.Any() ?? false)) return;
```

A different guard, same structural reasoning. This filter *only* wants property-slot calls (`MemberInfo != null`) and it skips slots that already have an `allOf` (any kind — not just `$ref`-wrappers) because the filter's job is to **add** an `allOf` entry, and re-running the filter on the post-modified shape would loop.

Reading these two guards side-by-side is the best way to internalize the role-disambiguation idiom. Same context object, very different intent, both using `MemberInfo`-presence + `AllOf`-shape as the discriminator.

## Where to look in your repo when in doubt

- `TehnoInnovaLk.SharedServer\AspHelpers\Swagger\AdvancedAnnotationsSchemaFilter.cs` — primary reference: shows the cast-to-write pattern at lines 58 and 137, and the role guard at line 95.
- `TehnoInnovaLk.SharedServer\AspHelpers\Swagger\SwaggerConcreteTypeSchemaFilter.cs` — contrasting use of the same context shape.
- `TehnoInnovaLk.Server\Program.cs:265` — `UseAllOfToExtendReferenceSchemas()` is what *makes* the wrapper case exist.
- `TehnoInnovaLk.Server\Program.cs:281,290,299,309` — note `OpenApiInfo` is in both `Microsoft.OpenApi` (new namespace) and `Microsoft.OpenApi.Models` (legacy shim). v2 collapsed many `Microsoft.OpenApi.Models.*` types up into `Microsoft.OpenApi.*`; the old namespace still resolves so existing code keeps compiling.

## “Is one schema generated per type / member / parameter, and is the filter invoked for each?”

Almost — with three nuances that matter.

### 1. Schemas are cached, the filter is **not** re-applied per usage

Swashbuckle keeps a `SchemaRepository` for the whole generation pass. The first time CLR type `Foo` is encountered it:

1. Generates the schema object.
2. Registers it at `components/schemas/Foo`.
3. Invokes every registered `ISchemaFilter.Apply` on it **once**.

Every later encounter of `Foo` — as a property type, parameter type, return type, collection item — produces a fresh `OpenApiSchemaReference` pointing back to the cached component. That reference is **not** passed through the filters again. So "one schema per CLR type" is true of the *canonical* schema, but the filter does not re-fire at every usage site.

### 2. A pure `$ref` slot does **not** invoke the filter

Slots that emit a bare `{ "$ref": "#/components/schemas/Foo" }` with no siblings are just pointers — no schema is *generated* for that slot, so no filter call happens there.

The filter **does** fire for slots where Swashbuckle has to generate a fresh schema object:

- Primitive inline schemas (`{ "type": "integer" }`).
- Wrapper schemas produced by `UseAllOfToExtendReferenceSchemas()` (`{ "allOf": [$ref], "description": ... }`).
- Inheritance/polymorphism wrappers (`{ "allOf": [$ref, { ...own props... }] }`).
- The canonical schema during first registration.

### 3. The same CLR type can produce **several** filter calls with different `context` shapes

For one CLR type `Foo` you can see multiple calls in a single generation pass:

| Call | `Type` | `MemberInfo` | `ParameterInfo` | Shape of `schema` |
|------|--------|--------------|------------------|--------------------|
| A | `Foo` | null | null | canonical component schema (the one decorated by `ApplyTypeAnnotations`) |
| B | `Foo` | the property | null | wrapper at a property slot |
| C | `Foo` | null | the param | wrapper at a parameter slot |

For *primitive* types there is no caching, so each property/parameter generates its own inline `OpenApiSchema` and the filter runs once per slot.

### Cases that are easy to forget

- **Method return types** go through the same generation pipeline as parameters and properties — same disambiguation rules.
- **Collection elements** (`List<Foo>`, `Foo[]`): the array slot is a freshly-generated schema (filter runs on it) and its `Items` resolves to a `$ref` to `Foo` (inert).
- **Dictionary values** (`IDictionary<string, Foo>`): same story via `AdditionalProperties`.
- **Inheritance**: derived types emit `allOf: [{ $ref: Base }, { ...own... }]`; the derived component’s filter call sees that shape — your `AllOf+$ref` guard would early-return on it, which is usually what you want for property/parameter slots but **not** for the canonical derived schema. If you ever need type-level annotations on a derived class, refine the guard to also check `MemberInfo == null && ParameterInfo == null` before bailing.
- **`IOperationFilter` is separate**: the same class implements both `ISchemaFilter` and `IOperationFilter`. `Apply(OpenApiOperation, OperationFilterContext)` runs **once per HTTP endpoint**, not per schema, and exposes `context.MethodInfo` instead of `context.Type`.

### v1 → v2 detail you may not have noticed

In v1 there was no `OpenApiSchemaReference` type. A schema was a `$ref` when its `.Reference` property was non-null on an otherwise-empty `OpenApiSchema`. The filter could therefore be called on a "reference schema" — same C# type, two meanings. In v2 the type system *itself* splits them, so the `schema is OpenApiSchemaReference` pattern is now a real type check rather than a property null-check. That is the only behavioural shift you actually need to internalise for filter migration.

## Verification

No code change is proposed in this plan — it is an explanation deliverable. To validate the model in your own running app:

1. Run `dotnet run -lp https_only` in `TehnoInnovaLk.Server`.
2. Open `/swagger/v1/swagger.json`.
3. Find a DTO property whose type is an enum or named class — note the `allOf: [{ $ref: ... }]` wrapper Swashbuckle emits.
4. Compare with the component schema definition at `components.schemas.<Name>` — that is where your type-level `Description`/`Title` and the `EnumValues` extension land (because the guard correctly skipped the wrapper).
5. Set a breakpoint at the top of `Apply(IOpenApiSchema, SchemaFilterContext)` and inspect `context.MemberInfo`, `context.ParameterInfo`, and `schema.AllOf` across successive calls to see the role-disambiguation table above play out for a single type.

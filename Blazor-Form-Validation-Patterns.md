# Blazor Form Validation Patterns in Adminka

Reference for **mutual and dependent validation** between two or more components inside `TehnoInnovaLk.Adminka` (Blazor + MudBlazor).

The canonical example is `TariffPositionEditDialog.razor` (parent dialog with a `<MudForm>`) containing `TariffPositionDiscountList.razor` (child component that hosts its own `<MudForm>`). The same six patterns recur throughout the project — this doc names each one, points at a live citation, and explains when to reach for it.

---

## Pattern 1 — Nested `<MudForm>` validity inheritance *(the headline rule)*

When a child component renders `<MudForm>` as its root inside the markup tree of a parent `<MudForm>`, MudBlazor automatically tracks the child form as a descendant of the parent. The parent's `IsValid` becomes `false` if any descendant form is invalid, and the parent's `Validate()` cascades down.

**Source**
- Parent form opens at `Components/Administrate/Partners/ProductTariffs/TariffPositionEditDialog.razor:24`.
- The child component sits inside that form at `TariffPositionEditDialog.razor:96-100`.
- Child's own form: `Components/Administrate/Partners/ProductTariffs/TariffPositionDiscountList.razor:4`.
- Parent's Submit relies on the inheritance — no child-specific validity check:

```csharp
// TariffPositionEditDialog.razor:339-343
await _form.Validate();
if (false == _form.IsValid)
{
    return;
}
```

**Use when** a child component encapsulates its own MudForm and you want its validity gating the outer Submit.

**Implication.** You do **not** need to expose `IsValid` or `Validate()` from the child just to make the parent wait on it. That would be duplicate plumbing.

---

## Pattern 2 — Per-field `Validation="..."` *(field-local rules)*

A method-group or lambda returning `string?` / `IEnumerable<string>` wired into a single input. Triggered when that input value changes (or when the form is validated).

**Examples**
- `TariffPositionEditDialog.razor:277` — `NomenklatureValidation` (rejects deleted products).
- `TariffPositionEditDialog.razor:322` — `PriceValidation` (non-negative, ≤2 decimal places).
- `TariffPositionEditDialog.razor:291` — `VatRateTypeValidation`.
- `TariffPositionDiscountList.razor:144` — `PriceValidation` reused in child.

**Use when** the rule depends only on the field's own value.

---

## Pattern 3 — Form-level dispatch validator `Func<object,string,IEnumerable<string>?>`

Wired as `Validation="ValidateForm"` on `<MudForm>`. The lambda receives `(model, fieldName)`; you `switch` on `fieldName` and read peer fields / component state. This is the right hammer for **cross-field** rules that live in the same form.

**Examples**
- `TariffPositionEditDialog.razor:247-266` — `AllowOrderFromIntegration` valid only when the selected nomenclature is a personified `LicenseNQ` type.
- `NomenklatureEditDialog.razor:214-273` — `LicenseTradeIn` / `QuartersToTradeIn` / `MaxQuarterForTradeIn` cross-validated against `ProductType`.

**Idiom for field names.** Always use `Dto.GetPropertyNamePath(x => x.SomeProp)` — refactor-safe, no magic strings:

```csharp
if (fieldName == Dto.GetPropertyNamePath(t => t.AllowOrderFromIntegration))
{
    // ...
}
```

**Use when** the rule for one field needs values of **other fields** in the same form.

---

## Pattern 4 — Cross-field re-validation triggered from change handlers

The dispatch validator (pattern 3) only fires for the field MudBlazor thinks changed. If field A invalidates field B, **B's error won't refresh until B is re-validated**. Pair cross-field rules with an explicit `_form.Validate()` from peer change handlers.

**Examples**
- `TariffPositionEditDialog.razor:107` — `DateChanged="(c) => { Dto.AvailableFrom = c; _form.Validate(); }"`.
- `TariffPositionEditDialog.razor:115` — same for `AvailableUntil`, so `ValidateFrom` / `ValidateUntill` (`:193-244`) see the new pair.
- `TariffPositionEditDialog.razor:269-274` — `SetSelectedNomenklature` does `await _form.Validate()` to refresh date-range overlap checks against the newly selected product.

**Use when** a single change can invalidate multiple peer rules (date ranges, type-dependent fields).

---

## Pattern 5 — Parent → child dependency via `[Parameter]` + child `OnParametersSetAsync` re-validate

The parent owns a value the child's validation depends on. Pass it as a `[Parameter]`; the child re-runs its own `Validate()` whenever parameters change. Combined with Pattern 1, the parent's `IsValid` automatically reflects the new child state.

**Example — parent → child**
```razor
@* TariffPositionEditDialog.razor:96-100 *@
<TariffPositionDiscountList Class="grid-fullRow"
                            PriceRulesDto="Dto.PriceRulesDto.PropertyValue"
                            MainPrice="Dto.Price"
                            VatRate="m_vatRate"
                            @ref="_positionDiscountList" />
```

**Example — child re-validates on parameter change**
```csharp
// TariffPositionDiscountList.razor:111-119
protected override async Task OnParametersSetAsync()
{
    if (_formWithDiscounts != null)
    {
        await _formWithDiscounts.Validate();
    }

    await base.OnParametersSetAsync();
}
```

(The child also calls `Validate()` once after `firstRender` at `TariffPositionDiscountList.razor:121-129` so initial state is reflected.)

**Use when** the child's validation rules depend on values **owned by the parent**. No `EventCallback` / `OnValidChanged` is needed — Blazor's parameter flow plus Pattern 1 carry the result back up.

---

## Pattern 6 — Public method on the child for state that is **not** a MudForm rule

Some constraints can't be expressed as a `<MudForm>` field rule — typically:

- "A row is mid-inline-edit, block Submit until it's committed or cancelled."
- "Every row in this table must have a non-empty value."

For these, expose a small public method on the child and call it from the parent's Submit, **in addition to** Pattern 1.

**Examples**
- `TariffPositionDiscountList.razor:210` — `public bool IsInEditMode() => elementBeforeEdit != null;` — consumed at `TariffPositionEditDialog.razor:344, :388`.
- `ProductionMetadataEditComponent.razor:287-290` — `public bool AllValuesValid()` — consumed at `ProductionEditDialog.razor:239`.

**Use when** the assertion lives outside the form's field validation (table edit state, transient UI state, custom data structures).

**Do not** use this pattern to expose `IsValid` / `Validate()` from a child that already hosts a `<MudForm>` — Pattern 1 already covers it.

---

## Pitfalls

### Inheritance only works inside the markup tree
A nested `<MudForm>` is tracked by the outer one only when it renders **inside** the parent's `<MudForm>...</MudForm>` markup. If the child is reached through a `RenderFragment` injected from outside, or through a cascaded layout slot, the inheritance chain can break. Verify by reading the parent `.razor` and confirming the child tag literally sits between the parent's `<MudForm>` and `</MudForm>`.

### Form-level dispatch silently no-ops on unknown field names
`ValidateForm` only fires for fields MudBlazor knows about — i.e. those bound via `For="@(() => Dto.X)"` or whose `fieldName` matches a `GetPropertyNamePath` call. A `switch` whose key never matches will silently return nothing. When wiring up a new branch, log `fieldName` once during development to confirm it fires.

### Manual re-validation is your responsibility
Cross-field rules (Pattern 3) do not auto-re-trigger when peer fields change — they only run when the field they're attached to is validated. Always pair them with `_form.Validate()` calls from each peer's change handler (Pattern 4).

### `field` keyword footgun
At `TariffPositionEditDialog.razor:249` there's a stray `Console.WriteLine(field);`. `field` is a C# 13 contextual keyword valid only inside property accessors; outside that context the code only compiles if there happens to be a local named `field`. Don't copy-paste this line into a new validator template.

---

## Critical files

| File | Patterns demonstrated |
|---|---|
| `Components/Administrate/Partners/ProductTariffs/TariffPositionEditDialog.razor` | 1, 2, 3, 4, 5, 6 |
| `Components/Administrate/Partners/ProductTariffs/TariffPositionDiscountList.razor` | child side of 1, 5, 6 |
| `Components/Administrate/Partners/Nomenklatures/NomenklatureEditDialog.razor` | second example of 3 |
| `Components/Administrate/CatalogProductions/ProductionEditDialog.razor` + `ProductionMetadataEditComponent.razor` | example of 6 (non-MudForm child state) |

---

## What Adminka does **not** use

- `EditForm` / `EditContext` (the stock Blazor validation pipeline).
- `FluentValidation` or any external validation library.
- `EventCallback`-based propagation of validation state up from a child.

All validation is MudBlazor's `Validation="..."` mechanism on `<MudForm>` and individual inputs. Stay within that convention unless there's a concrete reason to deviate.

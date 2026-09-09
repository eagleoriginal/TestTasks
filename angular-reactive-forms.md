# Реактивные формы: пять форм записи, два выключателя и одно дерево

Шпаргалка по Angular **22.1.5** (версия из `tehnoinnovalk.client`) и Kendo Angular **25.1.0**.
Все примеры — из этого репозитория, ничего выдуманного: каждая ссылка `файл:строка` открывается
и содержит ровно то, что написано рядом. Там, где конструкции в репозитории нет вовсе, пример
помечен как **синтетический** — таких мест шесть, и все они названы явно.

Утверждения про API сверены с `node_modules/@angular/forms/types/forms.d.ts` установленной
версии, а не с angular.dev: в формах слишком много поменялось между 14 и 22, чтобы верить памяти.

Соседний документ: [angular-animations-triggers.md](angular-animations-triggers.md).

---

## 1. Короткий ответ

| Надо | Жать это | Где |
|---|---|---|
| создать поле | `fb.group({ name: ['', Validators.required] })` | §2 |
| задать `updateOn` в шорткате | второй элемент кортежа: `['', {updateOn:'blur'}]` | §2 |
| понять, что ещё можно вписать в опции | список закрытый: `validators` / `asyncValidators` / `updateOn` / `nonNullable` | §3 |
| достать вложенный контрол без каста | типизировать корень — `get` уже типизирован | §4 |
| добавить или убрать валидатор в рантайме | `addValidators` + **обязательно** `updateValueAndValidity()` | §8 |
| проверить два поля друг против друга | `ValidatorFn` на группе, возвращающий `ValidationErrors` | §9 |
| показать ошибку с сервера | `aspFieldValidator` + `AspFormValidateSource` + `revalidateForm()` | §9 |
| записать значение молча | `setValue(v, { emitEvent: false })` | §7 |
| выключить кусок формы | `removeControl` (пропадёт из `value`) или `disable` (останется в `getRawValue`) | §11 |
| перебрать строки таблицы | `FormArray`; динамические ключи — `FormRecord` | §10 |
| подписаться без утечки | `BindToControl(this.destroyRef, ctrl, cb)` | §12 |
| узнать про `touched`/`pristine` реактивно | только `control.events`, отдельных потоков нет | §12 |
| сбросить форму после ответа сервера | ритуал из четырёх строк, а не `reset()` | §13 |

Три правила, которые закрывают большую часть паники:

1. **`onlySelf` — про предков, а не про потомков.** Он никогда не мешает изменению спуститься
   к детям; он мешает ему подняться к родителю.
2. **`emitEvent: false` не отключает валидацию.** Он отключает только эмиссию в обзёрваблы —
   значение и статус пересчитываются всегда.
3. **Всё, что меняет набор валидаторов, требует ручного `updateValueAndValidity()`.**
   Всё, что меняет значение, вызывает его само.

---

## 2. Что можно вписать в `fb.group({...})` — ровно пять форм

Главный источник тревоги: смотришь на `['', [Validators.required]]` и не помнишь, что здесь
значение, что валидатор и что ещё бывает. Ответ закрытый и лежит в JSDoc самого метода
(`forms.d.ts:4913-4963`), а тип кортежа — в `ControlConfig<T>` (`forms.d.ts:4820-4824`):

```ts
type ControlConfig<T> = [
    T | FormControlState<T>,                 // значение — или {value, disabled}
    (ValidatorFn | ValidatorFn[])?,          // синхронные валидаторы — или AbstractControlOptions
    (AsyncValidatorFn | AsyncValidatorFn[])? // асинхронные валидаторы
];
```

Пять форм, и ничего кроме:

| Что пишем | Что получается |
|---|---|
| `name: 'Ada'` | голое значение — то же, что `fb.control('Ada')` |
| `city: {value: 'London', disabled: true}` | `FormControlState` — значение **и** disabled-состояние |
| `email: ['a@b', Validators.email]` | кортеж `[значение, синхронный валидатор]` |
| `user: ['ada', Validators.required, checkName]` | кортеж `[значение, синхронный, асинхронный]` |
| `addr: new FormControl('', Validators.required)` | готовый контрол — проходит насквозь |

Первая позиция — **всегда значение**, вторая — **всегда валидаторы**. Перепутать их местами
не выйдет: у них разные типы, и TS ругнётся.

**Второй элемент принимает не только валидаторы.** Он же принимает `AbstractControlOptions` —
и это единственный способ задать `updateOn` в шорткате:

```ts
role: [{value: 'admin', disabled: true}, {updateOn: 'blur'}]
```

Как это выглядит в бою — `identity/register.component.ts:65-73`:

```ts
this.authGroup = this.formBuilder.group({
  userName: ['', [Validators.required, Validators.pattern(/.../), Validators.minLength(6),
                  aspFieldValidator(this.aspFormSource, 'userName')]],
  passwordRepeat: ['', Validators.required],
}, <AbstractControlOptions>{
  validator: MustMatch('password', 'passwordRepeat')
});
```

Второй аргумент `group()` — опции **самой группы**; туда идут кросс-полевые валидаторы.
Про каст `<AbstractControlOptions>` и ключ `validator` в единственном числе — §9. Это не
образец для подражания.

Форма `{value, disabled}` в репозитории встречается чаще простого литерала —
`tasks/task-add/task-add.component.ts:113-121`:

```ts
this.authGroup = this.formBuilder.group({
  name: [{ disabled: false, value: null }, [Validators.required, aspFieldValidator(this.aspFormSource, 'name')]],
  startImmediately: [true, [aspFieldValidator(this.aspFormSource, 'startImmediately')]],
});
```

### Чего писать не надо

`new FormControl(value, opts, asyncValidator)` — трёхаргументная форма помечена deprecated
(`forms.d.ts:1436-1438`) с формулировкой «при переданном `options` аргумент `asyncValidator`
не имеет эффекта». То есть асинхронный валидатор **молча игнорируется**. Передаёте объект
опций — складывайте асинхронные валидаторы туда же, в `asyncValidators`.

---

## 3. Опции: список закрытый

Второй источник тревоги — «а что ещё сюда можно вписать?». Ничего. Список исчерпывающий.

`AbstractControlOptions` (`forms.d.ts:2382-2398`) — для группы, массива и контрола:

| Поле | Тип |
|---|---|
| `validators` | `ValidatorFn \| ValidatorFn[] \| null` |
| `asyncValidators` | `AsyncValidatorFn \| AsyncValidatorFn[] \| null` |
| `updateOn` | `'change' \| 'blur' \| 'submit'` |

`FormControlOptions` (`forms.d.ts:1390-1403`) добавляет к этому ровно одно поле:

| Поле | Тип |
|---|---|
| `nonNullable` | `boolean` |

(плюс `initialValueIsDefault` — старое имя того же самого, помечено deprecated).

Всё. Четыре ключа на всю подсистему.

### `nonNullable` делает не то, что кажется по названию

Он не запрещает записать `null`. Он меняет **только поведение `reset()`**: без него `reset()`
без аргумента ставит `null`, с ним — возвращает то значение, с которым контрол был создан
(`forms.d.ts:1393-1397`).

Отсюда прямое следствие для нашего кода: `nonNullable: true` стоит на динамических контролах
(`marketplace-preorder-card.component.ts:332`, `:435`, `licenses-alienation.component.ts:77`),
а `reset()` не вызывается **нигде** (§13). Значит сейчас флаг не влияет ни на что. Он начнёт
работать в тот момент, когда появится первый `reset()`.

Это же закрывает вопрос, оставленный прямо в коде —
`cashboxes/cashboxes-table-editfrom/cashboxes-table-editfrom.component.ts:117`:

```ts
// control.reset() --  тут с options nonnullable разбираться надо
```

Разбираться так: ячейки создаются через `fb.control({disabled, value})` без `nonNullable`
(`:106`), поэтому `reset()` вернёт им `null`, а не исходное значение ячейки. Нужно
исходное — либо `nonNullable: true` при создании, либо `reset(значение)` явно.

---

## 4. Типизация: `<FormArray | null>form.get(...)` не нужен

Здесь главный разрыв между тем, как проект выглядит, и тем, что умеет Angular.

`tsconfig.json` включает `strict`, `strictTemplates` и `noPropertyAccessFromIndexSignature`.
При этом формы нетипизированы полностью: `new FormGroup`/`new FormControl` — ноль вхождений,
всё через `FormBuilder`; `fb.nonNullable.group` и `fb.group<...>` — ноль. Корневая группа
везде объявлена так:

```ts
authGroup!: FormGroup;            // marketplace-preorder-card.component.ts:233
positionsArrayGroup!: FormArray;  // :234
```

**`fb.group({...})` типы выводит.** Он возвращает `FormGroup<ɵNullableFormControls<T>>`
(`forms.d.ts:4963`), где `T` — переданный объект. Вывод выбрасывается на следующей же
строке — в момент присваивания в поле, объявленное как голый `FormGroup`, то есть `FormGroup<any>`.

Что теряется вместе с ним: `AbstractControl.get` **типизирован** (`forms.d.ts:3133-3140`).

```ts
get<P extends string | readonly (string | number)[]>(path: P):
    AbstractControl<ɵGetProperty<TRawValue, P>> | null;
```

`ɵGetProperty` разбирает и точечную строку `'address.street'`, и `['address','street'] as const`.
При типизированном корне `get` сам возвращает нужный тип, и каст не нужен в принципе.

### Во что это обходится сейчас

Каст плюс `!` плюс `| null` — фирменная форма записи в репозитории:

```ts
// marketplace-preorder-card.component.ts:257
return <FormRecord | null>this.positionControl(positionNumber).get(this.finRecAdvdata)!;
```

`!` («точно не null») и `| null` («может быть null») стоят в одном выражении и противоречат
друг другу: `!` снимает null, а каст его тут же возвращает. Компилятор молчит, потому что
каст сильнее любого вывода. То же самое — `subspace-edit.component.ts:94`,
`marketplace-balance-activate.component.ts:245,249`, `cashboxes-table-editfrom.component.ts:329`.

Дальше по цепочке `.value` тоже становится `any`, и кастовать приходится уже его —
`task-add.component.ts:179`:

```ts
var curValue = (<number[]>this.targetDevices.value);
```

### И во что это уже обошлось

`tasks/task-add/task-add.component.ts:253-280` — три геттера:

```ts
get taskTypeSelectiongr() { return <FormGroup | null>this.authGroup.get('taskSelectionGrname'); }
get rawCommandGr()        { return <FormGroup | null>this.authGroup.get('rawCommandGroupname'); }
get fileGr()              { return <FormGroup | null>this.authGroup.get('fileGroupname'); }
```

А ключи, под которыми эти группы кладутся в форму, объявлены на `:50-52`:

```ts
taskSelectionGrname = "taskSelection";
rawCommandGroupname = "rawCommand";
fileGroupname = "file";
```

В геттеры попало **имя поля вместо его значения**: ищется ключ `'taskSelectionGrname'`,
а лежит `'taskSelection'`. Все три всегда возвращают `null`. Спасает только то, что их никто
не вызывает — `grep` по `tasks/` находит ровно три строки объявления и ни одного
использования, а шаблон биндит группы напрямую (`[formGroup]="fileGroup"`,
`task-add.component.html:66,95`).

Это ровно тот класс ошибок, который типизированная форма ловит компилятором: для
`FormGroup<{taskSelection: ...}>` строка `'taskSelectionGrname'` в `get` не пройдёт.

### Что делать

Два честных выхода, третьего нет:

```ts
// 1. типизировать корень — тогда ни касты, ни геттеры-обёртки не нужны
authGroup!: FormGroup<{
  name: FormControl<string | null>;
  targetDevices: FormControl<number[] | null>;
}>;
```

либо **2.** оставить как есть, но осознанно — понимая, что `get` не проверяет ключи,
`.value` это `any`, и цена этому — геттеры выше.

Полумера, которая уже применена в репозитории ровно в одном файле и стоит копейки:
`nameof<T>()` вместо голых строк, `marketplace-licenses-alienation/licenses-alienation.component.ts:67-72`:

```ts
aspFieldValidator(this.aspFormSource, nameof<LkDtos_PartnerLicenses.LicenseAlienationDto>('targetUserId'))
```

Сравните с `register.component.ts:66-70`, где те же имена вписаны строками, и с
`subspace-edit.component.ts:195`, где ключ группы называется `finOrgname`, а поле на
сервере — `'name'`.

### Заодно: `value` против `getRawValue()`

`value` **не содержит disabled-полей**. `getRawValue()` содержит (`forms.d.ts:3066-3069`).
Это самая частая причина «я же заполнил, а на сервер ушло пусто»: поле выключено через
`disable()`, и в `value` его просто нет.

### Три дженерика, а не один

`AbstractControl<TValue, TRawValue, TValueWithOptionalControlStates>` (`forms.d.ts:2536`).
Второй — тип `getRawValue()` (с disabled-детьми), третий — тип аргумента `reset()`, который
умеет принимать `{value, disabled}` вместо голого значения. Руками задаётся только первый,
остальные выводятся.

---

## 5. Что запускает валидацию, а что нет

Валидаторы — это функции. Вопрос всегда один: в какой момент их позвали.

**Запускают прогон валидаторов:**

- `setValue()` / `patchValue()` — всегда, в том числе с `{emitEvent: false}`;
- `updateValueAndValidity()` — это и есть ручной запуск;
- ввод пользователем через UI — Angular зовёт `updateValueAndValidity` сам;
- создание контрола — стартовый статус тоже надо посчитать;
- `enable()` / `disable()` — статус пересчитывается, потому что `DISABLED` это тоже статус.

**Не запускают:**

- `markAsDirty()` / `markAsPristine()` (`forms.d.ts:2952-2981`);
- `markAsTouched()` / `markAsUntouched()` (`:2872-2932`);
- `markAllAsTouched()` / `markAllAsDirty()` (`:2889-2907`).

Переход `pristine → dirty` и `untouched → touched` к валидности не имеет отношения вообще:
это два независимых флага о том, трогал ли поле пользователь.

Отсюда же — **`setValue()` не делает контрол `dirty`**. Программная запись оставляет контрол
`pristine`; хотите иначе — зовите `markAsDirty()` руками. И наоборот: `markAsDirty()` не
перепроверит значение.

**Добавили или убрали валидатор в рантайме — валидация не запустится сама.** Набор валидаторов
и прогон валидаторов это разные вещи; нужен явный `updateValueAndValidity()` (§8).

**`setErrors()` — особый случай.** Он не запускает валидаторы, но обновляет статус и поднимает
изменение к родителю (`forms.d.ts:3096-3100`). И главное: выставленные вручную ошибки
**затираются результатом следующего прогона валидации** — прямо про это написано в JSDoc.
Поэтому `setErrors` годится для разовой пометки и не годится для ошибки, которая должна
пережить следующий ввод (§9).

---

## 6. Порядок выполнения

Что происходит после `setValue()` или `updateValueAndValidity()` — по шагам, сверху вниз
и потом снизу вверх:

**Фаза 1, сам контрол:**

1. обновляется внутреннее `value`;
2. прогоняются синхронные валидаторы;
3. выставляется статус (`VALID` / `INVALID`);
4. эмитит `valueChanges`;
5. эмитит `statusChanges`.

**Фаза 2, всплытие (если не задан `onlySelf: true`):**

6. родитель пересобирает своё `value` из детей;
7. родитель прогоняет **свои** валидаторы (кросс-полевые);
8. родитель выставляет статус — по своим валидаторам **и** по статусам всех детей;
9. родитель эмитит `valueChanges`;
10. родитель эмитит `statusChanges`;
11. и так далее до корня.

Порядок именно такой: ребёнок доигрывает свой цикл целиком, и только потом начинается
родительский. Поэтому в подписке на `valueChanges` ребёнка родитель ещё не пересчитан.

Название `updateValueAndValidity` — не «значение и валидность контрола», а «пересчитай
значение снизу вверх и заодно валидность». Для `FormControl` значение тривиально, а вот у
`FormGroup` и `FormArray` `value` — это производный объект из детей, и его надо пересобрать
даже если ни один ребёнок не менялся.

### С асинхронными валидаторами — две волны

**Синтетический раздел: асинхронных валидаторов в репозитории нет ни одного**
(`AsyncValidatorFn`, `asyncValidators`, `NG_ASYNC_VALIDATORS` — ноль вхождений). Как их
заменяют — §9.

Волна 1, синхронная:

1. значение обновилось;
2. прогнались синхронные валидаторы;
3. если синхронные прошли — статус становится **`PENDING`**, потому что запущена асинхронная
   проверка;
4. `valueChanges` эмитит новое значение;
5. `statusChanges` эмитит `PENDING`;
6. родитель тоже становится `PENDING` и эмитит.

Волна 2, по резолву:

7. асинхронный валидатор завершился;
8. статус ребёнка `PENDING → VALID | INVALID`;
9. `statusChanges` эмитит финальный статус — **`valueChanges` второй раз не эмитит**,
   значение-то не менялось;
10. родитель пересчитывает статус и эмитит свой.

### Что поменялось в Angular 20+

Два уточнения, которых нет в старых конспектах:

- **`markAs*` теперь принимают `{emitEvent}`** и эмитят в поток `events` —
  `PristineChangeEvent` и `TouchedChangeEvent` (`forms.d.ts:2872-2981`). Валидацию они
  по-прежнему не запускают, но молчаливыми быть перестали.
- **Появился `markAllAsDirty()`** (`forms.d.ts:2889`) — парный к давно знакомому
  `markAllAsTouched()`.

---

## 7. Два разных выключателя: `emitEvent` и `onlySelf`

Их путают постоянно, потому что оба выглядят как «сделай потише». Они выключают разное.

| | `emitEvent: false` | `onlySelf: true` |
|---|---|---|
| что глушит | эмиссию в `valueChanges`/`statusChanges`/`events` | обновление **предков** |
| пересчёт значения и статуса самого контрола | происходит | происходит |
| предки узнают об изменении | да | **нет** |
| предки эмитят события | нет | нет (их вообще не трогали) |

Короткая формулировка: `emitEvent: false` запрещает **кричать** подписчикам,
`onlySelf: true` запрещает **шептать** родителю.

### `emitEvent: false` не отключает валидацию

`child.setValue(v, {emitEvent: false})` — ребёнок прогонит свои валидаторы и обновит статус,
родитель тоже пересчитается. Не произойдёт только одного: обзёрваблы промолчат.

Так и задумано. Дерево формы обязано оставаться внутренне согласованным: если ребёнок стал
невалиден, родитель должен знать об этом немедленно, иначе `form.valid` начнёт врать.
Angular разделяет внутреннее обновление состояния (происходит всегда) и внешнюю эмиссию
событий (её можно заглушить).

Именно поэтому `{emitEvent: false}` — рабочий способ разорвать петлю «модель → форма → модель»
и в репозитории он расставлен именно так: `marketplace-preorder-card.component.ts:389,395`
пишет в контролы из модели, не поднимая обратной волны.

### `onlySelf: true` оставляет родителя врущим

А вот это уже опасно. `FormGroup.value` — **закэшированный снимок**, а не вычисляемое
свойство: группа не опрашивает детей при каждом чтении, она хранит собранный объект и
пересобирает его, когда ребёнок сообщил об изменении. Запретили сообщать — снимок остался
старым.

Живой пример, `cashboxes-table-editfrom.component.ts:523`:

```ts
this.cellControl(rowIndex, columnIndex)!.disable({ onlySelf: true, emitEvent: false });
```

Ячейку выключили, но родительскому `FormArray` об этом не сказали. Disabled-поля не должны
попадать в `value` (§4) — а здесь попадут, потому что снимок родителя не пересобирался.
Пока `getRawValue()` тут же не используется, это сходит с рук; станет заметно ровно в тот
день, когда кто-то начнёт читать значение строки целиком.

### Две группы вызовов, которые не делают ничего

Раз `onlySelf` относится к предкам, из этого следуют два наблюдения по репозиторию:

- **`onlySelf: false` — это значение по умолчанию.** Строки
  `markAsUntouched({ onlySelf: false })` и `markAsPristine({ onlySelf: false })`
  (`invite-register-user.component.ts:161-162`, `subspace-edit.component.ts:306-307`,
  `cashboxes-edit.component.ts:129-130`, `subspaces-users-add.component.ts:119-120`,
  `user-data-settings.component.ts:85-86`, `cashboxes-add-group.component.ts:135-136`)
  ведут себя ровно так же, как без параметра. Вреда нет, но и смысла тоже.

- **`onlySelf: true` на корневой группе не значит «не трогать детей».**
  `marketplace-preorder-card.component.ts:925,929` и `cashboxes-table-editfrom.component.ts:515,519`
  зовут `authGroup.disable({ onlySelf: true })`. Дети всё равно выключатся — `disable()`
  выключает всё поддерево по определению (`forms.d.ts:3004-3007`). А предков у корня нет.
  То есть флаг здесь не делает ничего вообще.

---

## 8. Добавить или убрать валидатор в рантайме

Полный набор методов (`forms.d.ts:2729-2852`):

| Метод | Что делает |
|---|---|
| `setValidators(v)` | **заменяет** весь набор |
| `addValidators(v)` | добавляет к существующим |
| `removeValidators(v)` | убирает конкретные (по ссылке) |
| `clearValidators()` | убирает **все** |
| `hasValidator(v)` | есть ли конкретный (по ссылке) |

Те же пять в асинхронном варианте: `setAsyncValidators`, `addAsyncValidators`,
`removeAsyncValidators`, `clearAsyncValidators`, `hasAsyncValidator`.

**После любого из них нужен `updateValueAndValidity()`** — иначе набор поменялся, а статус
остался от прошлого прогона.

Готовые обёртки в `utils/formUtils.ts`:

```ts
// formUtils.ts:13 — включить/выключить required
setRequiredValidator(ctrl, true);
// formUtils.ts:55 — enable/disable с проверкой текущего состояния
enableControl(ctrl, true);
```

`setRequiredValidator` (`formUtils.ts:13-28`) — образцовый: проверяет `hasValidator`, чтобы
не дублировать, и зовёт `updateValueAndValidity({ emitEvent: false })`.

### Ловушка: `hasValidator` сравнивает по ссылке

`Validators.required` — синглтон, одна и та же функция при каждом обращении. Поэтому
`hasValidator(Validators.required)` работает как ожидается.

А вот `Validators.minLength(6)`, `emailComplexValidator()` (`formUtils.ts:73`),
`aspFieldValidator(...)` (`:212`), `permanentErrorValidator(...)` (`:96`) — **фабрики**.
Каждый вызов возвращает новую функцию, и `hasValidator` для них всегда даст `false`.

Прямое следствие для `setValidatorsIfEmpty` (`formUtils.ts:31-53`):

```ts
if (contrl.hasValidator(validators[0])) { return; }   // :39 — на фабричном валидаторе никогда не сработает
contrl.setValidators(validators);
```

Проверка «уже стоит — не ставить второй раз» на фабриках не срабатывает, и `setValidators`
отрабатывает каждый раз заново. Здесь это безобидно, потому что `setValidators` **заменяет**
набор целиком, а не добавляет: дублей не накопится. Но если кто-то поменяет `setValidators`
на `addValidators`, защиты не останется.

Второй нюанс той же функции: ветка else зовёт `clearValidators()` (`:49`) — она снесёт
**все** валидаторы контрола, а не только те, что были переданы аргументом.

---

## 9. Валидаторы: свои, групповые, серверные

`ValidatorFn` — функция `(control: AbstractControl) => ValidationErrors | null`. Возвращает
объект ошибок или `null`, и ничего кроме. `null` значит «претензий нет».

Простой свой валидатор — `formUtils.ts:73-93`:

```ts
export function emailComplexValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (control.value == null) return null;
    if (IsString(control.value)) {
      if (!control.value) return null;
      return EMAIL_REGEXP.test(control.value) ? null : {'email': true};
    } else if ('text' in control.value && IsString(control.value.text) && !!control.value.text) {
      return EMAIL_REGEXP.test(control.value.text) ? null : {'email': true};
    }
    return null;
  };
}
```

Ветка с `'text' in control.value` существует не от хорошей жизни — это следствие того, что
Kendo-дропдауны кладут в контрол объект, а не строку (§14).

### Кросс-полевые валидаторы: как в репозитории и как надо

Групповой валидатор — это обычный `ValidatorFn`, просто повешенный на `FormGroup`; в
`control` ему приезжает группа. Правильный образец есть в этом же репозитории,
`marketplace-preorder-card.component.ts:356-360`:

```ts
{
  validators: [(control: AbstractControl): ValidationErrors | null => {
    return (!!this.FormModel$.value?.totalPositions) ? null : { [this.orderIsEmptyErrorName]: true };
  }]
}
```

А теперь — `MustMatch` (`formUtils.ts:107-124`), который так не умеет:

```ts
export function MustMatch(controlName: string, matchingControlName: string) {
  return (formGroup: FormGroup) => {
    const control = formGroup.controls[controlName];
    const matchingControl = formGroup.controls[matchingControlName];
    if (matchingControl.errors && !matchingControl.errors['mustMatch']) { return; }
    if (control.value !== matchingControl.value) {
      matchingControl.setErrors({ mustMatch: true });
    } else {
      matchingControl.setErrors(null);
    }
  }
}
```

Три проблемы разом:

1. **Возвращает `void`, а не `ValidationErrors | null`.** Значит это не `ValidatorFn`, и в
   `validators: [...]` его не передать — TS не пустит. Отсюда и берётся каст в четырёх файлах
   (`register.component.ts:71-73`, `restore-password.component.ts:72-73`,
   `subspaces-users-edit.component.ts:79-80`, `user-data-change-password.component.ts:60-61`):

   ```ts
   }, <AbstractControlOptions>{ validator: MustMatch('password', 'passwordRepeat') });
   ```

   Ключ `validator` в единственном числе — это legacy-форма, которой в современном
   `AbstractControlOptions` нет (§3), поэтому без каста код не собирается. Каст здесь не
   «для красоты» — он глушит ровно ту диагностику, которая пытается сказать, что функция
   не того типа.

2. **Ставит ошибку соседу через `setErrors`.** Ошибка оказывается на `passwordRepeat`, а не
   на группе. Это работает, но означает, что `setErrors(null)` в ветке else **сотрёт все
   ошибки контрола**, включая поставленные другими валидаторами. У `passwordRepeat` висит
   `Validators.required` (`register.component.ts:69`) — и в момент, когда пароли совпали,
   `required` с него снимется до следующего прогона валидации.

3. **Ошибки, выставленные `setErrors`, живут до следующего прогона** (§5). То есть результат
   `MustMatch` затирается любым `updateValueAndValidity` на этом контроле.

Как это пишется правильно (**синтетический пример**, в репозитории такого нет):

```ts
export function mustMatch(controlName: string, matchingControlName: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const a = group.get(controlName)?.value;
    const b = group.get(matchingControlName)?.value;
    return a === b ? null : { mustMatch: true };
  };
}
// и передаётся без каста:
this.formBuilder.group({ ... }, { validators: [mustMatch('password', 'passwordRepeat')] });
```

Ошибка при этом лежит на группе, читается как `authGroup.hasError('mustMatch')`, ничего
никому не затирает и переживает любой прогон валидации. Цена — в шаблоне ошибку надо
показывать от группы, а не от поля.

`MustBeGreater` (`formUtils.ts:128-145`) устроен точно так же и имеет те же три свойства.

### Серверные ошибки без асинхронных валидаторов

Самая своеобразная и при этом самая удачная идея в формах проекта. Задача стандартная:
сервер вернул `ProblemDetails` с ошибками по полям, надо показать их на полях. Обычно это
делают асинхронным валидатором; здесь — синхронно, и не зря.

Три части:

**1. Источник.** `AspFormValidateSource` (`formUtils.ts:260-281`) — держит
`BehaviorSubject<ProblemDetailErrors | null>`, то есть просто словарь «имя поля → массив
сообщений», плюс `clear(field)` и `setError(field, msg)`.

**2. Валидатор.** `aspFieldValidator(source, fieldName)` (`formUtils.ts:212-258`) — синхронно
читает из этого словаря:

```ts
return (control: AbstractControl): ValidationErrors | null => {
  if (!validateSource.ErrorResult.value) { return null; }
  var aspArray = validateSource.ErrorResult.value!.get(args[1]!);
  if (Array.isArray(aspArray) && !!aspArray.length) {
    return { [AspValidateError]: aspArray[0] };   // AspValidateError = 'aspError'
  }
  return null;
};
```

**3. Пинок.** После HTTP-ответа словарь наполняется, и зовётся `revalidateForm(this.authGroup)` —
чтобы валидаторы перечитали его (`subspace-edit.component.ts:305`,
`invite-register-user.component.ts:160`).

**Почему валидатором, а не `setErrors`.** Потому что ручные ошибки затираются следующим
прогоном валидации (`forms.d.ts:3100`), а валидатор — это и есть прогон. Серверная ошибка
держится ровно до тех пор, пока её не уберут из словаря, и переживает любой ввод. Снимается
она явно, обычно в подписке на изменение поля — `licenses-alienation.component.ts:82-84`,
`marketplace-preorder-card.component.ts:313-315`.

Цена подхода — имя поля это свободная строка, которая обязана совпасть с именем свойства
серверного DTO. Лечится через `nameof<T>()` (§4).

### `permanentErrorValidator` — прибить ошибку намертво

`formUtils.ts:96-103` — валидатор, который возвращает ошибку **всегда**:

```ts
return (control: AbstractControl): ValidationErrors | null => {
  return { [errorCodeName]: (!!args?.length) ? args[0] : true };
};
```

Применение — `marketplace-preorder-card.component.ts:334-340`: сервер сказал, что серийный
номер недействителен, и это факт о данных, а не о вводе. Пользователь ничего не может набрать,
чтобы его исправить, — значит ошибка должна держаться безусловно. Снимается вместе с
пересозданием контрола.

### Асинхронные валидаторы

**Синтетический раздел — в репозитории их нет.** `AsyncValidatorFn` это
`(control) => Promise<ValidationErrors|null> | Observable<ValidationErrors|null>`. Пока он
в полёте, статус контрола — `PENDING` (§6). Главное требование: поток **обязан завершиться**,
иначе контрол останется `PENDING` навсегда. Для потоков из HTTP это бесплатно; для
самодельных `Subject` — нет.

---

## 10. Дерево: `FormGroup`, `FormArray`, `FormRecord`

| Класс | Когда | Ключи | Дети |
|---|---|---|---|
| `FormGroup` | фиксированный набор полей | известны на этапе компиляции | разнотипные |
| `FormArray` | список одинаковых элементов | индексы `0..n` | однотипные |
| `FormRecord` | словарь с динамическими ключами | приходят из данных | **однотипные** |

`FormRecord extends FormGroup` (`forms.d.ts:2181`) — это буквально группа, у которой все дети
обязаны быть одного типа, зато ключи можно добавлять и убирать в рантайме. Отсюда же ответ
на вопрос «почему он биндится через `formGroupName`»: потому что он и есть `FormGroup`
(`marketplace-preorder-card.component.html:102`).

### Как глубоко это бывает

Самая глубокая конструкция в проекте — карточка предзаказа. Пять уровней:

```
authGroup (FormGroup)
└── finArrPositions (FormArray)          — позиции заказа
    └── [i] (FormGroup)                  — одна позиция
        ├── finInOrder (FormControl)
        ├── finCount   (FormControl)
        └── finRecAdvdata (FormRecord)   — ключ = серийный номер кассы
            └── "0012 3456 ..." (FormControl)
```

Сборка — `marketplace-preorder-card.component.ts:291-355`, доступ — `:236-265`.
Вторая по сложности — таблица настроек кассы, `FormArray<FormArray>`
(`cashboxes-table-editfrom.component.ts:62,99-126`): внешний массив — строки, внутренний —
ячейки строки.

### Как доставать

```ts
form.get('a.b.c')          // точечный путь, работает через все три класса
form.get(['a', 0, 'c'])    // массив — единственный способ для числовых индексов
array.at(i)                // элемент массива
group.controls['x']        // прямой доступ к словарю детей
record.contains('key')     // есть ли такой ключ
```

`get` возвращает `AbstractControl | null`, `at` — сам контрол. Про то, почему в репозитории
результат всегда кастуют, и почему это не обязательно — §4.

### Рекурсивный обход

`revalidateFormV2` (`formUtils.ts:179-198`) — рабочая версия, её и звать:

```ts
export function revalidateFormV2(cg: FormGroup<any>|FormRecord|FormArray|AbstractControl) {
  if (cg instanceof FormGroup) { /* обойти controls */ }
  else if (cg instanceof FormArray) { /* обойти controls */ }
  else if (cg instanceof FormRecord) { /* ← сюда управление не дойдёт */ }
  else { cg.updateValueAndValidity({onlySelf: false}); }
}
```

Две вещи, которые стоит знать:

- **Ветка `FormRecord` (`:189`) недостижима.** `FormRecord extends FormGroup`, а
  `instanceof FormGroup` проверяется первым (`:180`) — любой `FormRecord` уходит в первую
  ветку. Поведение от этого верное (тела веток идентичны), но код мёртвый.
- **`updateValueAndValidity({onlySelf: false})` зовётся на каждом листе.** Каждый лист
  поднимает пересчёт до корня, то есть корень пересобирается столько раз, сколько в форме
  листьев. На формах текущего размера незаметно; на таблице 50×20 это уже тысяча проходов.

`revalidateForm` (`formUtils.ts:167-176`) — старая версия, **не умеет `FormArray`**: она
проверяет только `instanceof FormGroup`, а массив уходит в ветку `else` и получает
`updateValueAndValidity()` как лист, из-за чего его элементы не перевалидируются. Именно эта
версия вызывается после серверных ответов (`subspace-edit.component.ts:305`,
`invite-register-user.component.ts:160`) — там форм с массивами нет, поэтому проблема не
проявляется. В формах с `FormArray` звать надо `revalidateFormV2`.

---

## 11. Динамические формы

Два независимых механизма, которые легко перепутать: менять **ключи группы** и менять
**элементы массива**.

### Ключи группы

```ts
group.addControl(name, control, {emitEvent?})     // добавить
group.removeControl(name, {emitEvent?})           // убрать
group.setControl(name, control, {emitEvent?})     // заменить (или добавить)
group.registerControl(name, control)              // добавить без пересчёта родителя
group.contains(name)                              // проверить
```

**Правило, которое всё упрощает: контрола нет в группе — он не валидируется и не попадает
в `value`.** Это и есть штатный способ выключить кусок формы.

Чем это отличается от `disable()`: при `disable()` контрол остаётся в дереве, его значение
сохраняется и достаётся через `getRawValue()`, а статус становится `DISABLED`. При
`removeControl` не остаётся ничего. Выбор между ними — это выбор «нужно ли мне потом это
значение».

Переключение ветки формы по дискриминатору — `subspace-edit.component.ts:193-272`. Три
заранее собранные группы кладутся в один и тот же слот:

```ts
this.grNone = this.formBuilder.group({});        // пустышка для «тип не выбран»
// ...
if (this.authGroup.get(this.finLegalGroup) != this.grIndividual) {
  this.grIndividual.patchValue({ ... }, { emitEvent: true });
  this.authGroup.setControl(this.finLegalGroup, this.grIndividual);
}
```

Приём с пустой группой `grNone` вместо `removeControl` стоит взять на вооружение: слот всегда
занят, шаблону не надо проверять существование, а валидировать в пустой группе нечего.

Три необязательные секции — `task-add.component.ts:192-235`, самый полный пример:
`contains` → `setControl` / `removeControl` для каждой из трёх подгрупп.

### Элементы массива

```ts
array.push(control)          array.insert(i, control)
array.removeAt(i)            array.clear()
array.at(i)                  array.setControl(i, control)
```

**В репозитории не используется ничего из этого списка.** `push`/`removeAt`/`clear`/`insert`
на `FormArray` — ноль вхождений. Массивы пересобираются целиком: собирается обычный JS-массив
контролов, из него делается новый `FormArray`, и он кладётся в форму
(`cashboxes-table-editfrom.component.ts:99-126`, `marketplace-preorder-card.component.ts:291-355`).

Так тоже можно, но цена есть: пересборка теряет `dirty`/`touched` на всех элементах и рвёт
ссылки, которые шаблон мог держать на старые контролы. Если понадобится добавлять строки
по одной, не теряя состояния, — вот тут и нужен `push`/`removeAt`.

### Диффинг ключей `FormRecord`

Готовый образец, `marketplace-preorder-card.component.ts:415-450` — сравнить с моделью,
лишнее убрать, недостающее добавить:

```ts
// удалить несуществующие
[...Object.keys(advDataRecord.controls)].forEach(serial => {
  if (false == currentPos.advdata.some(ad => ad.serialNumber == serial)) {
    advDataRecord.removeControl(serial);
  }
});
// добавить недостающие
currentPos.advdata.forEach(curAdvData => {
  if (advDataRecord.contains(curAdvData.serialNumber)) { /* обновить валидаторы */ return; }
  let control = this.formBuilder.control({value: ..., disabled: ...}, { nonNullable: true });
  advDataRecord.addControl(curAdvData.serialNumber, control, { emitEvent: false });
});
```

Копия ключей через `[...Object.keys(...)]` — обязательна: без неё удаление шло бы по
коллекции, которую тут же меняют.

Мелочь, на которую стоит смотреть при правках: `addControl` вызван с `{emitEvent: false}`,
а `removeControl` десятью строками выше — без. То есть удаление ключа поднимает волну
пересчёта и событий, а добавление — нет. Скорее всего несознательно.

### Фрагмент формы в отдельном компоненте

Локальная замена `ControlValueAccessor` — `user-data-all/user-partial-card/user-partial-card.component.ts:34-55`:

```ts
@Input({required: true}) authGroup!: FormGroup;

ngOnInit(): void {
  this.authGroup.registerControl('fullName',
    this.formBuilder.control(this.initData?.fullName, {validators: [aspFieldValidator(this.aspFormSource, 'fullName')]}));
  // ещё два
}
```

Шаблон — `<div [formGroup]="authGroup">`. Работает, и для трёх полей это дешевле CVA. Цена
честная: ребёнок знает и правит структуру родителя, имена контролов нигде не проверяются,
и в валидацию родителя компонент встроиться не может — только через те валидаторы, что сам
навесил. Когда этого станет мало — §15.

---

## 12. Подписки: `valueChanges`, `statusChanges` и `events`

### Как в репозитории

Ровно один способ — `BindToControl` (`formUtils.ts:200-208`):

```ts
export function BindToControl(destroyRef: DestroyRef, control: AbstractControl,
                              d: (newval: any, control?: AbstractControl) => void) {
  control.valueChanges.pipe(
    tap((val) => { d(val, control); }),
    takeUntilDestroyed(destroyRef)
  ).subscribe();
}
```

`destroyRef` передаётся аргументом, а не берётся из контекста инъекции, — поэтому функцию
можно звать откуда угодно, в том числе из циклов сборки формы. Утечек не даёт.

`statusChanges` не используется **ни разу**.

### Чего в репозитории нет

Начиная с Angular 18 у каждого контрола есть единый типизированный поток
`events: Observable<ControlEvent>` (`forms.d.ts:2691`) с шестью видами событий
(`forms.d.ts:2299-2375`):

| Событие | Поле | Когда |
|---|---|---|
| `ValueChangeEvent<T>` | `value` | значение изменилось |
| `StatusChangeEvent` | `status` | статус изменился |
| `TouchedChangeEvent` | `touched` | `touched` ⇄ `untouched` |
| `PristineChangeEvent` | `pristine` | `pristine` ⇄ `dirty` |
| `FormSubmittedEvent` | — | форма отправлена |
| `FormResetEvent` | — | форма сброшена |

У всех есть `source: AbstractControl` — контрол, из которого событие пришло. Это важно:
на группе можно слушать события всего поддерева и разбирать по источнику.

**Это единственный реактивный способ узнать про `touched` и `pristine`.** Отдельных
обзёрваблов для них не было никогда. Сейчас эти флаги опрашиваются синхронно на каждый цикл
проверки — `mixins/mixin-common.ts:334-336`:

```ts
public MustShowControlErros(control: AbstractControl) {
    return this.submitted && (control.touched || control.dirty) && control.errors;
}
```

Метод зовётся из шаблона по нескольку раз на поле (`register.component.html:51,56,78,82,93,97,107,111` — восемь вызовов на четыре поля),
то есть на каждую проверку изменений. Пока полей десятки — не проблема; но именно эту
конструкцию `events` и позволяет заменить на подписку.

Пример (**синтетический**, в репозитории `events` не используется):

```ts
control.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(e => {
  if (e instanceof TouchedChangeEvent) { /* e.touched, e.source */ }
  if (e instanceof StatusChangeEvent)  { /* e.status */ }
});
```

### Zoneless: `markForCheck` руками и `delay(10)`

Приложение работает без Zone.js — `app.config.ts:29`, `provideZonelessChangeDetection()`.
Формам это ничего не сломало (`valueChanges` — обычный обзёрвабл), но два следа видны.

**Ручные `markForCheck()`** после записи в форму из подписки —
`marketplace-preorder-card.component.ts:386`, `marketplace-balance-activate.component.ts:358`,
`cashboxes-table-editfrom.component.ts:160,178`. Это ровно то, от чего избавляет `toSignal`:
сигнал уведомляет планировщик сам.

**`delay(10)` перед мутациями формы** — `subspace-edit.component.ts:225`,
`task-add.component.ts:174`, `marketplace-preorder-card.component.ts:372`,
`cashboxes-table-editfrom.component.ts:131`. Это способ отложить правку формы на следующий
макрозадачный тик, чтобы не менять состояние посреди цикла проверки. Работает, но держится
на том, что 10 мс «обычно хватает», — величина ни к чему не привязана. Штатные средства для
той же задачи — `afterNextRender` и `queueMicrotask`; переход на них разбирается в документах
про zoneless, здесь только фиксируем, что это за конструкция.

Правок по этой части документ не предлагает — она относится к переходу на zoneless и делается
отдельно.

---

## 13. Сброс формы после ответа сервера

**`reset()` в проекте не вызывается ни разу.** Вместо него — ритуал из четырёх строк,
повторённый дословно примерно в шести компонентах. Эталон —
`invite-register-user.component.ts:160-163`:

```ts
revalidateForm(this.authGroup);                        // перечитать серверные ошибки (§9)
this.authGroup.markAsUntouched({ onlySelf: false });   // «пользователь ничего не трогал»
this.authGroup.markAsPristine({ onlySelf: false });    // «изменений нет»
this.authGroup.enable({ emitEvent: false });           // снять блокировку на время запроса
```

Значения при этом **сохраняются** — сбрасываются только флаги. Это осознанно: после успешного
сохранения форма должна показывать то, что сохранили, а не пустоту.

Чем от этого отличается `reset()`: он ещё и меняет значения — на `null` либо на исходные,
если контрол создан с `nonNullable: true` (§3). Для «сохранили и остались на странице» это
не то, что нужно; для «форма добавления, очистить и дать ввести следующее» — ровно то.

Парный ритуал на входе в отправку — `markAllAsTouched()` плюс `this.submitted = true`,
первые строки каждого `onSubmit()` (`register.component.ts:129`, `signin.component.ts:119`,
`task-add.component.ts:329`, `cashboxes-add.component.ts:152` и ещё четырнадцать мест).
Смысл: до отправки ошибки показываются только на тронутых полях, после — на всех.
`MustShowControlErros` (§12) читает оба флага сразу.

---

## 14. Kendo и реактивные формы

Все контролы в проекте — кендовские, своих нет ни одного (§15). Каноническая разметка поля —
`identity/register.component.html:48-76`:

```html
<form [formGroup]="authGroup" class="k-form k-form-xs" (ngSubmit)="onSubmit()">
  <fieldset [disabled]="pending$|async" class="k-form-fieldset">
    <kendo-formfield [class.k-form-field-error]="MustShowControlErros(userName)">
      <kendo-floatinglabel text="Login пользователя">
        <kendo-textbox formControlName="userName" [clearButton]="true" />
        @if(MustShowControlErros(userName); as s0) {
          <app-common-field-error [errors]="s0">
            @if(s0['minlength']) { <div>Минимум 6 символов</div> }
          </app-common-field-error>
        }
      </kendo-floatinglabel>
    </kendo-formfield>
```

`app-common-field-error` (`util-components/common-field-error/`) показывает общие коды —
`required` и `aspError` — а частные сообщения приходят в него через `<ng-content>`. Приём
хороший: серверная ошибка (§9) отображается автоматически везде, где компонент используется.

Что стоит знать про связку:

**1. `kendo-formfield` не подхватывает состояние ошибки сам.** Отсюда ручное
`[class.k-form-field-error]="MustShowControlErros(x)"` на каждом поле. Забыть эту привязку —
значит получить поле, которое невалидно, но выглядит нормально.

**2. Дропдауны кладут в контрол объект, а не примитив.** Это главная боль связки.
`[valuePrimitive]="false"` вместе с `textField`/`valueField` (`cashboxes-edit.component.html:30`,
`cashboxes-table-editfrom.component.html:93-94,102-103`) означает, что `control.value` — это
`LkDtos.DtoForUi<T>`, объект `{text, value}`.

Последствия расходятся по всему коду:

- валидаторы обязаны это учитывать — `'text' in control.value` в `emailComplexValidator`
  (`formUtils.ts:85`);
- подписки читают `v.value`, а не `v` (`subspace-edit.component.ts:208`);
- запись требует поиска объекта по примитиву, а не просто `patchValue`
  (`subspace-edit.component.ts:277`):

  ```ts
  this.allUtcOffsets.filter(v => v.value == val.Model.utcOffset)[0]
  ```

Альтернатива — `[valuePrimitive]="true"`, тогда в контроле лежит примитив, а Kendo сам ищет
объект в `[data]`. Выбор между ними стоит делать один раз на проект, а не на поле.

**3. Индекс массива как имя контрола.** `cashboxes-table-editfrom.component.html` строит
таблицу так:

```
[formGroup]="authGroup"                     → :1
  [formArrayName]="arrayGroupName"          → :3    (массив строк)
    [formGroupName]="DataItem.rowNumber - 1" → :75   (строка — тоже FormArray!)
      [formControlName]="cellControlName"    → :84   (ячейка, индекс числом)
```

Строка таблицы — это `FormArray`, но привязана она директивой `formGroupName`, а не
`formArrayName`. Работает, потому что обе директивы ищут ребёнка по имени в родителе; но
`FormGroupName` объявляет `control: FormGroup` (`forms.d.ts:4090`), а лежит там `FormArray`
(`FormArrayName` — отдельный класс, `forms.d.ts:4129`). Для новых мест правильная директива
`formArrayName`.

**4. Внутри шаблонов `kendo-grid` контекст директив формы теряется.** Ячейка грида
рендерится в своём `ng-template`, и `formControlName` там не к чему привязаться. Обход —
резолвить контрол методом компонента и биндить через `[formControl]`,
`marketplace-balance-activate.component.html:128-135`:

```html
<ng-template let-DataItem="dataItem" let-serialsRec="serialsRec" #serialNumberCtrl>
  @let serialCl = serialFromRecorControl(serialsRec!, DataItem.serialNumber);
  @if(!!serialCl) {
    <kendo-maskedtextbox [formControl]="serialCl" [readonly]="true">
```

**5. `(ngSubmit)` есть не везде.** На двух самых больших формах
(`marketplace-preorder-card.component.html:7`, `marketplace-balance-activate.component.html:7`)
его нет — отправка идёт кендовскими кнопками напрямую. Практическое следствие: `FormSubmittedEvent`
(§12) там не выстрелит никогда, и `updateOn: 'submit'` работать не будет.

**6. У Kendo свой поток изменений.** `(valueChange)` — это выход компонента, отдельный от
`valueChanges` контрола. Директива дебаунса подписана именно на него
(`util-components/directives/debounceValueChangedDirective.ts:26-28`, `@HostListener("valueChange")`).
То есть в приложении два параллельных потока изменений одного и того же поля; при отладке
стоит помнить, какой из них смотришь.

---

## 15. Свои контролы: `ControlValueAccessor`

**Синтетический раздел: в репозитории нет ни одного CVA.** `ControlValueAccessor`,
`NG_VALUE_ACCESSOR`, `writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`,
`NG_VALIDATORS` — ноль вхождений во всём `src`. Раздел нужен, чтобы при появлении первого
не пришлось собирать контракт заново.

CVA — это переводчик между `FormControl` и вашим компонентом. Четыре метода:

```ts
@Component({
  selector: 'app-my-input',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => MyInputComponent), multi: true }],
})
export class MyInputComponent implements ControlValueAccessor {
  private onChange: (v: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string): void { /* форма → компонент */ }
  registerOnChange(fn: (v: string) => void): void { this.onChange = fn; }   // компонент → форма
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }          // компонент → форма (blur)
  setDisabledState(isDisabled: boolean): void { /* реакция на disable() */ }
}
```

Что важно помнить:

- `writeValue` **не должен** звать `onChange` — иначе получится петля;
- `onTouched()` надо звать на `blur` руками, само оно не случится, а без него не работает
  ни `touched`, ни `updateOn: 'blur'`;
- `setDisabledState` вызывается при `disable()`/`enable()` — если его не реализовать,
  компонент останется активным на выключенной форме;
- `multi: true` в провайдере обязателен.

Если компонент должен ещё и **валидировать себя сам**, к этому добавляется `NG_VALIDATORS`
и метод `validate(control): ValidationErrors | null`. Это то, чего принципиально не умеет
приём с `@Input() authGroup` из §11: там ребёнок может навесить валидаторы при создании
контролов, но не может участвовать в валидации как единое целое.

**Когда что брать.** Компонент оборачивает одно значение (маска, адрес, период) — CVA.
Компонент добавляет в форму несколько независимых полей (как `user-partial-card`) — проще
`@Input() authGroup`, CVA тут будет натягиванием совы.

### Data-driven альтернатива

Третий путь, уже применённый в проекте: описать контрол данными и разобрать его в шаблоне.
`cashboxes/cashboxes-table-editfrom/cellControlDescription.ts` — размеченное объединение:

```ts
export interface CellControlBase {
    controlType: CellControlType;
    isReadOnly: boolean;
    validators: ValidatorFn[]|[];
    initialValue: any;
}
export interface CellControlNumberTextBox extends CellControlBase {
    controlType: CellControlType.NumberTextBox;
    min: number; max: number;
    initialValue: number|undefined|null;
}
```

Разбирается через `@switch (controlPoly.controlType)` (`cashboxes-table-editfrom.component.html:81-113`),
и внутри каждой ветки тип сужается до нужного варианта — `controlPoly.maxLength` доступен
только в ветке `EditTextBox`. Приём хороший и для таблиц с неизвестной заранее структурой —
единственно разумный.

Два замечания по типам: `validators: ValidatorFn[]|[]` (`:14`) — часть `|[]` избыточна,
пустой массив и так подходит под `ValidatorFn[]`. И `initialValue: any` в базовом интерфейсе
(`:15`) перебивается в каждом наследнике конкретным типом, но там, где работают с
`CellControlBase` без сужения, остаётся `any`.

---

## 16. Что выбирать

| Задача | Инструмент | Не брать |
|---|---|---|
| фиксированный набор полей | `fb.group({...})` | `FormRecord` «на всякий случай» |
| список одинаковых элементов | `FormArray` | группа с ключами `'0'`, `'1'`, … |
| ключи приходят из данных | `FormRecord` | пересборка группы на каждое изменение |
| выключить поле, значение нужно | `disable()` + `getRawValue()` | `removeControl` |
| выключить поле, значение не нужно | `removeControl` | `disable()` и фильтрация на отправке |
| переключить ветку формы | `setControl` с заранее собранными группами | собирать группу заново каждый раз |
| «тип не выбран» | пустая группа-заглушка (`grNone`) | `removeControl` + проверки в шаблоне |
| проверка двух полей | `ValidatorFn` на группе, возвращающий ошибку | `setErrors` соседу изнутри валидатора |
| ошибка приехала с сервера | валидатор, читающий внешний источник | `setErrors` (затрётся) |
| ошибка про данные, а не про ввод | `permanentErrorValidator` | ручной `setErrors` при каждом прогоне |
| записать значение из модели | `setValue(v, {emitEvent: false})` | флаг «я сейчас пишу сам» |
| не дать изменению уйти к родителю | `onlySelf: true` — и понимать, что `value` родителя устареет | он же «чтобы не трогать детей» |
| подписаться на значение | `BindToControl(destroyRef, ctrl, cb)` | `subscribe()` без `takeUntilDestroyed` |
| подписаться на `touched`/`pristine` | `control.events` | опрос флагов из шаблона |
| свой контрол на одно значение | `ControlValueAccessor` | `@Input() authGroup` |
| несколько полей одним куском | `@Input() authGroup` + `registerControl` | CVA с составным значением |

Правило, закрывающее большую часть случаев: **меняете значение — Angular пересчитает всё сам;
меняете структуру или набор валидаторов — пересчитывать надо руками.**

---

## 17. Куда двигаться

Вектор не про «переписать формы», а про то, чтобы новые формы перестали добавлять новые касты.
В порядке отношения пользы к риску:

1. **Типизировать корневые группы** (§4). Самое дешёвое и самое полезное: убирает разом
   около пятидесяти кастов, делает `get` проверяемым и ловит ошибки вроде трёх мёртвых
   геттеров в `task-add`. Делается по одному компоненту, ничего не ломая: `fb.group` уже
   возвращает типизированный результат, надо только перестать его выбрасывать.
2. **`nameof<T>()` вместо строк** в `aspFieldValidator` — образец уже есть
   (`licenses-alienation.component.ts:67-72`). Связывает имена полей формы с DTO сервера
   компилятором, а не договорённостью.
3. **Починить `MustMatch` и `MustBeGreater`** до настоящих `ValidatorFn` (§9). Убирает
   четыре каста `<AbstractControlOptions>` и заодно баг с затиранием `required`.
4. **`revalidateForm` → `revalidateFormV2`** в тех местах, где в форме есть `FormArray`,
   и удалить мёртвую ветку `FormRecord` (§10).
5. **`toSignal` вместо `BindToControl` + ручного `markForCheck`** — по мере перехода на
   zoneless, не раньше и не отдельно от него (§12).

Что делать **не** нужно: заводить обёртки над `FormBuilder`, «типобезопасные» фасады над
`get` и генераторы форм из DTO. Типизация корня (пункт 1) даёт то же самое средствами
компилятора и без своего слоя, который придётся сопровождать.

---

## 18. Грабли

- **Три геттера в `task-add` всегда возвращают `null`.** `task-add.component.ts:253-280`
  ищут ключи `'taskSelectionGrname'`, `'rawCommandGroupname'`, `'fileGroupname'`, а группы
  лежат под `'taskSelection'`, `'rawCommand'`, `'file'` (`:50-52`). В геттеры попало имя
  поля вместо его значения. Сейчас безвредно — ни один из трёх не вызывается.

- **`setErrors({ [name]: null })` не очищает ошибку, а делает контрол невалидным без
  сообщения.** `cashboxes-table-editfrom.component.ts:111-113`:

  ```ts
  if (control.hasError(this.cellErrorName)) {
    control.setErrors({ [this.cellErrorName]: null })
  }
  ```

  Объект `{cellError: null}` — не `null`, значит `errors` не пуст, значит статус `INVALID`.
  При этом `hasError('cellError')` вернёт `false`, потому что значение по ключу falsy.
  Контрол оказывается невалиден, а показать нечего. Очистка ошибок — это `setErrors(null)`.

- **`hasValidator` не видит валидаторов-фабрик.** `Validators.required` — синглтон, а
  `Validators.minLength(6)`, `aspFieldValidator(...)`, `permanentErrorValidator(...)` —
  каждый раз новая функция. Сравнение по ссылке всегда даст `false` (§8).

- **`setErrors(null)` в групповом валидаторе стирает чужие ошибки.** `formUtils.ts:121,142` —
  вместе с `mustMatch` снимается и `required`, поставленный обычным валидатором (§9).

- **`onlySelf: true` оставляет `value` родителя устаревшим.**
  `cashboxes-table-editfrom.component.ts:523` выключает ячейку, не сообщая строке; значение
  выключенной ячейки останется в снимке родителя (§7).

- **`onlySelf: false` — это умолчание.** Шесть мест пишут его явно, ничего этим не меняя (§7).

- **`onlySelf: true` на корне не защищает детей.** `disable()` выключает всё поддерево
  всегда; предков у корня нет. Флаг не делает ничего (§7).

- **Мёртвая ветка `FormRecord` в `revalidateFormV2`.** `formUtils.ts:189` недостижима,
  потому что `FormRecord extends FormGroup`, а `instanceof FormGroup` проверяется первым (§10).

- **`revalidateForm` не заходит в `FormArray`.** `formUtils.ts:167-176` — старая версия;
  в формах с массивами звать надо `revalidateFormV2` (§10).

- **Асимметричный `emitEvent` в двух соседних ветках.** `subspace-edit.component.ts:248`
  патчит с `{emitEvent: true}`, `:263` — с `{emitEvent: false}`, хотя ветки делают одно и
  то же для разных типов организации.

- **`Object.getOwnPropertyNames(control.parent)` не даёт имён контролов.** `formUtils.ts:246`:

  ```ts
  var controlName = Object.getOwnPropertyNames(control.parent).find((k) => control.parent?.get(k) === control);
  ```

  Метод возвращает собственные JS-свойства объекта `FormGroup` (`_pendingDirty`, `validator`
  и прочие внутренности), а не ключи из `controls`. Однопараметрическая ветка
  `aspFieldValidator` — та, что должна сама определять имя поля, — работать как задумано
  не может. Имя контрола лежит в `control.parent.controls` — искать надо там:

  ```ts
  var controlName = Object.keys(control.parent.controls).find(k => control.parent!.get(k) === control);
  ```

  Во всех рабочих местах валидатор вызывается с явным именем поля, поэтому эта ветка
  фактически не используется.

- **`value` молча теряет disabled-поля.** Не «иногда», а всегда и по определению (§4).

---

## 19. Куда всё это едет: Signal Forms

В 22.1.5 рядом с реактивными формами уже лежит вторая система — `@angular/forms/signals`.
Помечена экспериментальной, но поставляется в том же пакете.

Устроена иначе: не дерево объектов `FormControl`, а сигнал с данными плюс **схема** правил
над путями в этих данных.

```ts
// синтетический пример — в репозитории Signal Forms не используются
const model = signal({ email: '', password: '' });
const f = form(model, (path) => {
  required(path.email);
  email(path.email);
  minLength(path.password, 6);
});
```

Что доступно (`node_modules/@angular/forms/types/signals.d.ts`,
`_structure-chunk.d.ts`): `form()`, `schema()`, `apply`/`applyEach`/`applyWhen`, `submit()`,
валидаторы `required`, `min`, `max`, `minLength`, `maxLength`, `pattern`, `email`, `minDate`,
`maxDate`, `validate`, `validateTree`, а также `disabled`, `hidden`, `readonly`, `debounce`
и `transformedValue`.

Три вещи, которые стоит отметить именно для нас:

1. **`validateAsync` и `validateHttp`** — это ровно та задача, под которую в проекте построена
   связка `AspFormValidateSource` + `aspFieldValidator` + `revalidateForm` (§9). То есть
   штатное средство для неё наконец появилось.
2. **`@angular/forms/signals/compat`** — `compatForm`, `SignalFormControl`, `extractValue`.
   Мост между двумя системами: переезд не обязан быть разовым, реактивную форму можно отдать
   в сигнальный код и наоборот.
3. **Валидация описывается схемой, а не навешивается на контролы.** Вопрос «добавил валидатор,
   почему не пересчиталось» (§8) там не возникает в принципе — схема декларативна.

Вывод простой: **посмотреть стоит, переписывать сейчас — нет.** API экспериментальный и
поедет ещё не раз, а пять разделов этого документа (§5–§8) для него просто перестанут быть
нужны — вместе с классом задач, который их породил.

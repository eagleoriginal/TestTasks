# Lesson 3: Content Sections & Alternating Layouts

## The Two Fundamental Section Layouts

Every content section on a landing page is one of these:

### Layout A: Image + Text Side-by-Side

```
┌──────────────────────────────────┐
│  [Image]       [Title]           │
│                [Description]     │
│                [Cards grid]      │
└──────────────────────────────────┘
```

### Layout B: Centered Title + Grid Below

```
┌──────────────────────────────────┐
│         [Title centered]         │
│         [Subtitle centered]      │
│  [Card]    [Card]    [Card]      │
└──────────────────────────────────┘
```

That's it. Every section is one of these. The skill is knowing when to use which.

**Use Layout A when:** you have a strong image/illustration that tells a story alongside text. Our "Passengers" and "Carriers" sections use this.

**Use Layout B when:** the content is a list of equal-weight items (features, steps, advantages). Our "Advantages" and "How to connect" sections use this.

---

## Building Layout A: The Passengers Section

```html
<section id="passengers" class="section bg-light">
    <div class="container">
        <div class="row align-items-center">
            <div class="col-lg-5 text-center mb-4 mb-lg-0">
                <img src="img/Conductor_02.jpg" class="section-img">
            </div>
            <div class="col-lg-7">
                <h2 class="section-title">Пассажирам</h2>
                <p class="section-lead">Оплачивайте проезд смартфоном...</p>
                <div class="row g-4 mt-2">
                    <!-- feature cards here -->
                </div>
            </div>
        </div>
    </div>
</section>
```

### Decision: 5/7 split, not 6/6

The image is a standalone character (Conductor_02) — it doesn't need much space. The text side has a 2x2 card grid inside it — it needs MORE space. So `col-lg-5` for image, `col-lg-7` for text.

**Rule of thumb:**
- Image is a photo/scene → 6/6 (equal weight)
- Image is an illustration/icon → 5/7 (text dominates)
- Image is just decoration → 4/8

### The `align-items-center` trick

Without it, if the text column is taller than the image, the image sits at the TOP of its column. With `align-items-center`, the image vertically centers against the text. This looks intentional rather than accidental.

### Mobile: `mb-4 mb-lg-0`

On mobile, columns stack. The image stacks above the text. `mb-4` adds bottom margin so the image doesn't touch the title below it. `mb-lg-0` removes that margin on desktop (where they're side-by-side and don't need it).

---

## Feature Cards: The 2x2 Grid Inside a Column

```html
<div class="row g-4 mt-2">
    <div class="col-md-6">
        <div class="feature-card">
            <div class="feature-icon">📱</div>
            <h5>Просто отсканируйте QR</h5>
            <p>Используйте приложение любого банка...</p>
        </div>
    </div>
    <div class="col-md-6">
        <!-- card 2 -->
    </div>
    <div class="col-md-6">
        <!-- card 3 -->
    </div>
    <div class="col-md-6">
        <!-- card 4 -->
    </div>
</div>
```

### Why a nested grid?

The text column (`col-lg-7`) contains its own `row` with `col-md-6` cards. This creates a 2x2 grid INSIDE the 7-column text area. Bootstrap's grid is nestable — use this to create complex layouts without custom CSS.

### The `g-4` gutter

`g-4` = 24px gap between cards (Bootstrap's spacing scale: g-1=4px, g-2=8px, g-3=16px, g-4=24px, g-5=48px). 24px is wide enough to see clear separation, narrow enough to keep cards visually grouped.

### Feature Card Design Decisions

```css
.feature-card {
    background: #fff;
    border-radius: var(--vb-radius);   /* 16px — same everywhere */
    padding: 24px;
    height: 100%;                       /* CRITICAL */
    box-shadow: 0 2px 16px rgba(0,0,0,.05);
    transition: transform .2s, box-shadow .2s;
}

.feature-card:hover {
    transform: translateY(-4px);
    box-shadow: 0 8px 30px rgba(0,0,0,.1);
}
```

**`height: 100%`** — Without this, cards in the same row have different heights based on content. With it, all cards stretch to match the tallest one in their row. This is essential for visual grid alignment.

**Shadow: `0 2px 16px rgba(0,0,0,.05)`** — Almost invisible. The card is "barely floating." This is the modern approach: soft shadows, not dramatic ones. The `2px` Y-offset simulates light from above.

**Hover: `translateY(-4px)`** — The card "lifts" 4px. Combined with a stronger shadow, it feels like you're picking it up. This confirms interactivity to the user.

### The emoji icon approach

```html
<div class="feature-icon">📱</div>
```

Using emoji as icons is a **draft strategy**. It's fast, universally supported, and looks fine for prototyping. In production, you'd replace these with an icon library (Bootstrap Icons, Heroicons, or custom SVGs). But for a first draft, emojis let you move fast without getting stuck on icon sourcing.

```css
.feature-icon {
    font-size: 2rem;
    margin-bottom: 8px;
}
```

---

## The Zigzag Pattern: How It Works Section

This section uses **alternating image sides** for each step:

```
Step 1:  [Text ←]  [→ Image]
Step 2:  [Image ←] [→ Text]
Step 3:  [Text ←]  [→ Image]
```

This zigzag creates visual rhythm and prevents the page from feeling like a monotonous left-aligned document.

### Implementation with Bootstrap `order`

```html
<!-- Step 1: text left, image right (natural order) -->
<div class="row align-items-center mt-5">
    <div class="col-lg-6 order-lg-2 text-center">
        <img src="img/Ep_01.jpg" class="step-img">
    </div>
    <div class="col-lg-6 order-lg-1">
        <div class="step-number">01</div>
        <h3>Сканируйте QR-код</h3>
        <p>...</p>
    </div>
</div>

<!-- Step 2: image left, text right (natural order) -->
<div class="row align-items-center mt-5">
    <div class="col-lg-6 text-center">
        <img src="img/Ep_04.jpg" class="step-img">
    </div>
    <div class="col-lg-6">
        <div class="step-number">02</div>
        <h3>Подтвердите оплату</h3>
        <p>...</p>
    </div>
</div>

<!-- Step 3: text left, image right (using order again) -->
<div class="row align-items-center mt-5">
    <div class="col-lg-6 order-lg-2 text-center">
        <img src="img/Ep_02.jpg" class="step-img">
    </div>
    <div class="col-lg-6 order-lg-1">
        <div class="step-number">03</div>
        <h3>Готово!</h3>
        <p>...</p>
    </div>
</div>
```

### The `order` trick explained

In the HTML, the image `div` comes FIRST. On desktop, `order-lg-2` pushes it to the right, and `order-lg-1` pulls the text to the left. On mobile (below `lg`), the order classes don't apply, so both columns use their natural HTML order — image on top, text below.

**Why not just write the HTML in the desktop order?** Because on mobile, you ALWAYS want the image above the text. If you put the text div first in HTML, on mobile the text would appear above the image, which feels disconnected.

### The Big Number Pattern

```css
.step-number {
    font-size: 4rem;
    font-weight: 900;
    color: var(--vb-red);
    opacity: .25;          /* faded — it's decorative, not content */
    line-height: 1;
    margin-bottom: 8px;
}
```

The "01", "02", "03" numbers serve TWO purposes:
1. **Sequential orientation** — the user knows this is a process
2. **Visual anchor** — the large faded number draws the eye to the start of each step

**`opacity: .25`** is key. At full opacity, "01" competes with the heading. At 25%, it's clearly decorative — a background element that adds structure without demanding attention.

### Step Image Treatment

```css
.step-img {
    max-width: 380px;
    width: 100%;
    border-radius: var(--vb-radius);   /* rounded corners */
    box-shadow: 0 8px 30px rgba(0,0,0,.12);
}
```

Unlike the hero conductor (which has `drop-shadow` because it's on white), step images are full illustrations with backgrounds. `box-shadow` + `border-radius` makes them feel like physical cards floating above the page.

### Spacing between steps: `mt-5`

Each step row gets `mt-5` (48px top margin). This is enough to separate steps without making the section feel sparse. The centered section title + subtitle at the top binds them all together.

---

## The Carriers Section: Mirror of Passengers

```html
<div class="row align-items-center">
    <div class="col-lg-7">
        <!-- Text + cards on LEFT -->
    </div>
    <div class="col-lg-5 text-center">
        <img src="img/Conductor_03.jpg" class="section-img">
    </div>
</div>
```

The Carriers section is the MIRROR of the Passengers section:
- Passengers: image LEFT, text RIGHT
- Carriers: text LEFT, image RIGHT

This prevents the page from feeling like every section is a copy-paste. The zigzag principle applies at the section level too, not just within the "How it works" steps.

---

## Background Alternation in Action

Look at the class on each section tag:

```html
<header class="hero-section">              <!-- white/gradient -->
<section class="section bg-light">         <!-- gray -->
<section class="section">                  <!-- white -->
<section class="section bg-dark text-white"> <!-- dark -->
<section class="section">                  <!-- white -->
<section class="section bg-light">         <!-- gray -->
<section class="section">                  <!-- white -->
<section class="cta-section">              <!-- red -->
<footer class="footer">                    <!-- dark -->
```

Every adjacent section has a DIFFERENT background. The sequence uses these 4 backgrounds: white → gray → white → dark. The CTA at the end uses the brand color (red) for maximum urgency.

---

## Exercise

1. Take 2 of your feature cards and experiment with a 1-column stack vs. 2x2 grid. Notice how the 2x2 uses space more efficiently.
2. Try swapping the image and text sides in the Passengers section. Does it feel different? Why?
3. Remove `height: 100%` from `.feature-card` and see what happens when card content lengths differ.

---
Next: [Lesson 4 — Specialized Components](04-specialized-components.md)

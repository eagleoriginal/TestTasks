# Lesson 4: Specialized Components — Cards, Strips & CTAs

## The Dark Section: Breaking the Pattern

After three light/white sections, the eye is used to the pattern. A dark section SHOCKS it awake:

```html
<section class="section bg-dark text-white">
```

```css
.bg-dark {
    background: var(--vb-dark) !important;  /* #1b1918 — warm dark, not pure black */
}
```

### Why `#1b1918` and not `#000000`?

Pure black (`#000`) feels harsh and digital. `#1b1918` is a warm near-black with a hint of brown — it's the same color from the logo. Warm darks feel more sophisticated. The difference is subtle but the page feels less sterile.

**White text on warm dark** has better contrast perception than white on pure black. Paradoxically, pure black can feel harder to read because the contrast is TOO high — the text "vibrates."

---

## Problem Cards: Image + Caption

```html
<div class="problem-card">
    <img src="img/Ep_04.jpg" class="problem-img">
    <p>«Передайте за проезд!» — квест в переполненном автобусе</p>
</div>
```

```css
.problem-card {
    border-radius: var(--vb-radius);
    overflow: hidden;                       /* clips image to rounded corners */
    background: rgba(255,255,255,.07);      /* barely visible white overlay */
}

.problem-img {
    width: 100%;
    height: 280px;
    object-fit: cover;          /* CRITICAL */
    object-position: top;       /* show faces, not feet */
}

.problem-card p {
    padding: 16px;
    font-weight: 600;
    font-size: .95rem;
}
```

### `overflow: hidden` — the rounded corner trick

Without it, `border-radius` rounds the card container, but the image inside has square corners that poke out. `overflow: hidden` clips everything inside to the container's rounded shape.

### `object-fit: cover` + fixed height

The illustrations have different aspect ratios. Without `object-fit: cover`, some images would be squished, others stretched. With it:
- The image FILLS the 280px height container
- It crops whatever doesn't fit (like CSS background-size: cover, but for `<img>`)
- The aspect ratio is preserved

### `object-position: top`

When cropping, CSS defaults to centering the image. But our illustrations have characters — their FACES are at the top. `object-position: top` crops from the bottom, keeping faces visible. Always think about what's important in the image when setting object-position.

### The semi-transparent background

```css
background: rgba(255,255,255,.07);
```

On a dark background, this creates a barely-visible lighter area. It's enough to define the card boundary without using a visible border. Borders on dark backgrounds look heavy; translucent fills look elegant.

---

## Advantage Cards: The Centered Grid

```html
<div class="row g-4 mt-4">
    <div class="col-lg-4">
        <div class="advantage-card text-center">
            <div class="advantage-number">01</div>
            <h4>Единый платёжный инструмент</h4>
            <p>Оплата проезда в любом месте России...</p>
        </div>
    </div>
    <!-- 2 more cards -->
</div>
```

### Why `col-lg-4` (3 columns)?

Three items = three columns. The number of columns should match the number of items. 3 cards in 4 columns leaves an awkward empty space. 3 in 2 columns makes one card sit alone on the second row. Match the grid to the content.

| Items | Columns | Class |
|-------|---------|-------|
| 2 | col-lg-6 | 2 equal columns |
| 3 | col-lg-4 | 3 equal columns |
| 4 | col-md-6 col-lg-3 | 4 columns desktop, 2 on tablet |
| 4 | col-md-6 | 2x2 grid (often better) |
| 6 | col-md-4 | 3x2 grid |

### The big number as visual anchor

```css
.advantage-number {
    font-size: 2.4rem;
    font-weight: 900;
    color: var(--vb-red);
    margin-bottom: 12px;
}
```

Unlike the step numbers (which are faded), advantage numbers are full-opacity red. Why? Because the advantage section doesn't have images. The red numbers serve as the visual focal point for each card. Without them, three white text-only cards would feel flat.

**Design principle:** Every card needs ONE visual anchor — an image, an icon, a colored number, or a large emoji. Text-only cards feel like bullets in a document, not components on a page.

### Centered text in advantage cards

The advantage section uses `text-center` on each card. This works because:
1. The content is SHORT (2-3 lines per card)
2. There's no image to align with
3. The centered number creates symmetry

**Rule:** Center-align cards only when content is short AND items are symmetric. Left-align when content length varies or when cards have images.

---

## Connect Steps: The Numbered Process

```html
<div class="row g-4 mt-4">
    <div class="col-md-3">
        <div class="connect-step text-center">
            <div class="connect-step-num">1</div>
            <h5>Откройте счёт</h5>
            <p>Расчётный счёт в любом банке — агенте СБП</p>
        </div>
    </div>
    <!-- 3 more steps -->
</div>
```

### The circle number

```css
.connect-step-num {
    width: 56px;
    height: 56px;
    background: var(--vb-red);
    color: #fff;
    border-radius: 50%;          /* perfect circle */
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1.5rem;
    font-weight: 900;
    margin: 0 auto 16px;        /* centered horizontally */
}
```

**`width` + `height` + `border-radius: 50%`** — Creates a perfect circle. The width and height MUST be equal. If they're not, you get an ellipse.

**`display: flex` + `align-items: center` + `justify-content: center`** — Centers the number inside the circle both horizontally and vertically. This is the modern way to center content in a container.

**`margin: 0 auto`** — Centers the circle itself within the column (since the parent is `text-center`, this could also be achieved with `display: block` and `margin: auto`).

### 4 columns: `col-md-3`

Four steps = four columns. `col-md-3` means the steps go side-by-side on tablets and up, and stack on mobile. This works because each step is short (title + one line).

---

## The CTA Section: Maximum Urgency

```html
<section class="cta-section">
    <div class="container text-center">
        <h2 class="section-title text-white">Подключайтесь!</h2>
        <p class="section-lead text-white">...</p>
        <div class="cta-contacts mt-4">
            <a href="tel:+74957875764" class="btn btn-light btn-lg">+7 495 787-57-64</a>
            <a href="tel:88003339309" class="btn btn-light btn-lg">8 800 333-93-09</a>
            <a href="mailto:info@vashbilet.online" class="btn btn-outline-light btn-lg">
                info@vashbilet.online
            </a>
        </div>
    </div>
</section>
```

```css
.cta-section {
    background: linear-gradient(135deg, var(--vb-red) 0%, var(--vb-red-dark) 100%);
    padding: 80px 0;
}
```

### Why a gradient and not a flat color?

A flat color background feels like a paint swatch. A subtle gradient (same hue, slightly different lightness) adds depth and feels more polished. The 135deg angle matches the hero gradient — visual consistency.

### Button color inversion

On the CTA (red background), buttons are INVERTED:
- `btn-light` — white buttons with dark text (primary)
- `btn-outline-light` — transparent with white border (secondary)

This is the same primary/secondary pattern from the hero, but color-inverted for the dark background. The email gets `outline-light` because it's less urgent than a phone number (people call to buy, they email to ask).

### `tel:` and `mailto:` links

```html
<a href="tel:+74957875764">+7 495 787-57-64</a>
<a href="mailto:info@vashbilet.online">info@vashbilet.online</a>
```

These aren't just text — they're functional links. On mobile, `tel:` opens the phone dialer. `mailto:` opens the email app. This removes friction: one tap = action started.

---

## The Footer: Minimal & Functional

```html
<footer class="footer">
    <div class="container">
        <div class="row align-items-center">
            <div class="col-md-4">
                <img src="img/logo_full.svg" height="30" class="footer-logo">
            </div>
            <div class="col-md-4 text-center">
                <span>вашбилет.рф</span>
            </div>
            <div class="col-md-4 text-md-end">
                <a href="mailto:info@vashbilet.online">info@vashbilet.online</a>
            </div>
        </div>
    </div>
</footer>
```

### The 3-column footer layout

```
[Logo]          [Domain]          [Contact]
```

Three zones, each `col-md-4`. On mobile they stack. The center column uses `text-center`, the right uses `text-md-end` (right-aligned on desktop only).

### White logo on dark background

```css
.footer-logo {
    filter: brightness(0) invert(1);
}
```

This CSS trick converts ANY image to white:
1. `brightness(0)` — turns everything black
2. `invert(1)` — inverts black to white

This means you don't need to create a separate white version of your logo. One SVG file, two appearances.

---

## Component Reuse Patterns

Notice how the same card pattern repeats:

| Component | Background | Has Image | Has Icon | Has Number | Alignment |
|-----------|-----------|-----------|----------|------------|-----------|
| Feature card | white | no | emoji | no | left |
| Problem card | translucent | yes | no | no | left |
| Advantage card | white | no | no | red number | center |
| Connect step | none | no | no | circle | center |

They're all variations of the same structure:
```
[Visual anchor]  →  icon / image / number / circle
[Title]          →  h4 / h5
[Description]    →  p (short)
```

Each component tweaks exactly ONE thing to serve its context. Don't create wildly different components — build a family of related patterns.

---

## Exercise

1. Change the problem cards from 3 columns to a horizontal scrollable strip on mobile (hint: `flex-nowrap` + `overflow-x: auto`)
2. Add a connecting line between the 4 connect steps (hint: `::after` pseudo-element with `border-top`)
3. Try making the CTA background a solid color instead of gradient — notice the difference

---
Next: [Lesson 5 — Animations, jQuery & Responsive Polish](05-animations-and-polish.md)

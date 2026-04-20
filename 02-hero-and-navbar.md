# Lesson 2: Hero Section & Navbar — First Impression Is Everything

## The Navbar

### Structure Decision

A landing page navbar has exactly 3 zones:

```
[Logo]          [Nav Links]          [CTA Button]
```

In Bootstrap terms:

```html
<nav class="navbar navbar-expand-lg fixed-top bg-white shadow-sm">
    <div class="container">
        <a class="navbar-brand" href="#">
            <img src="img/logo_full.svg" height="36">
        </a>
        <!-- hamburger for mobile -->
        <button class="navbar-toggler" data-bs-toggle="collapse" 
                data-bs-target="#navbarNav">
            <span class="navbar-toggler-icon"></span>
        </button>
        <div class="collapse navbar-collapse" id="navbarNav">
            <ul class="navbar-nav ms-auto">
                <li class="nav-item">
                    <a class="nav-link" href="#passengers">Пассажирам</a>
                </li>
                <!-- ... more links ... -->
            </ul>
            <a href="#connect" class="btn btn-vb ms-lg-3">Подключиться</a>
        </div>
    </div>
</nav>
```

### Key decisions explained

**`fixed-top`** — The navbar scrolls with the page. On a landing page, the user should ALWAYS be able to navigate. Sticky nav = higher conversion.

**`shadow-sm`** — A tiny shadow separates the navbar from content. Without it, white navbar on white hero section = no visual boundary.

**`ms-auto`** on `navbar-nav` — Pushes nav links to the right. The logo is left, everything else is right. This is the standard layout because we read left-to-right: brand identity first, then navigation.

**CTA button in the navbar** — Always. The "Подключиться" button should be visible at ALL scroll positions. This is your #1 conversion element.

**`ms-lg-3`** on the CTA button — Adds left margin only on desktop, separating the button from nav links visually.

### Logo height

`height="36"` — Not 20 (too small, looks weak), not 50 (dominates the navbar). 32-40px is the sweet spot for a horizontal logo in a standard navbar.

### Nav link hover

```css
.navbar .nav-link {
    font-weight: 600;        /* semi-bold, not regular */
    margin: 0 4px;           /* breathing room between links */
    transition: color .2s;   /* smooth, not instant */
}

.navbar .nav-link:hover,
.navbar .nav-link.active {
    color: var(--vb-red);    /* brand color on hover */
}
```

**Why 600 weight?** Regular (400) nav links feel passive. Bold (700) feels heavy. Semi-bold (600) is assertive but not aggressive.

---

## The Hero Section

### The Layout: Text Left, Image Right

```
┌─────────────────────────────────────────────┐
│  [Title]                    [Mascot Image]  │
│  [Subtitle]                                 │
│  [CTA] [CTA]                                │
│  [Stats row]                                │
└─────────────────────────────────────────────┘
```

This is the most proven landing page hero layout. Why?
- **Left = text** because we read left-to-right, text gets attention first
- **Right = image** provides visual interest without competing with the message
- **Two CTAs** — primary (filled) and secondary (outline) give choice without confusion

```html
<header class="hero-section">
    <div class="container">
        <div class="row align-items-center min-vh-80">
            <div class="col-lg-6">
                <!-- Text content -->
            </div>
            <div class="col-lg-6 text-center">
                <img src="img/Conductor_01.jpg" class="hero-img">
            </div>
        </div>
    </div>
</header>
```

### Why `col-lg-6` + `col-lg-6`?

50/50 split is classic. You could do 7/5 if you have more text, but 50/50 works because:
- The image is a character illustration (needs space to breathe)
- The text side has stats underneath (fills the space)
- It's symmetrical = feels balanced

### The `min-vh-80` trick

```css
.min-vh-80 {
    min-height: 80vh;
}
```

**Why 80vh, not 100vh?**

100vh means the hero fills the ENTIRE screen. This creates two problems:
1. Users don't know there's content below (no "peek" of the next section)
2. On short laptops, content gets cramped

80vh shows a sliver of the next section at the bottom, hinting "scroll down." This small detail significantly improves scroll rate.

### Hero Title Anatomy

```html
<h1 class="hero-title">Оплата проезда<br>по <span class="text-accent">СБП</span></h1>
```

Three tricks here:

1. **`<br>` for line control** — Don't let the browser decide where to break your headline. YOU control it. "Оплата проезда" is one thought, "по СБП" is the punchline.

2. **Accent span** — The most important word ("СБП") gets the brand color. This creates a focal point in the headline.

3. **Size & weight**:
```css
.hero-title {
    font-size: 3.2rem;    /* BIG. This is the loudest element on the page */
    font-weight: 900;     /* Maximum weight = maximum authority */
    line-height: 1.15;    /* Tight line height for headlines. NOT 1.5 */
}
```

**`line-height: 1.15`** — Body text uses 1.5-1.6. Headlines use 1.1-1.2. Loose headlines look like paragraphs. Tight headlines look like headlines.

### The Subtitle

```css
.hero-subtitle {
    font-size: 1.2rem;   /* noticeably smaller than title */
    color: #555;          /* gray, NOT black — creates hierarchy */
    margin-top: 16px;
    max-width: 480px;     /* DON'T let it stretch full width */
}
```

**`max-width: 480px`** — Long lines are hard to read. Limiting the subtitle width to ~480px keeps it at 50-70 characters per line, which is the optimal reading width. Without this, on a wide screen the subtitle stretches across the entire col-lg-6 and feels like a wall of text.

**`color: #555`** — The subtitle is SUPPORTING text. It should be visually quieter than the title. Gray text on white background = lower visual priority.

### The Two-Button Pattern

```html
<div class="d-flex flex-wrap gap-3 mt-4">
    <a href="#carriers" class="btn btn-vb btn-lg">Перевозчикам</a>
    <a href="#passengers" class="btn btn-outline-vb btn-lg">Пассажирам</a>
</div>
```

**Why two buttons?** This page has two audiences (carriers and passengers). The primary audience (carriers — they pay money) gets the filled button. The secondary audience gets the outline button.

**`d-flex flex-wrap gap-3`** — Flexbox with wrap. On mobile, if buttons don't fit in one row, they stack. `gap-3` (16px) provides consistent spacing regardless of stacking.

### Hero Stats Row

```html
<div class="hero-stats mt-5">
    <div class="hero-stat">
        <span class="hero-stat-number">0,4%</span>
        <span class="hero-stat-label">ставка эквайринга</span>
    </div>
    <!-- ... more stats -->
</div>
```

```css
.hero-stats {
    display: flex;
    gap: 32px;
}

.hero-stat-number {
    font-size: 1.6rem;
    font-weight: 900;
    color: var(--vb-red);   /* brand color = draws attention */
}

.hero-stat-label {
    font-size: .85rem;
    color: #777;            /* quiet label */
}
```

**Why stats in the hero?** They provide instant credibility. A visitor sees "0,4%" and "0 ₽" and immediately understands the value proposition without reading a single paragraph.

**Why flex, not grid?** Stats are a simple row of items. Flexbox handles this with less code than grid. Grid is for 2D layouts.

### Hero Image Treatment

```css
.hero-img {
    max-width: 420px;
    width: 100%;                                    /* responsive */
    filter: drop-shadow(0 20px 40px rgba(0,0,0,.1)); /* depth */
}
```

**`drop-shadow` vs `box-shadow`**? The conductor image has a transparent/white background. `box-shadow` would put a shadow around the rectangular image boundary. `drop-shadow` traces the SHAPE of the visible content. For illustrations on white backgrounds, always use `drop-shadow`.

### The `padding-top: 120px` issue

```css
.hero-section {
    padding-top: 120px;  /* clear the fixed navbar */
}
```

Since the navbar is `fixed-top`, it covers the top of the page. Without extra top padding on the hero, the title hides behind the navbar. 120px = navbar height (~70px) + breathing room (~50px).

### Hero Background

```css
.hero-section {
    background: linear-gradient(135deg, #fff 60%, var(--vb-gray) 100%);
}
```

A subtle gradient from white to light gray. Why?
- Pure white feels flat and unfinished
- The gradient adds depth without being distracting
- 135deg angle creates a diagonal that feels dynamic
- The gray portion sits behind the image, subtly framing it

## Responsive Adjustments

```css
@media (max-width: 991px) {
    .hero-title { font-size: 2.4rem; }
    .hero-img { max-width: 300px; }
}

@media (max-width: 575px) {
    .hero-title { font-size: 2rem; }
}
```

On mobile, the columns stack (Bootstrap handles this — `col-lg-6` means 6 columns only on `lg+`, full width below). You just need to:
1. Shrink the title so it doesn't overflow
2. Shrink the image so it doesn't dominate

---
Next: [Lesson 3 — Content Sections & Alternating Layouts](03-content-sections.md)

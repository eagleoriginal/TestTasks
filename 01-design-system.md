# Lesson 1: Design System — Before You Write a Single Line

## The Biggest Rookie Mistake

Jumping straight into HTML. Before you touch code, answer these questions:

1. **What's the brand color?** Extract it from the logo/materials. Ours: `#e51e21` (red from the ticket logo SVG).
2. **What's the font?** Pick ONE font family. We chose Nunito — rounded, friendly, fits a transport/service brand. Rule of thumb: geometric sans-serifs (Nunito, Rubik, Inter) = modern & approachable. Serif fonts = formal/luxury.
3. **What sections does the page need?** List them before coding. Ours: Hero → Passengers → How it works → Problems → Carriers → Advantages → Connect → CTA → Footer.

## CSS Variables — Your Single Source of Truth

```css
:root {
    --vb-red: #e51e21;
    --vb-red-dark: #c41a1b;    /* hover states — darken by ~10% */
    --vb-dark: #1b1918;         /* text, dark backgrounds */
    --vb-gray: #f5f5f7;         /* light section backgrounds */
    --vb-radius: 16px;          /* consistent rounding everywhere */
}
```

### Why this matters

Every time you write `background: #e51e21` directly, you create a maintenance nightmare. With variables:
- Change the brand color once → updates everywhere
- Hover colors derived from the main color stay consistent
- Border radius is uniform across all cards

### How to pick `--vb-red-dark`

Don't guess. Take your brand color, go to HSL, reduce Lightness by 8-12%. Or just use any color picker:
- `#e51e21` → HSL(0, 82%, 51%)
- Darken → HSL(0, 82%, 43%) → `#c41a1b`

## The Section Rhythm

A landing page is a **vertical rhythm** of alternating visual blocks:

```
[Light bg] Hero
[Gray bg]  Passengers     ← alternating backgrounds
[White bg] How it works   ← create visual separation
[Dark bg]  Problems       ← dramatic contrast
[White bg] Carriers
[Gray bg]  Advantages
[White bg] Connect
[Red bg]   CTA            ← accent color = urgency
[Dark bg]  Footer
```

**Rule:** Never put two same-background sections next to each other. The eye needs contrast to know "this is a new topic."

## Spacing System

Don't invent padding values per section. Pick ONE and stick to it:

```css
.section {
    padding: 80px 0;     /* desktop */
}

/* mobile override */
@media (max-width: 575px) {
    .section { padding: 50px 0; }
}
```

80px is a sweet spot — enough breathing room without feeling empty. Every section gets the same padding. Consistency > creativity here.

## Font Weight Strategy

With Nunito (or any variable-weight font), define clear roles:

| Weight | Role | Example |
|--------|------|---------|
| 400 | Body text, paragraphs | Feature descriptions |
| 600 | Nav links, labels | Menu items |
| 700 | Card headings, buttons | "Электронный билет" |
| 800 | Section subheadings | Advantage card titles |
| 900 | Hero title, big numbers | "Оплата проезда по СБП" |

**Don't use more than 4 weights.** Each weight should have a clear purpose.

## The Button System

You need exactly TWO button styles:

```css
/* Primary — for main CTAs */
.btn-vb {
    background: var(--vb-red);
    color: #fff;
    border: 2px solid var(--vb-red);
    border-radius: 50px;          /* pill shape = modern */
    padding: 10px 28px;
    font-weight: 700;
}

/* Secondary — for alternative actions */
.btn-outline-vb {
    background: transparent;
    color: var(--vb-red);
    border: 2px solid var(--vb-red);
    border-radius: 50px;
    padding: 10px 28px;
    font-weight: 700;
}
```

### Why pill-shaped?

Pill buttons (`border-radius: 50px`) feel more clickable and modern than slightly-rounded (`border-radius: 8px`). They're the standard for landing pages in 2024-2026. Rectangular buttons feel like enterprise software.

### The hover formula

Every interactive element needs these 3 hover changes:
1. **Color shift** — darken background
2. **Lift** — `transform: translateY(-2px)`
3. **Shadow** — `box-shadow` with brand color at low opacity

```css
.btn-vb:hover {
    background: var(--vb-red-dark);
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(229, 30, 33, .3);
}
```

The shadow color should ALWAYS match the button color (red button → red shadow). White/gray shadows on colored buttons look wrong.

## File Structure

```
Landing/
├── index.html
├── style.css
├── script.js
└── img/
    ├── logo.svg
    ├── logo_full.svg
    ├── Conductor_01.jpg
    ├── Conductor_02.jpg
    └── ...
```

Keep it flat and simple. No nested CSS folders, no `assets/`, no build tools for a landing page. You'll add complexity when you actually need it.

## Exercise

Before moving to Lesson 2, do this:
1. Open your brand materials, extract the primary color
2. Create `style.css` with just the `:root` variables and the two button classes
3. Pick a Google Font that matches the brand mood
4. Write down your section list on paper

---
Next: [Lesson 2 — Hero Section & Navbar](02-hero-and-navbar.md)

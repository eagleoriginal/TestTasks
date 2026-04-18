# Lesson 5: Animations, jQuery & Responsive Polish

## Why jQuery on a Landing Page?

You asked for jQuery, so here's the honest take: for a landing page, jQuery gives you three things that vanilla JS does more verbosely:

1. **Smooth scroll** — `$('html, body').animate()` is one line
2. **Scroll event handling** — `$(window).on('scroll', fn)` with easy offset math
3. **Class toggling with selectors** — `$('.fade-up').addClass('visible')` works on collections

You DON'T need jQuery for DOM manipulation, AJAX, or anything complex. Bootstrap 5 handles its own JS (dropdowns, collapse). jQuery is here purely for scroll-related behavior.

---

## Smooth Scrolling

```js
$('a[href^="#"]').on('click', function (e) {
    e.preventDefault();
    var target = $(this.getAttribute('href'));
    if (target.length) {
        $('html, body').animate({
            scrollTop: target.offset().top - 70
        }, 600);
    }
    // Close mobile menu after click
    $('.navbar-collapse').collapse('hide');
});
```

### Line-by-line breakdown

**`$('a[href^="#"]')`** — Selects ALL anchor links that start with `#`. This catches navbar links, hero buttons, any in-page navigation. One handler for everything.

**`e.preventDefault()`** — Stops the browser's instant jump to the anchor. We want a SMOOTH scroll instead.

**`target.offset().top - 70`** — Scrolls to the section, but 70px above it. Why? Because the fixed navbar covers the top 70px of the viewport. Without the offset, the section title hides behind the navbar.

**`600`** — Animation duration in ms. 400 feels rushed, 1000 feels sluggish. 500-700 is the sweet spot for scroll animations.

**`$('.navbar-collapse').collapse('hide')`** — On mobile, after clicking a nav link, the hamburger menu should close. Without this line, users tap a link, the page scrolls, but the menu stays open covering the content.

---

## Active Nav Link on Scroll

```js
var sections = $('section[id], header[id]');

$(window).on('scroll', function () {
    var scrollPos = $(window).scrollTop() + 100;

    sections.each(function () {
        var top = $(this).offset().top;
        var bottom = top + $(this).outerHeight();
        var id = $(this).attr('id');

        if (scrollPos >= top && scrollPos < bottom) {
            $('.navbar-nav .nav-link').removeClass('active');
            $('.navbar-nav .nav-link[href="#' + id + '"]').addClass('active');
        }
    });
});
```

### What this does

As the user scrolls, the navbar link corresponding to the currently visible section gets highlighted. This gives orientation — "I'm in the Carriers section."

### How it works

1. Cache all sections that have an `id` attribute
2. On every scroll event, check which section the viewport is currently inside
3. The `+ 100` offset means "consider a section active when its top is 100px above the viewport top" — this prevents the brief moment where you're between sections and nothing is active
4. Remove `active` from ALL links, then add it to the matching one

### Performance note

Scroll handlers fire 60+ times per second. This code is fine for a landing page (small DOM, few sections). For complex pages, you'd use `IntersectionObserver` instead. But for 5-8 sections? jQuery scroll is perfectly adequate.

---

## Fade-Up Animation: CSS + jQuery

This is the most impactful animation on the page. Elements start invisible and slide up as they enter the viewport.

### CSS setup

```css
.fade-up {
    opacity: 0;
    transform: translateY(30px);
    transition: opacity .6s ease, transform .6s ease;
}

.fade-up.visible {
    opacity: 1;
    transform: translateY(0);
}
```

### How it works

1. `.fade-up` — Element starts 30px below its final position and invisible
2. `.fade-up.visible` — Element is at its normal position and fully visible
3. `transition` — The change between states takes 0.6 seconds

The animation is CSS-only. jQuery's job is just adding the `visible` class at the right time.

### jQuery trigger

```js
function checkFadeUp() {
    $('.fade-up').each(function () {
        var elementTop = $(this).offset().top;
        var viewBottom = $(window).scrollTop() + $(window).height() - 80;
        if (elementTop < viewBottom) {
            $(this).addClass('visible');
        }
    });
}

// Apply fade-up to specific elements
$('.feature-card, .advantage-card, .problem-card, .connect-step, .step-number')
    .closest('.col-md-6, .col-md-3, .col-md-4, .col-lg-4')
    .addClass('fade-up');
$('.step-img, .section-img').addClass('fade-up');

$(window).on('scroll', checkFadeUp);
checkFadeUp(); // run immediately for elements already in view
```

### Key decisions

**`$(window).height() - 80`** — The element triggers when it's 80px inside the viewport, NOT at the very bottom edge. Without this offset, elements animate just as they enter the screen — the user barely sees the animation start. With the 80px buffer, the element is solidly on screen before it starts, making the animation clearly visible.

**Adding `fade-up` via jQuery, not HTML** — Instead of manually adding `class="fade-up"` to 20+ elements in HTML, we use jQuery to add it programmatically. This keeps the HTML clean and makes it easy to change which elements animate.

**`.closest('.col-md-6, ...')`** — We add `fade-up` to the COLUMN wrapper, not the card itself. Why? If we animate the card, the column still takes up space (invisible card, visible column). Animating the column means the entire grid cell fades in, which looks cleaner.

**`checkFadeUp()` on load** — Elements above the fold (visible without scrolling) should be visible immediately. Without the initial call, the hero stats or top cards would be invisible until the user scrolls.

### Why not animate EVERYTHING?

We animate: cards, images, step numbers — elements that benefit from "reveal" effect.

We DON'T animate: headings, subtitles, nav, footer — structural elements that should be immediately visible. Animating too much feels like the page is broken or slow.

**Rule:** Animate content blocks (cards, images), not structural elements (headings, nav, footer).

---

## Hover Transitions: The 0.2s Standard

Every interactive element has:

```css
transition: transform .2s, box-shadow .2s;
```

**Why .2s?** Human perception research:
- < 100ms — feels instant (good for color changes)
- 150-250ms — feels responsive (perfect for hover)
- 300-500ms — feels animated (good for reveals)
- > 500ms — feels slow (only for major transitions)

Hover transitions are about RESPONSIVENESS, not spectacle. 0.2s = the user sees the change but doesn't wait for it.

### The three-property hover pattern

Across the whole page, hoverable elements use the same three changes:

```css
/* Cards */
.feature-card:hover {
    transform: translateY(-4px);                    /* lift */
    box-shadow: 0 8px 30px rgba(0,0,0,.1);         /* deeper shadow */
}

/* Buttons */
.btn-vb:hover {
    transform: translateY(-2px);                    /* smaller lift */
    box-shadow: 0 6px 20px rgba(229, 30, 33, .3);  /* colored shadow */
    background: var(--vb-red-dark);                 /* color change */
}
```

Cards lift 4px (they're bigger), buttons lift 2px (subtle). The shadow grows to match the increased "height." This consistent physics makes the page feel coherent.

---

## Responsive Breakpoints: What to Adjust

Bootstrap handles column stacking. You only need custom breakpoints for:

### 1. Font sizes

```css
@media (max-width: 991px) {
    .hero-title { font-size: 2.4rem; }   /* was 3.2rem */
}

@media (max-width: 575px) {
    .hero-title { font-size: 2rem; }     /* smaller phones */
}
```

**Only scale the hero title.** Body text, card text, and section titles are fine at their desktop sizes. Over-scaling makes the page feel inconsistent.

### 2. Image sizes

```css
@media (max-width: 991px) {
    .hero-img { max-width: 300px; }      /* was 420px */
    .section-img { max-width: 280px; }   /* was 380px */
    .step-img { max-width: 300px; }      /* was 380px */
}
```

On tablet, the columns are narrower. Images need to shrink proportionally or they'll overflow or dominate the layout.

### 3. Section padding

```css
@media (max-width: 575px) {
    .section { padding: 50px 0; }        /* was 80px */
}
```

On phones, 80px of padding between sections is wasteful — screen real estate is precious. 50px maintains separation without wasting space.

### 4. Image crop heights

```css
@media (max-width: 575px) {
    .problem-img { height: 200px; }      /* was 280px */
}
```

On phones, 280px-tall images dominate the screen. 200px keeps them visible but doesn't push the caption off-screen.

### What NOT to override

- Bootstrap column classes — they handle stacking automatically
- Card padding — 24px works on all screen sizes
- Font sizes below section title level — they're already appropriate
- Shadows — they look fine at all sizes

---

## The Loading Order: Script Tags

```html
<!-- At the end of body -->
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>
<script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
<script src="script.js"></script>
```

### Why at the bottom of `<body>`?

Scripts block page rendering. If they're in `<head>`, the user sees a blank page until all JS downloads. At the bottom of `<body>`, the HTML renders first, then JS loads. The page appears instantly, scripts enhance it afterward.

### Order matters

1. **Bootstrap** first — it provides the `collapse` method used for the mobile menu
2. **jQuery** second — our script depends on it (actually jQuery should come BEFORE Bootstrap if using Bootstrap's jQuery dependency, but Bootstrap 5 is standalone)
3. **Our script** last — it uses both Bootstrap and jQuery

---

## Quick Wins Checklist

Before shipping any landing page, check these:

- [ ] All images have `alt` text (accessibility + SEO)
- [ ] `<meta name="viewport">` is set (mobile rendering)
- [ ] Phone numbers use `tel:` links
- [ ] Email uses `mailto:` link
- [ ] Mobile menu closes after clicking a link
- [ ] No horizontal scroll on mobile (check for `overflow-x` issues)
- [ ] Favicon is set (even a simple one)
- [ ] Page title is descriptive, not "Untitled"
- [ ] Links to external resources use CDN with version pinned

---

## What Would a V2 Look Like?

Things you'd add when polishing beyond a first draft:

1. **Replace emoji icons** with SVG icons (Bootstrap Icons or Heroicons)
2. **Add a favicon** from the logo SVG
3. **Lazy-load images** with `loading="lazy"` attribute
4. **Add `preconnect`** for Google Fonts and CDN in `<head>`
5. **Staggered animations** — cards fade in with a delay between each one
6. **Mobile app download buttons** in the passengers section
7. **A contact form** instead of just email/phone links
8. **Cookie consent banner** if required by law
9. **Analytics** (Yandex.Metrica, Google Analytics)
10. **Performance** — compress images, self-host fonts

But that's all polish. The first draft is about structure, hierarchy, and rhythm — everything we've covered in these 5 lessons.

---

## Summary: The 10 Rules

1. **Design system first, code second** — colors, fonts, spacing defined before any HTML
2. **Section rhythm** — alternate backgrounds, never two identical adjacent sections
3. **Visual hierarchy** — title (900) > subtitle (gray) > body text > labels
4. **Two button styles only** — filled primary, outline secondary
5. **Image + text layout** — zigzag pattern, image side alternates
6. **Cards need a visual anchor** — icon, number, image, or color block
7. **`height: 100%` on cards** — always, unless you want ragged grids
8. **Hover = lift + shadow + color** — consistent physics across all elements
9. **Animate content, not structure** — cards and images fade in, headings don't
10. **Mobile = shrink fonts, shrink images, reduce padding** — don't redesign, just scale

---
You now have everything to build this landing from scratch. Go break it apart, modify it, make it yours.

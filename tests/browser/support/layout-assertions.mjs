// Executable assertions for the Forma layout contract (analysis/forma-layout-contract.json, rules FL-01..FL-10).
//
// Each assertion takes a Playwright page (and options) and returns the list of violations it found: an empty list is
// a pass. The same functions run in two places:
//   - in this package's CI, against Forma 0.12.0's own pattern pages and conforming fixtures (each must pass) and
//     against a deliberately broken fixture per assertion (each must fail), so every assertion is shown to work;
//   - in the event build, where the bootstrap copies this file to tests/browser/support/layout-assertions.mjs and the
//     plan-bound browser tests each rule names import it and run it against Causa's screens.
// Assertions only read the page, except focus and dialog assertions, which press keys and click as a user would.

const inPage = (page, fn, arg) => page.evaluate(fn, arg);

// Waits for every finite animation (entry animations start transparent) so colours are judged at rest.
const settle = page =>
  inPage(page, () => Promise.all(document.getAnimations().filter(a => a.effect && Number.isFinite(a.effect.getComputedTiming().endTime)).map(a => a.finished.catch(() => null))).then(() => null));

// Shared in-page helpers (window.__ef): colour arithmetic, the background actually painted behind an element (what is
// beneath it on screen, not only its ancestors), whether an element is really visible, and its visible text.
const helpers = () => {
  // Any CSS colour (rgb, color(srgb ...), oklch, color-mix results) normalised by painting it: sRGB 0-255 and alpha 0-1.
  const rgba = c => {
    const canvas = rgba.canvas || (rgba.canvas = Object.assign(document.createElement('canvas'), { width: 1, height: 1 }));
    const ctx = rgba.ctx || (rgba.ctx = canvas.getContext('2d', { willReadFrequently: true }));
    ctx.clearRect(0, 0, 1, 1);
    ctx.fillStyle = 'rgba(0, 0, 0, 0)';
    ctx.fillStyle = c || 'rgba(0, 0, 0, 0)';
    ctx.fillRect(0, 0, 1, 1);
    const [r, g, b, a] = ctx.getImageData(0, 0, 1, 1).data;
    return [r, g, b, a / 255];
  };
  const over = ([r, g, b, a], [R, G, B]) => [r * a + R * (1 - a), g * a + G * (1 - a), b * a + B * (1 - a), 1];
  const lum = ([r, g, b]) => [r, g, b].map(v => v / 255).map(v => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)).reduce((acc, v, k) => acc + v * [0.2126, 0.7152, 0.0722][k], 0);
  const ratio = (a, b) => (Math.max(lum(a), lum(b)) + 0.05) / (Math.min(lum(a), lum(b)) + 0.05);
  // Opacity and filter: opacity() both fade what an element paints.
  const filterOpacity = f => (f.match(/opacity\(\s*([\d.]+)(%?)\s*\)/g) || []).map(x => x.match(/([\d.]+)(%?)/)).reduce((o, [, v, pct]) => o * (pct ? parseFloat(v) / 100 : parseFloat(v)), 1);
  const opacityOf = e => { let o = 1; for (let n = e; n; n = n.parentElement) { const s = getComputedStyle(n); o *= parseFloat(s.opacity) * filterOpacity(s.filter || ''); } return o; };
  // The composited background under an element: the elements beneath its centre on screen, bottom first. Null when an
  // image or gradient is involved (judged by the plan's axe run instead).
  const backgroundAt = e => {
    const r = e.getBoundingClientRect();
    const x = Math.min(Math.max(r.left + r.width / 2, 0), window.innerWidth - 1);
    const y = Math.min(Math.max(r.top + Math.min(r.height / 2, 10), 0), window.innerHeight - 1);
    const stack = r.width > 0 && r.top < window.innerHeight && r.bottom > 0 ? document.elementsFromPoint(x, y) : [];
    const k = stack.indexOf(e);
    const beneath = k >= 0 ? stack.slice(k) : (() => { const a = []; for (let n = e; n; n = n.parentElement) a.push(n); return a; })();
    let colour = [255, 255, 255, 1];
    for (const n of beneath.slice().reverse()) {
      const s = getComputedStyle(n);
      if (s.backgroundImage !== 'none') return null;
      const [cr, cg, cb, ca] = rgba(s.backgroundColor);
      colour = over([cr, cg, cb, ca * (n === document.documentElement || n === document.body ? 1 : opacityOf(n))], colour);
    }
    return colour;
  };
  // An element is hidden from sight when it or an ancestor is not displayed, invisible, transparent, clipped to
  // nothing (visually-hidden patterns), zero-sized, or has a zero font size.
  const hidden = e => {
    if (e.closest('.ef-visually-hidden, [aria-hidden=true], [hidden]')) return true;
    for (let n = e; n && n !== document.documentElement; n = n.parentElement) {
      const s = getComputedStyle(n);
      if (s.display === 'none' || s.visibility === 'hidden' || parseFloat(s.opacity) === 0) return true;
      if (/inset\(\s*(50%|100%)/.test(s.clipPath) || /rect\(\s*0(px)?[ ,]+0(px)?[ ,]+0(px)?[ ,]+0(px)?\s*\)/.test(s.clip || '')) return true;
    }
    const r = e.getBoundingClientRect();
    return r.width <= 1 || r.height <= 1 || parseFloat(getComputedStyle(e).fontSize) === 0;
  };
  // Text a sighted user can read in an element: in text nodes whose element is not hidden and whose colour reaches
  // 3:1 against what is painted behind it.
  const visibleText = e => {
    const walker = document.createTreeWalker(e, NodeFilter.SHOW_TEXT);
    const parts = [];
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      const t = n.textContent.trim();
      const host = n.parentElement;
      if (!t || hidden(host) || parseFloat(getComputedStyle(host).fontSize) < 9) continue;
      const bg = backgroundAt(host);
      const [r, g, b, a] = rgba(getComputedStyle(host).color);
      if (bg && ratio(over([r, g, b, a * opacityOf(host)], bg), bg) < 3) continue;
      parts.push(t);
    }
    return parts.join(' ');
  };
  // The boxes of ancestors that clip their content (overflow hidden or clip).
  const clipBoxes = e => { const out = []; for (let a = e.parentElement; a && a !== document.documentElement; a = a.parentElement) { const s = getComputedStyle(a); if (['hidden', 'clip'].includes(s.overflowX) || ['hidden', 'clip'].includes(s.overflowY)) out.push(a.getBoundingClientRect()); } return out; };
  // The part of an element's box a user can see: inside the viewport and every clipping ancestor. Null when none.
  const shownRect = e => {
    const r = e.getBoundingClientRect();
    const b = [...clipBoxes(e), { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight }]
      .reduce((a, c) => ({ left: Math.max(a.left, c.left), top: Math.max(a.top, c.top), right: Math.min(a.right, c.right), bottom: Math.min(a.bottom, c.bottom) }), { left: r.left, top: r.top, right: r.right, bottom: r.bottom });
    return b.right - b.left > 1 && b.bottom - b.top > 1 ? b : null;
  };
  // Painted on top where it is shown: the element at the centre of its shown part is the element or inside it (an
  // ancestor there means the element is clipped or not painted at that point).
  const onTop = e => { const b = shownRect(e); if (!b) return false; const t = document.elementFromPoint((b.left + b.right) / 2, (b.top + b.bottom) / 2); return !!t && (t === e || e.contains(t)); };
  // The save-state indicator (FL-07): the status lozenge of the header (or, on a page without one, the first lozenge),
  // and the operation status or conflict review in the header region (the header and the two elements after it).
  const indicator = () => {
    // The investigation's record header; a plain header only when the page has no record header with a lozenge (a
    // workspace or site header may carry its own lozenge for the connection state, PERSIST-033).
    const withLozenge = sel => [...document.querySelectorAll(sel)].find(h => h.querySelector('.ef-status-lozenge'));
    const header = withLozenge('.ef-record-header') || withLozenge('header');
    const lozenge = header ? header.querySelector('.ef-status-lozenge') : document.querySelector('.ef-status-lozenge');
    const sel = '.ef-operation-status, .ef-conflict-review';
    const region = header ? [header, header.nextElementSibling, header.nextElementSibling && header.nextElementSibling.nextElementSibling].filter(Boolean) : [];
    const operation = header ? region.map(n => (n.matches(sel) ? n : n.querySelector(sel))).find(Boolean) || null : document.querySelector(sel);
    return { header, lozenge, operation };
  };
  // The seven save states (PERSIST-030), named by the record header's lozenge.
  const headingOf = e => ((e.querySelector('h1, h2, h3, h4, h5, h6, .ef-alert__title') || {}).textContent || '').trim();
  const recordState = () => ((indicator().lozenge || {}).textContent || '').trim();
  // Faults: alerts, Forma fault patterns and danger alerts or callouts, and the Save failed operation status however it
  // is announced (PERSIST-030 asks only for an assertive announcement). Unknown commit outcome while the automatic
  // reads run is a status, not a fault: PERSIST-031 offers no action yet.
  const faults = withConflict => {
    const base = '[class*="ef-fault"], [role=alert], .ef-alert[data-tone=danger], .ef-callout[data-tone=danger]' + (withConflict ? ', .ef-conflict-review' : '');
    const op = indicator().operation;
    const saveFailed = [...document.querySelectorAll('.ef-operation-status')].filter(o => /save failed/i.test(headingOf(o)) || (o === op && /^save failed$/i.test(recordState())));
    const all = [...new Set([...document.querySelectorAll(base), ...saveFailed])];
    return all.filter(e => !all.some(o => o !== e && o.contains(e)))
      .filter(e => !(e.closest('.ef-operation-status') && /unknown commit outcome/i.test(`${headingOf(e.closest('.ef-operation-status'))} ${recordState()}`) && /may or may not/i.test(e.textContent || '')));
  };
  window.__ef = { rgba, over, ratio, opacityOf, backgroundAt, hidden, visibleText, clipBoxes, shownRect, onTop, indicator, headingOf, recordState, faults };
};
const install = page => inPage(page, `(${helpers.toString()})()`);

// ---- FL-01 touch targets -----------------------------------------------------------------------------------------

// Every interactive control is at least 44x44 CSS px (--ef-layout-target-min).
export const touchTargets = page =>
  inPage(page, () => {
    const describe = e => `${e.tagName.toLowerCase()} "${(e.textContent || '').trim().slice(0, 30)}"`;
    const selector = 'button, a[href], input:not([type=hidden]), select, textarea, summary, [role=button], [role=tab], [role=menuitem], [role=menuitemradio], [role=menuitemcheckbox], [role=checkbox], [role=switch], [role=radio], [role=option], [role=treeitem], [role=gridcell][tabindex], [role=link], [onclick], [tabindex]:not([tabindex="-1"])';
    const visible = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden'; };
    // Every element, including those inside open shadow roots (custom elements).
    const deep = root => [...root.querySelectorAll('*')].flatMap(e => [e, ...(e.shadowRoot ? deep(e.shadowRoot) : [])]);
    const elements = deep(document.body);
    const inControl = e => !!(e.parentElement && e.parentElement.closest(selector));
    // Anything that looks clickable (pointer cursor) and is not inside a control counts as a control too.
    const pointerOnly = elements.filter(e => getComputedStyle(e).cursor === 'pointer' && !inControl(e) && !e.matches(selector) && ![...e.children].some(c => getComputedStyle(c).cursor === 'pointer'));
    return [...new Set([...elements.filter(e => e.matches(selector)), ...pointerOnly])]
      .filter(visible)
      .map(e => [e, e.getBoundingClientRect()])
      .filter(([, r]) => r.width < 43.5 || r.height < 43.5)
      .map(([e, r]) => `${describe(e)} is ${Math.round(r.width)}x${Math.round(r.height)} px`);
  });

// ---- FL-02 no horizontal page scroll; wide content scrolls in an accessible region --------------------------------

// The page neither scrolls sideways nor hides content past the viewport's edge (clipping with overflow-x on html or
// body is the usual way to hide the scroll bar, and it hides content too). Content inside a scroll container is that
// container's concern (scrollRegionsAccessible).
export const noHorizontalScroll = page =>
  inPage(page, () => {
    const width = document.scrollingElement.scrollWidth;
    const inScroller = e => { for (let n = e.parentElement; n && n !== document.body && n !== document.documentElement; n = n.parentElement) { if (['auto', 'scroll'].includes(getComputedStyle(n).overflowX)) return true; } return false; };
    const visible = e => { const r = e.getBoundingClientRect(); const s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.position !== 'fixed'; };
    const beyond = [...document.body.querySelectorAll('*')]
      .filter(e => visible(e) && !inScroller(e) && !e.closest('.ef-visually-hidden, [aria-hidden=true]'))
      .filter(e => e.getBoundingClientRect().right > window.innerWidth + 0.5 || e.getBoundingClientRect().left < -0.5)
      .filter(e => ![...e.children].some(c => c.getBoundingClientRect().right > window.innerWidth + 0.5))
      .slice(0, 3)
      .map(e => `${e.tagName.toLowerCase()} "${(e.textContent || '').trim().slice(0, 30)}" extends past the ${window.innerWidth} px viewport`);
    return [...(width > window.innerWidth + 0.5 ? [`page is ${width} px wide in a ${window.innerWidth} px viewport`] : []), ...beyond];
  });

// Content wider than its container scrolls inside it, and that container is reachable by keyboard and named, so the
// scrolling is perceivable and operable (the visible affordance of FL-02).
export const scrollRegionsAccessible = page =>
  inPage(page, () => {
    const scrolls = e => { const s = getComputedStyle(e); return ['auto', 'scroll'].includes(s.overflowX) && e.scrollWidth > e.clientWidth + 1; };
    const clipped = e => {
      const s = getComputedStyle(e);
      if (!['hidden', 'clip'].includes(s.overflowX)) return false;
      if (e === document.documentElement || e === document.body) return document.scrollingElement.scrollWidth > window.innerWidth + 0.5 || [...document.body.querySelectorAll('*')].some(c => c.getBoundingClientRect().right > window.innerWidth + 0.5);
      return e.scrollWidth > e.clientWidth + 1 && (e.children.length > 0 || (e.textContent || "").trim() !== "");
    };
    const named = e => ['aria-label', 'title'].some(k => (e.getAttribute(k) || '').trim() !== '') || (e.getAttribute('aria-labelledby') || '').split(/\s+/).some(id => { const l = id && document.getElementById(id); return l && l.textContent.trim() !== ''; });
    // Keyboard users can scroll a region that is focusable itself or holds focusable content (as axe's
    // scrollable-region-focusable rule accepts).
    const reachable = e => e.tabIndex >= 0 || !!e.querySelector('a[href], button, input, select, textarea, [tabindex]:not([tabindex="-1"])');
    // ::-webkit-scrollbar { display: none } (or zero size) hides the bar in WebKit and Chromium.
    const rules = [...document.styleSheets].flatMap(sheet => { try { return [...sheet.cssRules]; } catch (err) { return []; } }).filter(r => r.selectorText && r.selectorText.includes('::-webkit-scrollbar'));
    // A bar under 4 px is too thin to see or use.
    const thin = v => v !== '' && parseFloat(v) < 4;
    const hidesBar = r => r.style.display === 'none' || thin(r.style.width) || thin(r.style.height);
    // scrollbar-color with a transparent thumb paints an invisible bar.
    const invisibleThumb = e => { const c = getComputedStyle(e).scrollbarColor || 'auto'; return c !== 'auto' && /^(transparent|rgba\([^)]*,\s*0\))/.test(c.trim()); };
    const webkitHidden = e => rules.filter(hidesBar).some(r => r.selectorText.split(',').map(x => x.trim().replace(/::-webkit-scrollbar.*$/, '') || '*').some(sel => { try { return e.matches(sel); } catch (err) { return false; } }));
    // The visually-hidden pattern (a 1 px box, clipped to nothing) clips on purpose: its text is for screen readers.
    const visuallyHidden = e => { const r = e.getBoundingClientRect(); const st = getComputedStyle(e); return r.width <= 1 || r.height <= 1 || /inset\(\s*(50%|100%)/.test(st.clipPath) || /rect\(\s*0(px)?[ ,]+0(px)?[ ,]+0(px)?[ ,]+0(px)?\s*\)/.test(st.clip || ''); };
    const all = [document.documentElement, document.body, ...document.body.querySelectorAll('*')];
    return [
      ...all.filter(scrolls).filter(e => e !== document.documentElement && e !== document.body).filter(e => !(reachable(e) && (named(e) || e.tabIndex < 0))).map(e => `${e.className || e.tagName} scrolls but cannot be reached by keyboard or has no name`),
      ...all.filter(scrolls).filter(e => getComputedStyle(e).scrollbarWidth === 'none' || webkitHidden(e) || invisibleThumb(e)).map(e => `${e.className || e.tagName} hides its scroll bar, so its scrolling is not visible`),
      ...all.filter(clipped).filter(e => !e.closest('[aria-hidden=true], .ef-visually-hidden') && !visuallyHidden(e)).map(e => `${e.className || e.tagName} hides overflowing content instead of scrolling it`),
    ];
  });

// ---- FL-03 text is never clipped; line length within the measure ------------------------------------------------

export const textNotClipped = page =>
  inPage(page, () => {
    const labelled = e => e && (((e.getAttribute('title') || '').trim() !== '') || ((e.getAttribute('aria-label') || '').trim() !== ''));
    const hidden = e => e.closest('.ef-visually-hidden, [aria-hidden=true]');
    const clipsBox = s => ['hidden', 'clip'].includes(s.overflowX) || ['hidden', 'clip'].includes(s.overflowY) || /paint|strict|content/.test(s.contain || '') || /inset\(/.test(s.clipPath || '');
    // The visible box of a clipping ancestor: its border box, shrunk by a clip-path inset() where there is one.
    const clipRect = a => {
      const b = a.getBoundingClientRect();
      const m = /inset\(([^)]*)\)/.exec(getComputedStyle(a).clipPath || '');
      if (!m) return b;
      const v = m[1].split(/\s+round\s+/)[0].trim().split(/\s+/).map(x => x.endsWith('%') ? parseFloat(x) / 100 : parseFloat(x));
      const [t, r, bo, l] = [v[0], v[1] ?? v[0], v[2] ?? v[0], v[3] ?? v[1] ?? v[0]];
      const px = (x, size) => (x <= 1 && x >= 0 && m[1].includes('%') ? x * size : x);
      return { left: b.left + px(l, b.width), right: b.right - px(r, b.width), top: b.top + px(t, b.height), bottom: b.bottom - px(bo, b.height) };
    };
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, { acceptNode: n => (n.textContent.trim() ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT) });
    const texts = [];
    for (let n = walker.nextNode(); n; n = walker.nextNode()) texts.push(n);
    // A text range is clipped when part of it lies outside the box of an ancestor that clips (overflow hidden or
    // clip), or when its own element truncates with an ellipsis or a line clamp.
    const clipped = node => {
      const owner = node.parentElement;
      if (hidden(owner)) return false;
      const range = document.createRange();
      range.selectNodeContents(node);
      const rects = [...range.getClientRects()].filter(r => r.width > 0 && r.height > 0);
      const s = getComputedStyle(owner);
      const truncated = (s.textOverflow === 'ellipsis' && owner.scrollWidth > owner.clientWidth + 1) || (s.webkitLineClamp && s.webkitLineClamp !== 'none' && owner.scrollHeight > owner.clientHeight + 1);
      if (truncated) return !(labelled(owner) && [owner.getAttribute('title'), owner.getAttribute('aria-label')].some(v => v && v.includes(node.textContent.trim().slice(0, 40))));
      // Text in a fixed element that runs past the viewport can never be scrolled into view.
      const fixed = (() => { for (let a = owner; a && a !== document.documentElement; a = a.parentElement) if (getComputedStyle(a).position === 'fixed') return true; return false; })();
      // Text wholly off screen is the reveal-on-focus pattern (a skip link), not clipping.
      const onScreen = r => r.right > 0 && r.left < window.innerWidth && r.bottom > 0 && r.top < window.innerHeight;
      if (fixed && rects.some(onScreen) && rects.some(r => r.right > window.innerWidth + 1 || r.left < -1 || r.bottom > window.innerHeight + 1 || r.top < -1)) return true;
      for (let a = owner; a && a !== document.documentElement; a = a.parentElement) {
        if (!clipsBox(getComputedStyle(a))) continue;
        if (a === owner && !/inset\(|paint|strict|content/.test(`${getComputedStyle(a).clipPath} ${getComputedStyle(a).contain}`) && !['hidden', 'clip'].includes(getComputedStyle(a).overflowX) && !['hidden', 'clip'].includes(getComputedStyle(a).overflowY)) continue;
        const box = clipRect(a);
        const outside = rects.some(r => r.right > box.right + 1 || r.bottom > box.bottom + 1 || r.left < box.left - 1 || r.top < box.top - 1);
        // A label excuses clipping only when it carries the clipped text itself.
        const carries = el => el && [el.getAttribute('title'), el.getAttribute('aria-label')].some(v => v && v.includes(node.textContent.trim().slice(0, 40)));
        if (outside && !carries(owner) && !carries(a)) return true;
      }
      return false;
    };
    return texts.filter(clipped).map(n => `"${n.textContent.trim().slice(0, 40)}" is clipped without an accessible full value`);
  });

// Running text (any element whose own text runs to at least 80 characters) stays within --ef-layout-measure (72ch),
// wherever it is; preformatted code is exempt.
export const lineLength = page =>
  inPage(page, () => {
    const chWidth = e => {
      const probe = document.createElement('span');
      probe.textContent = '0'.repeat(100);
      probe.style.cssText = 'position:absolute;visibility:hidden;white-space:nowrap';
      e.appendChild(probe);
      const w = probe.getBoundingClientRect().width / 100;
      probe.remove();
      return w;
    };
    // A line is every text rectangle on the same baseline band: group the range's rectangles by line and measure each
    // line's full extent, so links and emphasis inside a paragraph do not split it.
    const lineWidth = e => {
      const r = document.createRange();
      r.selectNodeContents(e);
      const rects = [...r.getClientRects()].filter(x => x.width > 0 && x.height > 0);
      const lines = rects.reduce((acc, x) => { const line = acc.find(l => Math.abs(l.mid - (x.top + x.bottom) / 2) < x.height / 2); if (line) { line.left = Math.min(line.left, x.left); line.right = Math.max(line.right, x.right); } else acc.push({ mid: (x.top + x.bottom) / 2, left: x.left, right: x.right }); return acc; }, []);
      return Math.max(0, ...lines.map(l => l.right - l.left));
    };
    // Running text is any block whose children are all inline and whose text runs to at least 80 characters.
    const inlineOnly = e => [...e.children].every(c => ['inline', 'inline-block', 'inline-flex', 'inline-grid', 'inline-table', 'contents'].includes(getComputedStyle(c).display) || c.tagName === 'BR');
    const block = e => !['inline', 'inline-block', 'inline-flex', 'inline-grid', 'inline-table', 'contents', 'none'].includes(getComputedStyle(e).display);
    return [...document.body.querySelectorAll('*')]
      .filter(e => block(e) && inlineOnly(e) && (e.textContent || '').trim().length >= 80 && !e.closest('.ef-visually-hidden, [aria-hidden=true], pre, code') && !['SCRIPT', 'STYLE', 'PRE', 'CODE'].includes(e.tagName))
      .map(e => [e, lineWidth(e) / chWidth(e)])
      .filter(([, ch]) => ch > 72.5)
      .map(([e, ch]) => `"${e.textContent.trim().slice(0, 30)}" is ${ch.toFixed(0)}ch wide`);
  });

// ---- FL-04 keyboard: skip link first, DOM order, visible focus, dialog focus -------------------------------------

const ringWidth = page =>
  inPage(page, () => {
    const probe = document.createElement('div');
    probe.style.width = 'var(--ef-focus-ring-width, 3px)';
    document.body.appendChild(probe);
    const px = probe.getBoundingClientRect().width;
    probe.remove();
    return px;
  });

const tabSequence = async (page, maxTabs) => {
  const step = async seen => {
    await page.keyboard.press('Tab');
    const key = await inPage(page, () => {
      const e = document.activeElement;
      if (!e || e === document.body) return null;
      if (!e.dataset.efVisit) e.dataset.efVisit = String(document.querySelectorAll('[data-ef-visit]').length);
      return e.dataset.efVisit;
    });
    return key === null || seen.includes(key) ? seen : [...seen, key];
  };
  return Array.from({ length: maxTabs }).reduce(acc => acc.then(step), Promise.resolve([]));
};

// Every element reached by Tab shows a focus ring it does not show when blurred: a non-transparent outline at least
// --ef-focus-ring-width wide, or a box-shadow ring of at least that spread.
export const focusVisible = async (page, { maxTabs } = {}) => {
  maxTabs = maxTabs || Math.min(200, 5 + (await inPage(page, () => document.querySelectorAll('a[href], button, input, select, textarea, summary, [tabindex]:not([tabindex="-1"])').length)));
  const ring = await ringWidth(page);
  await inPage(page, tools => { window.__efColour = eval(`(${tools})()`); }, colourTools.toString());
  await install(page);
  const visits = await Array.from({ length: maxTabs }).reduce(async (acc) => {
    const seen = await acc;
    await page.keyboard.press('Tab');
    const v = await inPage(page, minWidth => {
      const e = document.activeElement;
      if (!e || e === document.body) return null;
      const transparent = c => /rgba\([^)]*,\s*0\)$/.test(c) || c === 'transparent';
      const ringOf = s => {
        const outline = s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= minWidth - 0.5 && !transparent(s.outlineColor);
        const shadows = (s.boxShadow === 'none' ? [] : s.boxShadow.split(/,(?![^(]*\))/))
          .map(x => ({ colour: (x.match(/rgba?\([^)]*\)/) || [''])[0], lengths: (x.replace(/rgba?\([^)]*\)/, '').match(/-?[\d.]+px/g) || []).map(parseFloat) }))
          .filter(x => x.lengths.length >= 4 && x.lengths[3] >= minWidth - 0.5 && !transparent(x.colour));
        return outline || shadows.length > 0;
      };
      const focused = getComputedStyle(e);
      const focusedRing = ringOf(focused);
      const signature = s => `${s.outlineStyle} ${s.outlineWidth} ${s.outlineColor} ${s.boxShadow}`;
      const focusedSignature = signature(focused);
      e.blur();
      const blurredSignature = signature(getComputedStyle(e));
      e.focus({ focusVisible: true });
      // The ring must also stand out from what is behind it: some component at 3:1 or more (WCAG 2.4.13).
      const { rgba, over, background, ratio } = window.__efColour;
      const colours = [...(focused.outlineStyle !== 'none' ? [focused.outlineColor] : []), ...(focused.boxShadow === 'none' ? [] : (focused.boxShadow.match(/rgba?\([^)]*\)/g) || []))].filter(c => rgba(c)[3] > 0);
      const bg = background(e.parentElement || e);
      const contrasting = bg === null || colours.some(c => ratio(over(rgba(c), bg), bg) >= 2.995);
      // The ring is drawn outside the element (outline width plus offset, or shadow spread); an ancestor that clips
      // its content must leave some of that band showing.
      const outlineReach = focused.outlineStyle !== 'none' ? parseFloat(focused.outlineWidth) + Math.max(0, parseFloat(focused.outlineOffset) || 0) : 0;
      const shadowReach = Math.max(0, ...((focused.boxShadow === 'none' ? [] : focused.boxShadow.split(/,(?![^(]*\))/)).map(x => ((x.replace(/rgba?\([^)]*\)/, '').match(/-?[\d.]+px/g) || [])[3] || '0px')).map(parseFloat)));
      const reach = Math.max(outlineReach, shadowReach);
      const r = e.getBoundingClientRect();
      const ringBox = { left: r.left - reach, top: r.top - reach, right: r.right + reach, bottom: r.bottom + reach };
      const area = b => Math.max(0, b.right - b.left) * Math.max(0, b.bottom - b.top);
      const meet = (a, b) => ({ left: Math.max(a.left, b.left), top: Math.max(a.top, b.top), right: Math.min(a.right, b.right), bottom: Math.min(a.bottom, b.bottom) });
      const clip = window.__ef.clipBoxes(e).reduce((acc, b) => meet(acc, b), { left: -1e9, top: -1e9, right: 1e9, bottom: 1e9 });
      const band = area(ringBox) - area(r);
      const shownBand = area(meet(ringBox, clip)) - area(meet(r, clip));
      const unclipped = reach === 0 || band <= 0 || shownBand >= band * 0.25;
      // The focused element itself is not covered by another element (WCAG 2.4.11): its centre is on top.
      const cx = Math.min(Math.max(r.left + r.width / 2, 0), window.innerWidth - 1);
      const cy = Math.min(Math.max(r.top + r.height / 2, 0), window.innerHeight - 1);
      const top = r.width > 0 && r.bottom > 0 && r.top < window.innerHeight ? document.elementFromPoint(cx, cy) : e;
      const uncovered = !top || e.contains(top) || top.contains(e);
      return { key: e.outerHTML.slice(0, 80), ok: focusedRing && focusedSignature !== blurredSignature && contrasting && unclipped && uncovered, label: `${e.tagName.toLowerCase()} "${(e.textContent || '').trim().slice(0, 30)}"` };
    }, ring);
    return v === null || seen.some(x => x.key === v.key) ? seen : [...seen, v];
  }, Promise.resolve([]));
  const interactive = await inPage(page, () => document.querySelectorAll('button, a[href], input, select, textarea, summary, [tabindex]').length);
  return [
    ...(interactive > 0 && visits.length === 0 ? ['no element received focus from the keyboard'] : []),
    ...visits.filter(v => !v.ok).map(v => `${v.label} shows no focus ring of at least ${ring} px, at 3:1 against its background, unclipped by its container and uncovered, that it lacks when blurred`),
  ];
};

// Tab follows DOM order: no positive tabindex, and the Tab sequence visits focusable elements in document order.
export const focusOrder = async (page, { maxTabs = 25 } = {}) => {
  const positive = await inPage(page, () => [...document.querySelectorAll('[tabindex]')].filter(e => e.tabIndex > 0).map(e => `${e.tagName.toLowerCase()} has tabindex ${e.tabIndex}`));
  await tabSequence(page, maxTabs);
  const order = await inPage(page, max => {
    const visited = [...document.querySelectorAll('[data-ef-visit]')];
    const byVisit = [...visited].sort((a, b) => Number(a.dataset.efVisit) - Number(b.dataset.efVisit));
    const inDom = visited; // querySelectorAll returns document order
    // Every focusable control is reached (none skipped by a script), when the page has no more than the Tabs pressed.
    const focusables = [...document.querySelectorAll('a[href], button:not([disabled]), input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])')]
      .filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden' && !e.closest('[inert], dialog:not([open])') && !(e.closest('details:not([open])') && e.tagName !== 'SUMMARY' && e.closest('details:not([open])').querySelector('summary') !== e); });
    const skipped = focusables.length <= max ? focusables.filter(e => !e.dataset.efVisit) : [];
    return [
      ...(byVisit.every((e, k) => e === inDom[k]) ? [] : ['the Tab sequence does not follow document order']),
      ...skipped.map(e => `${e.tagName.toLowerCase()} "${(e.textContent || '').trim().slice(0, 30)}" is never reached by Tab`),
    ];
  }, maxTabs);
  return [...positive, ...order];
};

// The first Tab reaches a skip link whose target exists.
export const skipLinkFirst = async page => {
  await install(page);
  await page.keyboard.press('Tab');
  return inPage(page, () => {
    const e = document.activeElement;
    const href = e && e.getAttribute && e.getAttribute('href');
    const target = href && href.startsWith('#') ? document.getElementById(href.slice(1)) : null;
    if (!(e && e.tagName === 'A' && /skip/i.test(e.textContent) && target && getComputedStyle(target).display !== 'none')) return ['the first Tab does not reach a skip link with a displayed target'];
    const r = e.getBoundingClientRect();
    const inView = r.width > 1 && r.height > 1 && r.right > 0 && r.bottom > 0 && r.left < window.innerWidth && r.top < window.innerHeight;
    // On screen and on top: the element at its centre is the link (not clipped away, not under a fixed header).
    const hit = inView && (() => { const x = Math.min(Math.max(r.left + r.width / 2, 0), window.innerWidth - 1); const y = Math.min(Math.max(r.top + r.height / 2, 0), window.innerHeight - 1); const top = document.elementFromPoint(x, y); return !!top && (top === e || e.contains(top)); })();
    return hit && getComputedStyle(e).opacity !== '0' && window.__ef.visibleText(e) !== '' ? [] : ['the skip link stays invisible or covered when focused'];
  });
};

// Opening a dialog moves focus into it, Tab stays inside it, and Escape closes it and returns focus to its invoker.
// invokers: [{ invoker: CSS selector, dialog: id }] names invokers whose handler is attached by script and that do not
// declare their dialog (AGENT-RULES asks every invoker to declare it with commandfor or aria-controls).
export const dialogFocus = async (page, { invokers: named = [] } = {}) => {
  // Invokers: declared commands, aria-controls pointing at a dialog, and controls whose click handler names a dialog
  // by id. A dialog is a <dialog> or an element with role=dialog.
  const invokers = await inPage(page, named => {
    const dialogs = [...document.querySelectorAll('dialog[id], [role=dialog][id], [role=alertdialog][id]')];
    const target = b => b.getAttribute('commandfor') || b.getAttribute('popovertarget') || b.getAttribute('aria-controls') || (dialogs.find(d => new RegExp(`\\b${d.id}\\b`).test(b.getAttribute('onclick') || '')) || {}).id;
    const declared = [...document.querySelectorAll('button[commandfor]:not([command=close]):not([command=request-close]), [popovertarget]:not([popovertargetaction=hide]), [aria-controls], button[onclick], [role=button][onclick]')]
      .filter(b => dialogs.some(d => d.id === target(b)) && !dialogs.some(d => d.contains(b)))
      .map(b => [b, target(b)]);
    const given = named.flatMap(n => [...document.querySelectorAll(n.invoker)].map(b => [b, n.dialog]));
    return [...declared, ...given.filter(([b]) => !declared.some(([d]) => d === b))]
      .map(([b, dialog], k) => { b.dataset.efInvoker = String(k); return { key: String(k), dialog }; });
  }, named);
  if (invokers.length === 0) return ['no dialog invoker on the page'];
  const open = async (key, dialog) => {
    await page.focus(`[data-ef-invoker="${key}"]`);
    await page.keyboard.press('Enter');
    // A browser without invoker commands cannot run a declared command="show-modal"; only then is the declared
    // command carried out as the browser would. Any other invoker must open the dialog itself, or the assertion fails.
    await inPage(page, ({ id, k }) => {
      const d = document.getElementById(id);
      const b = document.querySelector(`[data-ef-invoker="${k}"]`);
      const supported = 'command' in HTMLButtonElement.prototype;
      if (d.tagName === 'DIALOG' && !d.open && !supported && b.getAttribute('commandfor') === id && ['show-modal', 'show'].includes(b.getAttribute('command'))) (b.getAttribute('command') === 'show' ? d.show() : d.showModal());
    }, { id: dialog, k: key });
  };
  // Focus may leave a native modal dialog for the browser's own interface (activeElement becomes body); it must never
  // land on page content outside the dialog.
  const contained = dialog => inPage(page, id => { const a = document.activeElement; return !a || a === document.body || document.getElementById(id).contains(a); }, dialog);
  const shown = dialog => inPage(page, id => { const d = document.getElementById(id); return d.tagName === 'DIALOG' ? d.open : !d.hidden && getComputedStyle(d).display !== 'none' && getComputedStyle(d).visibility !== 'hidden'; }, dialog);
  const check = async ({ key, dialog }) => {
    await open(key, dialog);
    const isOpen = await shown(dialog);
    if (!isOpen) return [`the invoker does not open #${dialog}`];
    const opened = await inPage(page, id => document.getElementById(id).contains(document.activeElement), dialog);
    await page.keyboard.press('Escape');
    const closed = !(await shown(dialog));
    // Give scripts that move focus after closing a moment, then require focus still on the invoker.
    await page.waitForTimeout(1000);
    const returned = await inPage(page, k => document.activeElement && document.activeElement.dataset.efInvoker === k, key);
    await open(key, dialog);
    const trapped = await Array.from({ length: 8 }).reduce(async (acc) => { const ok = await acc; await page.keyboard.press('Tab'); return ok && (await contained(dialog)); }, Promise.resolve(true));
    await inPage(page, id => { const d = document.getElementById(id); if (d.tagName === 'DIALOG') d.close(); else if (d.hasAttribute('popover')) d.hidePopover(); else d.hidden = true; }, dialog);
    return [
      ...(opened ? [] : [`opening #${dialog} does not move focus into it`]),
      ...(closed ? [] : [`Escape does not close #${dialog}`]),
      ...(returned ? [] : [`closing #${dialog} does not return focus to its invoker`]),
      ...(trapped ? [] : [`Tab moves focus from the open dialog #${dialog} to page content outside it`]),
    ];
  };
  return invokers.reduce(async (acc, x) => [...(await acc), ...(await check(x))], Promise.resolve([]));
};

// ---- FL-05 reduced motion ------------------------------------------------------------------------------------------

// With prefers-reduced-motion, no running animation (CSS or script, document.getAnimations) and no transition lasts
// longer than 10 ms. The runner emulates reducedMotion: 'reduce' for this assertion.
// Script-driven motion (requestAnimationFrame, or animations started late) is caught by sampling every element's
// transform, opacity and position twice, 400 ms apart, after the page has run for 1.5 s.
const motionSample = page =>
  inPage(page, () => [...document.body.querySelectorAll('*')].map(e => { const s = getComputedStyle(e); const r = e.getBoundingClientRect(); return `${s.transform}|${s.opacity}|${Math.round(r.left)},${Math.round(r.top)},${Math.round(r.width)},${Math.round(r.height)}`; }));

export const reducedMotion = async page => {
  await page.waitForTimeout(1500);
  const first = await motionSample(page);
  await page.waitForTimeout(400);
  const second = await motionSample(page);
  const moving = first.length === second.length ? first.filter((x, k) => x !== second[k]).length : 1;
  const found = await inPage(page, () => {
    const seconds = v => v.split(',').map(x => x.trim()).map(x => (x.endsWith('ms') ? parseFloat(x) / 1000 : parseFloat(x) || 0));
    const longest = xs => Math.max(0, ...xs);
    const label = e => (e ? `${e.tagName.toLowerCase()}${typeof e.className === 'string' && e.className ? '.' + e.className.trim().split(/\s+/).join('.') : ''}` : 'document');
    const animations = document.getAnimations()
      .map(a => [a, a.effect ? a.effect.getComputedTiming() : { duration: 0 }])
      .filter(([, t]) => (typeof t.duration === 'number' ? t.duration : 0) > 10)
      .map(([a, t]) => `${label(a.effect && a.effect.target)} animates for ${t.duration} ms`);
    const transitions = [...document.querySelectorAll('*')]
      .flatMap(e => [[e, getComputedStyle(e)], [e, getComputedStyle(e, '::before')], [e, getComputedStyle(e, '::after')]])
      .filter(([, s]) => s.transitionProperty !== 'none' && longest(seconds(s.transitionDuration)) > 0.01)
      .map(([e, s]) => `${label(e)} transitions for ${s.transitionDuration}`);
    // Smooth scrolling animates every in-page jump.
    const smooth = [document.documentElement, document.body, ...document.body.querySelectorAll('*')].filter(e => getComputedStyle(e).scrollBehavior === 'smooth').map(e => `${label(e)} scrolls smoothly`);
    return [...animations, ...transitions, ...smooth];
  });
  return [...found, ...(moving > 0 ? [`${moving} element(s) keep moving under reduced motion (script-driven animation)`] : [])];
};

// ---- FL-06 contrast, non-text contrast, forced colours, amber with a label --------------------------------------

const colourTools = () => {
  // Any CSS colour (rgb, color(srgb ...), oklch, color-mix results) normalised by painting it: sRGB 0-255 and alpha 0-1.
  const rgba = c => {
    const canvas = rgba.canvas || (rgba.canvas = Object.assign(document.createElement('canvas'), { width: 1, height: 1 }));
    const ctx = rgba.ctx || (rgba.ctx = canvas.getContext('2d', { willReadFrequently: true }));
    ctx.clearRect(0, 0, 1, 1);
    ctx.fillStyle = 'rgba(0, 0, 0, 0)';
    ctx.fillStyle = c || 'rgba(0, 0, 0, 0)';
    ctx.fillRect(0, 0, 1, 1);
    const [r, g, b, a] = ctx.getImageData(0, 0, 1, 1).data;
    return [r, g, b, a / 255];
  };
  const over = ([r, g, b, a], [R, G, B]) => [r * a + R * (1 - a), g * a + G * (1 - a), b * a + B * (1 - a), 1];
  const background = e => {
    const layers = [];
    for (let n = e; n; n = n.parentElement) {
      const s = getComputedStyle(n);
      if (s.backgroundImage !== 'none') return null;
      layers.push(rgba(s.backgroundColor));
    }
    return layers.reverse().reduce((acc, layer) => over(layer, acc), [255, 255, 255, 1]);
  };
  const lum = ([r, g, b]) => [r, g, b].map(v => v / 255).map(v => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)).reduce((acc, v, k) => acc + v * [0.2126, 0.7152, 0.0722][k], 0);
  const ratio = (a, b) => (Math.max(lum(a), lum(b)) + 0.05) / (Math.min(lum(a), lum(b)) + 0.05);
  return { rgba, over, background, ratio };
};

// Text contrast at least 4.5:1, or 3:1 for large text, against the composited background.
export const textContrast = async page => {
  await settle(page);
  await install(page);
  return inPage(page, () => {
    const { rgba, over, ratio, opacityOf, backgroundAt, hidden } = window.__ef;
    const hasText = e => [...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim() !== '');
    const need = s => { const size = parseFloat(s.fontSize); return size >= 24 || (size >= 18.66 && parseInt(s.fontWeight, 10) >= 700) ? 3 : 4.5; };
    const judge = (e, colour, label) => {
      const bg = backgroundAt(e);
      if (bg === null) return [];
      const [r, g, b, a] = rgba(colour);
      const c = ratio(over([r, g, b, a * opacityOf(e)], bg), bg);
      const n = need(getComputedStyle(e));
      return c < n - 0.005 ? [`"${label.slice(0, 30)}" has contrast ${c.toFixed(2)}:1 (needs ${n}:1)`] : [];
    };
    // Text nodes, the values typed into fields, and placeholders.
    const texts = [...document.body.querySelectorAll('*')].filter(e => hasText(e) && !hidden(e)).flatMap(e => judge(e, getComputedStyle(e).color, e.textContent.trim()));
    const fields = [...document.querySelectorAll('input:not([type=hidden]):not([type=checkbox]):not([type=radio]), textarea')].filter(e => !hidden(e));
    const values = fields.filter(e => e.value).flatMap(e => judge(e, getComputedStyle(e).color, e.value));
    const placeholders = fields.filter(e => e.placeholder && !e.value).flatMap(e => judge(e, getComputedStyle(e, '::placeholder').color, e.placeholder));
    // Text generated by ::before and ::after (content: "...").
    const generated = [...document.body.querySelectorAll('*')].filter(e => !hidden(e)).flatMap(e => ['::before', '::after'].map(p => [e, getComputedStyle(e, p)]))
      .map(([e, s]) => [e, s, (s.content.match(/^"(.*)"$/) || [, ''])[1].trim()])
      .filter(([, s, t]) => t !== '' && s.display !== 'none' && s.visibility !== 'hidden')
      .flatMap(([e, s, t]) => judge(e, s.color, t));
    return [...texts, ...values, ...placeholders, ...generated];
  });
};

// Non-text marks at least 3:1 against what is behind them: the visible boundary of form controls (the strongest of
// their border sides, or their own fill), switch and checkbox tracks, icons (SVG fill and stroke) in controls and
// images, and the focus ring.
export const nonTextContrast = async page => {
  await settle(page);
  await install(page);
  return inPage(page, () => {
    const { rgba, over, ratio, backgroundAt, hidden } = window.__ef;
    const against = (colour, e) => { const bg = backgroundAt(e); if (!bg || rgba(colour)[3] === 0) return null; return ratio(over(rgba(colour), bg), bg); };
    const sides = ['Top', 'Right', 'Bottom', 'Left'];
    const boundary = e => {
      const s = getComputedStyle(e);
      const outer = e.parentElement || e;
      const borders = sides.filter(k => s[`border${k}Style`] !== 'none' && parseFloat(s[`border${k}Width`]) > 0).map(k => against(s[`border${k}Color`], outer)).filter(x => x !== null);
      const fill = against(s.backgroundColor, outer);
      return Math.max(0, ...borders, ...(fill === null ? [] : [fill]));
    };
    const controls = [...document.querySelectorAll('input:not([type=hidden]), select, textarea, [role=switch], [role=checkbox], .ef-switch, .ef-checkbox, progress, meter, [role=progressbar], [role=meter]')]
      .filter(e => !hidden(e))
      .map(e => [e, boundary(e)])
      .filter(([, r]) => r < 2.995)
      .map(([e, r]) => `${e.getAttribute('role') || e.tagName.toLowerCase()} boundary has contrast ${r.toFixed(2)}:1 (needs 3:1)`);
    const icons = [...document.querySelectorAll('button svg *, a svg *, [role=img] *, svg[role=img] *, svg[aria-label] *')]
      .filter(e => ['path', 'circle', 'rect', 'polygon', 'line', 'polyline', 'ellipse'].includes(e.tagName.toLowerCase()) && !hidden(e.closest('svg')))
      // A stroked part is seen by its stroke (an open path's default fill paints nothing); otherwise by its fill.
      .map(e => { const s = getComputedStyle(e); const stroked = s.stroke && s.stroke !== 'none' && parseFloat(s.strokeWidth) > 0; const paint = stroked ? s.stroke : s.fill; const host = e.closest('svg'); const r = paint && paint !== 'none' && !paint.startsWith('url') ? against(paint, host) : null; return [e, r === null ? 0 : r]; })
      .filter(([, r]) => r > 0 && r < 2.995)
      .map(([e, r]) => `icon ${e.tagName.toLowerCase()} has contrast ${r.toFixed(2)}:1 (needs 3:1)`);
    // A focus indicator may be layered (Forma draws an outline in the page colour as a gap, outside a dark shadow
    // ring): it is visible when its strongest component reaches 3:1.
    const ringColours = s => [
      ...(s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0 ? [s.outlineColor] : []),
      ...(s.boxShadow === 'none' ? [] : s.boxShadow.split(/,(?![^(]*\))/)).map(x => (x.match(/rgba?\([^)]*\)/) || [''])[0]).filter(Boolean),
    ].filter(c => rgba(c)[3] > 0);
    const rings = [...document.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea')]
      .filter(e => !hidden(e))
      .map(e => {
        e.focus({ focusVisible: true });
        const cs = ringColours(getComputedStyle(e));
        const outer = e.parentElement || e;
        const best = cs.map(c => against(c, outer)).filter(x => x !== null);
        e.blur();
        return [e, best.length ? Math.max(...best) : null];
      })
      .filter(([, r]) => r !== null && r < 2.995)
      .map(([e, r]) => `focus ring of ${e.tagName.toLowerCase()} has contrast ${r.toFixed(2)}:1 (needs 3:1)`);
    return [...controls, ...icons, ...rings];
  });
};

// Under forced colours every status indicator still carries text, and indicators of different states read differently.
// The runner emulates forcedColors: 'active' for this assertion.
export const forcedColorsDistinct = async page => {
  // Markers that differ only by colour (by inline style or by class) are found from their author colours, read with
  // forced colours off, then judged with forced colours on.
  await page.emulateMedia({ forcedColors: 'none' });
  await install(page);
  const colourOnly = await inPage(page, () => {
    const small = [...document.body.querySelectorAll('*')].filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.width <= 24 && r.height > 0 && r.height <= 24 && !/[A-Za-z]{2,}/.test(e.textContent || '') && !e.getAttribute('aria-label') && !e.closest('button, a, [role=img][aria-label]'); });
    const paint = e => { const s = getComputedStyle(e); return [s.backgroundColor, s.color, s.borderTopColor, s.fill].map(c => window.__ef.rgba(c).join(',')).join('|'); };
    const kinds = new Map();
    small.filter(e => window.__ef.rgba(getComputedStyle(e).backgroundColor)[3] > 0 || getComputedStyle(e).fill !== 'rgb(0, 0, 0)').forEach(e => { const r = e.getBoundingClientRect(); const k = `${e.tagName}|${Math.round(r.width)}x${Math.round(r.height)}`; kinds.set(k, [...(kinds.get(k) || []), paint(e)]); });
    return [...kinds.entries()].filter(([, ps]) => new Set(ps).size > 1).map(([k]) => `${k.split('|')[0].toLowerCase()} markers (${k.split('|')[1]}) differ only by colour and carry no text`);
  });
  await page.emulateMedia({ forcedColors: 'active' });
  await install(page);
  return inPage(page, () => {
    const lozenges = [...document.querySelectorAll('.ef-status-lozenge')];
    const text = e => window.__ef.visibleText(e);
    // A lozenge's state is its data-state, or else its class and inline style (how authors usually vary colour).
    const stateOf = e => e.dataset.state || `${e.className}|${e.getAttribute('style') || ''}`;
    const byState = new Map();
    lozenges.forEach(e => byState.set(stateOf(e), [...(byState.get(stateOf(e)) || []), text(e)]));
    const states = [...byState.entries()];
    return [
      ...(lozenges.length === 0 ? ['no status indicator on the page'] : []),
      ...lozenges.filter(e => text(e) === '').map(() => 'a status indicator shows no visible text under forced colours'),
      // Small textless markers of the same kind that differ only by inline colour carry state by colour alone.
      ...(() => {
        const markers = [...document.body.querySelectorAll('*')].filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.width <= 24 && r.height > 0 && r.height <= 24 && (e.textContent || '').trim() === '' && !e.getAttribute('aria-label') && /background|fill|border-color|color/.test(e.getAttribute('style') || ''); });
        const kinds = new Map();
        markers.forEach(e => { const k = `${e.tagName}|${e.className}`; kinds.set(k, [...(kinds.get(k) || []), (e.getAttribute('style') || '')]); });
        return [...kinds.entries()].filter(([, styles]) => new Set(styles).size > 1).map(([k]) => `markers ${k.split('|')[1] || k.split('|')[0].toLowerCase()} differ only by colour and carry no text`);
      })(),
      ...states.flatMap(([st, ts], k) => states.slice(k + 1).filter(([, us]) => us.some(u => ts.includes(u))).map(([u]) => `states "${st}" and "${u}" read the same`)),
    ];
  }).then(found => [...found, ...colourOnly]);
};

// Amber never stands alone: an amber element carries a text label (DEC-R7-002). Amber is UI-016's #E9A23B and close
// shades, and, as the owner mapped it (ODR-V41-05), Forma's warning-status colour as it resolves in the element's own
// theme (a nested dark region has its own value). The token is matched tightly, because other Forma colours sit near
// it (light accent-primary is 35 RGB units from light warning); where the token is undefined it matches nothing.
export const amberLabelled = async page => {
  await install(page);
  return inPage(page, () => {
    const literal = [233, 162, 59];
    const parsed = new Map();
    const tokenOf = e => {
      const value = getComputedStyle(e).getPropertyValue('--ef-color-status-warning').trim();
      if (value === '') return null;
      if (!parsed.has(value)) {
        const probe = document.createElement('span');
        probe.style.color = value;
        document.body.appendChild(probe);
        parsed.set(value, window.__ef.rgba(getComputedStyle(probe).color));
        probe.remove();
      }
      const v = parsed.get(value);
      return v[3] > 0 ? v : null;
    };
    const distance = (v, a) => Math.hypot(v[0] - a[0], v[1] - a[1], v[2] - a[2]);
    const nearIn = e => {
      const token = tokenOf(e);
      return c => { if (!c || c === 'none' || c.startsWith('url')) return false; const v = window.__ef.rgba(c); return v[3] > 0 && (distance(v, literal) < 40 || (token !== null && distance(v, token) < 12)); };
    };
    const colours = s => [s.backgroundColor, s.color, ...(s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0 ? [s.outlineColor] : []), ...(['Top', 'Right', 'Bottom', 'Left'].filter(k => parseFloat(s[`border${k}Width`]) > 0).map(k => s[`border${k}Color`])), s.fill, s.stroke, ...((s.boxShadow.match(/rgba?\([^)]*\)/g)) || []), ...((s.backgroundImage.match(/rgba?\([^)]*\)|#[0-9a-f]{6}\b/gi)) || []).map(c => c.startsWith('#') ? `rgb(${parseInt(c.slice(1, 3), 16)},${parseInt(c.slice(3, 5), 16)},${parseInt(c.slice(5, 7), 16)})` : c)];
    const amberish = e => { const near = nearIn(e); return colours(getComputedStyle(e)).some(c => c && near(c)); };
    // The label is visible text in the amber element or beside it (its parent), never only a screen-reader text.
    // A label is visible text with letters in it (a coloured glyph such as a dot is not a label).
    const words = t => /[A-Za-z]{2,}/.test(t);
    const labelled = e => words(window.__ef.visibleText(e)) || (e.parentElement && words(window.__ef.visibleText(e.parentElement)));
    const candidates = [...document.body.querySelectorAll('*')].filter(e => !window.__ef.hidden(e.closest('svg') || e) && amberish(e));
    // Report the outermost amber element only.
    return candidates.filter(e => !candidates.some(o => o !== e && o.contains(e))).filter(e => !labelled(e)).map(e => `${e.className && typeof e.className === 'string' ? e.className : e.tagName} is amber with no visible text label`);
  });
};

// ---- FL-07 save-state indicator --------------------------------------------------------------------------------

// A status indicator is present, carries text (never colour alone) and is visible without scrolling.
export const statusVisible = async page => {
  await settle(page);
  await install(page);
  return inPage(page, () => {
    // The save-state indicator: the status lozenge of the header (or, on a page without one, the first lozenge) and
    // the operation status or conflict review. Other lozenges on the page (timeline, grid) and empty live regions are
    // not the indicator.
    const { lozenge, operation } = window.__ef.indicator();
    const indicators = [lozenge, operation].filter(Boolean).filter(e => !e.closest('.ef-visually-hidden'));
    // Shown means inside the viewport and every clipping container, and painted on top there.
    // Its start is shown uncut: the full width and the first 48 px (all of a lozenge; a lozenge cut by its container
    // cannot be read). A tall operation status may continue below the fold.
    const shown = e => { const b = window.__ef.shownRect(e); const r = e.getBoundingClientRect(); return b !== null && b.left <= r.left + 1 && b.right >= r.right - 1 && b.top <= r.top + 1 && b.bottom >= r.top + Math.min(r.height, 48) - 1; };
    const label = e => e.className || e.getAttribute('role');
    return indicators.length === 0
      ? ['no save-state indicator (status lozenge, operation status or conflict review) on the page']
      : indicators.flatMap(e => [
          ...(window.__ef.visibleText(e) === '' ? [`${label(e)} shows no visible text`] : []),
          ...(shown(e) ? [] : [`${label(e)} is not visible in the initial viewport (outside it or clipped by a container)`]),
          ...(shown(e) && !window.__ef.onTop(e) ? [`${label(e)} is covered by another element or not painted`] : []),
        ]);
  });
};

// The indicator is composed as FL-07 states: a status lozenge, an operation status or conflict review, and a
// freshness line, together in the header region, each visible without scrolling.
export const indicatorComposed = async page => {
  await install(page);
  return inPage(page, () => {
    const inView = e => window.__ef.shownRect(e) !== null;
    const { header, lozenge } = window.__ef.indicator();
    if (!header) return ['no header carries a status lozenge'];
    // The freshness line is in the header or directly after it; the operation status or conflict review follows.
    const freshness = header.querySelector('.ef-freshness') || (header.nextElementSibling && (header.nextElementSibling.matches('.ef-freshness') ? header.nextElementSibling : header.nextElementSibling.querySelector('.ef-freshness')));
    const after = (a, b) => !!(a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING);
    // PERSIST-030: the lozenge names exactly one of seven states. An operation status explains Unknown commit outcome
    // and Save failed, a conflict review shows Conflict, and Saved, Unsaved, Saving and Offline show neither.
    const states = ['Saved', 'Unsaved', 'Saving', 'Offline', 'Conflict', 'Unknown commit outcome', 'Save failed'];
    const named = (lozenge.textContent || '').trim();
    const state = states.find(x => x.toLowerCase() === named.toLowerCase());
    const headerLozenges = [...header.querySelectorAll('.ef-status-lozenge')].filter(l => !l.closest('.ef-operation-status, .ef-conflict-review'));
    const region = [header, header.nextElementSibling, header.nextElementSibling && header.nextElementSibling.nextElementSibling].filter(Boolean);
    const within = sel => region.flatMap(n => [...(n.matches(sel) ? [n] : []), ...n.querySelectorAll(sel)]).filter((e, k, xs) => xs.indexOf(e) === k && !xs.some(o => o !== e && o.contains(e)));
    const operations = within('.ef-operation-status');
    const reviews = within('.ef-conflict-review');
    const shown = [...operations, ...reviews];
    const wants = state === 'Conflict' ? 'conflict review' : ['Unknown commit outcome', 'Save failed'].includes(state) ? 'operation status' : null;
    return [
      ...(state ? [] : [`the lozenge says "${named}", which is not one of the seven save states`]),
      ...(headerLozenges.length > 1 ? [`the header shows ${headerLozenges.length} save-state lozenges; it shows exactly one`] : []),
      ...(inView(lozenge) ? [] : ['the status lozenge is not visible without scrolling']),
      ...(freshness ? (inView(freshness) && !freshness.closest('.ef-visually-hidden') && window.getComputedStyle(freshness).visibility !== 'hidden' ? [] : ['the freshness line is not visible without scrolling']) : ['no freshness line in or directly after the header']),
      ...(state && !wants && shown.length > 0 ? [`${state} shows ${reviews.length ? 'a conflict review' : 'an operation status'}; PERSIST-030 gives it neither`] : []),
      ...(wants === 'operation status' && operations.length === 0 ? [`the lozenge says "${state}" but no operation status is in the header region`] : []),
      ...(wants === 'operation status' && reviews.length > 0 ? [`${state} shows a conflict review; only Conflict does`] : []),
      ...(wants === 'conflict review' && reviews.length === 0 ? ['the lozenge says "Conflict" but no conflict review is in the header region'] : []),
      ...(wants === 'conflict review' && operations.length > 0 && reviews.length === 0 ? ['in Conflict the header region shows an operation status instead of a conflict review'] : []),
      ...shown.filter(e => !inView(e)).map(() => 'the operation status or conflict review is not visible without scrolling'),
      ...(freshness ? shown.filter(e => !after(freshness, e)).map(() => 'the operation status or conflict review comes before the freshness line') : []),
    ];
  });
};

// ---- FL-08 loading and failures --------------------------------------------------------------------------------

// A loading placeholder tells assistive technology what is loading.
// A loading placeholder is announced: it names itself (own text of at least three letters, aria-label or
// aria-labelledby), or a live region on the page (role=status, role=alert or aria-live) says what is loading.
export const loadingAnnounced = page =>
  inPage(page, () => {
    const loaders = [...document.querySelectorAll('.ef-skeleton, [aria-busy=true], .ef-spinner')];
    const words = t => /[A-Za-z]{3,}/.test(t || '');
    const ownName = e => !e.closest('[aria-hidden=true]') && (words(e.textContent) || words(e.getAttribute('aria-label')) || (e.getAttribute('aria-labelledby') || '').split(/\s+/).some(id => { const l = document.getElementById(id); return l && words(l.textContent); }));
    // A live region announces a loader only if it is tied to it: it describes the loader (aria-describedby), or it sits
    // inside the same busy region, and its text says something is loading.
    const loadingWords = /load|fetch|open|read|wait|sync|prepar|connect/i;
    const describedBy = e => (e.getAttribute('aria-describedby') || '').split(/\s+/).some(id => { const l = id && document.getElementById(id); return l && words(l.textContent); });
    const sameBusyRegion = e => { const region = e.closest('[aria-busy=true]') || e; return [...region.querySelectorAll('[role=status], [role=alert], [aria-live=polite], [aria-live=assertive]')].some(r => !r.closest('[aria-hidden=true]') && loadingWords.test(r.textContent || '')); };
    return loaders.length === 0 ? ['no loading placeholder on the page'] : loaders.filter(e => !ownName(e) && !describedBy(e) && !sameBusyRegion(e)).map(e => `${e.className || e.tagName} is not announced (no name of its own and no live region tied to it saying what is loading)`);
  });

// A fault states what happened in text and offers a next step (a control).
export const faultActionable = async page => {
  // A blocking fault is a dialog shown when the fault happens: open any closed one, then wait for entry animations.
  await inPage(page, () => document.querySelectorAll('dialog[class*="ef-fault"]:not([open])').forEach(d => d.showModal()));
  await settle(page);
  await install(page);
  return inPage(page, () => {
    // A conflict review is a fault that needs its actions too (PERSIST-012).
    const faults = window.__ef.faults(true);
    // Offline offers no action until the connection returns (PERSIST-031): an Offline fault needs no next step.
    const offline = e => /\boffline\b/i.test(window.__ef.headingOf(e)) || /^offline$/i.test(window.__ef.recordState());
    // A next step is a visible, enabled control that takes pointer input and is not covered by another element.
    const onTop = c => {
      const r = c.getBoundingClientRect();
      c.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      const s = c.getBoundingClientRect();
      const t = document.elementFromPoint(Math.min(Math.max(s.left + s.width / 2, 0), window.innerWidth - 1), Math.min(Math.max(s.top + s.height / 2, 0), window.innerHeight - 1));
      return r.width > 0 && (!t || c.contains(t) || t.contains(c));
    };
    const usable = c => !window.__ef.hidden(c) && !c.disabled && c.getAttribute('aria-disabled') !== 'true' && !c.inert && getComputedStyle(c).pointerEvents !== 'none' && onTop(c);
    return faults.length === 0
      ? ['no fault on the page']
      : faults.flatMap(e => [
          ...(window.__ef.visibleText(e).length < 10 ? [`${e.className || 'alert'} does not say visibly what happened`] : []),
          ...(offline(e) || [...e.querySelectorAll('button, a[href]')].some(usable) ? [] : [`${e.className || 'alert'} offers no visible, enabled next step`]),
        ]);
  });
};

// A fault also says what was preserved (FL-08: "what happened, what was preserved and a safe next step").
export const faultStatesPreserved = async page => {
  await install(page);
  return inPage(page, () => {
    const faults = window.__ef.faults(true);
    // A wording check on the fault's own message: its text outside actions, diffs and code, split into sentences per
    // block. Some sentence says what was kept (PERSIST-031's copy: "exist only in this tab", "the tab's changes are
    // kept"), and none says the user's work was lost. "Queued" and "saved later" are not preservation (PERSIST-032).
    // The plan tests check the exact PERSIST-031 copy; this catches a fault that omits or contradicts it.
    const excluded = 'button, a, [role=group], [class*="actions"], [class*="diff"], pre, code, del, ins, table';
    const sentences = e => {
      const blocks = new Map();
      const walker = document.createTreeWalker(e, NodeFilter.SHOW_TEXT);
      for (let n = walker.nextNode(); n; n = walker.nextNode()) {
        const host = n.parentElement;
        if (host.closest(excluded) && e.contains(host.closest(excluded))) continue;
        let block = host;
        while (block !== e && ['inline', 'inline-block', 'contents'].includes(getComputedStyle(block).display)) block = block.parentElement;
        blocks.set(block, (blocks.get(block) || '') + n.textContent);
      }
      return [...blocks.values()].flatMap(t => t.split(/(?<=[.!?])\s+/)).map(t => t.trim()).filter(Boolean);
    };
    const preserved = /\b(kept|preserved|not lost|unchanged|no changes? (was|were) (applied|made|written)|nothing of this tab's changes? was written)\b|\b(exists?|remains?|stays?|held|still)\s+(only\s+)?(in|on)\s+this\s+(browser\s+)?tab\b/i;
    const notLost = /\b(not|never|won't be|will not be|nothing (is|was|has been))\s+lost\b/gi;
    // Loss is a past or present statement about this tab's own work: "your" (some, all, the last) changes, edits,
    // work, draft, attachments or notes. A lost connection, a discarded commit, another writer's change and a consequence
    // stated as a condition ("if you choose Discard", "when it closes", "will") are not lost work.
    const work = '(changes?|work|edits?|drafts?|attachments?|notes)';
    const own = "(your|the tab's|this tab's)";
    const subject = "(your|the tab's|this tab's|some|all|these|the last|some of (your|the|these))";
    const gone = '(lost|discarded|erased|wiped|gone|dropped|reverted|reset|deleted|removed|overwritten|thrown away)';
    const lost = [
      new RegExp(`\\b${subject}\\s+(\\w+\\s+){0,3}?${work}\\b[^.!?]{0,40}?\\b(was|were|is|are|have been|has been|got)\\s+${gone}\\b`, 'i'),
      new RegExp(`\\b(lost|discarded|erased|wiped|dropped|reverted|deleted|removed|overwrote|threw away)\\s+${own}\\s+(\\w+\\s+)?${work}\\b`, 'i'),
      /\b(kept|preserved)\s+(nothing|none)\b/i,
      /\bnothing\s+(was|is|has been|were|could be)\s+(kept|preserved)\b/i,
      /\b(not|never|no longer)\s+(been\s+)?(kept|preserved)\b/i,
      /\b(could not|couldn't|cannot|can't|unable to|failed to|did not|didn't)\s+(keep|preserve)\b/i,
    ];
    // PERSIST-032: never "saved locally", "on this device", "queued" or "saved later".
    const forbidden = /\b(queued|(saved|stored|kept) locally|(saved|stored|kept) on (this|your) device|(saved|synced|sync|uploaded) later|will (be )?(saved|synced|sync|uploaded|upload)|sync(s|ed)? (automatically|when))\b/i;
    const conditional = /\b(if|unless|would|will|once|whenever)\b|\bwhen\s+(it|the tab|this tab|you|the browser)\b/i;
    const otherWriter = /\b(another|the other|a different)\s+(writer|person|user|session|tab)\b/i;
    const says = t => !conditional.test(t) && !otherWriter.test(t) && lost.some(r => r.test(t.replace(notLost, '')));
    const states = e => sentences(e).some(t => preserved.test(t) && !says(t) && !forbidden.test(t));
    const denies = e => sentences(e).some(says);
    const claims = e => sentences(e).some(t => forbidden.test(t));
    return faults.length === 0
      ? ['no fault on the page']
      : faults.flatMap(e => [
          ...(states(e) ? [] : [`${e.className || 'alert'} does not say what was preserved`]),
          ...(denies(e) ? [`${e.className || 'alert'} says work was lost`] : []),
          ...(claims(e) ? [`${e.className || 'alert'} uses a persistence claim PERSIST-032 forbids (queued, saved or kept locally or on the device, saved or synced later)`] : []),
        ]);
  });
};

// ---- FL-09 navigation ----------------------------------------------------------------------------------------------

// Navigation is reachable by keyboard: a visible labelled nav, or a visible disclosure that opens one; at most one
// current page per nav.
export const navigationReachable = async (page, { maxTabs = 15 } = {}) => {
  const state = await inPage(page, () => {
    const visible = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden' && r.right > 0 && r.bottom > 0 && r.left < window.innerWidth && r.top < window.innerHeight && !e.closest('[aria-hidden=true], [inert]'); };
    const navs = [...document.querySelectorAll('nav')].filter(n => !n.closest('[aria-hidden=true]'));
    const label = n => (n.getAttribute('aria-label') || '').trim() !== '' || (n.getAttribute('aria-labelledby') || '').split(/\s+/).some(id => { const l = id && document.getElementById(id); return l && l.textContent.trim() !== ''; });
    return {
      navs: navs.length,
      hiddenNavs: document.querySelectorAll('nav[aria-hidden=true], [aria-hidden=true] nav').length,
      unlabelled: navs.filter(n => !label(n)).length,
      visible: navs.filter(visible).length,
      disclosure: navs.some(n => n.closest('details') && visible(n.closest('details').querySelector('summary'))),
      currents: navs.map(n => n.querySelectorAll('[aria-current=page]').length).filter(c => c > 1).length,
      empty: navs.filter(n => n.querySelectorAll('a[href], button').length === 0).length,
    };
  });
  if (state.navs === 0) return [state.hiddenNavs > 0 ? 'the navigation is hidden from assistive technology (aria-hidden)' : 'no navigation landmark'];
  const reach = async n => {
    if (n === 0) return false;
    await page.keyboard.press('Tab');
    const hit = await inPage(page, () => {
      const e = document.activeElement;
      return !!e && (e.closest('nav') !== null || (e.tagName === 'SUMMARY' && e.parentElement.querySelector('nav') !== null));
    });
    return hit || reach(n - 1);
  };
  const reachable = (state.visible > 0 || state.disclosure) && (await reach(maxTabs));
  return [
    ...(state.unlabelled > 0 ? [`${state.unlabelled} nav landmark(s) without a label`] : []),
    ...(state.currents > 0 ? ['a nav marks more than one current page'] : []),
    ...(state.empty > 0 ? [`${state.empty} nav landmark(s) offer no link or button`] : []),
    ...(reachable ? [] : ['navigation is not reachable by keyboard at this viewport']),
  ];
};

// At 48rem and narrower the navigation collapses through Forma's own pattern (the compact disclosure shows and the persistent
// list hides); at 48rem and wider the persistent list shows.
export const navigationCollapses = page =>
  inPage(page, () => {
    // Every navigation shell is Forma's (FL-09 names a workspace level and an investigation level); each collapses.
    const shells = [...document.querySelectorAll('.ef-navigation-shell')];
    const shell = shells[0];
    const inShell = e => shells.some(s => s.contains(e));
    const showsIn = (sh, sel) => { const e = sh && [...sh.querySelectorAll(sel)].find(x => x.closest('.ef-navigation-shell') === sh); if (!e) return false; const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).display !== 'none'; };
    // The same media query Forma's stylesheet uses, so the browser decides exactly as it applies the pattern.
    const narrow = window.matchMedia('(max-width: 48rem)').matches;
    // Never a custom menu: no visible navigation or menu toggle outside the Forma shell, except the navigation that
    // other Forma patterns carry (site header, workspace shell, landmark region, breadcrumb, hierarchy tree, sidebar,
    // pagination, scope trail and the rest), which the contract's reuse list composes with the shell.
    const formaNavigation = ['ef-site-header', 'ef-site-nav', 'ef-site-footer', 'ef-marketing-shell', 'ef-workspace-shell', 'ef-landmark-region', 'ef-record-header', 'ef-hierarchy-tree', 'ef-doc', 'ef-master-detail', 'ef-mobile-action-bar', 'ef-pagination', 'ef-scope-trail', 'ef-sidebar', 'ef-spatial-canvas'].map(c => `[class*="${c}"]`).concat('[aria-label=Breadcrumb]').join(', ');
    const visible = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden' && getComputedStyle(e).display !== 'none'; };
    const custom = [...document.querySelectorAll('nav, button[aria-expanded], [role=button][aria-expanded], [aria-haspopup=menu], [role=menu], [role=menubar], details:has(a[href] ~ a[href], li a[href])')].filter(e => !inShell(e) && visible(e) && !e.closest(formaNavigation));
    const customs = custom.map(e => `a custom ${e.tagName.toLowerCase() === 'nav' ? 'navigation' : 'menu control'} sits outside the Forma navigation shell`);
    if (customs.length) return customs;
    if (!shell) return ['no Forma navigation shell on the page'];
    return shells.flatMap((sh, k) => {
      const c = showsIn(sh, '.ef-navigation-shell__compact');
      const p = showsIn(sh, '.ef-navigation-shell__persistent');
      const which = shells.length > 1 ? `navigation shell ${k + 1}: ` : '';
      return narrow
        ? [...(c ? [] : [`${which}at 48rem or narrower the compact navigation is not shown`]), ...(p ? [`${which}at 48rem or narrower the persistent navigation still shows`] : [])]
        : [...(p ? [] : [`${which}wider than 48rem the persistent navigation is not shown`]), ...(c ? [`${which}wider than 48rem the compact navigation still shows`] : [])];
    });
  });

// ---- FL-10 tokens -------------------------------------------------------------------------------------------------

// No stylesheet other than Forma's, and no style attribute, declares an --ef-* property or uses one Forma does not
// define. Reads document.styleSheets (linked and inline) and every style attribute.
// formaHref names Forma's own stylesheet; by default the published package's dist/all.css. A linked stylesheet the
// page cannot read (file: URLs, other origins) is fetched from Node; one that cannot be read at all is a violation.
export const onlyFormaTokens = async (page, { inventory, hooks = [], formaHref = '/dist/all.css' }) => {
  const { readable, unreadable } = await inPage(page, forma => {
    const others = [...document.styleSheets, ...(document.adoptedStyleSheets || [])].filter(s => !(s.href && s.href.endsWith(forma)));
    const text = sheet => { try { return [...sheet.cssRules].map(r => r.cssText).join('\n'); } catch (e) { return null; } };
    return {
      readable: [...others.map(text).filter(t => t !== null), ...[...document.querySelectorAll('[style]')].map(e => e.getAttribute('style'))].join('\n'),
      unreadable: others.filter(s => text(s) === null).map(s => s.href),
    };
  }, formaHref);
  const fetched = await Promise.all(unreadable.map(async href => {
    try {
      if (href.startsWith('file:')) { const { readFileSync } = await import('node:fs'); const { fileURLToPath } = await import('node:url'); return { href, text: readFileSync(fileURLToPath(href), 'utf8') }; }
      const r = await page.request.get(href);
      return { href, text: r.ok() ? await r.text() : null };
    } catch (e) { return { href, text: null }; }
  }));
  const text = [readable, ...fetched.map(f => f.text || '')].join('\n');
  const declared = [...text.matchAll(/(--ef-[a-z0-9-]+)\s*:/g), ...text.matchAll(/@property\s+(--ef-[a-z0-9-]+)/g)].map(m => m[1]);
  const used = [...text.matchAll(/var\(\s*(--ef-[a-z0-9-]+)/g)].map(m => m[1]);
  return [
    ...fetched.filter(f => f.text === null).map(f => `stylesheet ${f.href} cannot be read`),
    // Setting one of Forma's hooks (a property its stylesheet reads but never defines) is using Forma's API.
    ...declared.filter(p => !hooks.includes(p)).map(p => `declares ${p}; only Forma defines --ef-* properties`),
    ...used.filter(p => !inventory.includes(p) && !hooks.includes(p)).map(p => `uses ${p}, which Forma does not define`),
  ];
};

// ---- FL-06e automated WCAG checks ---------------------------------------------------------------------------

// axe-core's WCAG 2.0-2.2 A and AA rules report no violation. The axe source comes from the caller (axeSource), or
// from the axe-core package installed beside this module (the bootstrap writes axe-core 4.14.0 into the event build's
// package.json, as the owner locked it in ODR-V41-09).
const loadAxe = async () => {
  const { createRequire } = await import('node:module');
  const { readFileSync } = await import('node:fs');
  return readFileSync(createRequire(import.meta.url).resolve('axe-core/axe.min.js'), 'utf8');
};

export const axeClean = async (page, { axeSource } = {}) => {
  await settle(page);
  if (!(await inPage(page, () => typeof window.axe !== 'undefined'))) await page.addScriptTag({ content: axeSource || (await loadAxe()) });
  return inPage(page, async () => {
    const r = await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] } });
    return r.violations.flatMap(v => v.nodes.slice(0, 3).map(n => `${v.id}: ${n.target.join(' ')}`));
  });
};

export const assertions = {
  touchTargets, noHorizontalScroll, scrollRegionsAccessible, textNotClipped, lineLength, focusVisible, focusOrder,
  skipLinkFirst, dialogFocus, reducedMotion, textContrast, nonTextContrast, forcedColorsDistinct, amberLabelled,
  statusVisible, indicatorComposed, loadingAnnounced, faultActionable, faultStatesPreserved, navigationReachable,
  navigationCollapses, onlyFormaTokens, axeClean,
};

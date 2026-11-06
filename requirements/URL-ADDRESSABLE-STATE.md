# URL-addressable state (deep linking)

Status: **Required** for this application. The canonical text is
[Praxis `requirements/SHARED-APPLICATION-FOUNDATIONS.md`](https://github.com/kemiller2002/praxis/blob/main/requirements/SHARED-APPLICATION-FOUNDATIONS.md)
("URL-addressable state (deep linking)", SAF-URL-1..10). Limen 0.9.0
implements it (`@echelon-foundry/limen/routing`, `EchelonFoundry.Limen.Routing`;
LCP-088..112, DF-LIMEN-2026-0006).

Navigable state means the screen or route, entity IDs, and view parameters
such as the selected date or period, filters, sort, search, tab and page. It
lives in the URL, so a copied URL opens the same view in another browser.

## Checklist

- [ ] **SAF-URL-1** The URL is the source of truth. A cold load of a copied
      URL reproduces the view, and sign-in returns to the target.
- [ ] **SAF-URL-2** IDs are path parameters and view parameters are query
      parameters, typed (`string`, `int`, `bool`, `date`, `month`, `enum`,
      `set`). The canonical form has declared parameters only, in
      declaration order, with defaults omitted, sets sorted and
      de-duplicated, and `%20` with upper-case hex.
- [ ] **SAF-URL-3** Navigation pushes history and refinement replaces it.
      Back, Forward and reload restore the view from the URL alone.
- [ ] **SAF-URL-4** An unknown, deleted or forbidden ID shows not found or
      not permitted, with a way back. The page is never blank and never
      shows another record.
- [ ] **SAF-URL-5** No secrets, tokens or sensitive personal or financial
      data in any URL. Limen's reserved parameter names are refused. No
      transient UI state goes in the URL.
- [ ] **SAF-URL-6** Hash routes (`index.html#/...`), relative links and no
      `<base href>`, so a reloaded deep link never 404s on GitHub Pages.
- [ ] **SAF-URL-7** Renamed routes keep their old URLs as legacy redirects.
- [ ] **SAF-URL-8** `.echelon/routes.json` (`echelon.routes/v1`) lists every
      addressable view and stays current with the route table.
- [ ] **SAF-URL-9** One pure parse/format codec with round-trip property
      tests (`src/engine/Routes.fs` until Limen 0.9.0, then `Limen.Routing`).
- [ ] **SAF-URL-10** A Copy link action wherever sharing is natural.

## Verification

`praxis foundations verify` (the `routing` capability in
`.echelon/foundations.json`) checks the inventory on every pull request.
Until the application can install Limen 0.9.0, it reports Limen routing as
pending (`ECHELON-FND-ROUTING-005`), which is not a failure.

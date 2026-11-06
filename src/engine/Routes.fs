namespace IndyInitRootCauseInvestigator.Engine

open System

/// The application's navigable views (SAF-URL-1..10). The URL is the source
/// of truth: the engine derives the view from `Routes.parse` and writes the
/// URL with `Routes.format`. Routes live in the hash (`index.html#/...`), so a
/// reloaded or pasted deep link never 404s on GitHub Pages
/// (DF-LIMEN-2026-0006).
///
/// PLACEHOLDER until the project declares limen-fsharp 0.9.0 or later in
/// conditor.json (Conditor then scaffolds this module on `Limen.Routing`). To
/// switch by hand, replace it with `Limen.Routing` (package EchelonFoundry.Limen.Routing:
/// RouteTable.define, RouteCodec.create/parse/format, Navigation.adopt,
/// navigate and refine, ReturnTo.capture/resume, Link.share), and regenerate
/// `.echelon/routes.json` with `Inventory.render`. Keep the inventory in step
/// with this table meanwhile.
type Route =
    | Home
    | NotFound of path: string

[<RequireQualifiedAccess>]
module Routes =
    let private pathOf (location: string) =
        let fragment = if location.StartsWith("#", StringComparison.Ordinal) then location.Substring 1 else location

        match fragment.IndexOf '?' with
        | -1 -> fragment
        | index -> fragment.Substring(0, index)

    /// Pure and total: a location hash (with or without the leading '#') to
    /// its route. Unknown paths are a value, never an exception.
    let parse (location: string) : Route =
        match pathOf location with
        | ""
        | "/" -> Home
        | path when path.StartsWith("/", StringComparison.Ordinal) -> NotFound path
        | path -> NotFound("/" + path)

    /// Pure: the canonical location hash of a route.
    let format (route: Route) : string =
        match route with
        | Home -> "#/"
        | NotFound path -> "#" + path

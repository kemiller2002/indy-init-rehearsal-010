// Owner: integration. Hash routes (the static host has no fallback, so every route is a fragment).
namespace Causa.App

open System

/// The hash grammar every route shares: `#/segment/segment...`, each segment URI-encoded. Typed routes (UiRoute) are
/// parsed in Ui/Presentation.fs (UiRoutes), which compiles after IF41-VIEW.
[<RequireQualifiedAccess>]
module Routes =
    /// The canonical form of a location hash: "", "#" and "#/" are all the root route "#/".
    let canonical (hash: string) : string =
        let h = hash.Trim()
        let body = if h.StartsWith "#" then h.Substring 1 else h
        let body = if body.StartsWith "/" then body else "/" + body
        "#" + body

    /// The decoded, non-empty path segments of a hash: "#/investigation/a%2Fb/1/overview" -> [investigation; a/b; 1; overview].
    let segments (hash: string) : string list =
        let body = (canonical hash).Substring 1
        let path = match body.IndexOf '?' with | -1 -> body | i -> body.Substring(0, i)
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
        |> Array.map Uri.UnescapeDataString
        |> List.ofArray

    /// A hash built from segments, each URI-encoded (so a workspace like workspaces/demo/r10 stays one segment).
    let ofSegments (segments: string list) : string =
        "#/" + String.Join("/", segments |> List.map Uri.EscapeDataString)

    let route (hash: string) : Route = Route(canonical hash)

    let hashOf (Route hash) = hash

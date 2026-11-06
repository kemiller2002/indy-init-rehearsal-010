// Owner: ui. The application shell's own bindings (UI-014, UI-020, UI-029): the connection summary in the masthead,
// the current-investigation context and its section navigation, and the session preferences the shell applies.
// The route keys (screen.*, nav.*, app.*) are integration's (View.fs); every key here starts with "shell.".
namespace Causa.App.Views

open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Shell =
    /// The session preferences Settings offers, by event name (their drafts hold the choice).
    [<Literal>]
    let MotionPreference = "settings.motion"

    [<Literal>]
    let TextSizePreference = "settings.text-size"

    /// A preference's effective value: the user's choice in this tab, else its default.
    let preference (state: AppState) (name: string) (defaultValue: string) (allowed: string list) =
        match Drafts.get state name with
        | v when List.contains v allowed -> v
        | _ -> defaultValue

    let motion state = preference state MotionPreference "system" [ "system"; "reduce" ]
    let textSize state = preference state TextSizePreference "default" [ "default"; "large" ]

    /// The connected repository as people name it ("owner/repository on branch"), when connected.
    let repository (model: Model) =
        model.Context |> Option.map (fun c -> sprintf "%s/%s on %s" c.Repository.Owner c.Repository.Name c.Repository.Branch)

    /// The masthead's connection summary: honest about not being connected (UI-022: no commit hash here).
    let connection (model: Model) =
        match model.Connection, repository model with
        | Connected _, Some repo -> "Connected to " + repo
        | Connecting _, _ -> "Connecting…"
        | CredentialRefused, _ -> "Token refused"
        | Reauthenticating, _ -> "Token needs re-entering"
        | _ -> "Not connected"

    /// What kind of copy the current investigation is (UI-014-O3): a catalog is a frozen, read-only demonstration;
    /// only a working copy is writable (EXT-DATA-013, EXT-DATA-006).
    let kind (role: WorkspaceRole) =
        match role with
        | WorkspaceRole.Catalog -> "Frozen demonstration (read-only)", "frozen"
        | WorkspaceRole.WorkingCopy -> "Working copy (writable)", "ok"

    /// The sections an investigation offers (UI-029-O2). Corrective Actions only where supported: no backend in this
    /// build supports it yet, so it is not offered.
    let sections = [ Overview; CausalAnalysis; EvidenceSection; HypothesesSection; TimelineSection ]

    let view (state: AppState) : (string * ViewValue) list =
        let target, current =
            match state.Route with
            | WorkspaceRoute(target, section) -> Some target, Some section
            | _ -> None, None
        let kindText, kindState = target |> Option.map (fun t -> kind t.Role) |> Option.defaultValue ("", "unknown")
        [ Bind.text "shell.connection" (connection state.Model)
          Bind.text "shell.motion" (motion state)
          Bind.text "shell.text-size" (textSize state)
          Bind.flag "shell.investigation" target.IsSome
          Bind.text "shell.investigation.kind" kindText
          Bind.text "shell.investigation.kind-state" kindState
          Bind.text "shell.investigation.workspace" (target |> Option.map (fun t -> t.Workspace) |> Option.defaultValue "")
          Bind.text "shell.investigation.id" (target |> Option.map (fun t -> let (Causa.Domain.InvestigationId g) = t.Id in g.ToString("D")) |> Option.defaultValue "")
          Bind.items
              "shell.sections"
              [ match target with
                | Some t ->
                    for s in sections do
                        [ Cell.text "key" (UiRoutes.sectionSlug s)
                          Cell.text "label" (UiRoutes.sectionLabel s)
                          Cell.text "href" (UiRoutes.href (WorkspaceRoute(t, s)))
                          Cell.text "current" (if current = Some s then "page" else "false") ]
                | None -> () ] ]

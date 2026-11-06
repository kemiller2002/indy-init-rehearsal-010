// Owner: integration. The whole Limen ViewState: the shell's keys (integration) followed by every screen's keys
// (ui, ui-2, ui-3: Views/*.fs, each `view : AppState -> (string * ViewValue) list`).
namespace Causa.App

open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App.Ui

[<RequireQualifiedAccess>]
module View =
    /// Key prefixes only this file writes. A screen's keys start with its own screen name instead.
    let reservedPrefixes = [ "app."; "nav."; "screen."; "route."; "announce." ]

    /// The shell: which screen and section are shown, the primary navigation, the route, the title, the network state
    /// and the live regions. Every key is always present, so any fragment may bind it.
    let shell (state: AppState) : (string * ViewValue) list =
        let current = UiRoutes.screenName state.Route
        let workspace, id, section =
            match state.Route with
            | WorkspaceRoute(target, s) ->
                let (Causa.Domain.InvestigationId guid) = target.Id
                target.Workspace, guid.ToString("D"), Some s
            | _ -> "", "", None
        let announced politeness =
            state.Announcements |> List.filter (fst >> (=) politeness) |> List.map snd |> String.concat " "
        [ Bind.flag "app.started" true
          Bind.text "app.title" (UiAdapter.titleAt state.Route (UiRoutes.isReport (Routes.hashOf state.Model.Route)))
          Bind.text "app.route" (UiRoutes.href state.Route)
          Bind.flag "app.offline" (state.Model.Network = NetworkState.Offline)
          // PERSIST-019-O1: the leave warning is wanted while any investigation of the tab is not Saved (the boundary
          // registers beforeunload only then).
          Bind.text "app.leave-guard" (
              if state.Model.Sessions |> Map.exists (fun _ s -> match s.State with SaveState.Saved _ -> false | _ -> true) then "on" else "off")
          for name in UiRoutes.screenNames do
              Bind.flag ("screen." + name) (name = current)
          let report = UiRoutes.isReport (Routes.hashOf state.Model.Route)
          for s in UiRoutes.allSections do
              Bind.flag ("screen.investigation." + UiRoutes.sectionSlug s) (section = Some s && not report)
          Bind.flag "screen.investigation.report" (section.IsSome && report)
          for link in UiAdapter.primary state.Route do
              Bind.current ("nav." + UiAdapter.destinationSlug link.Destination + ".current") link.Current
          Bind.text "route.workspace" workspace
          Bind.text "route.id" id
          Bind.text "route.section" (if report && section.IsSome then "report" else section |> Option.map UiRoutes.sectionSlug |> Option.defaultValue "")
          Bind.text "announce.polite" (announced Polite)
          Bind.text "announce.assertive" (announced Assertive) ]

    /// The screens' views, in navigation order. Each screen slice adds its module's `view` here when it lands
    /// (e.g. Causa.App.Views.InvestigationList.view).
    let screens: (AppState -> (string * ViewValue) list) list =
        [ Causa.App.Views.Shell.view
          Causa.App.Views.InvestigationList.view
          Causa.App.Views.Connection.view
          Causa.App.Views.Start.view
          Causa.App.Views.Frame.view
          Causa.App.Views.Details.view
          Causa.App.Views.SaveIndicator.view
          Causa.App.Views.ConflictReview.view
          Causa.App.Views.Workspace.Overview.view
          Causa.App.Views.Workspace.Hypotheses.view
          Causa.App.Views.Workspace.Evidence.view
          Causa.App.Views.Workspace.Links.view
          Causa.App.Views.Workspace.Conclusions.view
          Causa.App.Views.Workspace.Timeline.view
          Causa.App.Views.Graph.view
          Causa.App.Views.Report.view
          Causa.App.Views.Administration.view
          Causa.App.Views.Settings.view
          Causa.App.Views.NotFound.view ]

    /// The complete view: shell first; a later duplicate of a key is dropped (the first writer wins).
    let render (state: AppState) : (string * ViewValue) list =
        let all = shell state @ (screens |> List.collect (fun screen -> screen state))
        all |> List.distinctBy fst

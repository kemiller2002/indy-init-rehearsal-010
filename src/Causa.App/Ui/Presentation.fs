// Owner: integration. The application state screens read, the flat view-value representation Limen renders, the
// helpers screens build bindings with, and the typed hash routes.
namespace Causa.App.Ui

open System
open Causa.Domain
open Causa.Persistence
open Causa.App
open Causa.Ui.Contracts

/// A Limen view value (limen.core ViewValue): a string, number or boolean, or a list of flat rows. Alias of the frozen
/// IF41-VIEW BindingValue, so a screen's output is exactly the Bindings shape the contract tests read.
type ViewValue = BindingValue

/// One field of a flat row (limen.core ViewPrimitive). Rows never nest.
type ViewPrimitive = BindingPrimitive

/// What the integration engine holds: the frozen IF41-APP Model plus UI-only state. Screens read it; they never write
/// it (they emit events, which Ui/Events.fs maps to messages).
type AppState =
    { /// The frozen IF41-APP model (connection, network, sessions, listings). Model.Route is the canonical hash.
      Model: Model
      /// The typed route the hash names (parsed once, in Update).
      Route: UiRoute
      /// Text the user has typed and not yet submitted, keyed by event name (and row key: "<name>|<key>").
      Drafts: Map<string, string>
      /// The credential the user entered, opaque (IF41-WIRE Credential.T). Never a draft, never rendered or logged.
      Credential: Credential.T option
      /// Live-region announcements of the last update (frozen Politeness).
      Announcements: (Politeness * string) list
      /// The build record (dist/build-info.json) as this page read it from its own origin: not read yet, its text, or
      /// why it could not be read (UI-018). Never part of the GitHub machines.
      Build: Result<string, string> option }

/// Builds one binding: `Bind.text "investigations.heading" "Investigations"`. A screen's whole output is a list of
/// these (`view : AppState -> (string * ViewValue) list`).
[<RequireQualifiedAccess>]
module Bind =
    let text (key: string) (value: string) : string * ViewValue = key, BindingValue.Text value
    let flag (key: string) (value: bool) : string * ViewValue = key, BindingValue.Flag value
    let number (key: string) (value: float) : string * ViewValue = key, BindingValue.Number value
    let int (key: string) (value: int) : string * ViewValue = key, BindingValue.Number(float value)
    /// A data-each list: each row is a list of cells (Cell.text/flag/number); every row needs its data-key field.
    let items (key: string) (rows: (string * ViewPrimitive) list list) : string * ViewValue =
        key, BindingValue.Items(rows |> List.map Map.ofList)
    /// aria-current for a link: "page" when current, "false" otherwise (J2-08, J3-01).
    let current (key: string) (isCurrent: bool) : string * ViewValue = text key (if isCurrent then "page" else "false")

/// One field of a data-each row.
[<RequireQualifiedAccess>]
module Cell =
    let text (field: string) (value: string) : string * ViewPrimitive = field, BindingPrimitive.Text value
    let flag (field: string) (value: bool) : string * ViewPrimitive = field, BindingPrimitive.Flag value
    let number (field: string) (value: float) : string * ViewPrimitive = field, BindingPrimitive.Number value
    let int (field: string) (value: int) : string * ViewPrimitive = field, BindingPrimitive.Number(float value)

/// Draft text by event name, for data-bind-value. Absent means "".
[<RequireQualifiedAccess>]
module Drafts =
    let keyOf (name: string) (rowKey: string option) =
        match rowKey with
        | Some k -> name + "|" + k
        | None -> name

    let get (state: AppState) (name: string) : string =
        state.Drafts |> Map.tryFind name |> Option.defaultValue ""

    let getRow (state: AppState) (name: string) (rowKey: string) : string =
        state.Drafts |> Map.tryFind (keyOf name (Some rowKey)) |> Option.defaultValue ""

/// The typed hash routes (SCREEN-CONTRACTS URL contract, D-203; the integration contract's hash grammar):
/// `#/` and `#/investigations` (list), `#/new` (start), `#/investigation/<workspace>/<id>/<section>`,
/// `#/administration`, `#/settings`; anything else is NotFoundRoute.
[<RequireQualifiedAccess>]
module UiRoutes =
    let private sections =
        [ Overview, "overview"
          CausalAnalysis, "causal-analysis"
          EvidenceSection, "evidence"
          HypothesesSection, "hypotheses"
          TimelineSection, "timeline"
          CorrectiveActions, "corrective-actions" ]

    let sectionSlug (s: Section) = sections |> List.find (fst >> (=) s) |> snd

    let sectionOfSlug (slug: string) = sections |> List.tryFind (snd >> (=) slug) |> Option.map fst

    /// The section labels the investigation-section navigation shows (UI-029; design contract investigationSections).
    let sectionLabel (s: Section) =
        match s with
        | Overview -> "Overview"
        | CausalAnalysis -> "Causal Analysis"
        | EvidenceSection -> "Evidence"
        | HypothesesSection -> "Hypotheses"
        | TimelineSection -> "Timeline"
        | CorrectiveActions -> "Corrective Actions"

    /// A working copy is the reserved `workspaces/demo/causa-app-<run>` namespace (WorkspaceAddress.workingCopy); every
    /// other workspace a route names is read as a catalog. The target stays unresolved until the listings resolve it.
    let roleOf (workspace: string) =
        if workspace.StartsWith("workspaces/demo/causa-app-", StringComparison.Ordinal) then WorkspaceRole.WorkingCopy
        else WorkspaceRole.Catalog

    let parse (hash: string) : UiRoute =
        let canonical = Routes.canonical hash
        match Routes.segments hash with
        | []
        | [ "investigations" ] -> InvestigationsRoute
        | [ "new" ] -> StartRoute
        | [ "administration" ] -> AdministrationRoute
        | [ "settings" ] -> SettingsRoute
        | "investigation" :: workspace :: id :: rest when workspace <> "" ->
            let section =
                match rest with
                | [] -> Some Overview
                // The report is an investigation page outside the frozen Section set: its route stands on the
                // investigation (Overview) and View.shell shows the report instead (isReport).
                | [ "report" ] -> Some Overview
                | [ slug ] -> sectionOfSlug slug
                | _ -> None
            match Guid.TryParse id, section with
            | (true, guid), Some s -> WorkspaceRoute({ Workspace = workspace; Role = roleOf workspace; Id = InvestigationId guid }, s)
            | _ -> NotFoundRoute canonical
        | _ -> NotFoundRoute canonical

    let href (route: UiRoute) : string =
        match route with
        | InvestigationsRoute -> "#/investigations"
        | StartRoute -> "#/new"
        | AdministrationRoute -> "#/administration"
        | SettingsRoute -> "#/settings"
        | WorkspaceRoute(target, section) ->
            let (InvestigationId id) = target.Id
            Routes.ofSegments [ "investigation"; target.Workspace; id.ToString("D"); sectionSlug section ]
        | NotFoundRoute requested -> Routes.canonical requested

    /// The screen a route shows, as its view-key name: the fragment for it is `<template data-if="screen.<name>">`.
    let screenName (route: UiRoute) : string =
        match route with
        | InvestigationsRoute -> "investigations"
        | StartRoute -> "new"
        | WorkspaceRoute _ -> "investigation"
        | AdministrationRoute -> "administration"
        | SettingsRoute -> "settings"
        | NotFoundRoute _ -> "not-found"

    /// Whether a hash is an investigation's report page (`#/investigation/<workspace>/<id>/report`).
    let isReport (hash: string) : bool =
        match Routes.segments hash with
        | [ "investigation"; _; _; "report" ] -> true
        | _ -> false

    /// The report page of an investigation.
    let reportHref (target: RouteTarget) : string =
        let (InvestigationId id) = target.Id
        Routes.ofSegments [ "investigation"; target.Workspace; id.ToString("D"); "report" ]

    /// Every screen name, in navigation order.
    let screenNames = [ "investigations"; "new"; "investigation"; "administration"; "settings"; "not-found" ]

    let allSections = sections |> List.map fst

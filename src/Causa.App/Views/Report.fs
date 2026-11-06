// Owner: ui-3. The investigation report (UI-009, J2-10): the IF41-REPORT projection (Causa.App.Workspace.Report) as
// a read-only article with a print presentation. It renders only what the report producer projects, so it cannot
// present an unverified hypothesis as a confirmed cause.
namespace Causa.App.Views

open Causa.Domain
open Causa.App
open Causa.App.Ui
open Causa.App.Workspace
open Causa.App.Views.Workspace

[<RequireQualifiedAccess>]
module Report =
    let private p = "investigation.report."

    let keys (investigation: Investigation option) : (string * ViewValue) list =
        let report = investigation |> Option.map (Kernel.domain.Project >> Causa.App.Workspace.Report.reporting.Project)
        let sections = report |> Option.map (fun r -> r.Sections) |> Option.defaultValue []
        let uncertainties = report |> Option.map (fun r -> r.UnresolvedUncertainties) |> Option.defaultValue []
        [ Bind.flag (p + "loaded") investigation.IsSome
          Bind.flag (p + "missing") investigation.IsNone
          Bind.text (p + "notLoaded") SectionBindings.notLoaded
          Bind.text (p + "title") (investigation |> Option.map (fun s -> s.Title.Value) |> SectionBindings.optional)
          Bind.text (p + "id") (investigation |> Option.map (fun s -> Ids.text (Ids.investigation s.Id)) |> SectionBindings.optional)
          Bind.text (p + "state") (investigation |> Option.map (fun s -> Words.lifecycle s.Lifecycle) |> SectionBindings.optional)
          Bind.text (p + "version") (report |> Option.map (fun r -> string (AggregateVersion.value r.Version)) |> SectionBindings.optional)
          Bind.text (p + "asOf") (investigation |> Option.map (fun s -> Words.instant s.UpdatedAt) |> SectionBindings.optional)
          Bind.items
              (p + "sections")
              [ for section in sections ->
                    [ Cell.text "id" section.Heading
                      Cell.text "heading" section.Heading
                      Cell.flag "empty" section.Paragraphs.IsEmpty
                      Cell.text "body" (section.Paragraphs |> String.concat "\n")
                      Cell.int "sources" section.Provenance.Length ] ]
          Bind.flag (p + "uncertainties.empty") uncertainties.IsEmpty
          Bind.items (p + "uncertainties") [ for (i, u) in List.indexed uncertainties -> [ Cell.text "id" (string i); Cell.text "text" u ] ] ]

    let view (state: AppState) : (string * ViewValue) list = keys (Analysis.loaded state)

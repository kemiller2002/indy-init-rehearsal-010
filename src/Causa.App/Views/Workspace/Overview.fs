// Owner: ui-3. The Overview section of an opened investigation (screen.investigation.overview): its creation time
// (INV-007), original problem statement (INV-008), state (INV-018), and observations with the "Record an observation"
// form (INV-014, INV-024, INV-025). Everything shown is the kernel's projection; actions are offered only as the engine
// decides (UI-004).
namespace Causa.App.Views.Workspace

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

/// Bindings the analysis sections share.
[<RequireQualifiedAccess>]
module SectionBindings =
    /// An engine-decided action: `<key>.offered` (render it at all), `<key>.disabled` and `<key>.reason` (the engine's
    /// own words when it is unavailable).
    let action (key: string) (availability: Availability) : (string * ViewValue) list =
        [ Bind.flag (key + ".offered") (availability <> NotOffered)
          Bind.flag (key + ".disabled") (availability <> Available)
          Bind.text
              (key + ".reason")
              (match availability with
               | Unavailable reason -> reason
               | _ -> "") ]

    /// A draft field read back by its event name (data-bind-value).
    let draft (draft: string -> string) (name: string) : string * ViewValue = Bind.text name (draft name)

    let optional (value: string option) = value |> Option.defaultValue ""

    /// What a section says while no projection of this investigation is available (never "not supported", J2-08).
    let notLoaded = "This investigation's content is not loaded yet."

[<RequireQualifiedAccess>]
module Overview =
    let private p = "investigation.overview."

    /// The observation form's fields and action (event names; also the draft keys).
    let observationText = "investigation.observation.text"
    let observationSource = "investigation.observation.source"
    let recordObservation = "investigation.observation.record"

    /// The Overview bindings of an investigation (None: not loaded). Always the full key set.
    let keys (draft: string -> string) (investigation: Investigation option) : (string * ViewValue) list =
        let analysis = investigation |> Option.map (Analysis.project Analysis.actor)
        let problem = investigation |> Option.bind Analysis.problemStatement
        let observations = analysis |> Option.map (fun a -> a.Observations) |> Option.defaultValue []
        [ Bind.flag (p + "loaded") investigation.IsSome
          Bind.flag (p + "missing") investigation.IsNone
          Bind.text (p + "notLoaded") SectionBindings.notLoaded
          Bind.text (p + "title") (investigation |> Option.map (fun s -> s.Title.Value) |> SectionBindings.optional)
          Bind.text (p + "problem") (problem |> Option.map (fun o -> o.Value.Original.Value) |> SectionBindings.optional)
          Bind.text (p + "created") (investigation |> Option.map (fun s -> Words.instant s.CreatedAt) |> SectionBindings.optional)
          Bind.text (p + "state") (investigation |> Option.map (fun s -> Words.lifecycle s.Lifecycle) |> SectionBindings.optional)
          Bind.text (p + "version") (investigation |> Option.map (fun s -> string (AggregateVersion.value s.Version)) |> SectionBindings.optional)
          Bind.text (p + "hypothesisCount") (analysis |> Option.map (fun a -> string a.Hypotheses.Length) |> SectionBindings.optional)
          Bind.text (p + "evidenceCount") (analysis |> Option.map (fun a -> string a.Evidence.Length) |> SectionBindings.optional)
          Bind.flag (p + "observations.empty") observations.IsEmpty
          Bind.items
              (p + "observations")
              [ for o in observations ->
                    [ Cell.text "id" o.Id
                      Cell.text "text" o.Text
                      Cell.flag "hasSource" o.SourceReference.IsSome
                      Cell.text "source" (SectionBindings.optional o.SourceReference)
                      Cell.flag "hasObservedAt" o.ObservedAt.IsSome
                      Cell.text "observedAt" (o.ObservedAt |> Option.map Words.instant |> SectionBindings.optional) ] ]
          SectionBindings.draft draft observationText
          SectionBindings.draft draft observationSource ]
        @ SectionBindings.action
            recordObservation
            (analysis |> Option.map (fun a -> Analysis.action a "INV-014") |> Option.defaultValue NotOffered)

    let view (state: AppState) : (string * ViewValue) list = keys (Drafts.get state) (Analysis.loaded state)

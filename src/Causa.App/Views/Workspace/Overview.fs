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
    let notLoaded = SectionContext.notOpen.What

    /// Whether the section has content, and if not, why (UI-010: what happened, what was preserved, the next step),
    /// plus the investigation id and version every section states (UI-013).
    let status (prefix: string) (ctx: SectionContext) : (string * ViewValue) list =
        let s = ctx.Investigation
        [ Bind.flag (prefix + "loaded") s.IsSome
          Bind.flag (prefix + "missing") s.IsNone
          Bind.flag (prefix + "opening") (s.IsNone && ctx.Opening)
          Bind.text (prefix + "notLoaded") ctx.Unavailable.What
          Bind.text (prefix + "preserved") ctx.Unavailable.Preserved
          Bind.text (prefix + "next") ctx.Unavailable.NextStep
          Bind.text (prefix + "version") (s |> Option.map (fun s -> $"Version {AggregateVersion.value s.Version}") |> optional) ]

    /// A row's link to its record in this investigation (UI-033) and whether it is the selected one (UI-034).
    let recordCells (ctx: SectionContext) (r: EntityRef) : (string * ViewPrimitive) list =
        [ Cell.text "href" (RecordLinks.href ctx r); Cell.flag "selected" (ctx.Selected = Some r.Id) ]

[<RequireQualifiedAccess>]
module Overview =
    let private p = "investigation.overview."

    /// The observation form's fields and action (event names; also the draft keys).
    let observationText = "investigation.observation.text"
    let observationSource = "investigation.observation.source"
    let recordObservation = "investigation.observation.record"

    let private lines (key: string) (xs: string list) =
        [ Bind.flag (key + ".empty") xs.IsEmpty
          Bind.items key [ for i, x in List.indexed xs -> [ Cell.text "id" (string i); Cell.text "text" x ] ] ]

    /// The Overview bindings (UI-025): what happened, supported findings, evidence for and against, open questions,
    /// next steps (UI-032) and authorized actions, observations, and links to the other sections. Always every key.
    let render (draft: string -> string) (ctx: SectionContext) : (string * ViewValue) list =
        let investigation = ctx.Investigation
        let analysis = investigation |> Option.map (Analysis.project Analysis.actor)
        let problem = investigation |> Option.bind Analysis.problemStatement
        let observations = analysis |> Option.map (fun a -> a.Observations) |> Option.defaultValue []
        let current = analysis |> Option.map (fun a -> a.Relationships |> List.filter (fun l -> l.Current)) |> Option.defaultValue []
        let count kind = current |> List.filter (fun l -> l.Kind = CatalogKind kind) |> List.length
        let steps = investigation |> Option.map Summary.nextSteps |> Option.defaultValue []
        SectionBindings.status p ctx
        @ [ Bind.text (p + "title") (investigation |> Option.map (fun s -> s.Title.Value) |> SectionBindings.optional)
            Bind.text (p + "problem") (problem |> Option.map (fun o -> o.Value.Original.Value) |> SectionBindings.optional)
            Bind.text (p + "created") (investigation |> Option.map (fun s -> Words.instant s.CreatedAt) |> SectionBindings.optional)
            Bind.text (p + "updated") (investigation |> Option.map (fun s -> Words.instant s.UpdatedAt) |> SectionBindings.optional)
            Bind.text (p + "state") (investigation |> Option.map (fun s -> Words.lifecycle s.Lifecycle) |> SectionBindings.optional)
            Bind.text (p + "hypothesisCount") (analysis |> Option.map (fun a -> string a.Hypotheses.Length) |> SectionBindings.optional)
            Bind.text (p + "evidenceCount") (analysis |> Option.map (fun a -> string a.Evidence.Length) |> SectionBindings.optional)
            Bind.text (p + "supportingCount") (analysis |> Option.map (fun _ -> string (count Supports)) |> SectionBindings.optional)
            Bind.text (p + "contradictingCount") (analysis |> Option.map (fun _ -> string (count Contradicts)) |> SectionBindings.optional)
            Bind.text
                (p + "contradictionCount")
                (analysis
                 |> Option.map (fun a ->
                     match a.UnresolvedContradictions with
                     | Known n -> string n
                     | UnknownCount
                     | NotSupported -> "Unknown")
                 |> SectionBindings.optional)
            Bind.flag (p + "steps.empty") steps.IsEmpty
            Bind.items
                (p + "steps")
                [ for i, step in List.indexed steps ->
                      [ Cell.text "id" (string i); Cell.text "text" step.Text ] @ SectionBindings.recordCells ctx step.Target ]
            Bind.items
                (p + "sections")
                [ for section in [ CausalAnalysis; EvidenceSection; HypothesesSection; TimelineSection ] ->
                      [ Cell.text "id" (UiRoutes.sectionSlug section)
                        Cell.text "label" (UiRoutes.sectionLabel section)
                        Cell.text "href" (ctx.Href section None) ] ]
            Bind.flag (p + "observations.empty") observations.IsEmpty
            Bind.items
                (p + "observations")
                [ for o in observations ->
                      [ Cell.text "id" o.Id
                        Cell.text "text" o.Text
                        Cell.flag "hasSource" o.SourceReference.IsSome
                        Cell.text "source" (SectionBindings.optional o.SourceReference)
                        Cell.flag "hasObservedAt" o.ObservedAt.IsSome
                        Cell.text "observedAt" (o.ObservedAt |> Option.map Words.instant |> SectionBindings.optional) ]
                      @ SectionBindings.recordCells ctx { Kind = ObservationEntity; Id = o.Id } ]
            SectionBindings.draft draft observationText
            SectionBindings.draft draft observationSource ]
        @ lines (p + "findings") (investigation |> Option.map Summary.supportedFindings |> Option.defaultValue [])
        @ lines (p + "questions") (investigation |> Option.map Summary.openQuestions |> Option.defaultValue [])
        @ lines (p + "actions") (investigation |> Option.map Summary.authorizedActions |> Option.defaultValue [])
        @ SectionBindings.action
            recordObservation
            (analysis |> Option.map (fun a -> Analysis.action a "INV-014") |> Option.defaultValue NotOffered)

    let keys (draft: string -> string) (investigation: Investigation option) = render draft (SectionContext.ofInvestigation investigation)

    let view (state: AppState) : (string * ViewValue) list = render (Drafts.get state) (SectionContext.ofState state)

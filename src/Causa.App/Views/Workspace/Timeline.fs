// Owner: ui-3. The Timeline section (screen.investigation.timeline): every stored event of this investigation in
// canonical version order (UI-003), with its type, event time, actor and a summary in the domain's words, a filter by
// category, and a link to the record it affected (UI-033). A reassessment shows both the earlier and the new kind
// (INV-035, J2-06). The history is never edited here.
namespace Causa.App.Views.Workspace

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

[<RequireQualifiedAccess>]
module Timeline =
    let private p = "investigation.timeline."

    /// The filter's event name (a draft): one of the category ids below, or "" for every event.
    let category = "investigation.timeline.category"

    /// UI-003-O2: the categories an event belongs to, by its domain case.
    let categories =
        [ "investigation", "Investigation"
          "observation", "Observations"
          "hypothesis", "Hypotheses"
          "evidence", "Evidence"
          "assessment", "Assessments"
          "cause", "Causes and conclusions"
          "corrective", "Corrective actions"
          "other", "Other" ]

    let categoryOf (eventType: string) : string =
        if eventType.StartsWith "Investigation" && not (eventType.StartsWith "InvestigationAction") then "investigation"
        elif eventType = "ObservationRecorded" then "observation"
        elif eventType.StartsWith "Hypothesis" then "hypothesis"
        elif eventType = "EvidenceRelationshipRecorded" then "assessment"
        elif eventType.StartsWith "Evidence" then "evidence"
        elif eventType.StartsWith "Causal" || eventType.StartsWith "Cause" || eventType = "ConfirmedCauseReopened" || eventType = "ConclusionRecorded" then "cause"
        elif eventType.StartsWith "CorrectiveAction" || eventType.StartsWith "Verification" then "corrective"
        else "other"

    let private label (t: TimelineItem) =
        match t.Affects with
        | r :: _ ->
            match r.Kind with
            | ObservationEntity -> "Open the observation"
            | HypothesisEntity -> "Open the hypothesis"
            | EvidenceEntity -> "Open the evidence"
            | RelationshipEntity -> "Open the assessment"
            | ConclusionEntity -> "Open the conclusion"
            | ContradictionEntity -> "Open the contradiction"
            | CorrectiveActionEntity -> "Open the corrective action"
            | OtherEntity _ -> "Open the record"
        | [] -> ""

    let render (draft: string -> string) (ctx: SectionContext) : (string * ViewValue) list =
        let chosen = draft category
        let all = ctx.Investigation |> Option.map (fun s -> Analysis.timeline (Kernel.domain.Project s)) |> Option.defaultValue []
        let items = all |> List.filter (fun t -> chosen = "" || categoryOf t.EventType = chosen)
        SectionBindings.status p ctx
        @ [ Bind.flag (p + "empty") items.IsEmpty
            Bind.text (p + "count") (if ctx.Investigation.IsSome then $"{items.Length} of {all.Length} events" else "")
            SectionBindings.draft draft category
            Bind.flag (p + "allChosen") (chosen = "")
            Bind.items
                (p + "categories")
                [ for id, name in categories -> [ Cell.text "id" id; Cell.text "label" name; Cell.flag "selected" (chosen = id) ] ]
            Bind.items
                (p + "items")
                [ for t in items ->
                      let target = List.tryHead t.Affects
                      [ Cell.text "id" (string t.Version)
                        Cell.text "version" (string t.Version)
                        Cell.text "eventType" (Words.ofWire t.EventType |> fun w -> w.Trim())
                        Cell.text "category" (categories |> List.find (fst >> (=) (categoryOf t.EventType)) |> snd)
                        Cell.text "at" (Words.instant t.OccurredAt)
                        Cell.text "actor" (t.Actor |> Option.defaultValue "Not recorded")
                        Cell.text "summary" t.Summary
                        Cell.flag "hasLink" target.IsSome
                        Cell.text "linkLabel" (label t)
                        Cell.text "href" (target |> Option.map (RecordLinks.href ctx) |> SectionBindings.optional)
                        Cell.flag "selected" (ctx.Selected = Some("event-" + string t.Version)) ] ] ]

    let keys (investigation: Investigation option) = render (fun _ -> "") (SectionContext.ofInvestigation investigation)

    let view (state: AppState) : (string * ViewValue) list = render (Drafts.get state) (SectionContext.ofState state)

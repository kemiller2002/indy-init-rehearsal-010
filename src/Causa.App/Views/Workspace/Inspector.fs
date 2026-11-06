// Owner: ui-3. The record inspector (UI-034, UI-033, UI-005): the record a route selects (`?record=<id>`) shown with its
// provenance and its related records, each a link, in whichever analysis section is open. One shared inspector for
// observations, hypotheses, evidence, assessments, conclusions and timeline events; an id this investigation does not
// hold is said to be absent (UI-033-O2), never guessed.
namespace Causa.App.Views.Workspace

open System
open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

[<RequireQualifiedAccess>]
module Inspector =
    let private p = "investigation.inspector."

    type Detail =
        { Kind: string
          Title: string
          Body: string list
          Provenance: string
          Related: (string * EntityRef) list }

    let private by (actor: Actor) (at: DateTimeOffset) (version: AggregateVersion) =
        $"Recorded by {actor.ActorId.Value} at {Words.instant at}, version {AggregateVersion.value version}"

    let private recorded (pv: EntityProvenance) = by pv.Actor pv.RecordedAt pv.AtVersion

    let private links (s: Investigation) (pick: EvidenceRelationship -> bool) =
        Analysis.links s |> List.filter (fun l -> l.Current) |> List.filter (fun l ->
            s.Relationships |> Map.exists (fun k r -> Ids.text (Ids.relationship k) = l.Id && pick r.Value))

    /// The selected record's detail, or None when this investigation holds no record with that id.
    let detail (s: Investigation) (id: string) : Detail option =
        let matches (g: Guid) = Ids.text g = id
        let observation =
            s.Observations |> Map.tryPick (fun k o -> if matches (Ids.observation k) then Some o else None)
            |> Option.map (fun o ->
                { Kind = "Observation"; Title = o.Value.Original.Value
                  Body = [ "Source reference: not recorded" ]
                  Provenance = recorded o.Provenance; Related = [] })
        let hypothesis =
            s.Hypotheses |> Map.tryPick (fun k h -> if matches (Ids.hypothesis k) then Some(k, h) else None)
            |> Option.map (fun (k, h) ->
                let ls = links s (fun r -> r.HypothesisId = k)
                { Kind = "Hypothesis"; Title = h.Hypothesis.Statement.Value
                  Body =
                    [ yield $"State: {Words.hypothesisState h.State}"
                      match h.Hypothesis.Mechanism with
                      | Some m -> yield $"Explanation: {m.Value}"
                      | None -> yield "Explanation: not recorded"
                      for c in h.History do
                          yield $"Moved from {Words.hypothesisState c.From} to {Words.hypothesisState c.To} at {Words.instant c.At}: {c.Reason.Value}" ]
                  Provenance = recorded h.Provenance
                  Related = ls |> List.map (fun l -> $"{Words.kind l.Kind}: {l.EvidenceLabel}", l.Evidence) })
        let evidence =
            s.Evidence |> Map.tryPick (fun k e -> if matches (Ids.evidence k) then Some(k, e) else None)
            |> Option.map (fun (k, e) ->
                let ls = links s (fun r -> r.EvidenceId = k)
                { Kind = "Evidence"; Title = e.Evidence.Content.Value
                  Body =
                    [ $"Source: {e.Evidence.Source.Value}"
                      $"Category: {Words.evidenceCategory e.Evidence.Category}"
                      $"Verification: {Words.verification e.Verification}"
                      (match e.Evidence.CapturedAt with
                       | Some at -> $"Captured at {Words.instant at}"
                       | None -> "Capture time not recorded") ]
                  Provenance = recorded e.Provenance
                  Related = ls |> List.map (fun l -> $"{Words.kind l.Kind}: {l.HypothesisStatement}", l.Hypothesis) })
        let relationship =
            Analysis.links s |> List.tryFind (fun l -> l.Id = id)
            |> Option.map (fun l ->
                let r = s.Relationships |> Map.pick (fun k r -> if Ids.text (Ids.relationship k) = id then Some r else None)
                { Kind = "Assessment"; Title = $"{l.EvidenceLabel} {Words.kind l.Kind} {l.HypothesisStatement}"
                  Body = [ $"Rationale: {l.Rationale}"; (if l.Current then "This is the current assessment." else "A later assessment replaced this one.") ]
                  Provenance = recorded r.Provenance
                  Related = [ "Evidence", l.Evidence; "Hypothesis", l.Hypothesis ] })
        let conclusion =
            s.Conclusions |> Map.tryPick (fun k c -> if matches (Ids.conclusion k) then Some c else None)
            |> Option.map (fun c ->
                { Kind = "Conclusion"; Title = c.Value.Statement.Value
                  Body =
                    [ $"Strength: {Words.conclusionStrength c.Value.Strength}"
                      Summary.conclusionExplanation s c.Value
                      (match c.Value.ResidualUncertainty with
                       | Some r -> $"Residual uncertainty: {r.Value}"
                       | None -> "Residual uncertainty: not recorded") ]
                  Provenance = recorded c.Provenance; Related = [] })
        let event =
            if id.StartsWith "event-" then
                Analysis.timeline (Kernel.domain.Project s)
                |> List.tryFind (fun t -> "event-" + string t.Version = id)
                |> Option.map (fun t ->
                    let who = t.Actor |> Option.defaultValue "an unrecorded actor"
                    { Kind = "Event"; Title = t.Summary; Body = [ $"Event type: {(Words.ofWire t.EventType).Trim()}" ]
                      Provenance = $"Recorded by {who} at {Words.instant t.OccurredAt}, version {t.Version}"
                      Related = t.Affects |> List.map (fun r -> "Affected record", r) })
            else None
        [ observation; hypothesis; evidence; relationship; conclusion; event ] |> List.tryPick (fun d -> d)

    let render (ctx: SectionContext) (section: Section) : (string * ViewValue) list =
        let found = match ctx.Investigation, ctx.Selected with | Some s, Some id -> detail s id | _ -> None
        let absent = ctx.Investigation.IsSome && ctx.Selected.IsSome && found.IsNone
        let related = found |> Option.map (fun d -> d.Related) |> Option.defaultValue []
        [ Bind.flag (p + "open") found.IsSome
          Bind.flag (p + "absent") absent
          Bind.text (p + "absentText") "The selected record is not part of this investigation."
          Bind.text (p + "kind") (found |> Option.map (fun d -> d.Kind) |> SectionBindings.optional)
          Bind.text (p + "title") (found |> Option.map (fun d -> d.Title) |> SectionBindings.optional)
          Bind.text (p + "body") (found |> Option.map (fun d -> String.concat "\n" d.Body) |> SectionBindings.optional)
          Bind.text (p + "provenance") (found |> Option.map (fun d -> d.Provenance) |> SectionBindings.optional)
          Bind.text (p + "close") (ctx.Href section None)
          Bind.flag (p + "related.empty") related.IsEmpty
          Bind.items
              (p + "related")
              [ for i, (label, r) in List.indexed related ->
                    [ Cell.text "id" (string i); Cell.text "label" label; Cell.text "href" (RecordLinks.href ctx r) ] ] ]

    let keys (investigation: Investigation option) (selected: string option) (section: Section) =
        render { SectionContext.ofInvestigation investigation with Selected = selected } section

    let view (state: AppState) : (string * ViewValue) list =
        let section = match state.Route with WorkspaceRoute(_, s) -> s | _ -> Overview
        render (SectionContext.ofState state) section

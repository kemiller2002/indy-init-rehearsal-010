// Owner: ui-3. The Evidence section (screen.investigation.evidence): every evidence item (INV-013) with its label
// (INV-027), original content (INV-012), source reference (INV-028), source URL rendered as a link only when it is a
// SafeUrl (INV-029, UI-005), and the hypotheses it is currently assessed against (INV-044, J2-05); and the
// "Record evidence" form the engine offers. causa-storage v1.0.0 has no label or separate URL (OD-HC-06): the stored
// source becomes a link only when it is itself an absolute http(s) URL.
namespace Causa.App.Views.Workspace

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

[<RequireQualifiedAccess>]
module Evidence =
    let private p = "investigation.evidence."

    let label = "investigation.evidence.label"
    let source = "investigation.evidence.source"
    let url = "investigation.evidence.url"
    let content = "investigation.evidence.content"
    let record = "investigation.evidence.record"

    /// What a missing label reads (IF-UI EvidenceCard.Label).
    let noLabel = "No label recorded"

    /// One current assessment as the evidence shows it: kind, hypothesis and rationale.
    let linkLine (l: LinkCard) = $"{Words.kind l.Kind}: {l.HypothesisStatement} — {l.Rationale}"

    /// UI-005: provenance as the domain recorded it (who, when, at which version) and the evidence's verification.
    let private provenance (s: Investigation) (id: string) =
        s.Evidence
        |> Map.tryPick (fun k e -> if Ids.text (Ids.evidence k) = id then Some e else None)

    let rows (ctx: SectionContext) (analysis: AnalysisProjection) : (string * ViewPrimitive) list list =
        [ for e in analysis.Evidence ->
              let entry = ctx.Investigation |> Option.bind (fun s -> provenance s e.Id)
              [ Cell.text "id" e.Id
                Cell.flag "hasLabel" e.Label.IsSome
                Cell.text "label" (e.Label |> Option.defaultValue noLabel)
                Cell.text "content" e.Content
                Cell.flag "hasSource" e.SourceReference.IsSome
                Cell.text "source" (SectionBindings.optional e.SourceReference)
                Cell.flag "hasUrl" e.SourceUrl.IsSome
                Cell.text "url" (e.SourceUrl |> Option.map SafeUrl.value |> SectionBindings.optional)
                Cell.text "category" (SectionBindings.optional e.Category)
                Cell.flag "hasCapturedAt" e.CapturedAt.IsSome
                Cell.text "capturedAt" (e.CapturedAt |> Option.map Words.instant |> SectionBindings.optional)
                Cell.flag "hasLinks" (not e.Links.IsEmpty)
                Cell.int "linkCount" e.Links.Length
                Cell.text "links" (e.Links |> List.map linkLine |> String.concat "\n")
                Cell.text "capturedAtText" (e.CapturedAt |> Option.map Words.instant |> Option.defaultValue "Capture time not recorded")
                Cell.text "recordedBy" (entry |> Option.map (fun x -> x.Provenance.Actor.ActorId.Value) |> Option.defaultValue "Not recorded")
                Cell.text "recordedAt" (entry |> Option.map (fun x -> Words.instant x.Provenance.RecordedAt) |> Option.defaultValue "Not recorded")
                Cell.text "recordedVersion" (entry |> Option.map (fun x -> string (AggregateVersion.value x.Provenance.AtVersion)) |> Option.defaultValue "")
                Cell.text "verification" (entry |> Option.map (fun x -> Words.verification x.Verification) |> Option.defaultValue "Not recorded")
                Cell.flag "superseded" (entry |> Option.exists (fun x -> x.SupersededBy.IsSome)) ]
              @ SectionBindings.recordCells ctx { Kind = EvidenceEntity; Id = e.Id } ]

    let render (draft: string -> string) (ctx: SectionContext) : (string * ViewValue) list =
        let analysis = ctx.Investigation |> Option.map (Analysis.project Analysis.actor)
        let items = analysis |> Option.map (rows ctx) |> Option.defaultValue []
        SectionBindings.status p ctx
        @ [ Bind.flag (p + "empty") items.IsEmpty
            Bind.items (p + "items") items
            SectionBindings.draft draft label
            SectionBindings.draft draft source
            SectionBindings.draft draft url
            SectionBindings.draft draft content ]
        @ SectionBindings.action record (analysis |> Option.map (fun a -> Analysis.action a "UIA-RECORD-EVIDENCE") |> Option.defaultValue NotOffered)

    let keys (draft: string -> string) (investigation: Investigation option) = render draft (SectionContext.ofInvestigation investigation)

    let view (state: AppState) : (string * ViewValue) list = render (Drafts.get state) (SectionContext.ofState state)

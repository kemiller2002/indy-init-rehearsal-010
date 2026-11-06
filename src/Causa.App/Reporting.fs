// Owner: ui-3. The read-only analysis of one investigation, derived only from what the domain kernel projects
// (Kernel.domain.Project, the Investigation aggregate, Assessments, Kernel.capabilities): the IF-UI AnalysisProjection
// the workspace sections render (Views/Workspace/*.fs, Views/Graph.fs) and the IF41-REPORT projection Folio renders.
// Nothing here decides legality (UI-004) or invents content: a value the causa-storage v1.0.0 wire cannot hold is shown
// as absent (OD-HC-06 B), and a stored relationship kind INV-031 has no option for is shown verbatim (OD-HC-07 B).
namespace Causa.App

open Causa.Domain

/// IF41-REPORT: Boundary E, the read-only report projection Folio renders.
type ReportSection = { Heading: string; Paragraphs: string list; Provenance: EventId list }

type ReportProjection =
    { Investigation: InvestigationId
      Version: AggregateVersion
      Sections: ReportSection list
      UnresolvedUncertainties: string list }

type Reporting = { Project: InvestigationProjection -> ReportProjection }

namespace Causa.App.Workspace

open System
open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

/// Words for the domain's wire values (CONTRACT section 6), as plain lower-case text: `awaitingEvidence` reads
/// "awaiting evidence". The value is the domain's; only its spelling is presented.
[<RequireQualifiedAccess>]
module Words =
    let ofWire (wire: string) : string =
        wire
        |> Seq.collect (fun c -> if Char.IsUpper c then [ ' '; Char.ToLowerInvariant c ] else [ c ])
        |> Seq.toArray
        |> String

    let lifecycle (l: Lifecycle) = WireEnum.toWire Enums.lifecycle l |> ofWire
    let hypothesisState (s: HypothesisState) = WireEnum.toWire Enums.hypothesisState s |> ofWire
    let evidenceCategory (c: EvidenceCategory) = WireEnum.toWire Enums.evidenceCategory c |> ofWire
    let verification (v: VerificationStatus) = WireEnum.toWire Enums.verificationStatus v |> ofWire
    let causeState (s: CauseState) = WireEnum.toWire Enums.causeState s |> ofWire
    let causeRole (r: CauseRole) = WireEnum.toWire Enums.causeRole r |> ofWire
    let conclusionStrength (s: ConclusionStrength) = WireEnum.toWire Enums.conclusionStrength s |> ofWire
    let unknownState (s: UnknownState) = WireEnum.toWire Enums.unknownState s |> ofWire
    let contradictionState (s: ContradictionState) = WireEnum.toWire Enums.contradictionState s |> ofWire

    /// An INV-031 option as the catalog spells it, or a stored kind verbatim (OD-HC-07 B).
    let kind (k: KindShown) : string =
        match k with
        | CatalogKind Supports -> "supports"
        | CatalogKind Contradicts -> "contradicts"
        | CatalogKind ConsistentWith -> "consistent-with"
        | CatalogKind Inconclusive -> "inconclusive"
        | UninterpretedKind wire -> wire

    /// A UTC instant as ISO 8601 text (CONTRACT section 2.9), the only clock the UI shows: the domain's.
    let instant (at: DateTimeOffset) = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")

/// The IF-UI AnalysisProjection of one investigation, from the kernel's state and capabilities only.
[<RequireQualifiedAccess>]
module Analysis =
    let private text (t: NonEmptyText) = t.Value
    let private idText (g: Guid) = Ids.text g

    /// The domain's relationship kind as the UI may show it: supports and contradicts are INV-031 options; the wire's
    /// discriminates and insufficient have no option and are shown verbatim, never mapped (OD-HC-07 B).
    let kindShown (k: Causa.Domain.RelationshipKind) : KindShown =
        match k with
        | Causa.Domain.RelationshipKind.Supports -> CatalogKind Supports
        | Causa.Domain.RelationshipKind.Contradicts -> CatalogKind Contradicts
        | other -> UninterpretedKind(WireEnum.toWire Enums.relationshipKind other)

    /// The INV-031 option a catalog word names, and the domain kind that stores it. consistent-with and inconclusive
    /// have no causa-storage v1.0.0 value (OD-HC-06, OD-HC-07): None, so no command can be built for them.
    let storedKindOf (option: string) : Causa.Domain.RelationshipKind option =
        match option with
        | "supports" -> Some Causa.Domain.RelationshipKind.Supports
        | "contradicts" -> Some Causa.Domain.RelationshipKind.Contradicts
        | _ -> None

    /// INV-031's options, in catalog order.
    let kindOptions = [ "supports"; "contradicts"; "consistent-with"; "inconclusive" ]

    /// The original problem statement (INV-008): the observation InvestigationStarted recorded.
    let problemStatement (s: Investigation) : Recorded<Observation> option =
        s.History
        |> List.tryPick (fun e ->
            match e.Payload with
            | CoreEvent(InvestigationStarted started) -> s.Observations.TryFind started.ObservationId
            | _ -> None)

    /// What evidence is called where a name is needed. causa-storage v1.0.0 has no label (OD-HC-06), so it is the
    /// stored content, never an invented name.
    let evidenceName (s: Investigation) (id: EvidenceId) : string =
        match s.Evidence.TryFind id with
        | Some e -> text e.Evidence.Content
        | None -> idText (Ids.evidence id)

    let hypothesisStatement (s: Investigation) (id: HypothesisId) : string =
        match s.Hypotheses.TryFind id with
        | Some h -> text h.Hypothesis.Statement
        | None -> idText (Ids.hypothesis id)

    /// Every assessment, oldest first; Current marks the latest of each evidence-hypothesis pair (Assessments.current).
    let links (s: Investigation) : LinkCard list =
        let current = Assessments.current s |> List.map (fun r -> r.Value.RelationshipId) |> Set.ofList
        s.Relationships
        |> Map.toList
        |> List.map snd
        |> List.sortBy (fun r -> AggregateVersion.value r.Provenance.AtVersion)
        |> List.map (fun r ->
            let v = r.Value
            { Id = idText (Ids.relationship v.RelationshipId)
              Evidence = { Kind = EvidenceEntity; Id = idText (Ids.evidence v.EvidenceId) }
              EvidenceLabel = evidenceName s v.EvidenceId
              Hypothesis = { Kind = HypothesisEntity; Id = idText (Ids.hypothesis v.HypothesisId) }
              HypothesisStatement = hypothesisStatement s v.HypothesisId
              Kind = kindShown v.Kind
              Rationale = v.Rationale |> Option.map text |> Option.defaultValue ""
              Current = current.Contains v.RelationshipId })

    let observations (s: Investigation) : ObservationCard list =
        let problem = problemStatement s |> Option.map (fun o -> o.Value.ObservationId)
        s.Observations
        |> Map.toList
        |> List.map snd
        |> List.filter (fun o -> Some o.Value.ObservationId <> problem)
        |> List.sortBy (fun o -> AggregateVersion.value o.Provenance.AtVersion)
        |> List.map (fun o ->
            { Id = idText (Ids.observation o.Value.ObservationId)
              Text = text o.Value.Original
              // INV-025: causa-storage v1.0.0 has no observation source (OD-HC-06).
              SourceReference = None
              ObservedAt = o.Value.ObservedAt })

    let hypotheses (s: Investigation) (all: LinkCard list) : HypothesisCard list =
        s.Hypotheses
        |> Map.toList
        |> List.map snd
        |> List.sortBy (fun h -> AggregateVersion.value h.Provenance.AtVersion)
        |> List.map (fun h ->
            let id = idText (Ids.hypothesis h.Hypothesis.HypothesisId)
            { Id = id
              Statement = text h.Hypothesis.Statement
              State = Words.hypothesisState h.State
              Explanation = h.Hypothesis.Mechanism |> Option.map text
              Links = all |> List.filter (fun l -> l.Current && l.Hypothesis.Id = id) })

    let evidence (s: Investigation) (all: LinkCard list) : EvidenceCard list =
        s.Evidence
        |> Map.toList
        |> List.map snd
        |> List.sortBy (fun e -> AggregateVersion.value e.Provenance.AtVersion)
        |> List.map (fun e ->
            let id = idText (Ids.evidence e.Evidence.EvidenceId)
            let source = text e.Evidence.Source
            { Id = id
              // INV-027: causa-storage v1.0.0 has no label (OD-HC-06); None renders as "No label recorded".
              Label = None
              Content = text e.Evidence.Content
              SourceReference = Some source
              // INV-029: the stored source is a link only when it is an absolute http(s) URL (UI-005).
              SourceUrl = SafeUrl.tryCreate source
              Category = Some(Words.evidenceCategory e.Evidence.Category)
              CapturedAt = e.Evidence.CapturedAt
              Links = all |> List.filter (fun l -> l.Current && l.Evidence.Id = id) })

    let conclusions (s: Investigation) : ConclusionCard list =
        s.Conclusions
        |> Map.toList
        |> List.map snd
        |> List.sortBy (fun c -> AggregateVersion.value c.Provenance.AtVersion)
        |> List.map (fun c ->
            { Id = idText (Ids.conclusion c.Value.ConclusionId)
              Statement = text c.Value.Statement
              ResidualUncertainty = c.Value.ResidualUncertainty |> Option.map text
              RecordedAtVersion = Some(AggregateVersion.value c.Provenance.AtVersion) })

    /// One timeline line per stored event, in version order, in the domain's words. An assessment says which kind it
    /// recorded and, for a reassessment, the kind it replaced, so the history shows both (INV-035, J2-06).
    let private summary (s: Investigation) (envelope: EventEnvelope) : string * EntityRef list =
        let ent kind (g: Guid) = { Kind = kind; Id = idText g }
        match envelope.Payload with
        | ExtensionEvent(id, _) -> $"Extension event {id.Namespace.Value}/{id.Name.Value}", []
        | CoreEvent e ->
            match e with
            | InvestigationStarted p ->
                $"Investigation started: {text p.Title}. Problem statement: {text p.Original}",
                [ ent ObservationEntity (Ids.observation p.ObservationId) ]
            | ObservationRecorded p -> $"Observation recorded: {text p.Original}", [ ent ObservationEntity (Ids.observation p.ObservationId) ]
            | HypothesisRecorded p -> $"Hypothesis recorded: {text p.Statement}", [ ent HypothesisEntity (Ids.hypothesis p.HypothesisId) ]
            | EvidenceAdmitted p ->
                $"Evidence recorded: {text p.Content} (source: {text p.Source})", [ ent EvidenceEntity (Ids.evidence p.EvidenceId) ]
            | EvidenceRelationshipRecorded p ->
                let kind = Words.kind (kindShown p.Kind)
                let earlier =
                    Assessments.history s p.EvidenceId p.HypothesisId
                    |> List.filter (fun r -> AggregateVersion.value r.Provenance.AtVersion < AggregateVersion.value envelope.Version)
                    |> List.tryLast
                let verb =
                    match earlier with
                    | Some r -> $"Assessment revised from {Words.kind (kindShown r.Value.Kind)} to {kind}"
                    | None -> $"Assessment recorded as {kind}"
                let why = p.Rationale |> Option.map (fun r -> $". Rationale: {text r}") |> Option.defaultValue ""
                $"{verb}: {evidenceName s p.EvidenceId} {kind} {hypothesisStatement s p.HypothesisId}{why}",
                [ ent RelationshipEntity (Ids.relationship p.RelationshipId)
                  ent EvidenceEntity (Ids.evidence p.EvidenceId)
                  ent HypothesisEntity (Ids.hypothesis p.HypothesisId) ]
            | ConclusionRecorded p -> $"Conclusion recorded: {text p.Statement}", [ ent ConclusionEntity (Ids.conclusion p.ConclusionId) ]
            | ContradictionRecorded p -> $"Contradiction recorded: {text p.Statement}", [ ent ContradictionEntity (Ids.contradiction p.ContradictionId) ]
            | CausalAssertionRecorded p -> $"Causal assertion recorded: {text p.Outcome}", []
            | CorrectiveActionRecorded p ->
                $"Corrective action recorded: {text p.Description}", [ ent CorrectiveActionEntity (Ids.correctiveAction p.CorrectiveActionId) ]
            | other ->
                match Events.lifecycleChange other, Events.hypothesisChange other with
                | Some(from, to'), _ -> $"Investigation moved from {Words.lifecycle from} to {Words.lifecycle to'}", []
                | None, Some(t, to') ->
                    $"Hypothesis {hypothesisStatement s t.HypothesisId} moved from {Words.hypothesisState t.From} to {Words.hypothesisState to'}: {text t.Reason}",
                    [ ent HypothesisEntity (Ids.hypothesis t.HypothesisId) ]
                | None, None -> Words.ofWire (Events.caseName other) |> fun w -> string (Char.ToUpperInvariant w[0]) + w.Substring 1, []

    let timeline (projection: InvestigationProjection) : TimelineItem list =
        let s = projection.Investigation
        projection.Timeline
        |> List.map (fun e ->
            let text', affects = summary s e
            { Version = AggregateVersion.value e.Version
              EventType =
                match e.Payload with
                | CoreEvent c -> Events.caseName c
                | ExtensionEvent _ -> "ExtensionEvent"
              Summary = text'
              OccurredAt = e.Provenance.OccurredAt
              Actor = Some e.Provenance.Actor.ActorId.Value
              Affects = affects })

    /// The engine's verdict on one command as IF-UI Availability: offered only when the engine probes it (UI-004),
    /// available when it is available for at least one target, otherwise disabled with the engine's own reasons.
    let availability (capabilities: Causa.Domain.Capability list) (command: string) : Availability =
        match capabilities |> List.filter (fun c -> c.Command = command) with
        | [] -> NotOffered
        | found when found |> List.exists (fun c -> c.State = CapabilityState.Available) -> Available
        | found ->
            found
            |> List.collect (fun c ->
                match c.State with
                | CapabilityState.Available -> []
                | CapabilityState.Unavailable blockers -> blockers |> List.map (fun b -> b.Message)
                | CapabilityState.ReviewRequired reviews -> reviews
                | CapabilityState.SemanticsUnavailable semantics -> semantics)
            |> List.distinct
            |> String.concat " "
            |> Unavailable

    /// The content actions of the analysis sections, keyed by catalog action id, and the command that decides each.
    /// INV-032 (Record a conclusion) has no command in the frozen contract, so the engine never offers it.
    let actionCommands =
        [ ActionId "INV-014", "RecordObservation"
          ActionId "INV-009", "RecordHypothesis"
          ActionId "UIA-RECORD-EVIDENCE", "AdmitEvidence"
          ActionId "INV-030", "RelateEvidence"
          ActionId "INV-032", "RecordConclusion" ]

    /// The policy and actor the analysis asks the engine under (the kernel's core policy).
    let policy = Policy.core

    let project (actor: ActorContext) (s: Investigation) : AnalysisProjection =
        let projection = Kernel.domain.Project s
        let capabilities = Kernel.capabilities policy actor s
        let all = links s
        { Observations = observations s
          Hypotheses = hypotheses s all
          Evidence = evidence s all
          Relationships = all
          Timeline = timeline projection
          Conclusions = conclusions s
          NextSteps = []
          UnresolvedContradictions =
            s.Contradictions
            |> Map.filter (fun _ c -> c.State <> ContradictionState.Resolved && c.State <> ContradictionState.Superseded)
            |> Map.count
            |> Known
          CorrectiveActionsSupported = false
          Actions = actionCommands |> List.map (fun (a, command) -> a, availability capabilities command) |> Map.ofList }

    /// The action's availability in a projection (NotOffered when absent).
    let action (p: AnalysisProjection) (id: string) : Availability =
        p.Actions |> Map.tryFind (ActionId id) |> Option.defaultValue NotOffered

    /// The investigation the route names, as the loader verified it (SM-LOAD Loaded). Only the integration engine holds
    /// the unsaved commands of a session; see the change request for the current-state source.
    let loaded (state: AppState) : Investigation option =
        match state.Route with
        | WorkspaceRoute(target, _) ->
            state.Model.Loads
            |> Map.toList
            |> List.tryPick (fun (r, load) ->
                let (Workspace workspace) = r.Address.Workspace
                match load with
                | Loaded v when r.Id = target.Id && workspace = target.Workspace -> Some v.Investigation
                | _ -> None)
        | _ -> None

    /// The actor capabilities are asked for.
    let actor: ActorContext = { ActorId = "causa" }

    /// The analysis the workspace sections render for the current route.
    let ofState (state: AppState) : AnalysisProjection option = loaded state |> Option.map (project actor)

/// IF41-REPORT producer: the read-only report of a projection. It reports causes, hypotheses and conclusions with the
/// state the domain gives them, so an unverified hypothesis is never presented as a confirmed cause (Boundary E).
[<RequireQualifiedAccess>]
module Report =
    let private text (t: NonEmptyText) = t.Value

    let private eventsWhere (p: InvestigationProjection) (pick: CoreEvent -> bool) =
        p.Timeline
        |> List.filter (fun e ->
            match e.Payload with
            | CoreEvent c -> pick c
            | ExtensionEvent _ -> false)
        |> List.map (fun e -> e.EventId)

    let project (p: InvestigationProjection) : ReportProjection =
        let s = p.Investigation
        let problem = Analysis.problemStatement s |> Option.map (fun o -> text o.Value.Original)
        let hypotheses =
            s.Hypotheses
            |> Map.toList
            |> List.map snd
            |> List.map (fun h -> $"{text h.Hypothesis.Statement} (hypothesis, {Words.hypothesisState h.State})")
        let evidence =
            s.Evidence
            |> Map.toList
            |> List.map snd
            |> List.map (fun e ->
                $"{text e.Evidence.Content} (source: {text e.Evidence.Source}; {Words.verification e.Verification})")
        let assessments =
            Assessments.current s
            |> List.map (fun r ->
                let rationale = r.Value.Rationale |> Option.map (fun t -> $": {text t}") |> Option.defaultValue ""
                $"{Analysis.evidenceName s r.Value.EvidenceId} {Words.kind (Analysis.kindShown r.Value.Kind)} {Analysis.hypothesisStatement s r.Value.HypothesisId}{rationale}")
        let causes =
            s.Causes
            |> Map.toList
            |> List.map snd
            |> List.map (fun c ->
                let members =
                    c.Cause.Members
                    |> List.map (fun m -> $"{Analysis.hypothesisStatement s m.HypothesisId} ({Words.causeRole m.Role})")
                    |> String.concat "; "
                match c.State with
                | CauseState.Confirmed ->
                    let under = c.ConfirmedUnder |> Option.map (fun t -> $" under {text t}") |> Option.defaultValue ""
                    $"Confirmed cause{under}: {text c.Cause.Outcome}. Members: {members}"
                | state -> $"Causal assertion ({Words.causeState state}, not confirmed): {text c.Cause.Outcome}. Members: {members}")
        let conclusions =
            s.Conclusions
            |> Map.toList
            |> List.map snd
            |> List.map (fun c -> $"{text c.Value.Statement} ({Words.conclusionStrength c.Value.Strength})")
        let section heading paragraphs provenance = { Heading = heading; Paragraphs = paragraphs; Provenance = provenance }
        let uncertainties =
            [ for KeyValue(_, u) in s.Unknowns do
                  if u.State = UnknownState.Identified || u.State = UnknownState.Investigating then
                      yield $"Open question ({Words.unknownState u.State}): {text u.Unknown.Question}"
              for KeyValue(_, c) in s.Contradictions do
                  if c.State <> ContradictionState.Resolved && c.State <> ContradictionState.Superseded then
                      yield $"Unresolved contradiction ({Words.contradictionState c.State}): {text c.Contradiction.Statement}"
              for KeyValue(_, c) in s.Causes do
                  if c.State <> CauseState.Confirmed then yield $"Cause not confirmed: {text c.Cause.Outcome}"
              for KeyValue(_, c) in s.Conclusions do
                  match c.Value.ResidualUncertainty with
                  | Some r -> yield $"Residual uncertainty: {text r}"
                  | None -> () ]
        { Investigation = s.Id
          Version = s.Version
          Sections =
            [ section "Problem" [ yield text s.Title; yield! Option.toList problem; yield $"State: {Words.lifecycle s.Lifecycle}" ]
                  (eventsWhere p (function InvestigationStarted _ -> true | _ -> false))
              section "Hypotheses" hypotheses (eventsWhere p (fun c -> match c with HypothesisRecorded _ -> true | c -> (Events.hypothesisChange c).IsSome))
              section "Evidence" evidence (eventsWhere p (function EvidenceAdmitted _ | EvidenceSuperseded _ | EvidenceVerificationAssessed _ -> true | _ -> false))
              section "Evidence assessments" assessments (eventsWhere p (function EvidenceRelationshipRecorded _ -> true | _ -> false))
              section "Causes" causes (eventsWhere p (function CausalAssertionRecorded _ | CauseConfirmed _ | ConfirmedCauseReopened _ -> true | _ -> false))
              section "Conclusions" conclusions (eventsWhere p (function ConclusionRecorded _ -> true | _ -> false)) ]
          UnresolvedUncertainties = uncertainties }

    /// The IF41-REPORT value.
    let reporting: Reporting = { Project = project }

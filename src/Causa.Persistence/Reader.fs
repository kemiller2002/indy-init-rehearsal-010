// Owner: persistence. causa-storage v1.0.0 wire mapping of the domain's events (CONTRACT sections 5-7), the normative
// snapshot projection (section 8.3, ported from the causa-data reference consumer
// tools/consumers/clean-fsharp/Projection.fs, same owner) and the file sets a save writes (section 9).
namespace Causa.Persistence

open System
open Causa.Domain
open Causa.Persistence.Canonical

/// Why a domain value has no v1 wire form (CAUSA.UNREPRESENTABLE) or a wire value no domain form.
type WireError = WireError of string

[<RequireQualifiedAccess>]
module WireEvents =

    let private text (t: NonEmptyText) = JString t.Value
    let private optText (t: NonEmptyText option) = t |> Option.map text |> Option.defaultValue JNull
    let private uuid (g: Guid) = JString(uuidText g)
    let private ids (f: 'a -> Guid) (xs: 'a list) = JArray(xs |> List.map (f >> uuid))
    let private enum (e: WireEnum<'T>) (v: 'T) = JString(WireEnum.toWire e v)

    let private ts (t: DateTimeOffset) : Result<JsonValue, WireError> =
        match timestamp t with
        | Some s -> Ok(JString s)
        | None -> Error(WireError(sprintf "sub-millisecond instant %O (CAUSA.UNREPRESENTABLE)" t))

    let private optTs (t: DateTimeOffset option) =
        match t with
        | None -> Ok JNull
        | Some v -> ts v

    let private obs (ObservationId g) = g
    let private hyp (HypothesisId g) = g
    let private evi (EvidenceId g) = g
    let private cause (CausalAssertionId g) = g
    let private ca (CorrectiveActionId g) = g
    let private prop (ProposalId g) = g

    let private hypothesisTransition (command: string) (target: HypothesisState) (t: HypothesisTransition) =
        "HypothesisTransitioned",
        [ "hypothesisId", uuid (hyp t.HypothesisId)
          "from", enum Enums.hypothesisState t.From
          "to", enum Enums.hypothesisState target
          "command", JString command
          "reason", text t.Reason
          "evidenceIds", ids evi t.EvidenceIds ]

    let private lifecycle (command: string) (from: Lifecycle) (target: Lifecycle) (reason: NonEmptyText option) =
        "LifecycleChanged", [ "from", enum Enums.lifecycle from; "to", enum Enums.lifecycle target; "command", JString command; "reason", optText reason ]

    let private correctiveTransition (id: CorrectiveActionId) (state: CorrectiveActionState) =
        "CorrectiveActionTransitioned", [ "correctiveActionId", uuid (ca id); "state", enum Enums.correctiveActionState state ]

    /// The wire event type and payload of a domain event (CONTRACT section 7).
    let payload (event: EventPayload) : Result<string * (string * JsonValue) list, WireError> =
        match event with
        | ExtensionEvent _ -> Error(WireError "v1.0.0 defines no extension events (CAUSA.UNREPRESENTABLE)")
        | CoreEvent e ->
            match e with
            | InvestigationStarted s ->
                Ok("InvestigationStarted", [ "title", text s.Title; "observationId", uuid (obs s.ObservationId); "original", text s.Original ])
            | InvestigationActivated from -> Ok(lifecycle "activateInvestigation" from Lifecycle.Active None)
            | InvestigationMarkedAwaitingEvidence(from, reason) -> Ok(lifecycle "markAwaitingEvidence" from Lifecycle.AwaitingEvidence (Some reason))
            | InvestigationMarkedAwaitingVerification from -> Ok(lifecycle "markAwaitingVerification" from Lifecycle.AwaitingVerification None)
            | InvestigationResolved from -> Ok(lifecycle "resolveInvestigation" from Lifecycle.Resolved None)
            | InvestigationClosed from -> Ok(lifecycle "closeInvestigation" from Lifecycle.Closed None)
            | InvestigationReopened(from, reason) -> Ok(lifecycle "reopenInvestigation" from Lifecycle.Active (Some reason))
            | InvestigationVerificationFailed reason ->
                Ok(lifecycle "verificationFailed" Lifecycle.AwaitingVerification Lifecycle.Active (Some reason))
            | ObservationRecorded o ->
                optTs o.ObservedAt
                |> Result.map (fun at -> "ObservationRecorded", [ "observationId", uuid (obs o.ObservationId); "original", text o.Original; "observedAt", at ])
            | HypothesisRecorded h ->
                Ok("HypothesisRecorded", [ "hypothesisId", uuid (hyp h.HypothesisId); "statement", text h.Statement; "mechanism", optText h.Mechanism ])
            | HypothesisMarkedPlausible t -> Ok(hypothesisTransition "markPlausible" HypothesisState.Plausible t)
            | HypothesisMarkedSupported t -> Ok(hypothesisTransition "markSupported" HypothesisState.Supported t)
            | HypothesisWeakened t -> Ok(hypothesisTransition "markWeakened" HypothesisState.Weakened t)
            | HypothesisFalsified t -> Ok(hypothesisTransition "falsify" HypothesisState.Falsified t)
            | HypothesisSuperseded t -> Ok(hypothesisTransition "supersede" HypothesisState.Superseded t)
            | HypothesisReopened t -> Ok(hypothesisTransition "reopen" HypothesisState.Plausible t)
            | EvidenceAdmitted ev ->
                optTs ev.CapturedAt
                |> Result.map (fun at ->
                    "EvidenceAdmitted",
                    [ "evidenceId", uuid (evi ev.EvidenceId)
                      "contentKind", enum Enums.contentKind ev.ContentKind
                      "content", text ev.Content
                      "category", enum Enums.evidenceCategory ev.Category
                      "source", text ev.Source
                      "capturedAt", at ])
            | EvidenceSuperseded s ->
                Ok("EvidenceSuperseded", [ "evidenceId", uuid (evi s.EvidenceId); "replacementEvidenceId", uuid (evi s.ReplacementEvidenceId); "reason", text s.Reason ])
            | EvidenceVerificationAssessed a ->
                Ok("EvidenceVerificationAssessed", [ "evidenceId", uuid (evi a.EvidenceId); "verification", enum Enums.verificationStatus a.Verification ])
            | EvidenceRelationshipRecorded r ->
                let (EvidenceRelationshipId rid) = r.RelationshipId
                Ok(
                    "EvidenceRelated",
                    [ "relationshipId", uuid rid
                      "evidenceId", uuid (evi r.EvidenceId)
                      "hypothesisId", uuid (hyp r.HypothesisId)
                      "kind", enum Enums.relationshipKind r.Kind
                      "rationale", optText r.Rationale ]
                )
            | UnknownIdentified u ->
                let (UnknownId id) = u.UnknownId
                Ok("UnknownIdentified", [ "unknownId", uuid id; "question", text u.Question; "materiality", enum Enums.materiality u.Materiality ])
            | UnknownTransitioned u ->
                let (UnknownId id) = u.UnknownId
                Ok("UnknownTransitioned", [ "unknownId", uuid id; "state", enum Enums.unknownState u.State; "basis", text u.Basis ])
            | AssumptionRecorded a ->
                let (AssumptionId id) = a.AssumptionId
                Ok("AssumptionRecorded", [ "assumptionId", uuid id; "proposition", text a.Proposition; "materiality", enum Enums.materiality a.Materiality ])
            | AssumptionTransitioned a ->
                let (AssumptionId id) = a.AssumptionId
                Ok("AssumptionTransitioned", [ "assumptionId", uuid id; "state", enum Enums.assumptionState a.State ])
            | ContradictionRecorded c ->
                let (ContradictionId id) = c.ContradictionId
                Ok(
                    "ContradictionRecorded",
                    [ "contradictionId", uuid id; "statement", text c.Statement; "evidenceIds", ids evi c.EvidenceIds; "materiality", enum Enums.materiality c.Materiality ]
                )
            | ContradictionTransitioned c ->
                let (ContradictionId id) = c.ContradictionId
                Ok("ContradictionTransitioned", [ "contradictionId", uuid id; "state", enum Enums.contradictionState c.State; "basis", text c.Basis ])
            | PredictionRecorded p ->
                let (PredictionId id) = p.PredictionId
                Ok(
                    "PredictionRecorded",
                    [ "predictionId", uuid id
                      "hypothesisId", uuid (hyp p.HypothesisId)
                      "statement", text p.Statement
                      "strength", enum Enums.predictionStrength p.Strength
                      "timing", enum Enums.predictionTiming p.Timing ]
                )
            | InvestigationActionRecorded a ->
                let (InvestigationActionId id) = a.ActionId
                Ok("ActionRecorded", [ "actionId", uuid id; "kind", enum Enums.actionKind a.Kind; "purpose", text a.Purpose; "targetHypothesisIds", ids hyp a.TargetHypothesisIds ])
            | InvestigationActionTransitioned a ->
                let (InvestigationActionId id) = a.ActionId
                Ok("ActionTransitioned", [ "actionId", uuid id; "state", enum Enums.actionState a.State ])
            | FindingRecorded f ->
                let (FindingId id) = f.FindingId
                let (InvestigationActionId aid) = f.ActionId
                Ok("FindingRecorded", [ "findingId", uuid id; "actionId", uuid aid; "originalResult", text f.OriginalResult; "interpretation", optText f.Interpretation ])
            | NegativeKnowledgeEstablished n ->
                let (NegativeKnowledgeId id) = n.NegativeKnowledgeId
                Ok(
                    "NegativeKnowledgeEstablished",
                    [ "negativeKnowledgeId", uuid id; "statement", text n.Statement; "scope", text n.Scope; "basisEvidenceIds", ids evi n.BasisEvidenceIds ]
                )
            | CausalAssertionRecorded c ->
                let members =
                    c.Members |> List.map (fun m -> JObject [ "hypothesisId", uuid (hyp m.HypothesisId); "role", enum Enums.causeRole m.Role ])
                Ok("CausalAssertionRecorded", [ "causeId", uuid (cause c.CauseId); "outcome", text c.Outcome; "members", JArray members; "mechanism", optText c.Mechanism ])
            | CauseConfirmed c -> Ok("CausalAssertionConfirmed", [ "causeId", uuid (cause c.CauseId); "policy", text c.Policy ])
            | ConfirmedCauseReopened(id, reason) -> Ok("CausalAssertionReopened", [ "causeId", uuid (cause id); "reason", text reason ])
            | CorrectiveActionRecorded c ->
                Ok(
                    "CorrectiveActionRecorded",
                    [ "correctiveActionId", uuid (ca c.CorrectiveActionId)
                      "kind", enum Enums.correctiveActionKind c.Kind
                      "description", text c.Description
                      "targetCauseIds", ids cause c.TargetCauseIds ]
                )
            | CorrectiveActionApproved id -> Ok(correctiveTransition id CorrectiveActionState.Approved)
            | CorrectiveActionStarted id -> Ok(correctiveTransition id CorrectiveActionState.InProgress)
            | CorrectiveActionImplemented id -> Ok(correctiveTransition id CorrectiveActionState.Implemented)
            | CorrectiveActionAwaitingVerificationOfEffect id -> Ok(correctiveTransition id CorrectiveActionState.AwaitingVerificationOfEffect)
            | CorrectiveActionVerifiedEffective id -> Ok(correctiveTransition id CorrectiveActionState.VerifiedEffective)
            | CorrectiveActionVerificationFailed id -> Ok(correctiveTransition id CorrectiveActionState.VerificationFailed)
            | CorrectiveActionReworkRequired id -> Ok(correctiveTransition id CorrectiveActionState.ReworkRequired)
            | CorrectiveActionCancelled id -> Ok(correctiveTransition id CorrectiveActionState.Cancelled)
            | VerificationCriterionRecorded c ->
                let (VerificationCriterionId id) = c.CriterionId
                Ok(
                    "CriterionRecorded",
                    [ "criterionId", uuid id; "correctiveActionId", uuid (ca c.CorrectiveActionId); "expectedOutcome", text c.ExpectedOutcome; "method", optText c.Method ]
                )
            | VerificationRecorded v ->
                let (VerificationId id) = v.VerificationId
                let (VerificationCriterionId cid) = v.CriterionId
                Ok(
                    "VerificationRecorded",
                    [ "verificationId", uuid id; "criterionId", uuid cid; "outcome", enum Enums.verificationOutcome v.Outcome; "evidenceIds", ids evi v.EvidenceIds ]
                )
            | ConclusionRecorded c ->
                let (ConclusionId id) = c.ConclusionId
                Ok(
                    "ConclusionRecorded",
                    [ "conclusionId", uuid id
                      "statement", text c.Statement
                      "strength", enum Enums.conclusionStrength c.Strength
                      "causeIds", ids cause c.CauseIds
                      "residualUncertainty", optText c.ResidualUncertainty ]
                )
            | ProposalRecorded p ->
                Ok("ProposalRecorded", [ "proposalId", uuid (prop p.ProposalId); "kind", enum Enums.proposalKind p.Kind; "text", text p.ProposalText; "rationale", optText p.Rationale ])
            | ProposalAccepted(id, target) -> Ok("ProposalWasAccepted", [ "proposalId", uuid (prop id); "targetId", uuid target ])
            | ProposalRejected(id, reason) -> Ok("ProposalWasRejected", [ "proposalId", uuid (prop id); "reason", text reason ])
            | ExtensionAttached _ -> Error(WireError "v1.0.0 defines no extension events (CAUSA.UNREPRESENTABLE)")

    let actor (a: Actor) : JsonValue =
        match a.Kind with
        | ActorKind.Human -> JObject [ "actorId", text a.ActorId; "kind", JString "human" ]
        | ActorKind.System -> JObject [ "actorId", text a.ActorId; "kind", JString "system" ]
        | ActorKind.Agent(provider, model) -> JObject [ "actorId", text a.ActorId; "kind", JString "agent"; "provider", text provider; "model", text model ]

    /// One line of events.ndjson (section 5). The event id is the section 5.2 derivation, whatever the envelope says.
    let envelope (e: EventEnvelope) : Result<JsonValue, WireError> =
        let (InvestigationId inv) = e.Investigation
        let (AggregateVersion v) = e.Version
        payload e.Payload
        |> Result.bind (fun (eventType, fields) ->
            ts e.Provenance.OccurredAt
            |> Result.map (fun at ->
                JObject
                    [ "formatVersion", JString e.Provenance.FormatVersion
                      "eventId", uuid (eventId inv v e.Provenance.CorrelationId)
                      "investigationId", uuid inv
                      "aggregateVersion", JInt v
                      "eventType", JString eventType
                      "occurredAt", at
                      "actor", actor e.Provenance.Actor
                      "semanticVersion", JString e.Provenance.SemanticVersion
                      "correlationId", uuid e.Provenance.CorrelationId
                      "payload", JObject fields ]))

    let all (events: EventEnvelope list) : Result<JsonValue list, WireError> =
        events
        |> List.fold (fun acc e -> acc |> Result.bind (fun xs -> envelope e |> Result.map (fun x -> x :: xs))) (Ok [])
        |> Result.map List.rev

/// CONTRACT section 8.3, the normative fold over wire events, and the section 8.1 snapshot and 9.1 manifest.
[<RequireQualifiedAccess>]
module Projection =
    type private Entity = Map<string, JsonValue>

    type private State =
        { Header: Map<string, JsonValue>
          Arrays: Map<string, Map<string, Entity>> }

    /// The 17 entity arrays of the state object (section 8.2).
    let entityArrays =
        [ "observations"; "hypotheses"; "evidence"; "relationships"; "unknowns"; "assumptions"; "contradictions"; "predictions"
          "actions"; "findings"; "negativeKnowledge"; "causes"; "correctiveActions"; "criteria"; "verifications"; "conclusions"
          "proposals" ]

    let private get (name: string) (o: JsonValue) = field name o |> Option.defaultValue JNull

    let private add (arrayName: string) (entity: (string * JsonValue) list) (provenance: JsonValue) (st: State) =
        let e = Map.ofList (("provenance", provenance) :: entity)
        let key =
            match e["id"] with
            | JString s -> s
            | other -> canonical other
        { st with Arrays = st.Arrays |> Map.add arrayName (st.Arrays[arrayName] |> Map.add key e) }

    let private update (arrayName: string) (idValue: JsonValue) (f: Entity -> Entity) (st: State) =
        match idValue with
        | JString key ->
            match Map.tryFind key st.Arrays[arrayName] with
            | Some e -> { st with Arrays = st.Arrays |> Map.add arrayName (st.Arrays[arrayName] |> Map.add key (f e)) }
            | None -> st
        | _ -> st

    let private set (k: string) (v: JsonValue) (e: Entity) = e |> Map.add k v

    let private apply (st: State) (ev: JsonValue) : State =
        let p = get "payload" ev
        let pf k = get k p
        let version = get "aggregateVersion" ev
        let occurredAt = get "occurredAt" ev
        let provenance = JObject [ "actor", get "actor" ev; "recordedAt", occurredAt ]
        let st = { st with Header = st.Header |> Map.add "version" version }
        let addE name fields = add name fields provenance st
        match str "eventType" ev |> Option.defaultValue "" with
        | "InvestigationStarted" -> st
        | "LifecycleChanged" -> { st with Header = st.Header |> Map.add "lifecycle" (pf "to") }
        | "ObservationRecorded" -> addE "observations" [ "id", pf "observationId"; "original", pf "original"; "observedAt", pf "observedAt" ]
        | "HypothesisRecorded" ->
            addE "hypotheses" [ "id", pf "hypothesisId"; "statement", pf "statement"; "mechanism", pf "mechanism"; "state", JString "proposed"; "history", JArray [] ]
        | "HypothesisTransitioned" ->
            let change =
                JObject
                    [ "from", pf "from"; "to", pf "to"; "command", pf "command"; "reason", pf "reason"; "evidenceIds", pf "evidenceIds"
                      "at", occurredAt; "aggregateVersion", version ]
            st
            |> update "hypotheses" (pf "hypothesisId") (fun h ->
                let history =
                    match h["history"] with
                    | JArray xs -> xs
                    | _ -> []
                h |> set "state" (pf "to") |> set "history" (JArray(history @ [ change ])))
        | "EvidenceAdmitted" ->
            addE
                "evidence"
                [ "id", pf "evidenceId"; "contentKind", pf "contentKind"; "content", pf "content"; "category", pf "category"
                  "source", pf "source"; "capturedAt", pf "capturedAt"; "verification", JString "unverified"; "supersededBy", JNull
                  "admittedAtVersion", version ]
        | "EvidenceSuperseded" -> st |> update "evidence" (pf "evidenceId") (set "supersededBy" (pf "replacementEvidenceId"))
        | "EvidenceVerificationAssessed" -> st |> update "evidence" (pf "evidenceId") (set "verification" (pf "verification"))
        | "EvidenceRelated" ->
            addE
                "relationships"
                [ "id", pf "relationshipId"; "evidenceId", pf "evidenceId"; "hypothesisId", pf "hypothesisId"; "kind", pf "kind"; "rationale", pf "rationale" ]
        | "UnknownIdentified" ->
            addE "unknowns" [ "id", pf "unknownId"; "question", pf "question"; "materiality", pf "materiality"; "state", JString "identified"; "basis", JNull ]
        | "UnknownTransitioned" -> st |> update "unknowns" (pf "unknownId") (set "state" (pf "state") >> set "basis" (pf "basis"))
        | "AssumptionRecorded" ->
            addE "assumptions" [ "id", pf "assumptionId"; "proposition", pf "proposition"; "materiality", pf "materiality"; "state", JString "explicitUnverified" ]
        | "AssumptionTransitioned" -> st |> update "assumptions" (pf "assumptionId") (set "state" (pf "state"))
        | "ContradictionRecorded" ->
            addE
                "contradictions"
                [ "id", pf "contradictionId"; "statement", pf "statement"; "evidenceIds", pf "evidenceIds"; "materiality", pf "materiality"
                  "state", JString "detected"; "basis", JNull ]
        | "ContradictionTransitioned" -> st |> update "contradictions" (pf "contradictionId") (set "state" (pf "state") >> set "basis" (pf "basis"))
        | "PredictionRecorded" ->
            addE
                "predictions"
                [ "id", pf "predictionId"; "hypothesisId", pf "hypothesisId"; "statement", pf "statement"; "strength", pf "strength"; "timing", pf "timing" ]
        | "ActionRecorded" ->
            addE "actions" [ "id", pf "actionId"; "kind", pf "kind"; "purpose", pf "purpose"; "targetHypothesisIds", pf "targetHypothesisIds"; "state", JString "proposed" ]
        | "ActionTransitioned" -> st |> update "actions" (pf "actionId") (set "state" (pf "state"))
        | "FindingRecorded" ->
            addE "findings" [ "id", pf "findingId"; "actionId", pf "actionId"; "originalResult", pf "originalResult"; "interpretation", pf "interpretation" ]
        | "NegativeKnowledgeEstablished" ->
            addE "negativeKnowledge" [ "id", pf "negativeKnowledgeId"; "statement", pf "statement"; "scope", pf "scope"; "basisEvidenceIds", pf "basisEvidenceIds" ]
        | "CausalAssertionRecorded" ->
            addE "causes" [ "id", pf "causeId"; "outcome", pf "outcome"; "members", pf "members"; "mechanism", pf "mechanism"; "state", JString "proposed" ]
        | "CausalAssertionConfirmed" -> st |> update "causes" (pf "causeId") (set "state" (JString "confirmed"))
        | "CausalAssertionReopened" -> st |> update "causes" (pf "causeId") (set "state" (JString "reopened"))
        | "CorrectiveActionRecorded" ->
            addE
                "correctiveActions"
                [ "id", pf "correctiveActionId"; "kind", pf "kind"; "description", pf "description"; "targetCauseIds", pf "targetCauseIds"
                  "state", JString "proposed"; "awaitingSinceVersion", JInt 0L ]
        | "CorrectiveActionTransitioned" ->
            st
            |> update "correctiveActions" (pf "correctiveActionId") (fun c ->
                let c = c |> set "state" (pf "state")
                if pf "state" = JString "awaitingVerificationOfEffect" then c |> set "awaitingSinceVersion" version else c)
        | "CriterionRecorded" ->
            addE "criteria" [ "id", pf "criterionId"; "correctiveActionId", pf "correctiveActionId"; "expectedOutcome", pf "expectedOutcome"; "method", pf "method" ]
        | "VerificationRecorded" ->
            addE
                "verifications"
                [ "id", pf "verificationId"; "criterionId", pf "criterionId"; "outcome", pf "outcome"; "evidenceIds", pf "evidenceIds"; "recordedAtVersion", version ]
        | "ConclusionRecorded" ->
            addE
                "conclusions"
                [ "id", pf "conclusionId"; "statement", pf "statement"; "strength", pf "strength"; "causeIds", pf "causeIds"
                  "residualUncertainty", pf "residualUncertainty"; "recordedAtVersion", version ]
        | "ProposalRecorded" ->
            addE "proposals" [ "id", pf "proposalId"; "kind", pf "kind"; "text", pf "text"; "rationale", pf "rationale"; "state", JString "active"; "decision", JNull ]
        | "ProposalWasAccepted" -> st |> update "proposals" (pf "proposalId") (set "state" (JString "accepted"))
        | "ProposalWasRejected" -> st |> update "proposals" (pf "proposalId") (set "state" (JString "rejected") >> set "decision" (pf "reason"))
        | _ -> st

    let private start (ev: JsonValue) : Result<State, string> =
        match str "eventType" ev with
        | Some "InvestigationStarted" ->
            let p = get "payload" ev
            let empty = entityArrays |> List.map (fun a -> a, Map.empty) |> Map.ofList
            let header =
                Map.ofList
                    [ "investigationId", get "investigationId" ev; "title", get "title" p; "lifecycle", JString "draft"
                      "version", get "aggregateVersion" ev; "createdAt", get "occurredAt" ev ]
            Ok(
                add "observations" [ "id", get "observationId" p; "original", get "original" p; "observedAt", JNull ]
                    (JObject [ "actor", get "actor" ev; "recordedAt", get "occurredAt" ev ])
                    { Header = header; Arrays = empty }
            )
        | _ -> Error "projection is undefined: the history does not begin with InvestigationStarted"

    let private toJson (st: State) : JsonValue =
        let arrays =
            entityArrays
            |> List.map (fun name ->
                let items =
                    st.Arrays[name]
                    |> Map.toList
                    |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
                    |> List.map (fun (_, e) -> JObject(Map.toList e))
                name, JArray items)
        JObject((st.Header |> Map.toList) @ arrays)

    /// The section 8.2 state object of a wire history (in aggregateVersion order).
    let state (events: JsonValue list) : Result<JsonValue, string> =
        match events with
        | [] -> Error "empty history"
        | first :: rest -> start first |> Result.map (fun st -> rest |> List.fold apply st |> toJson)

    /// The section 8.1 snapshot: generatedAt is the occurredAt of the last applied event.
    let snapshot (events: JsonValue list) : Result<JsonValue, string> =
        state events
        |> Result.map (fun s ->
            let last = List.last events
            JObject
                [ "formatVersion", JString "1.0.0"; "investigationId", get "investigationId" (List.head events)
                  "aggregateVersion", get "aggregateVersion" last; "projectionVersion", JInt 1L; "semanticVersion", JString "1.0.0"
                  "generatedAt", get "occurredAt" last; "state", s ])

    /// The section 9.1 manifest of a file set.
    let manifest (events: JsonValue list) (eventsBytes: byte[]) (snapshot: JsonValue) : JsonValue =
        let n = JInt(int64 events.Length)
        JObject
            [ "formatVersion", JString "1.0.0"; "investigationId", get "investigationId" (List.head events); "schemaVersion", JString "1.0.0"
              "semanticVersion", JString "1.0.0"; "aggregateVersion", n; "eventCount", n
              "eventsSha256", JString(sha256Hex eventsBytes); "snapshotVersion", JInt 1L
              "snapshotSha256", JString(digest snapshot); "domainPacks", JArray []
              "createdAt", get "occurredAt" (List.head events); "updatedAt", get "occurredAt" (List.last events) ]

/// The file sets a save writes (CONTRACT sections 9.1-9.4).
[<RequireQualifiedAccess>]
module FileSets =
    let private lines (events: JsonValue list) : byte[] =
        events |> List.map (fun e -> canonical e + "\n") |> String.concat "" |> Text.Encoding.UTF8.GetBytes

    /// The file set of a whole history.
    let ofHistory (history: EventEnvelope list) : Result<FileSet, WireError> =
        WireEvents.all history
        |> Result.bind (fun wire ->
            let events = lines wire
            Projection.snapshot wire
            |> Result.mapError WireError
            |> Result.map (fun snapshot ->
                { Manifest = fileOf (Projection.manifest wire events snapshot)
                  Events = events
                  Snapshot = fileOf snapshot }))

    /// The successor file set of an append (section 9.2/9.3): the stored events bytes are kept as they are and the new
    /// lines are appended; snapshot and manifest are rebuilt from the whole history.
    let append (base': FileSet) (history: EventEnvelope list) (appended: EventEnvelope list) : Result<FileSet, WireError> =
        WireEvents.all history
        |> Result.bind (fun before ->
            WireEvents.all appended
            |> Result.bind (fun added ->
                let events = Array.append base'.Events (lines added)
                let wire = before @ added
                Projection.snapshot wire
                |> Result.mapError WireError
                |> Result.map (fun snapshot ->
                    { Manifest = fileOf (Projection.manifest wire events snapshot)
                      Events = events
                      Snapshot = fileOf snapshot })))

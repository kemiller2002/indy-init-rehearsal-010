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

/// The wire-to-domain mapping (CONTRACT sections 5-7): every registered event decodes to its domain case or fails
/// closed with the field it cannot read.
[<RequireQualifiedAccess>]
module WireDecode =
    type private D<'T> = Result<'T, string>

    let private fieldOf (name: string) (o: JsonValue) : D<JsonValue> =
        match o with
        | JObject members ->
            match members |> List.tryFind (fst >> (=) name) with
            | Some(_, v) -> Ok v
            | None -> Error(sprintf "missing field %s" name)
        | _ -> Error(sprintf "expected an object holding %s" name)

    let private textOf (v: JsonValue) name : D<NonEmptyText> =
        match v with
        | JString s -> NonEmptyText.create s |> Result.mapError (fun e -> sprintf "%s: %s" name e)
        | _ -> Error(sprintf "%s is not a string" name)

    let private text (name: string) (o: JsonValue) = fieldOf name o |> Result.bind (fun v -> textOf v name)

    let private optText (name: string) (o: JsonValue) : D<NonEmptyText option> =
        fieldOf name o
        |> Result.bind (function
            | JNull -> Ok None
            | v -> textOf v name |> Result.map Some)

    let private uuidRegex = Text.RegularExpressions.Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\z")

    let private guidOf (v: JsonValue) name : D<Guid> =
        match v with
        | JString s when uuidRegex.IsMatch s && s <> "00000000-0000-0000-0000-000000000000" -> Ok(Guid s)
        | _ -> Error(sprintf "%s is not a canonical non-nil UUID" name)

    let private guid (name: string) (o: JsonValue) = fieldOf name o |> Result.bind (fun v -> guidOf v name)

    let private guids (name: string) (o: JsonValue) : D<Guid list> =
        fieldOf name o
        |> Result.bind (function
            | JArray xs ->
                xs |> List.fold (fun acc x -> acc |> Result.bind (fun l -> guidOf x name |> Result.map (fun g -> g :: l))) (Ok []) |> Result.map List.rev
            | _ -> Error(sprintf "%s is not an array" name))

    let private enumOf (e: WireEnum<'T>) (name: string) (o: JsonValue) : D<'T> =
        match fieldOf name o with
        | Ok(JString s) ->
            match WireEnum.tryParse e s with
            | Some v -> Ok v
            | None -> Error(sprintf "%s: %s is not a %s" name s e.Name)
        | Ok _ -> Error(sprintf "%s is not a string" name)
        | Error e -> Error e

    let private optTime (name: string) (o: JsonValue) : D<DateTimeOffset option> =
        match fieldOf name o with
        | Ok JNull -> Ok None
        | Ok(JString s) ->
            match parseTimestamp s with
            | Some t -> Ok(Some t)
            | None -> Error(sprintf "%s is not a millisecond UTC timestamp" name)
        | Ok _ -> Error(sprintf "%s is not a timestamp" name)
        | Error e -> Error e

    let private str (name: string) (o: JsonValue) : D<string> =
        match fieldOf name o with
        | Ok(JString s) -> Ok s
        | Ok _ -> Error(sprintf "%s is not a string" name)
        | Error e -> Error e

    type private ResultBuilder() =
        member _.Bind(r: Result<'a, 'e>, f: 'a -> Result<'b, 'e>) = Result.bind f r
        member _.Return(v) = Ok v
        member _.ReturnFrom(r) = r

    let private res = ResultBuilder()

    let private hypothesisTransition (p: JsonValue) : D<HypothesisTransition> =
        res {
            let! id = guid "hypothesisId" p
            let! from = enumOf Enums.hypothesisState "from" p
            let! reason = text "reason" p
            let! evidence = guids "evidenceIds" p
            return { HypothesisId = HypothesisId id; From = from; Reason = reason; EvidenceIds = evidence |> List.map EvidenceId }
        }

    /// The domain event of one wire payload.
    let payload (eventType: string) (p: JsonValue) : D<CoreEvent> =
        match eventType with
        | "InvestigationStarted" ->
            res {
                let! title = text "title" p
                let! obs = guid "observationId" p
                let! original = text "original" p
                return InvestigationStarted { Title = title; ObservationId = ObservationId obs; Original = original }
            }
        | "LifecycleChanged" ->
            res {
                let! from = enumOf Enums.lifecycle "from" p
                let! command = str "command" p
                let! reason = optText "reason" p
                let need () =
                    match reason with
                    | Some r -> Ok r
                    | None -> Error "reason is required for this command"
                match command with
                | "activateInvestigation" -> return InvestigationActivated from
                | "markAwaitingEvidence" ->
                    let! r = need ()
                    return InvestigationMarkedAwaitingEvidence(from, r)
                | "markAwaitingVerification" -> return InvestigationMarkedAwaitingVerification from
                | "resolveInvestigation" -> return InvestigationResolved from
                | "closeInvestigation" -> return InvestigationClosed from
                | "reopenInvestigation" ->
                    let! r = need ()
                    return InvestigationReopened(from, r)
                | "verificationFailed" ->
                    let! r = need ()
                    return InvestigationVerificationFailed r
                | other -> return! Error(sprintf "command %s is not a lifecycleCommand" other)
            }
        | "ObservationRecorded" ->
            res {
                let! id = guid "observationId" p
                let! original = text "original" p
                let! at = optTime "observedAt" p
                return ObservationRecorded { ObservationId = ObservationId id; Original = original; ObservedAt = at }
            }
        | "HypothesisRecorded" ->
            res {
                let! id = guid "hypothesisId" p
                let! statement = text "statement" p
                let! mechanism = optText "mechanism" p
                return HypothesisRecorded { HypothesisId = HypothesisId id; Statement = statement; Mechanism = mechanism }
            }
        | "HypothesisTransitioned" ->
            res {
                let! t = hypothesisTransition p
                let! command = str "command" p
                match command with
                | "markPlausible" -> return HypothesisMarkedPlausible t
                | "markSupported" -> return HypothesisMarkedSupported t
                | "markWeakened" -> return HypothesisWeakened t
                | "falsify" -> return HypothesisFalsified t
                | "supersede" -> return HypothesisSuperseded t
                | "reopen" -> return HypothesisReopened t
                | other -> return! Error(sprintf "command %s is not a hypothesisCommand" other)
            }
        | "EvidenceAdmitted" ->
            res {
                let! id = guid "evidenceId" p
                let! kind = enumOf Enums.contentKind "contentKind" p
                let! content = text "content" p
                let! category = enumOf Enums.evidenceCategory "category" p
                let! source = text "source" p
                let! at = optTime "capturedAt" p
                return
                    EvidenceAdmitted
                        { EvidenceId = EvidenceId id; ContentKind = kind; Content = content; Category = category; Source = source; CapturedAt = at }
            }
        | "EvidenceSuperseded" ->
            res {
                let! id = guid "evidenceId" p
                let! replacement = guid "replacementEvidenceId" p
                let! reason = text "reason" p
                return EvidenceSuperseded { EvidenceId = EvidenceId id; ReplacementEvidenceId = EvidenceId replacement; Reason = reason }
            }
        | "EvidenceVerificationAssessed" ->
            res {
                let! id = guid "evidenceId" p
                let! v = enumOf Enums.verificationStatus "verification" p
                return EvidenceVerificationAssessed { EvidenceId = EvidenceId id; Verification = v }
            }
        | "EvidenceRelated" ->
            res {
                let! id = guid "relationshipId" p
                let! e = guid "evidenceId" p
                let! h = guid "hypothesisId" p
                let! kind = enumOf Enums.relationshipKind "kind" p
                let! rationale = optText "rationale" p
                return
                    EvidenceRelationshipRecorded
                        { RelationshipId = EvidenceRelationshipId id; EvidenceId = EvidenceId e; HypothesisId = HypothesisId h; Kind = kind; Rationale = rationale }
            }
        | "UnknownIdentified" ->
            res {
                let! id = guid "unknownId" p
                let! q = text "question" p
                let! m = enumOf Enums.materiality "materiality" p
                return UnknownIdentified { UnknownId = UnknownId id; Question = q; Materiality = m }
            }
        | "UnknownTransitioned" ->
            res {
                let! id = guid "unknownId" p
                let! s = enumOf Enums.unknownState "state" p
                let! b = text "basis" p
                return UnknownTransitioned { UnknownId = UnknownId id; State = s; Basis = b }
            }
        | "AssumptionRecorded" ->
            res {
                let! id = guid "assumptionId" p
                let! prop = text "proposition" p
                let! m = enumOf Enums.materiality "materiality" p
                return AssumptionRecorded { AssumptionId = AssumptionId id; Proposition = prop; Materiality = m }
            }
        | "AssumptionTransitioned" ->
            res {
                let! id = guid "assumptionId" p
                let! s = enumOf Enums.assumptionState "state" p
                return AssumptionTransitioned { AssumptionId = AssumptionId id; State = s }
            }
        | "ContradictionRecorded" ->
            res {
                let! id = guid "contradictionId" p
                let! st = text "statement" p
                let! ev = guids "evidenceIds" p
                let! m = enumOf Enums.materiality "materiality" p
                return ContradictionRecorded { ContradictionId = ContradictionId id; Statement = st; EvidenceIds = ev |> List.map EvidenceId; Materiality = m }
            }
        | "ContradictionTransitioned" ->
            res {
                let! id = guid "contradictionId" p
                let! s = enumOf Enums.contradictionState "state" p
                let! b = text "basis" p
                return ContradictionTransitioned { ContradictionId = ContradictionId id; State = s; Basis = b }
            }
        | "PredictionRecorded" ->
            res {
                let! id = guid "predictionId" p
                let! h = guid "hypothesisId" p
                let! st = text "statement" p
                let! strength = enumOf Enums.predictionStrength "strength" p
                let! timing = enumOf Enums.predictionTiming "timing" p
                return
                    PredictionRecorded
                        { PredictionId = PredictionId id; HypothesisId = HypothesisId h; Statement = st; Strength = strength; Timing = timing }
            }
        | "ActionRecorded" ->
            res {
                let! id = guid "actionId" p
                let! kind = enumOf Enums.actionKind "kind" p
                let! purpose = text "purpose" p
                let! targets = guids "targetHypothesisIds" p
                return
                    InvestigationActionRecorded
                        { ActionId = InvestigationActionId id; Kind = kind; Purpose = purpose; TargetHypothesisIds = targets |> List.map HypothesisId }
            }
        | "ActionTransitioned" ->
            res {
                let! id = guid "actionId" p
                let! s = enumOf Enums.actionState "state" p
                return InvestigationActionTransitioned { ActionId = InvestigationActionId id; State = s }
            }
        | "FindingRecorded" ->
            res {
                let! id = guid "findingId" p
                let! a = guid "actionId" p
                let! r = text "originalResult" p
                let! i = optText "interpretation" p
                return FindingRecorded { FindingId = FindingId id; ActionId = InvestigationActionId a; OriginalResult = r; Interpretation = i }
            }
        | "NegativeKnowledgeEstablished" ->
            res {
                let! id = guid "negativeKnowledgeId" p
                let! st = text "statement" p
                let! scope = text "scope" p
                let! basis = guids "basisEvidenceIds" p
                return
                    NegativeKnowledgeEstablished
                        { NegativeKnowledgeId = NegativeKnowledgeId id; Statement = st; Scope = scope; BasisEvidenceIds = basis |> List.map EvidenceId }
            }
        | "CausalAssertionRecorded" ->
            res {
                let! id = guid "causeId" p
                let! outcome = text "outcome" p
                let! members = fieldOf "members" p
                let! mechanism = optText "mechanism" p
                let! ms =
                    match members with
                    | JArray xs ->
                        xs
                        |> List.fold
                            (fun acc m ->
                                acc
                                |> Result.bind (fun l ->
                                    res {
                                        let! h = guid "hypothesisId" m
                                        let! role = enumOf Enums.causeRole "role" m
                                        return ({ HypothesisId = HypothesisId h; Role = role }: CausalMember) :: l
                                    }))
                            (Ok [])
                        |> Result.map List.rev
                    | _ -> Error "members is not an array"
                return CausalAssertionRecorded { CauseId = CausalAssertionId id; Outcome = outcome; Members = ms; Mechanism = mechanism }
            }
        | "CausalAssertionConfirmed" ->
            res {
                let! id = guid "causeId" p
                let! policy = text "policy" p
                return CauseConfirmed { CauseId = CausalAssertionId id; Policy = policy }
            }
        | "CausalAssertionReopened" ->
            res {
                let! id = guid "causeId" p
                let! reason = text "reason" p
                return ConfirmedCauseReopened(CausalAssertionId id, reason)
            }
        | "CorrectiveActionRecorded" ->
            res {
                let! id = guid "correctiveActionId" p
                let! kind = enumOf Enums.correctiveActionKind "kind" p
                let! d = text "description" p
                let! targets = guids "targetCauseIds" p
                return
                    CorrectiveActionRecorded
                        { CorrectiveActionId = CorrectiveActionId id; Kind = kind; Description = d; TargetCauseIds = targets |> List.map CausalAssertionId }
            }
        | "CorrectiveActionTransitioned" ->
            res {
                let! g = guid "correctiveActionId" p
                let id = CorrectiveActionId g
                let! s = enumOf Enums.correctiveActionState "state" p
                match s with
                | CorrectiveActionState.Approved -> return CorrectiveActionApproved id
                | CorrectiveActionState.InProgress -> return CorrectiveActionStarted id
                | CorrectiveActionState.Implemented -> return CorrectiveActionImplemented id
                | CorrectiveActionState.AwaitingVerificationOfEffect -> return CorrectiveActionAwaitingVerificationOfEffect id
                | CorrectiveActionState.VerifiedEffective -> return CorrectiveActionVerifiedEffective id
                | CorrectiveActionState.VerificationFailed -> return CorrectiveActionVerificationFailed id
                | CorrectiveActionState.ReworkRequired -> return CorrectiveActionReworkRequired id
                | CorrectiveActionState.Cancelled -> return CorrectiveActionCancelled id
                | CorrectiveActionState.Proposed -> return! Error "proposed is not a correctiveActionTarget"
            }
        | "CriterionRecorded" ->
            res {
                let! id = guid "criterionId" p
                let! c = guid "correctiveActionId" p
                let! e = text "expectedOutcome" p
                let! m = optText "method" p
                return
                    VerificationCriterionRecorded
                        { CriterionId = VerificationCriterionId id; CorrectiveActionId = CorrectiveActionId c; ExpectedOutcome = e; Method = m }
            }
        | "VerificationRecorded" ->
            res {
                let! id = guid "verificationId" p
                let! c = guid "criterionId" p
                let! o = enumOf Enums.verificationOutcome "outcome" p
                let! ev = guids "evidenceIds" p
                return
                    VerificationRecorded
                        { VerificationId = VerificationId id; CriterionId = VerificationCriterionId c; Outcome = o; EvidenceIds = ev |> List.map EvidenceId }
            }
        | "ConclusionRecorded" ->
            res {
                let! id = guid "conclusionId" p
                let! st = text "statement" p
                let! strength = enumOf Enums.conclusionStrength "strength" p
                let! causes = guids "causeIds" p
                let! residual = optText "residualUncertainty" p
                return
                    ConclusionRecorded
                        { ConclusionId = ConclusionId id
                          Statement = st
                          Strength = strength
                          CauseIds = causes |> List.map CausalAssertionId
                          ResidualUncertainty = residual }
            }
        | "ProposalRecorded" ->
            res {
                let! id = guid "proposalId" p
                let! kind = enumOf Enums.proposalKind "kind" p
                let! t = text "text" p
                let! r = optText "rationale" p
                return ProposalRecorded { ProposalId = ProposalId id; Kind = kind; ProposalText = t; Rationale = r }
            }
        | "ProposalWasAccepted" ->
            res {
                let! id = guid "proposalId" p
                let! target = guid "targetId" p
                return ProposalAccepted(ProposalId id, target)
            }
        | "ProposalWasRejected" ->
            res {
                let! id = guid "proposalId" p
                let! reason = text "reason" p
                return ProposalRejected(ProposalId id, reason)
            }
        | other -> Error(sprintf "unregistered eventType %s" other)

    let actor (a: JsonValue) : D<Actor> =
        res {
            let! id = text "actorId" a
            let! kind = str "kind" a
            match kind with
            | "human" -> return { ActorId = id; Kind = ActorKind.Human }
            | "system" -> return { ActorId = id; Kind = ActorKind.System }
            | "agent" ->
                let! provider = text "provider" a
                let! model = text "model" a
                return { ActorId = id; Kind = ActorKind.Agent(provider, model) }
            | other -> return! Error(sprintf "actor kind %s" other)
        }

    /// The domain envelope of one events.ndjson line.
    let envelope (line: JsonValue) : D<EventEnvelope> =
        res {
            let! eventId = guid "eventId" line
            let! inv = guid "investigationId" line
            let! version =
                match fieldOf "aggregateVersion" line with
                | Ok(JInt v) when v >= 1L -> Ok v
                | _ -> Error "aggregateVersion is not a positive integer"
            let! eventType = str "eventType" line
            let! occurredAt =
                str "occurredAt" line
                |> Result.bind (fun s -> parseTimestamp s |> Option.map Ok |> Option.defaultValue (Error "occurredAt is not a timestamp"))
            let! actorJson = fieldOf "actor" line
            let! a = actor actorJson
            let! correlation = guid "correlationId" line
            let! formatVersion = str "formatVersion" line
            let! semanticVersion = str "semanticVersion" line
            let! p = fieldOf "payload" line
            let! event = payload eventType p
            return
                { EventId = EventId eventId
                  Investigation = InvestigationId inv
                  Version = AggregateVersion version
                  Provenance =
                    { OccurredAt = occurredAt
                      Actor = a
                      CorrelationId = correlation
                      FormatVersion = formatVersion
                      SemanticVersion = semanticVersion }
                  Payload = CoreEvent event }
        }

/// The CONTRACT section 11.1 reader: an investigation directory's file set is verified stage by stage and the first
/// failing stage is reported; a verified file set yields the replayed investigation (fail closed, section 11.2).
[<RequireQualifiedAccess>]
module StorageReader =
    let private registered =
        set
            [ "InvestigationStarted"; "LifecycleChanged"; "ObservationRecorded"; "HypothesisRecorded"; "HypothesisTransitioned"
              "EvidenceAdmitted"; "EvidenceSuperseded"; "EvidenceVerificationAssessed"; "EvidenceRelated"; "UnknownIdentified"
              "UnknownTransitioned"; "AssumptionRecorded"; "AssumptionTransitioned"; "ContradictionRecorded"; "ContradictionTransitioned"
              "PredictionRecorded"; "ActionRecorded"; "ActionTransitioned"; "FindingRecorded"; "NegativeKnowledgeEstablished"
              "CausalAssertionRecorded"; "CausalAssertionConfirmed"; "CausalAssertionReopened"; "CorrectiveActionRecorded"
              "CorrectiveActionTransitioned"; "CriterionRecorded"; "VerificationRecorded"; "ConclusionRecorded"; "ProposalRecorded"
              "ProposalWasAccepted"; "ProposalWasRejected" ]

    let private envelopeKeys =
        set [ "formatVersion"; "eventId"; "investigationId"; "aggregateVersion"; "eventType"; "occurredAt"; "actor"; "semanticVersion"; "correlationId"; "payload" ]

    let private manifestKeys =
        set
            [ "aggregateVersion"; "createdAt"; "domainPacks"; "eventCount"; "eventsSha256"; "formatVersion"; "investigationId"; "schemaVersion"
              "semanticVersion"; "snapshotSha256"; "snapshotVersion"; "updatedAt" ]

    let private snapshotKeys =
        set [ "aggregateVersion"; "formatVersion"; "generatedAt"; "investigationId"; "projectionVersion"; "semanticVersion"; "state" ]

    let private stage code file line detail = VerificationFailure.ReaderStage(code, file, line, detail)

    let private utf8 = Text.UTF8Encoding(false, true)

    let private decodeText (file: string) (bytes: byte[]) : Result<string, VerificationFailure> =
        if bytes.Length = 0 then Error(stage "CAUSA.ENCODING" file None "empty file")
        elif bytes.Length >= 3 && bytes[0] = 0xEFuy && bytes[1] = 0xBBuy && bytes[2] = 0xBFuy then Error(stage "CAUSA.ENCODING" file None "byte order mark")
        else
            try
                let s = utf8.GetString bytes
                if s.Contains '\r' then Error(stage "CAUSA.ENCODING" file None "CR in file")
                elif not (s.EndsWith "\n") then Error(stage "CAUSA.ENCODING" file None "missing final LF")
                else Ok s
            with _ -> Error(stage "CAUSA.ENCODING" file None "invalid UTF-8")

    let private keysOf (v: JsonValue) =
        match v with
        | JObject m -> m |> List.map fst |> set
        | _ -> Set.empty

    let private versionsOk (v: JsonValue) (file: string) (line: int option) =
        [ "formatVersion"; "schemaVersion"; "semanticVersion" ]
        |> List.tryPick (fun k ->
            match field k v with
            | Some(JString s) when s <> "1.0.0" -> Some(stage "CAUSA.UNSUPPORTED_VERSION" file line (sprintf "%s %s" k s))
            | _ -> None)

    /// Verifies one investigation's file set read at `commit` from `address`, for the investigation `expected` names
    /// (when given). Stages 1-14, 16 and 17 are this reader's; stage 15 (admissibility) is the domain kernel's replay.
    let verify
        (kernel: DomainKernel)
        (expected: InvestigationId option)
        (address: WorkspaceAddress.T)
        (commit: CommitOid)
        (files: FileSet)
        : Result<VerifiedInvestigation, VerificationFailure list> =
        let fail f = Error [ f ]
        let parseFile file text =
            match parse (text: string).[.. text.Length - 2] with
            | Ok v -> Ok v
            | Error e -> Error(stage "CAUSA.JSON_MALFORMED" file None e)
        match decodeText "manifest.json" files.Manifest, decodeText "events.ndjson" files.Events, decodeText "snapshot.json" files.Snapshot with
        | Error e, _, _
        | _, Error e, _
        | _, _, Error e -> fail e
        | Ok manifestText, Ok eventsText, Ok snapshotText ->
            let lines = eventsText.[.. eventsText.Length - 2].Split('\n') |> List.ofArray
            if lines |> List.exists ((=) "") then fail (stage "CAUSA.ENCODING" "events.ndjson" None "blank line")
            else
            match parseFile "manifest.json" manifestText, parseFile "snapshot.json" snapshotText with
            | Error e, _
            | _, Error e -> fail e
            | Ok manifest, Ok snapshot ->
            let parsedLines =
                lines |> List.mapi (fun i l -> i + 1, l, parse l)
            match parsedLines |> List.tryPick (fun (i, _, r) -> match r with Error e -> Some(i, e) | Ok _ -> None) with
            | Some(i, e) -> fail (stage "CAUSA.JSON_MALFORMED" "events.ndjson" (Some i) e)
            | None ->
            let events = parsedLines |> List.map (fun (i, l, r) -> i, l, (match r with Ok v -> v | Error _ -> JNull))
            // Stage 4: canonical bytes.
            let nonCanonical =
                if canonical manifest + "\n" <> manifestText then Some(stage "CAUSA.NON_CANONICAL" "manifest.json" None "not RFC 8785")
                elif canonical snapshot + "\n" <> snapshotText then Some(stage "CAUSA.NON_CANONICAL" "snapshot.json" None "not RFC 8785")
                else events |> List.tryPick (fun (i, l, v) -> if canonical v <> l then Some(stage "CAUSA.NON_CANONICAL" "events.ndjson" (Some i) "not RFC 8785") else None)
            match nonCanonical with
            | Some f -> fail f
            | None ->
            // Stage 5: versions.
            let unsupported =
                versionsOk manifest "manifest.json" None
                |> Option.orElse (versionsOk snapshot "snapshot.json" None)
                |> Option.orElse (events |> List.tryPick (fun (i, _, v) -> versionsOk v "events.ndjson" (Some i)))
            match unsupported with
            | Some f -> fail f
            | None ->
            // Stage 6: registered event types.
            let unknownType =
                events
                |> List.tryPick (fun (i, _, v) ->
                    match field "eventType" v with
                    | Some(JString t) when not (registered.Contains t) -> Some(stage "CAUSA.UNKNOWN_EVENT_TYPE" "events.ndjson" (Some i) t)
                    | _ -> None)
            match unknownType with
            | Some f -> fail f
            | None ->
            // Stage 7: closed structures and typed decoding (each event decodes to its domain case).
            let schema =
                let badTime (v: JsonValue) k = (str k v |> Option.bind parseTimestamp).IsNone
                if keysOf manifest <> manifestKeys then Some(stage "CAUSA.SCHEMA_VIOLATION" "manifest.json" None "manifest fields")
                elif badTime manifest "createdAt" || badTime manifest "updatedAt" then Some(stage "CAUSA.SCHEMA_VIOLATION" "manifest.json" None "timestamp")
                elif badTime snapshot "generatedAt" then Some(stage "CAUSA.SCHEMA_VIOLATION" "snapshot.json" None "timestamp")
                elif keysOf snapshot <> snapshotKeys then Some(stage "CAUSA.SCHEMA_VIOLATION" "snapshot.json" None "snapshot fields")
                else
                    events
                    |> List.tryPick (fun (i, _, v) ->
                        if keysOf v <> envelopeKeys then Some(stage "CAUSA.SCHEMA_VIOLATION" "events.ndjson" (Some i) "envelope fields")
                        else
                            match WireDecode.envelope v with
                            | Error e -> Some(stage "CAUSA.SCHEMA_VIOLATION" "events.ndjson" (Some i) e)
                            | Ok decoded when
                                (match decoded.Provenance.Actor.Kind with ActorKind.Agent _ -> true | _ -> false)
                                && str "eventType" v <> Some "ProposalRecorded" ->
                                Some(stage "CAUSA.SCHEMA_VIOLATION" "events.ndjson" (Some i) "an agent actor may record only proposals")
                            | Ok { Payload = CoreEvent(EvidenceAdmitted ev) } when
                                ev.ContentKind = ContentKind.Artifact
                                && not (ev.Content.Value.StartsWith "http://" || ev.Content.Value.StartsWith "https://") ->
                                Some(stage "CAUSA.SCHEMA_VIOLATION" "events.ndjson" (Some i) "an artifact is referenced by an absolute http(s) URL")
                            | Ok decoded ->
                                // Closed payloads: re-encoding must give exactly the stored payload fields.
                                match WireEvents.envelope decoded with
                                | Ok again when keysOf (field "payload" again |> Option.defaultValue JNull) = keysOf (field "payload" v |> Option.defaultValue JNull) -> None
                                | _ -> Some(stage "CAUSA.SCHEMA_VIOLATION" "events.ndjson" (Some i) "payload fields"))
            match schema with
            | Some f -> fail f
            | None ->
            let decoded = events |> List.map (fun (_, _, v) -> match WireDecode.envelope v with Ok e -> e | Error e -> failwith e)
            let n = int64 events.Length
            let firstAt = events |> List.head |> fun (_, _, v) -> field "occurredAt" v
            let lastAt = events |> List.last |> fun (_, _, v) -> field "occurredAt" v
            let manifestId = str "investigationId" manifest
            // Stage 8: manifest.
            let mismatch =
                if integer "aggregateVersion" manifest <> Some n || integer "eventCount" manifest <> Some n then Some "counts"
                elif field "createdAt" manifest <> firstAt || field "updatedAt" manifest <> lastAt then Some "created/updated times"
                else
                    match expected with
                    | Some(InvestigationId g) when manifestId <> Some(uuidText g) -> Some "investigationId differs from the directory"
                    | _ -> None
            match mismatch with
            | Some m -> fail (stage "CAUSA.MANIFEST_MISMATCH" "manifest.json" None m)
            | None ->
            // Stage 9: events digest.
            if str "eventsSha256" manifest <> Some(sha256Hex files.Events) then fail (stage "CAUSA.EVENTS_HASH_MISMATCH" "manifest.json" None "eventsSha256")
            else
            // Stage 10: one investigation.
            match events |> List.tryFind (fun (_, _, v) -> str "investigationId" v <> manifestId) with
            | Some(i, _, _) -> fail (stage "CAUSA.INVESTIGATION_MISMATCH" "events.ndjson" (Some i) "another investigation")
            | None ->
            // Stage 11: unique event ids.
            let ids = decoded |> List.map _.EventId
            if (List.distinct ids).Length <> ids.Length then fail (stage "CAUSA.EVENT_ID_DUPLICATE" "events.ndjson" None "duplicate eventId")
            else
            // Stage 12: versions 1..N.
            match decoded |> List.indexed |> List.tryFind (fun (i, e) -> e.Version <> AggregateVersion(int64 (i + 1))) with
            | Some(i, _) -> fail (stage "CAUSA.SEQUENCE_INVALID" "events.ndjson" (Some(i + 1)) "aggregateVersion")
            | None ->
            // Stage 13: event id derivation.
            let derived (e: EventEnvelope) =
                let (InvestigationId inv) = e.Investigation
                let (AggregateVersion v) = e.Version
                EventId(eventId inv v e.Provenance.CorrelationId)
            match decoded |> List.indexed |> List.tryFind (fun (_, e) -> e.EventId <> derived e) with
            | Some(i, _) -> fail (stage "CAUSA.EVENT_ID_MISMATCH" "events.ndjson" (Some(i + 1)) "eventId derivation")
            | None ->
            // Stage 14: a correlation id names one group.
            let groups =
                decoded
                |> List.fold
                    (fun (acc: Guid list) e ->
                        match acc with
                        | last :: _ when last = e.Provenance.CorrelationId -> acc
                        | _ -> e.Provenance.CorrelationId :: acc)
                    []
            if (List.distinct groups).Length <> groups.Length then fail (stage "CAUSA.CORRELATION_REUSED" "events.ndjson" None "correlationId")
            else
            // Stage 15: admissibility (the domain kernel's replay).
            match kernel.Replay decoded with
            | Error e -> Error [ VerificationFailure.ReplayRejected e; stage "CAUSA.HISTORY_INADMISSIBLE" "events.ndjson" None (sprintf "%A" e) ]
            | Ok investigation ->
            // Stage 16: snapshot digest.
            if str "snapshotSha256" manifest <> Some(digest snapshot) then fail (stage "CAUSA.SNAPSHOT_HASH_MISMATCH" "manifest.json" None "snapshotSha256")
            else
            // Stage 17: snapshot equals the projection of the replayed history.
            match Projection.snapshot (events |> List.map (fun (_, _, v) -> v)) with
            | Ok projected when canonical projected = canonical snapshot ->
                Ok { Investigation = investigation; History = decoded; ReadAt = commit; Address = address; Bytes = files }
            | Ok _ -> fail (stage "CAUSA.SNAPSHOT_MISMATCH" "snapshot.json" None "snapshot differs from the projection of the history")
            | Error e -> fail (stage "CAUSA.SNAPSHOT_MISMATCH" "snapshot.json" None e)

    /// The IF41-STORAGE codec over the domain kernel.
    let codec (kernel: DomainKernel) : StorageCodec =
        { PathOf = fun (Workspace w) (InvestigationId g) -> RepoPath(sprintf "%s/investigations/%s" w (uuidText g))
          Encode =
            fun basis appended ->
                match FileSets.append basis.Bytes basis.History appended with
                | Ok fs -> fs
                | Error(WireError e) -> failwithf "CAUSA.UNREPRESENTABLE: %s" e
          EncodeNew =
            fun id history ->
                match FileSets.ofHistory history with
                | Error(WireError e) -> Error [ VerificationFailure.ReaderStage("CAUSA.UNREPRESENTABLE", "events.ndjson", None, e) ]
                | Ok fs ->
                    // Only a file set that verifies is ever sent.
                    let probe = WorkspaceAddress.catalog (Workspace "probe")
                    verify kernel (Some id) probe (CommitOid "unsaved") fs |> Result.map (fun _ -> fs)
          Verify = fun address commit files -> verify kernel None address commit files }

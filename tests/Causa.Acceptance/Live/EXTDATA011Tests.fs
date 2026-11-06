// Owner: domain. EXT-DATA-011 (live): causa-data's catalog histories are read from GitHub at the pinned commit, never
// vendored (O1), verified against the digests recorded in provenance.json, decoded and replayed through the kernel;
// incompatible data fails closed (O2). The codec is a copy of the test-side codec in
// tests/Causa.Contract.Tests/Domain/Producer.fs (CausaWire): Causa.Acceptance cannot reference that project.
namespace Causa.Acceptance.Live

open System
open System.Globalization
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open Xunit
open Causa.Domain

/// Reads one events.ndjson line into the domain envelope and writes it back in canonical form (RFC 8785: keys sorted,
/// no whitespace, minimal escaping). Decoding then encoding a stored line must reproduce it byte for byte.
module CausaWire =
    type J =
        | JStr of string
        | JNum of int64
        | JNull
        | JBool of bool
        | JArr of J list
        | JObj of (string * J) list

    let rec private ofElement (e: JsonElement) : J =
        match e.ValueKind with
        | JsonValueKind.String -> JStr(e.GetString() |> nonNull)
        | JsonValueKind.Number -> JNum(e.GetInt64())
        | JsonValueKind.Null -> JNull
        | JsonValueKind.True -> JBool true
        | JsonValueKind.False -> JBool false
        | JsonValueKind.Array -> JArr [ for x in e.EnumerateArray() -> ofElement x ]
        | JsonValueKind.Object -> JObj [ for p in e.EnumerateObject() -> p.Name, ofElement p.Value ]
        | k -> failwithf "unexpected JSON %A" k

    let parse (text: string) : J =
        use d = JsonDocument.Parse(text)
        ofElement d.RootElement

    let private escape (s: string) =
        let b = StringBuilder()
        for c in s do
            match c with
            | '"' -> b.Append("\\\"") |> ignore
            | '\\' -> b.Append("\\\\") |> ignore
            | '\b' -> b.Append("\\b") |> ignore
            | '\f' -> b.Append("\\f") |> ignore
            | '\n' -> b.Append("\\n") |> ignore
            | '\r' -> b.Append("\\r") |> ignore
            | '\t' -> b.Append("\\t") |> ignore
            | c when c < ' ' -> b.Append(sprintf "\\u%04x" (int c)) |> ignore
            | c -> b.Append(c) |> ignore
        b.ToString()

    let rec canonical (j: J) : string =
        match j with
        | JStr s -> "\"" + escape s + "\""
        | JNum n -> n.ToString(CultureInfo.InvariantCulture)
        | JNull -> "null"
        | JBool v -> if v then "true" else "false"
        | JArr xs -> "[" + String.Join(",", xs |> List.map canonical) + "]"
        | JObj ps ->
            let sorted = ps |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
            "{" + String.Join(",", sorted |> List.map (fun (k, v) -> "\"" + escape k + "\":" + canonical v)) + "}"

    let field (o: J) (name: string) : J =
        match o with
        | JObj ps ->
            match ps |> List.tryFind (fst >> (=) name) with
            | Some(_, v) -> v
            | None -> failwithf "missing field %s" name
        | _ -> failwithf "not an object reading %s" name

    let str o n = match field o n with JStr s -> s | j -> failwithf "%s is not a string: %A" n j
    let private text o n = match NonEmptyText.create (str o n) with Ok t -> t | Error e -> failwithf "%s: %s" n e
    let private optText o n = match field o n with JNull -> None | JStr s -> Some(match NonEmptyText.create s with Ok t -> t | Error e -> failwith e) | j -> failwithf "%A" j
    let private guid o n = Guid.ParseExact(str o n, "D")
    let private guids o n = match field o n with JArr xs -> xs |> List.map (function JStr s -> Guid.ParseExact(s, "D") | j -> failwithf "%A" j) | j -> failwithf "%A" j
    let private timeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"
    let private time (s: string) = DateTimeOffset.ParseExact(s, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal)
    let private optTime o n = match field o n with JNull -> None | JStr s -> Some(time s) | j -> failwithf "%A" j
    let private enum (e: WireEnum<'T>) o n =
        match WireEnum.tryParse e (str o n) with
        | Some v -> v
        | None -> failwithf "%s: '%s' is not a %s value" n (str o n) e.Name

    let private ts (d: DateTimeOffset) = JStr(d.ToUniversalTime().ToString(timeFormat, CultureInfo.InvariantCulture))
    let private g (x: Guid) = JStr(Ids.text x)
    let private gs (xs: Guid list) = JArr(xs |> List.map g)
    let private tx (t: NonEmptyText) = JStr t.Value
    let private otx (t: NonEmptyText option) = match t with Some t -> tx t | None -> JNull
    let private ots (t: DateTimeOffset option) = match t with Some t -> ts t | None -> JNull
    let private en (e: WireEnum<'T>) v = JStr(WireEnum.toWire e v)

    let private lifecycleCommands =
        [ "activateInvestigation"; "markAwaitingEvidence"; "markAwaitingVerification"; "resolveInvestigation"; "closeInvestigation"
          "reopenInvestigation"; "verificationFailed" ]

    /// The payload of a stored event as the domain's CoreEvent (total over the 31 registered event types).
    let decodeEvent (eventType: string) (p: J) : CoreEvent =
        let hId () = HypothesisId(guid p "hypothesisId")
        match eventType with
        | "InvestigationStarted" -> InvestigationStarted { Title = text p "title"; ObservationId = ObservationId(guid p "observationId"); Original = text p "original" }
        | "LifecycleChanged" ->
            let from = enum Enums.lifecycle p "from"
            let target = enum Enums.lifecycle p "to"
            let reason = optText p "reason"
            let needReason () = match reason with Some r -> r | None -> failwith "LifecycleChanged command requires a reason"
            let e =
                match str p "command" with
                | "activateInvestigation" -> InvestigationActivated from
                | "markAwaitingEvidence" -> InvestigationMarkedAwaitingEvidence(from, needReason ())
                | "markAwaitingVerification" -> InvestigationMarkedAwaitingVerification from
                | "resolveInvestigation" -> InvestigationResolved from
                | "closeInvestigation" -> InvestigationClosed from
                | "reopenInvestigation" -> InvestigationReopened(from, needReason ())
                | "verificationFailed" -> InvestigationVerificationFailed(needReason ())
                | c -> failwithf "unknown lifecycle command %s" c
            match Events.lifecycleChange e with
            | Some(f, t) when f = from && t = target -> e
            | _ -> failwithf "LifecycleChanged %s from %A to %A is not representable" (str p "command") from target
        | "ObservationRecorded" -> ObservationRecorded { ObservationId = ObservationId(guid p "observationId"); Original = text p "original"; ObservedAt = optTime p "observedAt" }
        | "HypothesisRecorded" -> HypothesisRecorded { HypothesisId = hId (); Statement = text p "statement"; Mechanism = optText p "mechanism" }
        | "HypothesisTransitioned" ->
            let t: HypothesisTransition =
                { HypothesisId = hId (); From = enum Enums.hypothesisState p "from"; Reason = text p "reason"
                  EvidenceIds = guids p "evidenceIds" |> List.map EvidenceId }
            let e =
                match str p "command" with
                | "markPlausible" -> HypothesisMarkedPlausible t
                | "markSupported" -> HypothesisMarkedSupported t
                | "markWeakened" -> HypothesisWeakened t
                | "falsify" -> HypothesisFalsified t
                | "supersede" -> HypothesisSuperseded t
                | "reopen" -> HypothesisReopened t
                | c -> failwithf "unknown hypothesis command %s" c
            match Events.hypothesisChange e with
            | Some(_, target) when target = enum Enums.hypothesisState p "to" -> e
            | _ -> failwithf "HypothesisTransitioned %s to %s is not representable" (str p "command") (str p "to")
        | "EvidenceAdmitted" ->
            EvidenceAdmitted
                { EvidenceId = EvidenceId(guid p "evidenceId"); ContentKind = enum Enums.contentKind p "contentKind"; Content = text p "content"
                  Category = enum Enums.evidenceCategory p "category"; Source = text p "source"; CapturedAt = optTime p "capturedAt" }
        | "EvidenceSuperseded" ->
            EvidenceSuperseded { EvidenceId = EvidenceId(guid p "evidenceId"); ReplacementEvidenceId = EvidenceId(guid p "replacementEvidenceId"); Reason = text p "reason" }
        | "EvidenceVerificationAssessed" ->
            EvidenceVerificationAssessed { EvidenceId = EvidenceId(guid p "evidenceId"); Verification = enum Enums.verificationStatus p "verification" }
        | "EvidenceRelated" ->
            EvidenceRelationshipRecorded
                { RelationshipId = EvidenceRelationshipId(guid p "relationshipId"); EvidenceId = EvidenceId(guid p "evidenceId"); HypothesisId = hId ()
                  Kind = enum Enums.relationshipKind p "kind"; Rationale = optText p "rationale" }
        | "UnknownIdentified" -> UnknownIdentified { UnknownId = UnknownId(guid p "unknownId"); Question = text p "question"; Materiality = enum Enums.materiality p "materiality" }
        | "UnknownTransitioned" -> UnknownTransitioned { UnknownId = UnknownId(guid p "unknownId"); State = enum Enums.unknownState p "state"; Basis = text p "basis" }
        | "AssumptionRecorded" -> AssumptionRecorded { AssumptionId = AssumptionId(guid p "assumptionId"); Proposition = text p "proposition"; Materiality = enum Enums.materiality p "materiality" }
        | "AssumptionTransitioned" -> AssumptionTransitioned { AssumptionId = AssumptionId(guid p "assumptionId"); State = enum Enums.assumptionState p "state" }
        | "ContradictionRecorded" ->
            ContradictionRecorded
                { ContradictionId = ContradictionId(guid p "contradictionId"); Statement = text p "statement"
                  EvidenceIds = guids p "evidenceIds" |> List.map EvidenceId; Materiality = enum Enums.materiality p "materiality" }
        | "ContradictionTransitioned" ->
            ContradictionTransitioned { ContradictionId = ContradictionId(guid p "contradictionId"); State = enum Enums.contradictionState p "state"; Basis = text p "basis" }
        | "PredictionRecorded" ->
            PredictionRecorded
                { PredictionId = PredictionId(guid p "predictionId"); HypothesisId = hId (); Statement = text p "statement"
                  Strength = enum Enums.predictionStrength p "strength"; Timing = enum Enums.predictionTiming p "timing" }
        | "ActionRecorded" ->
            InvestigationActionRecorded
                { ActionId = InvestigationActionId(guid p "actionId"); Kind = enum Enums.actionKind p "kind"; Purpose = text p "purpose"
                  TargetHypothesisIds = guids p "targetHypothesisIds" |> List.map HypothesisId }
        | "ActionTransitioned" -> InvestigationActionTransitioned { ActionId = InvestigationActionId(guid p "actionId"); State = enum Enums.actionState p "state" }
        | "FindingRecorded" ->
            FindingRecorded
                { FindingId = FindingId(guid p "findingId"); ActionId = InvestigationActionId(guid p "actionId"); OriginalResult = text p "originalResult"
                  Interpretation = optText p "interpretation" }
        | "NegativeKnowledgeEstablished" ->
            NegativeKnowledgeEstablished
                { NegativeKnowledgeId = NegativeKnowledgeId(guid p "negativeKnowledgeId"); Statement = text p "statement"; Scope = text p "scope"
                  BasisEvidenceIds = guids p "basisEvidenceIds" |> List.map EvidenceId }
        | "CausalAssertionRecorded" ->
            let members =
                match field p "members" with
                | JArr ms -> ms |> List.map (fun m -> { HypothesisId = HypothesisId(guid m "hypothesisId"); Role = enum Enums.causeRole m "role" })
                | j -> failwithf "%A" j
            CausalAssertionRecorded { CauseId = CausalAssertionId(guid p "causeId"); Outcome = text p "outcome"; Members = members; Mechanism = optText p "mechanism" }
        | "CausalAssertionConfirmed" -> CauseConfirmed { CauseId = CausalAssertionId(guid p "causeId"); Policy = text p "policy" }
        | "CausalAssertionReopened" -> ConfirmedCauseReopened(CausalAssertionId(guid p "causeId"), text p "reason")
        | "CorrectiveActionRecorded" ->
            CorrectiveActionRecorded
                { CorrectiveActionId = CorrectiveActionId(guid p "correctiveActionId"); Kind = enum Enums.correctiveActionKind p "kind"
                  Description = text p "description"; TargetCauseIds = guids p "targetCauseIds" |> List.map CausalAssertionId }
        | "CorrectiveActionTransitioned" ->
            let id = CorrectiveActionId(guid p "correctiveActionId")
            match enum Enums.correctiveActionState p "state" with
            | CorrectiveActionState.Approved -> CorrectiveActionApproved id
            | CorrectiveActionState.InProgress -> CorrectiveActionStarted id
            | CorrectiveActionState.Implemented -> CorrectiveActionImplemented id
            | CorrectiveActionState.AwaitingVerificationOfEffect -> CorrectiveActionAwaitingVerificationOfEffect id
            | CorrectiveActionState.VerifiedEffective -> CorrectiveActionVerifiedEffective id
            | CorrectiveActionState.VerificationFailed -> CorrectiveActionVerificationFailed id
            | CorrectiveActionState.ReworkRequired -> CorrectiveActionReworkRequired id
            | CorrectiveActionState.Cancelled -> CorrectiveActionCancelled id
            | CorrectiveActionState.Proposed -> failwith "'proposed' is not a corrective action target"
        | "CriterionRecorded" ->
            VerificationCriterionRecorded
                { CriterionId = VerificationCriterionId(guid p "criterionId"); CorrectiveActionId = CorrectiveActionId(guid p "correctiveActionId")
                  ExpectedOutcome = text p "expectedOutcome"; Method = optText p "method" }
        | "VerificationRecorded" ->
            VerificationRecorded
                { VerificationId = VerificationId(guid p "verificationId"); CriterionId = VerificationCriterionId(guid p "criterionId")
                  Outcome = enum Enums.verificationOutcome p "outcome"; EvidenceIds = guids p "evidenceIds" |> List.map EvidenceId }
        | "ConclusionRecorded" ->
            ConclusionRecorded
                { ConclusionId = ConclusionId(guid p "conclusionId"); Statement = text p "statement"; Strength = enum Enums.conclusionStrength p "strength"
                  CauseIds = guids p "causeIds" |> List.map CausalAssertionId; ResidualUncertainty = optText p "residualUncertainty" }
        | "ProposalRecorded" ->
            ProposalRecorded { ProposalId = ProposalId(guid p "proposalId"); Kind = enum Enums.proposalKind p "kind"; ProposalText = text p "text"; Rationale = optText p "rationale" }
        | "ProposalWasAccepted" -> ProposalAccepted(ProposalId(guid p "proposalId"), guid p "targetId")
        | "ProposalWasRejected" -> ProposalRejected(ProposalId(guid p "proposalId"), text p "reason")
        | other -> failwithf "unknown event type %s" other

    /// The stored discriminator and payload of a domain event (the inverse of decodeEvent).
    let encodeEvent (e: CoreEvent) : string * J =
        let lc command (from: Lifecycle) (target: Lifecycle) (reason: NonEmptyText option) =
            "LifecycleChanged", JObj [ "command", JStr command; "from", en Enums.lifecycle from; "to", en Enums.lifecycle target; "reason", otx reason ]
        let ht command (t: HypothesisTransition) target =
            "HypothesisTransitioned",
            JObj
                [ "hypothesisId", g (Ids.hypothesis t.HypothesisId); "from", en Enums.hypothesisState t.From; "to", en Enums.hypothesisState target
                  "command", JStr command; "reason", tx t.Reason; "evidenceIds", gs (t.EvidenceIds |> List.map Ids.evidence) ]
        let ca (id: CorrectiveActionId) state =
            "CorrectiveActionTransitioned", JObj [ "correctiveActionId", g (Ids.correctiveAction id); "state", en Enums.correctiveActionState state ]
        match e with
        | InvestigationStarted p -> "InvestigationStarted", JObj [ "title", tx p.Title; "observationId", g (Ids.observation p.ObservationId); "original", tx p.Original ]
        | InvestigationActivated f -> lc "activateInvestigation" f Lifecycle.Active None
        | InvestigationMarkedAwaitingEvidence(f, r) -> lc "markAwaitingEvidence" f Lifecycle.AwaitingEvidence (Some r)
        | InvestigationMarkedAwaitingVerification f -> lc "markAwaitingVerification" f Lifecycle.AwaitingVerification None
        | InvestigationResolved f -> lc "resolveInvestigation" f Lifecycle.Resolved None
        | InvestigationClosed f -> lc "closeInvestigation" f Lifecycle.Closed None
        | InvestigationReopened(f, r) -> lc "reopenInvestigation" f Lifecycle.Active (Some r)
        | InvestigationVerificationFailed r -> lc "verificationFailed" Lifecycle.AwaitingVerification Lifecycle.Active (Some r)
        | ObservationRecorded p -> "ObservationRecorded", JObj [ "observationId", g (Ids.observation p.ObservationId); "original", tx p.Original; "observedAt", ots p.ObservedAt ]
        | HypothesisRecorded p -> "HypothesisRecorded", JObj [ "hypothesisId", g (Ids.hypothesis p.HypothesisId); "statement", tx p.Statement; "mechanism", otx p.Mechanism ]
        | HypothesisMarkedPlausible t -> ht "markPlausible" t HypothesisState.Plausible
        | HypothesisMarkedSupported t -> ht "markSupported" t HypothesisState.Supported
        | HypothesisWeakened t -> ht "markWeakened" t HypothesisState.Weakened
        | HypothesisFalsified t -> ht "falsify" t HypothesisState.Falsified
        | HypothesisSuperseded t -> ht "supersede" t HypothesisState.Superseded
        | HypothesisReopened t -> ht "reopen" t HypothesisState.Plausible
        | EvidenceAdmitted p ->
            "EvidenceAdmitted",
            JObj
                [ "evidenceId", g (Ids.evidence p.EvidenceId); "contentKind", en Enums.contentKind p.ContentKind; "content", tx p.Content
                  "category", en Enums.evidenceCategory p.Category; "source", tx p.Source; "capturedAt", ots p.CapturedAt ]
        | EvidenceSuperseded p ->
            "EvidenceSuperseded", JObj [ "evidenceId", g (Ids.evidence p.EvidenceId); "replacementEvidenceId", g (Ids.evidence p.ReplacementEvidenceId); "reason", tx p.Reason ]
        | EvidenceVerificationAssessed p ->
            "EvidenceVerificationAssessed", JObj [ "evidenceId", g (Ids.evidence p.EvidenceId); "verification", en Enums.verificationStatus p.Verification ]
        | EvidenceRelationshipRecorded p ->
            "EvidenceRelated",
            JObj
                [ "relationshipId", g (Ids.relationship p.RelationshipId); "evidenceId", g (Ids.evidence p.EvidenceId)
                  "hypothesisId", g (Ids.hypothesis p.HypothesisId); "kind", en Enums.relationshipKind p.Kind; "rationale", otx p.Rationale ]
        | UnknownIdentified p -> "UnknownIdentified", JObj [ "unknownId", g (Ids.unknown p.UnknownId); "question", tx p.Question; "materiality", en Enums.materiality p.Materiality ]
        | UnknownTransitioned p -> "UnknownTransitioned", JObj [ "unknownId", g (Ids.unknown p.UnknownId); "state", en Enums.unknownState p.State; "basis", tx p.Basis ]
        | AssumptionRecorded p ->
            "AssumptionRecorded", JObj [ "assumptionId", g (Ids.assumption p.AssumptionId); "proposition", tx p.Proposition; "materiality", en Enums.materiality p.Materiality ]
        | AssumptionTransitioned p -> "AssumptionTransitioned", JObj [ "assumptionId", g (Ids.assumption p.AssumptionId); "state", en Enums.assumptionState p.State ]
        | ContradictionRecorded p ->
            "ContradictionRecorded",
            JObj
                [ "contradictionId", g (Ids.contradiction p.ContradictionId); "statement", tx p.Statement
                  "evidenceIds", gs (p.EvidenceIds |> List.map Ids.evidence); "materiality", en Enums.materiality p.Materiality ]
        | ContradictionTransitioned p ->
            "ContradictionTransitioned", JObj [ "contradictionId", g (Ids.contradiction p.ContradictionId); "state", en Enums.contradictionState p.State; "basis", tx p.Basis ]
        | PredictionRecorded p ->
            "PredictionRecorded",
            JObj
                [ "predictionId", g (Ids.prediction p.PredictionId); "hypothesisId", g (Ids.hypothesis p.HypothesisId); "statement", tx p.Statement
                  "strength", en Enums.predictionStrength p.Strength; "timing", en Enums.predictionTiming p.Timing ]
        | InvestigationActionRecorded p ->
            "ActionRecorded",
            JObj
                [ "actionId", g (Ids.action p.ActionId); "kind", en Enums.actionKind p.Kind; "purpose", tx p.Purpose
                  "targetHypothesisIds", gs (p.TargetHypothesisIds |> List.map Ids.hypothesis) ]
        | InvestigationActionTransitioned p -> "ActionTransitioned", JObj [ "actionId", g (Ids.action p.ActionId); "state", en Enums.actionState p.State ]
        | FindingRecorded p ->
            "FindingRecorded",
            JObj [ "findingId", g (Ids.finding p.FindingId); "actionId", g (Ids.action p.ActionId); "originalResult", tx p.OriginalResult; "interpretation", otx p.Interpretation ]
        | NegativeKnowledgeEstablished p ->
            "NegativeKnowledgeEstablished",
            JObj
                [ "negativeKnowledgeId", g (Ids.negativeKnowledge p.NegativeKnowledgeId); "statement", tx p.Statement; "scope", tx p.Scope
                  "basisEvidenceIds", gs (p.BasisEvidenceIds |> List.map Ids.evidence) ]
        | CausalAssertionRecorded p ->
            "CausalAssertionRecorded",
            JObj
                [ "causeId", g (Ids.cause p.CauseId); "outcome", tx p.Outcome; "mechanism", otx p.Mechanism
                  "members", JArr(p.Members |> List.map (fun m -> JObj [ "hypothesisId", g (Ids.hypothesis m.HypothesisId); "role", en Enums.causeRole m.Role ])) ]
        | CauseConfirmed p -> "CausalAssertionConfirmed", JObj [ "causeId", g (Ids.cause p.CauseId); "policy", tx p.Policy ]
        | ConfirmedCauseReopened(id, r) -> "CausalAssertionReopened", JObj [ "causeId", g (Ids.cause id); "reason", tx r ]
        | CorrectiveActionRecorded p ->
            "CorrectiveActionRecorded",
            JObj
                [ "correctiveActionId", g (Ids.correctiveAction p.CorrectiveActionId); "kind", en Enums.correctiveActionKind p.Kind
                  "description", tx p.Description; "targetCauseIds", gs (p.TargetCauseIds |> List.map Ids.cause) ]
        | CorrectiveActionApproved id -> ca id CorrectiveActionState.Approved
        | CorrectiveActionStarted id -> ca id CorrectiveActionState.InProgress
        | CorrectiveActionImplemented id -> ca id CorrectiveActionState.Implemented
        | CorrectiveActionAwaitingVerificationOfEffect id -> ca id CorrectiveActionState.AwaitingVerificationOfEffect
        | CorrectiveActionVerifiedEffective id -> ca id CorrectiveActionState.VerifiedEffective
        | CorrectiveActionVerificationFailed id -> ca id CorrectiveActionState.VerificationFailed
        | CorrectiveActionReworkRequired id -> ca id CorrectiveActionState.ReworkRequired
        | CorrectiveActionCancelled id -> ca id CorrectiveActionState.Cancelled
        | VerificationCriterionRecorded p ->
            "CriterionRecorded",
            JObj
                [ "criterionId", g (Ids.criterion p.CriterionId); "correctiveActionId", g (Ids.correctiveAction p.CorrectiveActionId)
                  "expectedOutcome", tx p.ExpectedOutcome; "method", otx p.Method ]
        | VerificationRecorded p ->
            "VerificationRecorded",
            JObj
                [ "verificationId", g (Ids.verification p.VerificationId); "criterionId", g (Ids.criterion p.CriterionId)
                  "outcome", en Enums.verificationOutcome p.Outcome; "evidenceIds", gs (p.EvidenceIds |> List.map Ids.evidence) ]
        | ConclusionRecorded p ->
            "ConclusionRecorded",
            JObj
                [ "conclusionId", g (Ids.conclusion p.ConclusionId); "statement", tx p.Statement; "strength", en Enums.conclusionStrength p.Strength
                  "causeIds", gs (p.CauseIds |> List.map Ids.cause); "residualUncertainty", otx p.ResidualUncertainty ]
        | ProposalRecorded p ->
            "ProposalRecorded",
            JObj [ "proposalId", g (Ids.proposal p.ProposalId); "kind", en Enums.proposalKind p.Kind; "text", tx p.ProposalText; "rationale", otx p.Rationale ]
        | ProposalAccepted(id, target) -> "ProposalWasAccepted", JObj [ "proposalId", g (Ids.proposal id); "targetId", g target ]
        | ProposalRejected(id, r) -> "ProposalWasRejected", JObj [ "proposalId", g (Ids.proposal id); "reason", tx r ]
        | ExtensionAttached _ -> failwith "ExtensionAttached has no causa-storage v1.0.0 form"

    let private actorOf (a: J) : Actor =
        let id = text a "actorId"
        match str a "kind" with
        | "human" -> { ActorId = id; Kind = ActorKind.Human }
        | "system" -> { ActorId = id; Kind = ActorKind.System }
        | "agent" -> { ActorId = id; Kind = ActorKind.Agent(text a "provider", text a "model") }
        | k -> failwithf "unknown actor kind %s" k

    let private actorJson (a: Actor) : J =
        match a.Kind with
        | ActorKind.Human -> JObj [ "actorId", tx a.ActorId; "kind", JStr "human" ]
        | ActorKind.System -> JObj [ "actorId", tx a.ActorId; "kind", JStr "system" ]
        | ActorKind.Agent(provider, model) -> JObj [ "actorId", tx a.ActorId; "kind", JStr "agent"; "provider", tx provider; "model", tx model ]

    /// One events.ndjson line as the domain envelope.
    let decode (line: string) : EventEnvelope =
        let o = parse line
        // causa-storage v1.0.0 only: any other format or semantic version is incompatible data and fails closed.
        for name in [ "formatVersion"; "semanticVersion" ] do
            if str o name <> "1.0.0" then failwithf "unsupported %s '%s' (causa-storage v1.0.0 reads only 1.0.0)" name (str o name)
        let version = match field o "aggregateVersion" with JNum n -> n | j -> failwithf "%A" j
        { EventId = EventId(guid o "eventId")
          Investigation = InvestigationId(guid o "investigationId")
          Version = AggregateVersion version
          Provenance =
            { OccurredAt = time (str o "occurredAt")
              Actor = actorOf (field o "actor")
              CorrelationId = guid o "correlationId"
              FormatVersion = str o "formatVersion"
              SemanticVersion = str o "semanticVersion" }
          Payload = CoreEvent(decodeEvent (str o "eventType") (field o "payload")) }

    /// The domain envelope as its canonical events.ndjson line (without the terminating LF).
    let encode (e: EventEnvelope) : string =
        let eventType, payload =
            match e.Payload with
            | CoreEvent c -> encodeEvent c
            | ExtensionEvent _ -> failwith "extension events have no causa-storage v1.0.0 form"
        canonical (
            JObj
                [ "formatVersion", JStr e.Provenance.FormatVersion
                  "eventId", g (Ids.event e.EventId)
                  "investigationId", g (Ids.investigation e.Investigation)
                  "aggregateVersion", JNum(AggregateVersion.value e.Version)
                  "eventType", JStr eventType
                  "occurredAt", ts e.Provenance.OccurredAt
                  "actor", actorJson e.Provenance.Actor
                  "semanticVersion", JStr e.Provenance.SemanticVersion
                  "correlationId", g e.Provenance.CorrelationId
                  "payload", payload ]
        )

/// The pinned causa-data commit, its recorded digests, and anonymous reads of its files.
module LiveCausaData =
    open CausaWire

    let commit = "e8259df74add723765c57a5b1a5888d9c3f707af"
    let baseUrl = $"https://raw.githubusercontent.com/kemiller2002/causa-data/{commit}/"

    let array (o: J) (name: string) = match field o name with JArr xs -> xs | j -> failwithf "%s is not an array: %A" name j
    let sha256 (bytes: byte[]) = Convert.ToHexString(Security.Cryptography.SHA256.HashData bytes).ToLowerInvariant()
    /// provenance.json, vendored with the contract fixtures (CR-T15-FIXTURES): every file's sha256 at the commit.
    let provenance: Map<string, string> =
        let rec up (dir: DirectoryInfo | null) =
            match dir with
            | null -> failwithf "tests/fixtures/causa-data/%s/provenance.json not found above %s" commit AppContext.BaseDirectory
            | d ->
                let candidate = Path.Combine(d.FullName, "tests", "fixtures", "causa-data", commit, "provenance.json")
                if File.Exists candidate then candidate else up d.Parent
        let doc = parse (File.ReadAllText(up (DirectoryInfo AppContext.BaseDirectory)))
        array doc "files" |> List.map (fun f -> str f "path", str f "sha256") |> Map.ofList

    let private client = lazy (new HttpClient(Timeout = TimeSpan.FromSeconds 60.0))

    /// The vendored fixture directory (the contract's conformance fixtures only).
    let vendoredRoot () =
        let rec up (dir: DirectoryInfo | null) =
            match dir with
            | null -> failwith "tests/fixtures/causa-data not found"
            | d ->
                let candidate = Path.Combine(d.FullName, "tests", "fixtures", "causa-data", commit)
                if Directory.Exists candidate then candidate else up d.Parent
        up (DirectoryInfo AppContext.BaseDirectory)

    /// Fetches one file anonymously and refuses it unless its bytes match the expected digest.
    let fetchExpecting (path: string) (expected: string) : string =
        let bytes = client.Value.GetByteArrayAsync(baseUrl + path).GetAwaiter().GetResult()
        let actual = sha256 bytes
        if actual <> expected then failwithf "%s: sha256 %s, provenance records %s" path actual expected
        Encoding.UTF8.GetString bytes

    /// Fetches one file recorded in provenance.json and verifies it against its recorded digest.
    let fetch (path: string) : string =
        match provenance.TryFind path with
        | Some sha -> fetchExpecting path sha
        | None -> failwithf "%s is not recorded in provenance.json" path

    /// The lines of an events.ndjson file (each terminated by LF, CONTRACT section 2).
    let lines (events: string) =
        if not (events.EndsWith "\n") then failwith "events.ndjson does not end with LF"
        events.Substring(0, events.Length - 1).Split('\n') |> List.ofArray

    /// Differences between the replayed investigation and the engine-generated snapshot state (CONTRACT section 8):
    /// title, lifecycle, version, the id set of every entity array, and every entity's state.
    let snapshotDifferences (s: Investigation) (state: J) : string list =
        let ids (name: string) = array state name |> List.map (fun x -> str x "id") |> set
        let stateOf (name: string) = array state name |> List.map (fun x -> str x "id", (match field x "state" with JStr v -> v | j -> sprintf "%A" j)) |> Map.ofList
        let mine (keys: Guid seq) = keys |> Seq.map Ids.text |> set
        let compareStates name (actual: (Guid * string) seq) =
            let expectedStates = stateOf name
            [ for id, st in actual do
                  match expectedStates.TryFind(Ids.text id) with
                  | Some e when e <> st -> yield $"{name} {Ids.text id}: replayed {st}, snapshot {e}"
                  | _ -> () ]
        [ if str state "title" <> s.Title.Value then yield "title"
          let storedLifecycle = str state "lifecycle"
          let replayedLifecycle = WireEnum.toWire Enums.lifecycle s.Lifecycle
          if storedLifecycle <> replayedLifecycle then yield $"lifecycle: replayed {replayedLifecycle}, snapshot {storedLifecycle}"
          if field state "version" <> JNum(AggregateVersion.value s.Version) then yield "version"
          for name, actual in
              [ "observations", mine (s.Observations.Keys |> Seq.map Ids.observation)
                "hypotheses", mine (s.Hypotheses.Keys |> Seq.map Ids.hypothesis)
                "evidence", mine (s.Evidence.Keys |> Seq.map Ids.evidence)
                "relationships", mine (s.Relationships.Keys |> Seq.map Ids.relationship)
                "unknowns", mine (s.Unknowns.Keys |> Seq.map Ids.unknown)
                "assumptions", mine (s.Assumptions.Keys |> Seq.map Ids.assumption)
                "contradictions", mine (s.Contradictions.Keys |> Seq.map Ids.contradiction)
                "predictions", mine (s.Predictions.Keys |> Seq.map Ids.prediction)
                "actions", mine (s.Actions.Keys |> Seq.map Ids.action)
                "findings", mine (s.Findings.Keys |> Seq.map Ids.finding)
                "negativeKnowledge", mine (s.NegativeKnowledge.Keys |> Seq.map Ids.negativeKnowledge)
                "causes", mine (s.Causes.Keys |> Seq.map Ids.cause)
                "correctiveActions", mine (s.CorrectiveActions.Keys |> Seq.map Ids.correctiveAction)
                "criteria", mine (s.Criteria.Keys |> Seq.map Ids.criterion)
                "verifications", mine (s.Verifications.Keys |> Seq.map Ids.verification)
                "conclusions", mine (s.Conclusions.Keys |> Seq.map Ids.conclusion)
                "proposals", mine (s.Proposals.Keys |> Seq.map Ids.proposal) ] do
              let stored = ids name
              if stored <> actual then yield $"{name}: id sets differ ({stored.Count} in snapshot, {actual.Count} replayed)"
          yield! compareStates "hypotheses" [ for KeyValue(HypothesisId g, h) in s.Hypotheses -> g, WireEnum.toWire Enums.hypothesisState h.State ]
          yield! compareStates "unknowns" [ for KeyValue(UnknownId g, u) in s.Unknowns -> g, WireEnum.toWire Enums.unknownState u.State ]
          yield! compareStates "assumptions" [ for KeyValue(AssumptionId g, a) in s.Assumptions -> g, WireEnum.toWire Enums.assumptionState a.State ]
          yield! compareStates "contradictions" [ for KeyValue(ContradictionId g, c) in s.Contradictions -> g, WireEnum.toWire Enums.contradictionState c.State ]
          yield! compareStates "actions" [ for KeyValue(InvestigationActionId g, a) in s.Actions -> g, WireEnum.toWire Enums.actionState a.State ]
          yield! compareStates "causes" [ for KeyValue(CausalAssertionId g, c) in s.Causes -> g, WireEnum.toWire Enums.causeState c.State ]
          yield! compareStates "correctiveActions" [ for KeyValue(CorrectiveActionId g, c) in s.CorrectiveActions -> g, WireEnum.toWire Enums.correctiveActionState c.State ]
          yield! compareStates "proposals" [ for KeyValue(ProposalId g, p) in s.Proposals -> g, WireEnum.toWire Enums.proposalState p.State ]
          let verification = array state "evidence" |> List.map (fun x -> str x "id", str x "verification") |> Map.ofList
          for KeyValue(EvidenceId g, e) in s.Evidence do
              match verification.TryFind(Ids.text g) with
              | Some v when v <> WireEnum.toWire Enums.verificationStatus e.Verification -> yield $"evidence {Ids.text g}: verification replayed differs from snapshot {v}"
              | _ -> () ]

module EXTDATA011Tests =
    open CausaWire
    open LiveCausaData

    /// Decodes and replays one events.ndjson: the replayed state and any line that does not re-encode to itself, or the
    /// reason the whole history was refused (nothing partial is returned).
    let private replay (events: string) : Result<Investigation * string list, string> =
        try
            let ls = lines events
            let envelopes = ls |> List.map decode
            let lossy = List.zip ls envelopes |> List.filter (fun (l, e) -> encode e <> l) |> List.map (fun (_, e) -> AggregateVersion.value e.Version)
            match Kernel.domain.Replay envelopes with
            | Ok s -> Ok(s, lossy |> List.map (sprintf "version %d re-encodes differently"))
            | Error e -> Error $"%A{e}"
        with ex -> Error("decode: " + ex.Message)

    /// The catalog histories: every case of every catalog manifest, and the everyday-v1 workspace, with the version and
    /// lifecycle their manifests expect.
    let private histories () =
        let catalogs =
            [ for path in provenance.Keys do
                  if path.StartsWith "catalog/" && path.EndsWith "/manifest.json" then
                      for case in array (parse (fetch path)) "cases" do
                          yield str case "path", (match field case "expectedVersion" with JNum n -> n | j -> failwithf "%A" j), str case "expectedLifecycle" ]
        let everyday =
            [ for path in provenance.Keys do
                  if path.StartsWith "workspaces/demo/everyday-v1/" && path.EndsWith "/manifest.json" then
                      let dir = path.Substring(0, path.Length - "/manifest.json".Length)
                      let version = match field (parse (fetch path)) "aggregateVersion" with JNum n -> n | j -> failwithf "%A" j
                      yield dir, version, "" ]
        catalogs @ everyday

    [<Fact>]
    let ``EXT-DATA-011-O1`` () =
        // The demonstration records are not in this repository: they are read at the pinned commit.
        let root = vendoredRoot ()
        Assert.False(Directory.Exists(Path.Combine(root, "workspaces")), "demonstration records are vendored")
        Assert.False(Directory.Exists(Path.Combine(root, "catalog")), "catalog records are vendored")
        let hs = histories ()
        Assert.Equal(8, hs.Length) // demonstration-v1 (4), historical-v1 (3), everyday-v1 (1)
        let problems =
            [ for path, version, lifecycle in hs do
                  let state = field (parse (fetch $"{path}/snapshot.json")) "state"
                  match replay (fetch $"{path}/events.ndjson") with
                  | Error reason -> yield $"{path}: refused: {reason}"
                  | Ok(s, lossy) ->
                      yield! lossy |> List.truncate 3 |> List.map (fun l -> $"{path}: {l}")
                      if AggregateVersion.value s.Version <> version then yield $"{path}: version {AggregateVersion.value s.Version}, manifest {version}"
                      let replayed = WireEnum.toWire Enums.lifecycle s.Lifecycle
                      if lifecycle <> "" && replayed <> lifecycle then yield $"{path}: lifecycle {replayed}, manifest {lifecycle}"
                      yield! snapshotDifferences s state |> List.truncate 5 |> List.map (fun d -> $"{path}: snapshot: {d}") ]
        Assert.True(problems.IsEmpty, String.Join("\n", problems))
        Assert.Equal(1363 + 1315 + 48, hs |> List.sumBy (fun (_, v, _) -> int v))

    [<Fact>]
    let ``EXT-DATA-011-O2`` () =
        // The contract is versioned: a history written in another format or semantic version, or holding an event kind
        // causa-storage v1.0.0 does not register, is refused whole; nothing is partially loaded.
        let path, _, _ = histories () |> List.find (fun (p, _, _) -> p.StartsWith "workspaces/demo/everyday-v1/")
        let events = fetch $"{path}/events.ndjson"
        Assert.True(Result.isOk (replay events), "the unmodified history must load")
        let ls = lines events
        let rewrite (i: int) (f: string -> string) = String.Join("\n", ls |> List.mapi (fun j l -> if j = i then f l else l)) + "\n"
        let incompatible =
            [ "formatVersion 2.0.0", rewrite 3 (fun l -> l.Replace("\"formatVersion\":\"1.0.0\"", "\"formatVersion\":\"2.0.0\""))
              "semanticVersion 1.1.0", rewrite 3 (fun l -> l.Replace("\"semanticVersion\":\"1.0.0\"", "\"semanticVersion\":\"1.1.0\""))
              "unknown event kind", rewrite 3 (fun l -> Text.RegularExpressions.Regex.Replace(l, "\"eventType\":\"[A-Za-z]+\"", "\"eventType\":\"InvestigationDeleted\"")) ]
        for name, text in incompatible do
            Assert.NotEqual<string>(events, text)
            match replay text with
            | Error reason -> Assert.False(String.IsNullOrWhiteSpace reason, name)
            | Ok _ -> failwithf "%s was loaded" name
        // A file whose bytes differ from the recorded digest is refused before it is read.
        let wrong = provenance["catalog/demonstration-v1/manifest.json"]
        let ex = Assert.ThrowsAny<exn>(fun () -> fetchExpecting "catalog/active.json" wrong |> ignore)
        Assert.Contains("sha256", ex.Message)

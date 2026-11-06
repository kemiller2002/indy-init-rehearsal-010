// Owner: domain. IF41-DOMAIN producer contract tests, and a test-side causa-storage v1.0.0 event codec (CONTRACT
// sections 5 and 7) used to replay the frozen fixtures and catalog histories through the kernel.
namespace Causa.Contract.Tests.Domain

open System
open System.Globalization
open System.IO
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

/// The causa-data fixture tree (tests/fixtures/causa-data/<commit>, CR-T15-FIXTURES) and its provenance.
module CausaData =
    open CausaWire

    let commit = "e8259df74add723765c57a5b1a5888d9c3f707af"

    let root: string =
        let rec up (dir: DirectoryInfo | null) =
            match dir with
            | null -> failwithf "tests/fixtures/causa-data/%s not found above %s" commit AppContext.BaseDirectory
            | d ->
                let candidate = Path.Combine(d.FullName, "tests", "fixtures", "causa-data", commit)
                if Directory.Exists candidate then candidate else up d.Parent
        up (DirectoryInfo AppContext.BaseDirectory)

    let read (relative: string) = File.ReadAllText(Path.Combine(root, relative))
    let json (relative: string) = parse (read relative)

    let array (o: J) (name: string) = match field o name with JArr xs -> xs | j -> failwithf "%s is not an array: %A" name j

    let sha256 (bytes: byte[]) = Convert.ToHexString(Security.Cryptography.SHA256.HashData bytes).ToLowerInvariant()

    /// A valid history to replay: where it is, and the version and lifecycle its manifest expects.
    type History = { Path: string; ExpectedVersion: int64; ExpectedLifecycle: string }

    let private expected (entry: J) (path: string) =
        { Path = path
          ExpectedVersion = (match field entry "expectedVersion" with JNum n -> n | j -> failwithf "%A" j)
          ExpectedLifecycle = str entry "expectedLifecycle" }

    /// The five frozen scenarios of the contract's conformance fixtures (the only causa-data files vendored here;
    /// the catalog histories are read live by Causa.Acceptance.Live.EXTDATA011Tests, EXT-DATA-011-O1).
    let histories: History list =
        array (json "fixtures/v1.0.0/manifest.json") "investigations"
        |> List.map (fun e -> expected e $"""fixtures/v1.0.0/investigations/{str e "investigationId"}""")

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


module Producer =
    let private guid (n: int) = Guid(sprintf "00000000-0000-4000-8000-%012d" n)

    let private text (s: string) =
        match NonEmptyText.create s with
        | Ok t -> t
        | Error e -> failwith e

    let private inv = InvestigationId(guid 1)
    let private at = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)
    let private actor: ActorContext = { ActorId = "tester" }
    let private policy = Transitions.corePolicy

    let private command (n: int) (expected: AggregateVersion option) (c: CoreCommand) : CommandEnvelope =
        { CommandId = guid (10000 + n); Investigation = inv; ExpectedVersion = expected; Command = Core c }

    /// Appends an Allowed decision's events as the application would (provenance from the command; no clock).
    let private append (state: Investigation option) (cmd: CommandEnvelope) (events: EventPayload list) =
        let start = state |> Option.map (fun s -> AggregateVersion.value s.Version) |> Option.defaultValue 0L
        events
        |> List.mapi (fun i payload ->
            let v = start + int64 (i + 1)
            { EventId = EventId(guid (50000 + int v))
              Investigation = inv
              Version = AggregateVersion v
              Provenance =
                { OccurredAt = at.AddSeconds(float v)
                  Actor = { ActorId = text actor.ActorId; Kind = ActorKind.Human }
                  CorrelationId = cmd.CommandId
                  FormatVersion = Events.formatVersion
                  SemanticVersion = Events.semanticVersion }
              Payload = payload })

    /// Runs commands through the kernel; every one must be Allowed. Returns the state and the history.
    let private run (commands: CoreCommand list) =
        commands
        |> List.indexed
        |> List.fold
            (fun (state: Investigation option, history: EventEnvelope list) (i, c) ->
                let cmd = command i None c
                match Kernel.domain.Decide policy actor state cmd with
                | Allowed events ->
                    let envs = append state cmd events
                    let next =
                        envs
                        |> List.fold (fun s e -> match Kernel.domain.Evolve s e with Ok x -> Some x | Error err -> failwithf "%A" err) state
                    next, history @ envs
                | other -> failwithf "fixture command %s was not allowed: %A" (Commands.name c) other)
            (None, [])

    let private h n = HypothesisId(guid n)
    let private ev n = EvidenceId(guid n)
    let private mark n : MarkHypothesis = { HypothesisId = h n; Reason = text "basis"; EvidenceIds = [ ev 30 ] }

    let private start: StartInvestigation = { Title = text "Payment failures"; ObservationId = ObservationId(guid 2); Original = text "Checkout fails" }

    /// One representative of every CoreCommand case (29) plus the extension command.
    let private everyCommand: Command list =
        [ Core(StartInvestigation start)
          Core ActivateInvestigation
          Core(RecordObservation { ObservationId = ObservationId(guid 3); Original = text "o"; ObservedAt = None })
          Core(RecordHypothesis { HypothesisId = h 40; Statement = text "h"; Mechanism = None })
          Core(MarkHypothesisPlausible(mark 20))
          Core(MarkHypothesisSupported(mark 21))
          Core(FalsifyHypothesis(mark 22))
          Core(ReopenHypothesis { mark 23 with EvidenceIds = [ ev 36 ] })
          Core(AdmitEvidence { EvidenceId = ev 41; ContentKind = ContentKind.Text; Content = text "e"; Category = EvidenceCategory.Log; Source = text "s"; CapturedAt = None })
          Core(RelateEvidence { RelationshipId = EvidenceRelationshipId(guid 42); EvidenceId = ev 30; HypothesisId = h 20; Kind = RelationshipKind.Supports; Rationale = None })
          Core(IdentifyUnknown { UnknownId = UnknownId(guid 43); Question = text "q"; Materiality = Materiality.Material })
          Core(RecordAssumption { AssumptionId = AssumptionId(guid 44); Proposition = text "p"; Materiality = Materiality.Blocking })
          Core(RecordContradiction { ContradictionId = ContradictionId(guid 45); Statement = text "c"; EvidenceIds = [ ev 30 ]; Materiality = Materiality.Material })
          Core(RecordPrediction { PredictionId = PredictionId(guid 46); HypothesisId = h 20; Statement = text "p"; Strength = PredictionStrength.Expected; Timing = PredictionTiming.Prospective })
          Core(RecordInvestigationAction { ActionId = InvestigationActionId(guid 47); Kind = ActionKind.Test; Purpose = text "a"; TargetHypothesisIds = [ h 20 ] })
          Core(RecordFinding { FindingId = FindingId(guid 48); ActionId = InvestigationActionId(guid 31); OriginalResult = text "r"; Interpretation = None })
          Core(EstablishNegativeKnowledge { NegativeKnowledgeId = NegativeKnowledgeId(guid 49); Statement = text "n"; Scope = text "s"; BasisEvidenceIds = [ ev 30 ] })
          Core(RecordCausalAssertion { CauseId = CausalAssertionId(guid 50); Outcome = text "o"; Members = [ { HypothesisId = h 21; Role = CauseRole.RootCause } ]; Mechanism = None })
          Core(ConfirmCause { CauseId = CausalAssertionId(guid 32); Policy = text "core" })
          Core(RecordCorrectiveAction { CorrectiveActionId = CorrectiveActionId(guid 51); Kind = CorrectiveActionKind.Correction; Description = text "d"; TargetCauseIds = [ CausalAssertionId(guid 32) ] })
          Core(RecordVerificationCriterion { CriterionId = VerificationCriterionId(guid 52); CorrectiveActionId = CorrectiveActionId(guid 33); ExpectedOutcome = text "x"; Method = None })
          Core(MarkCorrectiveActionImplemented(CorrectiveActionId(guid 33)))
          Core(RecordVerification { VerificationId = VerificationId(guid 53); CriterionId = VerificationCriterionId(guid 34); Outcome = VerificationOutcome.Satisfied; EvidenceIds = [ ev 30 ] })
          Core ResolveInvestigation
          Core(ReopenInvestigation { Reason = text "new evidence" })
          Core(RecordProposal { ProposalId = ProposalId(guid 54); Kind = ProposalKind.Hypothesis; ProposalText = text "p"; Rationale = None })
          Core(AcceptProposal(ProposalId(guid 35)))
          Core(RejectProposal(ProposalId(guid 35), text "no"))
          Core(AttachExtension { SemanticId = { Namespace = text "edf"; Name = text "x"; Version = text "1" }; SchemaId = "s"; Stability = StabilityClass.Experimental; Requiredness = Requiredness.Optional; PayloadJson = "{}" })
          Extension({ Namespace = text "edf"; Name = text "y"; Version = text "1" }, "{}") ]

    /// A state with every entity kind a command may reference.
    let private rich, richHistory =
        run
            [ StartInvestigation start
              ActivateInvestigation
              AdmitEvidence { EvidenceId = ev 30; ContentKind = ContentKind.Text; Content = text "e"; Category = EvidenceCategory.Log; Source = text "s"; CapturedAt = None }
              RecordHypothesis { HypothesisId = h 20; Statement = text "h20"; Mechanism = None }
              RecordHypothesis { HypothesisId = h 21; Statement = text "h21"; Mechanism = None }
              RecordHypothesis { HypothesisId = h 22; Statement = text "h22"; Mechanism = None }
              RecordHypothesis { HypothesisId = h 23; Statement = text "h23"; Mechanism = None }
              MarkHypothesisPlausible(mark 21)
              FalsifyHypothesis(mark 23)
              RecordInvestigationAction { ActionId = InvestigationActionId(guid 31); Kind = ActionKind.Query; Purpose = text "a"; TargetHypothesisIds = [] }
              RecordCausalAssertion { CauseId = CausalAssertionId(guid 32); Outcome = text "o"; Members = [ { HypothesisId = h 21; Role = CauseRole.Trigger } ]; Mechanism = None }
              RecordCorrectiveAction { CorrectiveActionId = CorrectiveActionId(guid 33); Kind = CorrectiveActionKind.Mitigation; Description = text "d"; TargetCauseIds = [] }
              RecordVerificationCriterion { CriterionId = VerificationCriterionId(guid 34); CorrectiveActionId = CorrectiveActionId(guid 33); ExpectedOutcome = text "x"; Method = None }
              RecordProposal { ProposalId = ProposalId(guid 35); Kind = ProposalKind.Unknown; ProposalText = text "p"; Rationale = None }
              AdmitEvidence { EvidenceId = ev 36; ContentKind = ContentKind.Text; Content = text "new"; Category = EvidenceCategory.Log; Source = text "s"; CapturedAt = None } ]

    let private fresh, _ = run [ StartInvestigation start ]

    [<Fact>]
    let DecideIsTotalOverCommands () =
        // The representatives cover every case of the frozen CoreCommand union.
        let cases = Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<CoreCommand>) |> Array.map (fun c -> c.Name) |> set
        let covered = everyCommand |> List.choose (function Core c -> Some(Commands.name c) | Extension _ -> None) |> set
        Assert.Equal<Set<string>>(cases, covered)
        let states = [ "none", None; "draft", fresh; "active", rich ]
        let decisions =
            [ for name, state in states do
                  for i, c in List.indexed everyCommand do
                      let version = state |> Option.map (fun s -> s.Version)
                      let cmd = { CommandId = guid (90000 + i); Investigation = inv; ExpectedVersion = version; Command = c }
                      yield name, c, state, cmd, Kernel.domain.Decide policy actor state cmd ]
        // Total: a decision for every (state, command), and every Allowed decision's events evolve.
        Assert.Equal(states.Length * everyCommand.Length, decisions.Length)
        for name, _, state, cmd, decision in decisions do
            match decision with
            | Allowed events ->
                Assert.NotEmpty(events)
                let applied = append state cmd events |> List.fold (fun s e -> s |> Result.bind (fun x -> Kernel.domain.Evolve x e |> Result.map Some)) (Ok state)
                Assert.True(Result.isOk applied, $"{name}: allowed events do not evolve: {applied}")
            | _ -> ()
        let decisionOf stateName (c: Command) =
            decisions |> List.pick (fun (n, c', _, _, d) -> if n = stateName && c' = c then Some d else None)
        // StartInvestigation only without an investigation; every other core command needs one.
        Assert.True(match decisionOf "none" (Core(StartInvestigation start)) with Allowed _ -> true | _ -> false)
        Assert.True(match decisionOf "active" (Core(StartInvestigation start)) with Blocked _ -> true | _ -> false)
        Assert.True(match decisionOf "none" (Core ActivateInvestigation) with Blocked _ -> true | _ -> false)
        // In the rich state, each structurally legal command is allowed.
        for c in everyCommand.[1..25] |> List.filter (fun c -> c <> Core ActivateInvestigation && c <> Core(ReopenInvestigation { Reason = text "new evidence" })) do
            let ok = match decisionOf "active" c with Allowed _ -> true | _ -> false
            let expected =
                match c with
                | Core(MarkCorrectiveActionImplemented _) -> false // the corrective action is Proposed, not InProgress
                | Core(ConfirmCause _) -> false // the core cause-confirmation gate: h21 is only plausible
                | Core ResolveInvestigation -> false // the core resolution gate: no conclusion, criterion unverified
                | _ -> true
            let actual = decisionOf "active" c
            Assert.True((ok = expected), $"active: {c} -> {actual}")
        // Extensions are unsupported semantics, never silently applied.
        Assert.True(match decisionOf "active" everyCommand.[28] with UnsupportedSemantics _ -> true | _ -> false)
        Assert.True(match decisionOf "active" everyCommand.[29] with UnsupportedSemantics _ -> true | _ -> false)
        // A deliberately invalid case fails for the intended reason: falsifying an already falsified hypothesis.
        let refalsify = command 99 None (FalsifyHypothesis(mark 23))
        match Kernel.domain.Decide policy actor rich refalsify with
        | Blocked [ b ] -> Assert.Equal("transition.illegal", b.Code)
        | other -> failwithf "re-falsifying was %A" other
        // A stale expected version is a Conflict; a repeated command is a Duplicate.
        let stale = command 98 (Some(AggregateVersion 1L)) (RecordObservation { ObservationId = ObservationId(guid 60); Original = text "o"; ObservedAt = None })
        Assert.Equal(Conflict(AggregateVersion 1L, rich.Value.Version), Kernel.domain.Decide policy actor rich stale)
        let repeated = command 2 None (RecordObservation { ObservationId = ObservationId(guid 61); Original = text "o"; ObservedAt = None })
        match Kernel.domain.Decide policy actor rich repeated with
        | Duplicate [ EventId _ ] -> ()
        | other -> failwithf "a repeated command was %A" other

    [<Fact>]
    let ReplayRefusesGaps () =
        Assert.True(richHistory.Length >= 10)
        // The whole history replays to the state it was built from.
        Assert.Equal(Ok rich.Value, Kernel.domain.Replay richHistory)
        Assert.Equal(Error EmptyHistory, Kernel.domain.Replay [])
        // A missing version, a duplicated version and a history that does not start at 1 are refused, never patched.
        let without2 = richHistory |> List.filter (fun e -> e.Version <> AggregateVersion 2L)
        Assert.Equal(Error(NonContiguousVersion(AggregateVersion 2L, AggregateVersion 3L)), Kernel.domain.Replay without2)
        let duplicated = richHistory.[0..2] @ [ richHistory.[2] ] @ richHistory.[3..]
        Assert.Equal(Error(NonContiguousVersion(AggregateVersion 4L, AggregateVersion 3L)), Kernel.domain.Replay duplicated)
        Assert.Equal(Error(NonContiguousVersion(AggregateVersion 1L, AggregateVersion 2L)), Kernel.domain.Replay richHistory.Tail)
        // An event kind causa-storage v1.0.0 does not have is refused by name, not skipped.
        let ext = { richHistory.[1] with Payload = ExtensionEvent({ Namespace = text "edf"; Name = text "x"; Version = text "1" }, "{}") }
        Assert.Equal(Error(UnknownEventKind "edf/x@1"), Kernel.domain.Replay(richHistory.[0] :: ext :: richHistory.[2..]))
        // A contiguous history with an illegal event (a transition whose 'from' is not the replayed state) is refused.
        let forged =
            richHistory
            |> List.map (fun e ->
                match e.Payload with
                | CoreEvent(HypothesisFalsified t) -> { e with Payload = CoreEvent(HypothesisFalsified { t with From = HypothesisState.Supported }) }
                | _ -> e)
        match Kernel.domain.Replay forged with
        | Error(IllegalEvent err) -> Assert.Contains("from", err.Reason)
        | other -> failwithf "a forged transition replayed as %A" other

    /// Decodes and replays one events.ndjson through the kernel: Ok with the state, or the reason it was refused.
    let private replayText (events: string) : Result<Investigation * string list, string> =
        try
            let lines = CausaData.lines events
            let envelopes = lines |> List.map CausaWire.decode
            let lossy =
                List.zip lines envelopes
                |> List.choose (fun (line, e) ->
                    let back = CausaWire.encode e
                    if back = line then None else Some $"version {AggregateVersion.value e.Version} re-encodes differently:\n  stored  {line}\n  encoded {back}")
            match Kernel.domain.Replay envelopes with
            | Ok s -> Ok(s, lossy)
            | Error e -> Error $"%A{e}"
        with ex -> Error("decode: " + ex.Message)

    [<Fact>]
    let FrozenFixturesReplay () =
        // The fixture copies are exactly the files provenance.json records (CR-T15-FIXTURES): a drifted copy fails.
        let provenance = CausaData.json "provenance.json"
        Assert.Equal(CausaData.commit, CausaWire.str provenance "commit")
        let listed = CausaData.array provenance "files" |> List.map (fun f -> CausaWire.str f "path", CausaWire.str f "sha256")
        let onDisk =
            Directory.GetFiles(CausaData.root, "*", SearchOption.AllDirectories)
            |> Array.map (fun f -> Path.GetRelativePath(CausaData.root, f).Replace('\\', '/'))
            |> Array.filter ((<>) "provenance.json")
            |> set
        // Only the contract's conformance fixtures are vendored (EXT-DATA-011-O1); the catalog files are listed with
        // their digests for the live test but are not in the repository.
        let vendored = listed |> List.filter (fun (p, _) -> p.StartsWith "fixtures/v1.0.0/")
        Assert.Equal<Set<string>>(vendored |> List.map fst |> set, onDisk)
        Assert.True(listed |> List.exists (fun (p, _) -> p.StartsWith "workspaces/demo/demonstration-v1/"))
        for path, sha in vendored do
            Assert.True((CausaData.sha256 (File.ReadAllBytes(Path.Combine(CausaData.root, path))) = sha), $"{path} does not match its recorded sha256")
        // Every frozen scenario decodes, re-encodes byte for byte, replays through the kernel to the
        // manifest's version and lifecycle, and equals the engine-generated snapshot.
        let histories = CausaData.histories
        let problems =
            [ for h in histories do
                  match replayText (CausaData.read $"{h.Path}/events.ndjson") with
                  | Error reason -> yield $"{h.Path}: refused: {reason}"
                  | Ok(s, lossy) ->
                      for l in lossy |> List.truncate 3 do
                          yield $"{h.Path}: {l}"
                      if AggregateVersion.value s.Version <> h.ExpectedVersion then
                          yield $"{h.Path}: version {AggregateVersion.value s.Version}, manifest expects {h.ExpectedVersion}"
                      if WireEnum.toWire Enums.lifecycle s.Lifecycle <> h.ExpectedLifecycle then
                          yield $"{h.Path}: lifecycle {WireEnum.toWire Enums.lifecycle s.Lifecycle}, manifest expects {h.ExpectedLifecycle}"
                      for d in CausaData.snapshotDifferences s (CausaWire.field (CausaData.json $"{h.Path}/snapshot.json") "state") |> List.truncate 5 do
                          yield $"{h.Path}: snapshot: {d}" ]
        Assert.True(problems.IsEmpty, String.Join("\n", problems))
        // All of it was replayed: the 5 scenarios, 184 events.
        Assert.Equal(5, histories.Length)
        Assert.Equal(184, histories |> List.sumBy (fun h -> int h.ExpectedVersion))
        // A deliberately invalid case fails for the intended reason: a stored event whose `from` contradicts the replayed
        // state (a forged transition) is refused by the kernel, and a byte-level change is detected by re-encoding.
        let scenario = (List.head histories).Path
        let events = CausaData.read $"{scenario}/events.ndjson"
        let forged =
            CausaData.lines events
            |> List.map (fun l -> if l.Contains("\"eventType\":\"HypothesisTransitioned\"") then l.Replace("\"from\":\"proposed\"", "\"from\":\"weakened\"") else l)
        Assert.NotEqual<string list>(CausaData.lines events, forged)
        match replayText (String.Join("\n", forged) + "\n") with
        | Error reason -> Assert.Contains("from", reason)
        | Ok _ -> failwith "a forged transition replayed"
        let spaced = (CausaData.lines events |> List.mapi (fun i l -> if i = 1 then l.Replace("\",\"", "\", \"") else l))
        match replayText (String.Join("\n", spaced) + "\n") with
        | Ok(_, lossy) -> Assert.Single(lossy) |> ignore
        | Error reason -> failwith reason
        // Every negative fixture whose refusal belongs to the domain (sequence, correlation, investigation identity,
        // unknown kind, and the 34 inadmissible histories) is refused by the kernel; the remaining codes are reader
        // stages of persistence (encoding, schema, digests, paths, manifests).
        let domainCodes =
            set [ "CAUSA.HISTORY_INADMISSIBLE"; "CAUSA.SEQUENCE_INVALID"; "CAUSA.CORRELATION_REUSED"; "CAUSA.UNKNOWN_EVENT_TYPE"; "CAUSA.INVESTIGATION_MISMATCH" ]
        let negatives =
            [ for n in CausaData.array (CausaData.json "fixtures/v1.0.0/manifest.json") "negativeFixtures" do
                  let code = CausaWire.str n "expectedErrorCode"
                  if domainCodes.Contains code then
                      let path = CausaWire.str n "path"
                      let fixture = CausaData.json $"fixtures/v1.0.0/{path}"
                      let events = CausaWire.field (CausaWire.field fixture "files") "events.ndjson"
                      yield path, code, Text.Encoding.UTF8.GetString(Convert.FromBase64String(CausaWire.str events "base64")) ]
        Assert.Equal(39, negatives.Length) // 34 inadmissible histories, 2 sequence, 1 correlation, 1 unknown kind, 1 investigation
        let admitted =
            [ for path, code, text in negatives do
                  match replayText text with
                  | Error reason when code = "CAUSA.UNKNOWN_EVENT_TYPE" || not (reason.StartsWith "decode:") -> ()
                  | Error reason -> yield $"{path} ({code}) was refused by the decoder, not the kernel: {reason}"
                  | Ok _ -> yield $"{path} ({code}) replayed" ]
        Assert.True(admitted.IsEmpty, String.Join("\n", admitted))

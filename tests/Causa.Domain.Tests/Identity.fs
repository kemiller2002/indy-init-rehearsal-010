// Owner: domain. Identity, text and wire-enumeration tests of the domain kernel.
namespace Causa.Domain.Tests

open System
open Xunit
open Causa.Domain

module Identity =
    [<Fact>]
    let ``NonEmptyText keeps text verbatim and refuses whitespace-only text`` () =
        let verbatim = "  Checkout fails at 12:00 \n"
        match NonEmptyText.create verbatim with
        | Ok t -> Assert.Equal(verbatim, NonEmptyText.value t)
        | Error e -> failwith e
        for blank in [ ""; " "; "\t\r\n"; "  　" ] do
            Assert.True(Result.isError (NonEmptyText.create blank), sprintf "%A was accepted" blank)
        // Equality is ordinal: no normalisation, so differently composed text is different text.
        Assert.NotEqual(NonEmptyText.tryCreate "é", NonEmptyText.tryCreate "é")

    [<Fact>]
    let ``Typed identities are distinct and render as lowercase canonical UUIDs`` () =
        let g = Guid("0A1B2C3D-4E5F-4061-8728-394A5B6C7D8E")
        Assert.Equal("0a1b2c3d-4e5f-4061-8728-394a5b6c7d8e", Ids.text (Ids.investigation (InvestigationId g)))
        Assert.Equal(g, Ids.hypothesis (HypothesisId g))
        Assert.Equal(AggregateVersion 2L, AggregateVersion.next (AggregateVersion 1L))
        Assert.Equal(0L, AggregateVersion.value AggregateVersion.zero)

    let private bijective (e: WireEnum<'T>) (count: int) =
        let values = WireEnum.values e
        Assert.Equal(count, values.Length)
        Assert.Equal(count, Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<'T>).Length)
        Assert.Equal(count, values |> List.distinct |> List.length)
        for v in values do
            Assert.Equal(Some v, WireEnum.tryParse e (WireEnum.toWire e v))
        // Closed: no case-folded or unknown value is accepted.
        Assert.Equal(None, WireEnum.tryParse e ((WireEnum.toWire e values.Head).ToUpperInvariant()))
        Assert.Equal(None, WireEnum.tryParse e "other")

    [<Fact>]
    let ``Every wire enumeration is a closed bijection with the contract values`` () =
        bijective Enums.lifecycle 7
        bijective Enums.hypothesisState 6
        bijective Enums.unknownState 5
        bijective Enums.materiality 4
        bijective Enums.assumptionState 5
        bijective Enums.contradictionState 6
        bijective Enums.actionState 7
        bijective Enums.actionKind 5
        bijective Enums.causeRole 4
        bijective Enums.causeState 4
        bijective Enums.correctiveActionState 9
        bijective Enums.correctiveActionKind 4
        bijective Enums.evidenceCategory 7
        bijective Enums.verificationStatus 4
        bijective Enums.relationshipKind 4
        bijective Enums.predictionStrength 4
        bijective Enums.predictionTiming 2
        bijective Enums.verificationOutcome 3
        bijective Enums.conclusionStrength 5
        bijective Enums.proposalKind 3
        bijective Enums.proposalState 3
        bijective Enums.contentKind 2
        Assert.Equal("awaitingVerificationOfEffect", WireEnum.toWire Enums.correctiveActionState CorrectiveActionState.AwaitingVerificationOfEffect)
        Assert.Equal("nonMaterial", WireEnum.toWire Enums.materiality Materiality.NonMaterial)

    [<Fact>]
    let ``CoreEvent keeps every frozen case and has one case per causa-storage event`` () =
        let names = Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<CoreEvent>) |> Array.map (fun c -> c.Name) |> set
        Assert.Equal(50, names.Count)
        for frozen in
            [ "InvestigationStarted"; "InvestigationActivated"; "ObservationRecorded"; "HypothesisRecorded"; "HypothesisMarkedPlausible"
              "HypothesisMarkedSupported"; "HypothesisFalsified"; "HypothesisReopened"; "EvidenceAdmitted"; "EvidenceRelationshipRecorded"
              "UnknownIdentified"; "AssumptionRecorded"; "ContradictionRecorded"; "PredictionRecorded"; "InvestigationActionRecorded"
              "FindingRecorded"; "CausalAssertionRecorded"; "CauseConfirmed"; "ConfirmedCauseReopened"; "CorrectiveActionRecorded"
              "VerificationCriterionRecorded"; "CorrectiveActionImplemented"; "VerificationRecorded"; "InvestigationResolved"
              "InvestigationReopened"; "ProposalRecorded"; "ProposalRejected"; "ExtensionAttached" ] do
            Assert.Contains(frozen, names)

    let private fields (t: Type) =
        Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(t) |> Array.map (fun p -> p.Name, p.PropertyType) |> List.ofArray

    let private cases (t: Type) =
        Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(t) |> Array.map (fun c -> c.Name) |> List.ofArray

    [<Fact>]
    let ``The frozen IF41-DOMAIN shapes match the interface manifest`` () =
        Assert.Equal("IF41-DOMAIN", InterfaceManifest.contract)
        Assert.Equal("4.1.5", InterfaceManifest.version)
        Assert.Equal<string list>([ "A"; "B"; "C" ], InterfaceManifest.boundaries |> List.map (fun b -> b.Id))
        // Every named type exists in the domain assembly.
        let asm = typeof<DomainKernel>.Assembly
        for b in InterfaceManifest.boundaries do
            for name in b.Types do
                Assert.True(not (isNull (asm.GetType("Causa.Domain." + name))), $"boundary {b.Id}: type {name} is missing")
        // The kernel record has exactly the manifest's operations, with the frozen signatures.
        let operations = InterfaceManifest.boundaries |> List.collect (fun b -> b.Operations) |> List.map (fun o -> o.Name)
        Assert.Equal<string list>([ "Decide"; "Evolve"; "Project"; "Replay" ], List.sort operations)
        Assert.Equal<(string * Type) list>(
            [ "Decide", typeof<PolicySet -> ActorContext -> Investigation option -> CommandEnvelope -> Decision>
              "Evolve", typeof<Investigation option -> EventEnvelope -> Result<Investigation, ProjectionError>>
              "Replay", typeof<EventEnvelope list -> Result<Investigation, ReplayError>>
              "Project", typeof<Investigation -> InvestigationProjection> ],
            fields typeof<DomainKernel>)
        // The error variants are exactly the manifest's.
        let errorsOf name = InterfaceManifest.boundaries |> List.collect (fun b -> b.Operations) |> List.find (fun o -> o.Name = name) |> fun o -> o.Errors
        Assert.Equal<string list>("Allowed" :: errorsOf "Decide", cases typeof<Decision>)
        Assert.Equal<string list>(errorsOf "Replay", cases typeof<ReplayError>)
        // Frozen envelopes (00-DOMAIN-STUBS.fs): field names and order.
        Assert.Equal<string list>([ "CommandId"; "Investigation"; "ExpectedVersion"; "Command" ], fields typeof<CommandEnvelope> |> List.map fst)
        Assert.Equal<string list>([ "EventId"; "Investigation"; "Version"; "Provenance"; "Payload" ], fields typeof<EventEnvelope> |> List.map fst)
        Assert.Equal<string list>([ "OccurredAt"; "Actor"; "CorrelationId"; "FormatVersion"; "SemanticVersion" ], fields typeof<EventProvenance> |> List.map fst)
        Assert.Equal<string list>([ "Investigation"; "Timeline"; "Capabilities" ], fields typeof<InvestigationProjection> |> List.map fst)
        Assert.Equal<string list>([ "Code"; "Message" ], fields typeof<Blocker> |> List.map fst)
        Assert.Equal<string list>([ "Core"; "Extension" ], cases typeof<Command>)
        Assert.Equal<string list>([ "CoreEvent"; "ExtensionEvent" ], cases typeof<EventPayload>)
        Assert.Equal<string list>(
            [ "StartInvestigation"; "ActivateInvestigation"; "RecordObservation"; "RecordHypothesis"; "MarkHypothesisPlausible"
              "MarkHypothesisSupported"; "FalsifyHypothesis"; "ReopenHypothesis"; "AdmitEvidence"; "RelateEvidence"; "IdentifyUnknown"
              "RecordAssumption"; "RecordContradiction"; "RecordPrediction"; "RecordInvestigationAction"; "RecordFinding"
              "EstablishNegativeKnowledge"; "RecordCausalAssertion"; "ConfirmCause"; "RecordCorrectiveAction"; "RecordVerificationCriterion"
              "MarkCorrectiveActionImplemented"; "RecordVerification"; "ResolveInvestigation"; "ReopenInvestigation"; "RecordProposal"
              "AcceptProposal"; "RejectProposal"; "AttachExtension" ],
            cases typeof<CoreCommand>)
        // The frozen Investigation fields lead the record.
        Assert.Equal<(string * Type) list>([ "Id", typeof<InvestigationId>; "Version", typeof<AggregateVersion> ], fields typeof<Investigation> |> List.take 2)

    // ---- T13: the semantic kernel end to end (Decide -> events -> Evolve), policy gates and capabilities.

    let private g (n: int) = Guid(sprintf "00000000-0000-4000-8000-%012d" n)

    let private t (s: string) =
        match NonEmptyText.create s with
        | Ok x -> x
        | Error e -> failwith e

    let private inv = InvestigationId(g 1)
    let private actor: ActorContext = { ActorId = "investigator" }

    /// Decides a command and, when allowed, appends its events as the application does (provenance from the command).
    let private submit (policies: PolicySet) (state: Investigation option) (n: int) (c: CoreCommand) : Decision * Investigation option =
        let cmd = { CommandId = g (10000 + n); Investigation = inv; ExpectedVersion = (state |> Option.map (fun s -> s.Version)); Command = Core c }
        match Kernel.domain.Decide policies actor state cmd with
        | Allowed events as d ->
            let start = state |> Option.map (fun s -> AggregateVersion.value s.Version) |> Option.defaultValue 0L
            let next =
                events
                |> List.indexed
                |> List.fold
                    (fun s (i, payload) ->
                        let v = start + int64 (i + 1)
                        let env =
                            { EventId = EventId(g (50000 + int v)); Investigation = inv; Version = AggregateVersion v
                              Provenance =
                                { OccurredAt = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero).AddMinutes(float v)
                                  Actor = { ActorId = t actor.ActorId; Kind = ActorKind.Human }
                                  CorrelationId = cmd.CommandId; FormatVersion = Events.formatVersion; SemanticVersion = Events.semanticVersion }
                              Payload = payload }
                        match Kernel.domain.Evolve s env with
                        | Ok x -> Some x
                        | Error e -> failwithf "allowed event did not evolve: %A" e)
                    state
            d, next
        | d -> d, state

    let private run (policies: PolicySet) (commands: CoreCommand list) =
        commands
        |> List.indexed
        |> List.fold
            (fun (state, n) (_, c) ->
                match submit policies state n c with
                | Allowed _, next -> next, n + 1
                | d, _ -> failwithf "%s was not allowed: %A" (Commands.name c) d)
            (None, 0)
        |> fst
        |> Option.get

    let private start: StartInvestigation = { Title = t "Checkout failures"; ObservationId = ObservationId(g 2); Original = t "Payments fail at checkout since 09:00" }
    let private h1, h2 = HypothesisId(g 20), HypothesisId(g 21)
    let private e1, e2 = EvidenceId(g 30), EvidenceId(g 31)
    let private relate n e h kind rationale : CoreCommand =
        RelateEvidence { RelationshipId = EvidenceRelationshipId(g n); EvidenceId = e; HypothesisId = h; Kind = kind; Rationale = Some(t rationale) }
    let private admit e content : CoreCommand =
        AdmitEvidence { EvidenceId = e; ContentKind = ContentKind.Text; Content = t content; Category = EvidenceCategory.Log; Source = t "gateway log"; CapturedAt = None }
    let private basis h : MarkHypothesis = { HypothesisId = h; Reason = t "assessed"; EvidenceIds = [ e1 ] }

    let private core = Policy.core

    let private investigation =
        run core
            [ StartInvestigation start
              ActivateInvestigation
              RecordObservation { ObservationId = ObservationId(g 3); Original = t "Error rate 12%"; ObservedAt = None }
              RecordHypothesis { HypothesisId = h1; Statement = t "Certificate expired"; Mechanism = Some(t "TLS handshake fails") }
              RecordHypothesis { HypothesisId = h2; Statement = t "Database saturated"; Mechanism = None }
              admit e1 "handshake_failure in gateway log"
              admit e2 "db CPU 40%"
              relate 40 e1 h1 RelationshipKind.Supports "handshake failures match the error window"
              relate 41 e2 h2 RelationshipKind.Contradicts "the database was idle"
              RecordInvestigationAction { ActionId = InvestigationActionId(g 50); Kind = ActionKind.Inspection; Purpose = t "Inspect certificate"; TargetHypothesisIds = [ h1 ] }
              RecordFinding { FindingId = FindingId(g 51); ActionId = InvestigationActionId(g 50); OriginalResult = t "expired 08:59"; Interpretation = Some(t "matches onset") } ]

    [<Fact>]
    let ``Kernel records an investigation's observations, hypotheses, evidence, assessments and findings`` () =
        let s = investigation
        Assert.Equal(Lifecycle.Active, s.Lifecycle)
        Assert.Equal(AggregateVersion 11L, s.Version)
        Assert.Equal(t "Checkout failures", s.Title)
        Assert.Equal(2, s.Observations.Count) // the problem statement is the first observation
        Assert.Equal(HypothesisState.Proposed, s.Hypotheses[h1].State)
        Assert.Equal(VerificationStatus.Unverified, s.Evidence[e1].Verification)
        Assert.Equal(Some(t "matches onset"), s.Findings[FindingId(g 51)].Value.Interpretation)
        // The projection's timeline is the history in version order; replaying it reproduces the state.
        let projection = Kernel.domain.Project s
        Assert.Equal<int64 list>([ 1L .. 11L ], projection.Timeline |> List.map (fun e -> AggregateVersion.value e.Version))
        Assert.Equal(Ok s, Kernel.domain.Replay projection.Timeline)
        // A command refused before side effects: a relationship to a missing hypothesis appends nothing.
        match submit core (Some s) 99 (relate 49 e1 (HypothesisId(g 99)) RelationshipKind.Supports "x") with
        | Blocked [ b ], after ->
            Assert.Equal("hypothesis.missing", b.Code)
            Assert.Equal(Some s, after)
        | other -> failwithf "%A" other

    [<Fact>]
    let ``A reassessment of evidence becomes current and keeps the earlier assessment`` () =
        let before = Assessments.ofHypothesis investigation h2
        Assert.Equal(RelationshipKind.Contradicts, (List.exactlyOne before).Value.Kind)
        let s =
            match submit core (Some investigation) 60 (relate 42 e2 h2 RelationshipKind.Supports "the replica was saturated, not the primary") with
            | Allowed _, Some s -> s
            | other -> failwithf "%A" other
        let current = Assessments.ofHypothesis s h2 |> List.exactlyOne
        Assert.Equal(RelationshipKind.Supports, current.Value.Kind)
        Assert.Equal(Some(t "the replica was saturated, not the primary"), current.Value.Rationale)
        let history = Assessments.history s e2 h2 |> List.map (fun r -> r.Value.Kind)
        Assert.Equal<RelationshipKind list>([ RelationshipKind.Contradicts; RelationshipKind.Supports ], history)
        // Superseded evidence no longer counts as a current assessment.
        // Superseded evidence no longer counts as a current assessment (its history stays).
        let last = List.head s.History
        let superseded =
            { last with
                EventId = EventId(g 59999)
                Version = AggregateVersion.next s.Version
                Payload = CoreEvent(EvidenceSuperseded { EvidenceId = e2; ReplacementEvidenceId = e1; Reason = t "metric was for the replica" }) }
        match Kernel.domain.Evolve (Some s) superseded with
        | Ok s2 ->
            Assert.Empty(Assessments.ofHypothesis s2 h2)
            Assert.Equal(2, (Assessments.history s2 e2 h2).Length)
        | Error e -> failwithf "%A" e

    let private cause = CausalAssertionId(g 70)
    let private confirm = ConfirmCause { CauseId = cause; Policy = t "causa.core/1" }

    let private blockersOf (d: Decision) =
        match d with
        | Blocked bs -> bs |> List.map (fun b -> b.Code) |> List.sort
        | other -> failwithf "expected Blocked, got %A" other

    [<Fact>]
    let ``The cause-confirmation gate lists every blocker and then allows confirmation`` () =
        let proposed =
            match submit core (Some investigation) 70 (RecordCausalAssertion { CauseId = cause; Outcome = t "checkout failures"; Members = [ { HypothesisId = h1; Role = CauseRole.RootCause } ]; Mechanism = Some(t "an expired certificate fails the TLS handshake") }) with
            | Allowed _, Some s -> s
            | other -> failwithf "%A" other
        let withUnknown =
            match submit core (Some proposed) 71 (IdentifyUnknown { UnknownId = UnknownId(g 72); Question = t "Was the renewal job run?"; Materiality = Materiality.Blocking }) with
            | Allowed _, Some s -> s
            | other -> failwithf "%A" other
        // h1 is only proposed, h2 is an unaddressed alternative, and a blocking unknown is open: all three are reported.
        Assert.Equal<string list>(
            [ "cause.alternative-unaddressed"; "cause.member-unsupported"; "cause.unknown-open" ],
            submit core (Some withUnknown) 72 confirm |> fst |> blockersOf)
        // The capability shows the same reasons, decided by the engine.
        let capability = Kernel.capabilities core actor withUnknown |> List.find (fun c -> c.Command = "ConfirmCause")
        match capability.State with
        | CapabilityState.Unavailable bs -> Assert.Equal(3, bs.Length)
        | other -> failwithf "%A" other
        // Support h1, falsify h2: the gate holds and the cause is confirmed.
        let ready =
            [ MarkHypothesisPlausible(basis h1); MarkHypothesisSupported(basis h1); FalsifyHypothesis(basis h2) ]
            |> List.indexed
            |> List.fold (fun s (i, c) -> match submit core (Some s) (80 + i) c with | Allowed _, Some x -> x | other -> failwithf "%A" other) proposed
        match submit core (Some ready) 90 confirm with
        | Allowed [ CoreEvent(CauseConfirmed _) ], Some s -> Assert.Equal(CauseState.Confirmed, s.Causes[cause].State)
        | other -> failwithf "%A" other
        // Composed policy: requiring verified support adds its blocker; an unknown component is unsupported, never ignored.
        let strict = { Id = "causa.core/1+causa.verified-support/1" }
        Assert.Equal<string list>([ "cause.support-unverified" ], submit strict (Some ready) 91 confirm |> fst |> blockersOf)
        match submit { Id = "causa.core/1+edf.unknown/9" } (Some ready) 92 confirm |> fst with
        | UnsupportedSemantics [ "policy edf.unknown/9" ] -> ()
        | other -> failwithf "%A" other
        // Without supporting evidence the gate refuses even supported members.
        let unsupported =
            run core
                [ StartInvestigation start; ActivateInvestigation
                  RecordHypothesis { HypothesisId = h1; Statement = t "Certificate expired"; Mechanism = None }
                  MarkHypothesisPlausible { HypothesisId = h1; Reason = t "r"; EvidenceIds = [] }
                  MarkHypothesisSupported { HypothesisId = h1; Reason = t "r"; EvidenceIds = [] }
                  RecordCausalAssertion { CauseId = cause; Outcome = t "o"; Members = [ { HypothesisId = h1; Role = CauseRole.Trigger } ]; Mechanism = None } ]
        Assert.Equal<string list>(
            [ "cause.no-mechanism"; "cause.no-root"; "cause.no-supporting-evidence" ],
            submit core (Some unsupported) 93 confirm |> fst |> blockersOf)

    [<Fact>]
    let ``The resolution gate requires a conclusion and satisfied verification`` () =
        let ca = CorrectiveActionId(g 80)
        let criterion = VerificationCriterionId(g 81)
        let s =
            run core
                [ StartInvestigation start; ActivateInvestigation
                  RecordCorrectiveAction { CorrectiveActionId = ca; Kind = CorrectiveActionKind.Correction; Description = t "renew certificate"; TargetCauseIds = [] }
                  RecordVerificationCriterion { CriterionId = criterion; CorrectiveActionId = ca; ExpectedOutcome = t "no handshake failures for 1 h"; Method = None } ]
        Assert.Equal<string list>(
            [ "resolution.criterion-unverified"; "resolution.no-conclusion" ],
            submit core (Some s) 101 ResolveInvestigation |> fst |> blockersOf)
        let verified =
            match submit core (Some s) 102 (RecordVerification { VerificationId = VerificationId(g 82); CriterionId = criterion; Outcome = VerificationOutcome.Satisfied; EvidenceIds = [] }) with
            | Allowed _, Some x -> x
            | other -> failwithf "%A" other
        // causa-storage records conclusions (ConclusionRecorded) but the frozen CoreCommand has no RecordConclusion, so
        // a conclusion can come only from history; the gate still refuses resolution without one.
        Assert.Equal<string list>([ "resolution.no-conclusion" ], submit core (Some verified) 103 ResolveInvestigation |> fst |> blockersOf)
        Assert.DoesNotContain("ResolveInvestigation", Kernel.domain.Project verified |> fun p -> p.Capabilities)
        Assert.Contains("RecordObservation", (Kernel.domain.Project verified).Capabilities)

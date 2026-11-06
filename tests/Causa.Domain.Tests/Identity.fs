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

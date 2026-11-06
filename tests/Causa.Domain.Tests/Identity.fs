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

// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-006 (live): the populated investigation fixture (causa-data v1.0.0 "authentication-outage").
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF006Tests =
    let populatedId = "a6747bd3-2794-5ac7-83b8-5ea2a0c50f5e"

    let private openPopulated (p: GitHubProvider) =
        FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        match ProviderPort.catalog p with
        | Error f -> failwith (ProviderPort.failureCode f)
        | Ok(_, rs) ->
            match (rs |> List.find (fun r -> r.Listed.Ref.Id = InvestigationId(Guid populatedId))).Result with
            | Ok v -> v
            | Error f -> failwith (ProviderPort.failureCode f)

    let private entities (name: string) (state: Canonical.JsonValue) =
        match Canonical.field name state with
        | Some(Canonical.JArray xs) -> xs
        | _ -> []

    let private statesOf name state =
        entities name state |> List.choose (fun e -> Canonical.str "state" e |> Option.orElse (Canonical.str "kind" e))

    /// O1: a compact complete investigation: observations, competing hypotheses, supporting and contradicting evidence,
    /// provenance on every entity, unknowns, causal relationships, corrective actions, verification records, timeline
    /// events, and entities in the states the semantic colours show (teal: supported / confirmed / verified effective;
    /// amber: pending verification of effect; coral: contradicting evidence and contradictions).
    let o1 (mk: string -> GitHubProvider) =
        let v = openPopulated (mk "pf006-o1")
        let state = ProviderPort.state v.Bytes.Events
        Assert.True(v.History.Length <= 100, "compact")
        Assert.NotEmpty(entities "observations" state)
        let hypotheses = statesOf "hypotheses" state
        Assert.True(hypotheses.Length >= 2 && (List.distinct hypotheses).Length >= 2, "competing hypotheses")
        let relations = statesOf "relationships" state
        Assert.Contains("supports", relations)
        Assert.Contains("contradicts", relations)
        for name in Projection.entityArrays do
            for e in entities name state do
                Assert.True((Canonical.field "provenance" e).IsSome, sprintf "%s entity without provenance" name)
        for name in [ "unknowns"; "causes"; "correctiveActions"; "verifications"; "contradictions" ] do
            Assert.NotEmpty(entities name state)
        // Timeline: every event carries its time, in order.
        let times = v.History |> List.map (fun e -> e.Provenance.OccurredAt)
        Assert.Equal<DateTimeOffset list>(List.sort times, times)
        Assert.True((List.distinct times).Length > 10)
        // Semantic states: teal, amber and coral each have a populated cause (navy and ivory are the structure and
        // surface every investigation view has).
        Assert.Contains("supported", hypotheses)
        Assert.Contains("confirmed", statesOf "causes" state)
        Assert.Contains("verifiedEffective", statesOf "correctiveActions" state)
        Assert.Contains("implemented", statesOf "correctiveActions" state)
        Assert.NotEmpty(statesOf "contradictions" state)

    /// O2: stable ids, timestamps and deterministic ordering: two independent reads (fixture and GitHub) give the same
    /// bytes and the same state; versions run 1..n; ids are causa-data's name-based UUIDs (v5).
    let o2 (mk: string -> GitHubProvider) =
        let fixture, _ = FixtureData.fixtureProvider []
        let a = openPopulated fixture
        let b = openPopulated (mk "pf006-o2")
        Assert.Equal(a.Bytes, b.Bytes)
        Assert.Equal(Canonical.canonical (ProviderPort.state a.Bytes.Events), Canonical.canonical (ProviderPort.state b.Bytes.Events))
        Assert.Equal<int64 list>([ 1L .. int64 b.History.Length ], b.History |> List.map (fun e -> let (AggregateVersion n) = e.Version in n))
        for e in b.History do
            let (EventId g) = e.EventId
            Assert.Equal('5', (g.ToString "D")[14])
        Assert.Equal('5', populatedId[14])
        Assert.Equal<_ list>(a.History |> List.map (fun e -> e.EventId, e.Provenance.OccurredAt), b.History |> List.map (fun e -> e.EventId, e.Provenance.OccurredAt))

    [<Fact>]
    let ``PF-006-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-006-O2`` () = o2 PfKit.live

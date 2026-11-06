// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-002 (live): one domain authority. Fixtures supply files; the F# engine validates and reconstructs both providers' data.
namespace Causa.Acceptance.Live

open System
open System.Reflection
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF002Tests =
    let private domainNamespace (t: Type) = t.Namespace = "Causa.Domain" || (t.IsGenericType && t.GetGenericArguments() |> Array.exists (fun a -> a.Namespace = "Causa.Domain"))

    /// O1: the fixture provider holds and serves bytes only. No member of the fake takes or returns a domain type, so it
    /// cannot decide, transition, replay or infer; and it stores an inadmissible history unchanged, which only the
    /// engine refuses when it is read, from the fixture and from GitHub alike.
    let o1 (mk: string -> GitHubProvider) =
        let fake =
            match typeof<FakeState>.Assembly.GetType("Causa.TestSupport.StatefulFake") with
            | null -> failwith "the StatefulFake module was not found"
            | t -> t
        let members = fake.GetMethods(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Static)
        Assert.NotEmpty(members)
        for m in members do
            Assert.False(domainNamespace m.ReturnType, sprintf "%s returns a domain type" m.Name)
            for p in m.GetParameters() do
                Assert.False(domainNamespace p.ParameterType, sprintf "%s takes a domain type" m.Name)
        for f in typeof<FakeState>.GetProperties() do
            Assert.False(domainNamespace f.PropertyType, sprintf "FakeState.%s is a domain value" f.Name)
        let invalid = FixtureData.edgeCases () |> List.find (fun c -> c.Name.StartsWith "invalid event history")
        let fixture, running = FixtureData.fixtureProvider []
        for p in [ fixture; mk "pf002-o1" ] do
            Assert.True(FixtureData.meets invalid (FixtureData.runEdgeCase p "pf002" invalid))
        // The fake kept the inadmissible bytes exactly as written: it did not validate them.
        let state = running.Current()
        let stored = state.Commits[state.Branches["causa-acceptance"]].Tree |> Map.toList |> List.filter (fun (k, _) -> k.EndsWith "/events.ndjson") |> List.map snd
        Assert.Contains(invalid.Files.Value.Events, stored)

    /// O2: the existing engine (Kernel.domain through the causa-storage reader) validates and reconstructs fixture and
    /// GitHub data: each reconstruction is the kernel's replay of the decoded history, and a tampered file set is refused
    /// with the same reader stage from both.
    let o2 (mk: string -> GitHubProvider) =
        let fixture, _ = FixtureData.fixtureProvider []
        let real = mk "pf002-o2"
        for p in [ fixture; real ] do
            FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
            match ProviderPort.catalog p with
            | Error f -> failwith (ProviderPort.failureCode f)
            | Ok(_, rs) ->
                Assert.Equal(5, rs.Length)
                for r in rs do
                    match r.Result with
                    | Error f -> failwith (ProviderPort.failureCode f)
                    | Ok v ->
                        Assert.Equal(Ok v.Investigation, Kernel.domain.Replay v.History)
                        Assert.True(ProviderPort.digestMatches r)
        let tampered = FixtureData.edgeCases () |> List.find (fun c -> c.Name.StartsWith "invalid schema")
        let codes = [ fixture; real ] |> List.map (fun p -> FixtureData.runEdgeCase p "pf002-tamper" tampered |> Result.mapError ProviderPort.failureCode |> Result.map ignore)
        Assert.Equal<Result<unit, string> list>([ Error "CAUSA.SCHEMA_VIOLATION"; Error "CAUSA.SCHEMA_VIOLATION" ], codes)

    [<Fact>]
    let ``PF-002-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-002-O2`` () = o2 PfKit.live

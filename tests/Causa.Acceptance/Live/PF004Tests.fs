// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-004 (live): fixture data follows the frozen causa-storage contract and is read through the same path as real data.
namespace Causa.Acceptance.Live

open System
open System.Text
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF004Tests =
    let private uuid = Text.RegularExpressions.Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")

    /// O1: the fixture files are the frozen v1.0.0 bytes, and the fixture provider and GitHub both hand them to the
    /// same lister, SM-LOAD and causa-storage reader, which accept them and reproduce the frozen expected states.
    let o1 (mk: string -> GitHubProvider) =
        let fixture, _ = FixtureData.fixtureProvider []
        let real = mk "pf004-o1"
        let results =
            [ for p in [ fixture; real ] ->
                  FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
                  match ProviderPort.catalog p with
                  | Error f -> failwith (ProviderPort.failureCode f)
                  | Ok(_, rs) ->
                      rs
                      |> List.map (fun r ->
                          match r.Result with
                          | Error f -> failwith (ProviderPort.failureCode f)
                          | Ok v ->
                              let (InvestigationId g) = v.Investigation.Id
                              // The bytes the reader verified are the frozen bytes, unchanged in transit.
                              Assert.Equal(FixtureData.frozenFiles (g.ToString "D"), v.Bytes)
                              Assert.True(ProviderPort.digestMatches r)
                              ProviderPort.summary v) ]
        Assert.Equal(5, results.Head.Length)
        Assert.Equal<_ list>(results.Head, results.Tail.Head)

    /// O2: the machine-readable fixture manifest (causa-data fixtures/v1.0.0/manifest.json) names every fixture's id,
    /// format and schema versions, expected version, lifecycle and digests, and every negative fixture's expected error;
    /// the investigations read from GitHub meet each expected value.
    let o2 (mk: string -> GitHubProvider) =
        let manifest = FixtureData.frozenManifest ()
        Assert.Equal(Some "1.0.0", Canonical.str "formatVersion" manifest)
        Assert.Matches("^[0-9a-f]{64}$", Canonical.str "schemaSha256" manifest |> Option.defaultValue "")
        let entries = FixtureData.frozenEntries ()
        Assert.Equal(5, entries.Length)
        for e in entries do
            Assert.Matches(uuid, Canonical.str "investigationId" e |> Option.defaultValue "")
            for digest in [ "eventsSha256"; "expectedStateSha256"; "manifestSha256"; "snapshotSha256" ] do
                Assert.Matches("^[0-9a-f]{64}$", Canonical.str digest e |> Option.defaultValue "")
            Assert.True((Canonical.integer "expectedVersion" e).IsSome)
            Assert.True((Canonical.str "expectedLifecycle" e).IsSome)
        let negatives = FixtureData.negativeEntries ()
        Assert.True(negatives.Length >= 50)
        Assert.All(negatives, fun (path, code) -> Assert.StartsWith("negative/", path); Assert.StartsWith("CAUSA.", code))
        let real = mk "pf004-o2"
        FixtureData.seed real (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        match ProviderPort.catalog real with
        | Error f -> failwith (ProviderPort.failureCode f)
        | Ok(_, rs) ->
            for e in entries do
                let id = Canonical.str "investigationId" e |> Option.get
                let r = rs |> List.find (fun r -> r.Listed.Ref.Id = InvestigationId(Guid id))
                match r.Result with
                | Error f -> failwith (ProviderPort.failureCode f)
                | Ok v ->
                    let (AggregateVersion n) = v.Investigation.Version
                    Assert.Equal(Canonical.integer "expectedVersion" e, Some n)
                    Assert.Equal(Canonical.str "expectedStateSha256" e, Some(ProviderPort.stateDigest v))
                    Assert.Equal(Canonical.str "expectedLifecycle" e, Canonical.str "lifecycle" (ProviderPort.state v.Bytes.Events))

    /// O3: the frozen historical dataset is never mutated: every embedded file still has the digest the frozen manifest
    /// records, before and after it is written to and read back from GitHub, and what comes back is byte-identical.
    let o3 (mk: string -> GitHubProvider) =
        let check () =
            for e in FixtureData.frozenEntries () do
                let fs = FixtureData.frozenFiles (Canonical.str "investigationId" e |> Option.get)
                Assert.Equal(Canonical.str "eventsSha256" e, Some(Canonical.sha256Hex fs.Events))
                // manifestSha256 and snapshotSha256 are canonical JSON digests (CAUSA-DATA-CONTRACT-v1 section 4).
                let json (b: byte[]) = Canonical.parse (Encoding.UTF8.GetString(b).TrimEnd('\n')) |> Result.defaultWith failwith
                Assert.Equal(Canonical.str "manifestSha256" e, Some(Canonical.digest (json fs.Manifest)))
                Assert.Equal(Canonical.str "snapshotSha256" e, Some(Canonical.digest (json fs.Snapshot)))
        check ()
        let real = mk "pf004-o3"
        let commit = FixtureData.seed real (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog"
        for path, bytes in FixtureData.frozenCatalog () do
            Assert.Equal(Some bytes, Scenarios.readFile real commit path |> Probes.ok path)
        check ()

    [<Fact>]
    let ``PF-004-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-004-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-004-O3`` () = o3 PfKit.live

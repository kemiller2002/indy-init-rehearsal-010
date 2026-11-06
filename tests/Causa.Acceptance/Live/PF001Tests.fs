// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-001 (live): one provider-neutral investigation-data port, shared by the fixture provider and the GitHub provider.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF001Tests =
    /// Both providers hold the frozen fixtures as a catalog, written by the application's own save mutation.
    let private seeded (mk: string -> GitHubProvider) (label: string) =
        let fixture, _ = FixtureData.fixtureProvider []
        let real = mk label
        for p in [ fixture; real ] do
            FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        fixture, real

    let private reconstruct (p: GitHubProvider) =
        match ProviderPort.catalog p with
        | Error f -> failwithf "catalog: %s" (ProviderPort.failureCode f)
        | Ok(listing, rs) -> listing, rs |> List.map (fun r -> r.Listed, (match r.Result with Ok v -> v | Error f -> failwithf "open: %s" (ProviderPort.failureCode f)))

    /// O1: the typed port (list, open, reconstruct over IF41-WIRE Execute) is the same code for both providers, and
    /// it yields the same reconstructions from both.
    let o1 (mk: string -> GitHubProvider) =
        let fixture, real = seeded mk "pf001-o1"
        let (fl: Listing), fixtureResults = reconstruct fixture
        let (rl: Listing), realResults = reconstruct real
        Assert.Equal(5, fixtureResults.Length)
        Assert.Equal<Guid list>(fl.Investigations |> List.map (fun i -> let (InvestigationId g) = i.Ref.Id in g), rl.Investigations |> List.map (fun i -> let (InvestigationId g) = i.Ref.Id in g))
        let summaries rs = rs |> List.map (snd >> ProviderPort.summary)
        Assert.Equal<_ list>(summaries fixtureResults, summaries realResults)
        for listed, v in realResults do
            Assert.Equal(listed.ExpectedStateSha256, Some(ProviderPort.stateDigest v))
        ProviderEvidence.record real "PF-001-O1" [ rl.ReadAt ] [ "investigations", string realResults.Length ] |> ignore

    /// O2: the same result, error, cancellation, freshness and provenance semantics from either provider.
    let o2 (mk: string -> GitHubProvider) =
        let fixture, real = seeded mk "pf001-o2"
        let semantics (p: GitHubProvider) =
            let listing, _ = reconstruct p
            let first = listing.Investigations.Head.Ref
            // Error: an investigation the workspace does not hold is NotFound, from the provider, not a domain failure.
            let missing = ProviderPort.openRef p { first with Id = InvestigationId(Guid "0e7e0000-0000-4000-8000-0000000000a1") }
            // Cancellation: an answer to a request that is no longer awaited is superseded, never applied.
            let machine = SaveMachines.create SaveMachines.support
            let ctx = ProviderPort.context p
            let loading, effects = machine.StepLoad ctx NotLoaded (LoadInput.OpenRequested first) |> Result.defaultWith (fun f -> failwith f.Reason)
            let request = (SaveKit.requests effects).Head
            let earlier = p.Execute { request with Id = RequestId(-1L) }
            let cancelled = machine.InterpretLoad (ProviderPort.context p) loading earlier
            // Freshness: after another commit, a refresh reads at the new head.
            let v = ProviderPort.openRef p first |> Result.defaultWith (fun f -> failwith (ProviderPort.failureCode f))
            let other = FixtureData.seed p [ "workspaces/demo/causa-app-other/notes.json", "{}\n"B ] "another writer"
            let refreshed = ProviderPort.openRef p first |> Result.defaultWith (fun f -> failwith (ProviderPort.failureCode f))
            Assert.Equal(other, refreshed.ReadAt)
            Assert.NotEqual(v.ReadAt, refreshed.ReadAt)
            // Provenance: where the history was read from, and every event's recorded actor and correlation.
            Assert.Equal(WorkspaceRole.Catalog, refreshed.Address.Role)
            let provenance = refreshed.History |> List.map (fun e -> e.Provenance.Actor, e.Provenance.CorrelationId, e.Provenance.OccurredAt)
            (missing |> Result.mapError ProviderPort.failureCode |> Result.map ignore), (sprintf "%A" cancelled), provenance
        let fm, fc, fp = semantics fixture
        let rm, rc, rp = semantics real
        Assert.Equal(Error "provider:NotFound", fm)
        Assert.Equal(fm, rm)
        Assert.Equal("LoadEvent (SupersededReply (RequestId -1L))", fc.Replace("\n", "").Replace("  ", " "))
        Assert.Equal(fc, rc)
        Assert.Equal<_ list>(fp, rp)

    /// O3: which provider answered changes neither the domain meaning (the reconstructed investigation and what the
    /// engine decides on it) nor authorization (a catalog investigation is never writable).
    let o3 (mk: string -> GitHubProvider) =
        let fixture, real = seeded mk "pf001-o3"
        let _, fr = reconstruct fixture
        let _, rr = reconstruct real
        for (_, a), (_, b) in List.zip fr rr do
            Assert.Equal(a.Investigation, b.Investigation)
            Assert.Equal(Kernel.domain.Project a.Investigation, Kernel.domain.Project b.Investigation)
            let command: CommandEnvelope =
                { CommandId = Guid "0e7e0000-0000-4000-8000-0000000000c1"
                  Investigation = a.Investigation.Id
                  ExpectedVersion = Some a.Investigation.Version
                  Command = Core(RecordObservation { ObservationId = ObservationId(Guid "0e7e0000-0000-4000-8000-0000000000c2"); Original = SaveKit.text "Same meaning"; ObservedAt = None }) }
            let decide (v: VerifiedInvestigation) = sprintf "%A" (Kernel.domain.Decide Transitions.corePolicy SaveMachines.support.Actor (Some v.Investigation) command)
            Assert.Equal(decide a, decide b)
            Assert.Equal(None, Writable.ofAddress a.Address)
            Assert.Equal(None, Writable.ofAddress b.Address)
            Assert.True((Persistence.sessionOf a).IsNone && (Persistence.sessionOf b).IsNone)

    [<Fact>]
    let ``PF-001-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-001-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-001-O3`` () = o3 PfKit.live

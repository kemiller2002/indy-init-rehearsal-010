// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// EXT-DATA-010 (live): the R001 datasets stay an immutable baseline; external-data startup, authorization, read,
// write and recovery are measured independently; no provider PASS is claimed from mocked requests.
namespace Causa.Acceptance.Live

open System
open System.Diagnostics
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module EXTDATA010Tests =
    /// O1: the R001 baseline (the five frozen v1.0.0 investigations) is immutable: every file still has the digest the
    /// frozen manifest records, it is never part of a production assembly, and it is written to and read back from
    /// GitHub byte-identical.
    let o1 (mk: string -> GitHubProvider) =
        let unchanged () =
            for e in FixtureData.frozenEntries () do
                let fs = FixtureData.frozenFiles (Canonical.str "investigationId" e |> Option.get)
                Assert.Equal(Canonical.str "eventsSha256" e, Some(Canonical.sha256Hex fs.Events))
        unchanged ()
        for a in [ typeof<DomainKernel>.Assembly; typeof<SaveMachine>.Assembly; typeof<Causa.App.Model>.Assembly ] do
            Assert.DoesNotContain(a.GetTypes(), fun t -> t.Name = "Datasets")
        let p = mk "extdata010-o1"
        let commit = FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa: R001 baseline, read only"
        for path, bytes in FixtureData.frozenCatalog () do
            Assert.Equal(Some bytes, Scenarios.readFile p commit path |> Probes.ok path)
        unchanged ()

    type private Phase = { Name: string; Milliseconds: int64; Passed: bool; Detail: string }

    let private measure (name: string) (f: unit -> bool * string) =
        let w = Stopwatch.StartNew()
        let passed, detail = try f () with e -> false, e.Message
        { Name = name; Milliseconds = w.ElapsedMilliseconds; Passed = passed; Detail = detail }

    /// The five phases, each measured on its own with its own outcome.
    let private phases (p: GitHubProvider) =
        FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa: populate the test repository" |> ignore
        let app = EngineDriver.start p
        let mutable run: LiveRun.T option = None
        [ measure "startup" (fun () -> app.Model.Loads.IsEmpty && app.Model.Context.IsNone, "fresh tab, nothing loaded")
          measure "authorization" (fun () ->
              EngineDriver.connect app p.Connection.Credential
              (match app.Model.Connection with Causa.App.Connected _ -> true | _ -> false), sprintf "%A" app.Model.Connection)
          measure "read" (fun () ->
              EngineDriver.listCatalog app
              let listing = app.Model.Listings |> Map.toList |> List.map snd |> List.find (fun l -> l.Address.Role = WorkspaceRole.Catalog)
              for i in listing.Investigations do EngineDriver.openInvestigation app i.Ref
              listing.Investigations |> List.forall (fun i -> match app.Model.Loads.TryFind i.Ref with Some(Loaded v) -> Some(ProviderPort.stateDigest v) = i.ExpectedStateSha256 | _ -> false), "catalog read and verified")
          measure "write" (fun () ->
              let r = LiveRun.createOn p "extdata010"
              LiveRun.edit r "measured write" |> ignore
              LiveRun.input r SaveInput.SaveRequested |> ignore
              run <- Some r
              MachineKit.stateName r.Session.State = "Saved", MachineKit.stateName r.Session.State)
          measure "recovery" (fun () ->
              // A save whose answer is lost after GitHub applied it is recovered by reconciliation, then reloaded.
              let r = run.Value
              LiveRun.edit r "recovered write" |> ignore
              let losing = { r with Provider = PfKit.through (fun req o -> if SaveKit.isMutation req then NotAnswered(req.Id, LostAfterSending) else o) r.Provider }
              LiveRun.input losing SaveInput.SaveRequested |> ignore
              let afterLoss = MachineKit.stateName losing.Session.State
              Threading.Thread.Sleep(if p.Mode = LiveProvider then 2000 else 0)
              match losing.Machine.ReconcileDue (SaveKit.context losing.Clock) losing.Session with
              | Ok(next, effects) ->
                  SaveKit.advance losing.Clock effects
                  let final, _, _, _ = SaveKit.driveAll losing.Machine losing.Clock r.Provider.Execute next (SaveKit.requests effects)
                  let reloaded = LiveRun.openInNewTab { r with Session = final }
                  afterLoss = "UnknownOutcome" && MachineKit.stateName final.State = "Saved" && (match reloaded with Ok v -> v.Investigation.Version = AggregateVersion 3L | Error _ -> false),
                  sprintf "%s then %s" afterLoss (MachineKit.stateName final.State)
              | Error f -> false, f.Reason) ]

    /// O2: startup, authorization, read, write and recovery are measured independently: each phase has its own time
    /// and its own outcome in the evidence.
    let o2 (mk: string -> GitHubProvider) =
        let p = mk "extdata010-o2"
        let measured = phases p
        Assert.Equal<string list>([ "startup"; "authorization"; "read"; "write"; "recovery" ], measured |> List.map (fun m -> m.Name))
        let facts = [ for m in measured do yield m.Name + ".ms", string m.Milliseconds; yield m.Name + ".passed", string m.Passed ]
        let line = ProviderEvidence.record p "EXT-DATA-010-O2" [] facts
        for m in measured do
            Assert.True(m.Passed, sprintf "%s failed: %s" m.Name m.Detail)
            Assert.Contains(sprintf "\"%s.ms\"" m.Name, line)

    /// O3: no provider PASS from mocked requests: the same phases on the fixture provider are labelled fake-only, and the
    /// live phases pass only on GitHub's own answers.
    let o3 (mk: string -> GitHubProvider) =
        let p = mk "extdata010-o3"
        let sent = ResizeArray<HttpOutcome>()
        let observed = { p with Execute = fun r -> let o = p.Execute r in sent.Add o; o }
        let measured = phases observed
        Assert.All(measured, fun m -> Assert.True(m.Passed, m.Name + ": " + m.Detail))
        Assert.NotEmpty(sent)
        Assert.All(sent |> Seq.filter (function Answered _ -> true | _ -> false), fun o -> Assert.Equal(p.Mode = LiveProvider, PfKit.fromGitHub o))
        let fixture, _ = FixtureData.fixtureProvider []
        Assert.Contains("fake-only, not live verification", ProviderEvidence.line fixture "EXT-DATA-010-O3" [] [])
        Assert.Equal(p.Mode = LiveProvider, (ProviderEvidence.line p "EXT-DATA-010-O3" [] []).Contains "\"verified\":\"live\"")

    [<Fact>]
    let ``EXT-DATA-010-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``EXT-DATA-010-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``EXT-DATA-010-O3`` () = o3 PfKit.live

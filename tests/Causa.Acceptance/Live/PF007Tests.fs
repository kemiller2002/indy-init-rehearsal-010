// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-007 (live): the edge-case fixtures and their explicit, fail-closed expected outcomes.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF007Tests =
    let private required =
        [ "empty investigation"; "completed and verified investigation"; "missing provenance"; "incomplete provenance"
          "unresolved and contradictory evidence"; "invalid event history"; "invalid schema"; "missing investigation" ]

    /// O1: every edge case is supplied (empty; completed and verified; missing or incomplete provenance; unresolved and
    /// contradictory evidence; invalid event history; invalid schema; malformed or missing catalog; missing
    /// investigation) and has its expected outcome when read from GitHub.
    let o1 (mk: string -> GitHubProvider) =
        let cases = FixtureData.edgeCases ()
        for name in required do
            Assert.Contains(cases, fun c -> c.Name.StartsWith name)
        let real = mk "pf007-o1"
        for c in cases do
            let outcome = FixtureData.runEdgeCase real "pf007" c
            Assert.True(FixtureData.meets c outcome, sprintf "%s: %A" c.Name (outcome |> Result.map ignore |> Result.mapError ProviderPort.failureCode))
        let catalogs = FixtureData.catalogEdgeCases ()
        Assert.Contains(catalogs, fun (n, _, _) -> n.StartsWith "malformed catalog")
        Assert.Contains(catalogs, fun (n, _, _) -> n.StartsWith "missing catalog")
        for name, files, expected in catalogs do
            let p = mk "pf007-o1-catalog"
            // The case's catalog files replace whatever catalog the branch holds.
            PfKit.replace p files [ "catalog/active.json" ] ("Causa fixture: " + name) |> ignore
            let got = ProviderPort.list p ActiveCatalog |> Result.map ignore |> Result.mapError ProviderPort.failureCode
            Assert.True((got = Error expected), sprintf "%s: %A" name got)

    /// O2: each outcome is explicit and every refusal fails closed: SM-LOAD ends in LoadFailed with the stated cause and
    /// nothing loaded, the same from the fixture provider and from GitHub.
    let o2 (mk: string -> GitHubProvider) =
        let fixture, _ = FixtureData.fixtureProvider []
        let real = mk "pf007-o2"
        for c in FixtureData.edgeCases () do
            match c.Expected with
            | FixtureData.Opens(_, meaning) -> Assert.False(String.IsNullOrWhiteSpace meaning)
            | FixtureData.RefusedWith code -> Assert.False(String.IsNullOrWhiteSpace code)
            let outcomes =
                [ for p in [ fixture; real ] ->
                      let run = sprintf "pf007-%s" (Guid.NewGuid().ToString("N").Substring(0, 8))
                      let address = FixtureData.edgeWorkspace run
                      let files = FixtureData.filesOfEdgeCases address [ c ]
                      if not files.IsEmpty then FixtureData.seed p files c.Name |> ignore
                      let machine = SaveMachines.create SaveMachines.support
                      let r: InvestigationRef = { Address = address; Id = InvestigationId c.Id }
                      let state, effects = machine.StepLoad (ProviderPort.context p) NotLoaded (LoadInput.OpenRequested r) |> Result.defaultWith (fun f -> failwith f.Reason)
                      let rec drive state (request: HttpRequest) =
                          match machine.InterpretLoad (ProviderPort.context p) state (p.Execute request) with
                          | LoadNextRequest(next, req) -> drive next req
                          | LoadEvent input -> machine.StepLoad (ProviderPort.context p) state input |> Result.defaultWith (fun f -> failwith f.Reason) |> fst
                      drive state (SaveKit.requests effects).Head ]
            for final in outcomes do
                match c.Expected, final with
                | FixtureData.Opens(check, meaning), Loaded v -> Assert.True(check v, sprintf "%s: %s" c.Name meaning)
                | FixtureData.RefusedWith code, LoadFailed f -> Assert.Equal(code, ProviderPort.failureCode f)
                | _, other -> failwithf "%s: unexpected final state %A" c.Name other
            let names = outcomes |> List.map (fun s -> match s with Loaded _ -> "Loaded" | LoadFailed f -> ProviderPort.failureCode f | other -> sprintf "%A" other)
            Assert.Equal(names.Head, names.Tail.Head)

    [<Fact>]
    let ``PF-007-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-007-O2`` () = o2 PfKit.live

// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// PERSIST-041 (live, the dotnet half; the browser half is tests/browser/live/PERSIST-041.spec.js): three tabs save
// changes to different investigations at the same moment.
namespace Causa.Acceptance.Live

open System
open System.Text.Json
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PERSIST041Tests =
    /// Three tabs (each its own machine and tab memory) with one investigation each on one branch, each with an
    /// unsaved change; the three saves start at the same moment.
    let simultaneous (p: GitHubProvider) =
        let tabs = [ for i in 1..3 -> LiveRun.createOn p (sprintf "persist041-%d" i) ]
        let created = tabs |> List.map (fun t -> t.Sent.Length)
        for t in tabs do
            LiveRun.edit t (sprintf "simultaneous change of %s" (LiveRun.prefix t)) |> ignore
        use gate = new Threading.Barrier(tabs.Length)
        tabs
        |> List.map (fun t -> Threading.Tasks.Task.Run(fun () -> gate.SignalAndWait(); LiveRun.input t SaveInput.SaveRequested |> ignore))
        |> Array.ofList
        |> Threading.Tasks.Task.WaitAll
        List.zip tabs created

    let private marker (r: HttpRequest) =
        use doc = JsonDocument.Parse(r.Body.Value)
        doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("message").GetProperty("body").GetString() |> string |> GitHubRequests.markerOf

    let private paths (r: HttpRequest) =
        use doc = JsonDocument.Parse(r.Body.Value)
        let changes = doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("fileChanges")
        [ for name in [ "additions"; "deletions" ] do
              for x in changes.GetProperty(name).EnumerateArray() -> string (x.GetProperty("path").GetString()) ]

    /// O3: every mutation is one createCommitOnBranch with an expected head and paths inside its investigation, every
    /// marker is unique, and every commit stays reachable from the branch head; each change is confirmed once.
    let o3 (mk: string -> GitHubProvider) =
        let p = mk "persist041-o3"
        let tabs = simultaneous p
        let all = [ for t, _ in tabs -> t, LiveRun.mutations t ]
        for t, mutations in all do
            Assert.Equal("Saved", MachineKit.stateName t.Session.State)
            Assert.NotEmpty(mutations)
            for m in mutations do
                Assert.EndsWith("/graphql", m.Url)
                match GitHubRequests.operationOf m with
                | Some(CommitOnBranch(CommitOid e)) -> Assert.Matches("^[0-9a-f]{40}$", e)
                | other -> failwithf "not a createCommitOnBranch: %A" other
                Assert.All(paths m, fun path -> Assert.StartsWith(LiveRun.prefix t + "/", path))
        let markers = all |> List.collect (snd >> List.map marker)
        Assert.All(markers, fun m -> Assert.True(m.IsSome))
        Assert.Equal(markers.Length, (List.distinct markers).Length)
        // Every confirmed commit is on the branch; each tab's change was confirmed exactly once (one commit carries it).
        for t, created in tabs do
            Assert.Equal(Ok true, Scenarios.reachable p t.Session.BaseHead)
            let mine = LiveRun.mutations { t with Sent = t.Sent |> List.skip created } |> List.map marker
            let onBranch = mine |> List.filter (fun m -> (Scenarios.commitMessage p t.Session.BaseHead |> Probes.ok "message" |> GitHubRequests.markerOf) = m)
            Assert.Equal(1, onBranch.Length)
        ProviderEvidence.record p "PERSIST-041-O3" [ for t, _ in tabs -> t.Session.BaseHead ] [ "mutations", string markers.Length ] |> ignore

    [<Fact>]
    let ``PERSIST-041-O3`` () = o3 PfKit.live

// Owner: persistence.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

/// PERSIST-025: the GitHub adapter behind a provider interface, tested against the stateful fake and the real test
/// repository. Every test needs CAUSA_GITHUB_TOKEN; without it the test fails as BLOCKED (never passes).
module PERSIST025Tests =

    let private live (label: string) = LiveGitHub.freshBranch (LiveGitHub.require ()) label

    let private fake () = Scenarios.fakeProvider "causa-acceptance" [ "README.md", "synthetic"B ]

    /// PAT-02 core: one save (one createCommitOnBranch at the read head) and its read-back.
    let private saveAndReadBack (p: GitHubProvider) (prefix: string) =
        let head = Scenarios.readHead p |> Probes.ok "save: head"
        let change = Scenarios.commitRequest head [ prefix + "/events.ndjson", "{\"seq\":1}\n" ] (Probes.marker "save")
        let commit = Scenarios.commit p change |> snd |> Probes.ok "save: commit"
        let bytes = Scenarios.readFile p commit (prefix + "/events.ndjson") |> Probes.ok "save: read back"
        head, commit, change, bytes

    let private hasGitHubRequestId (outcome: HttpOutcome) =
        match outcome with
        | Answered(_, _, headers, _) -> headers |> List.exists (fun (k, _) -> String.Equals(k, "x-github-request-id", StringComparison.OrdinalIgnoreCase))
        | NotAnswered _ -> false

    [<Fact>]
    let ``PERSIST-025-O1`` () =
        let p = live "persist025-o1"
        let f, running = fake ()
        // The same adapter code (GitHubRequests + Classify over IF41-WIRE's Execute) runs against both providers.
        for provider in [ f; p ] do
            let _, commit, change, bytes = saveAndReadBack provider (Probes.investigationPrefix "boundary")
            Assert.Equal(Some change.Additions.Head.Contents, bytes)
            Assert.Equal(Ok true, Scenarios.reachable provider commit)
        // The fake saw only requests the adapter built: each is a GitHub operation the adapter names.
        let log = running.Current().Log
        Assert.NotEmpty(log)
        Assert.All(log, fun entry -> Assert.True(entry.Operation.IsSome, sprintf "unrecognised request %s" entry.Url))
        Assert.Equal(LiveProvider, p.Mode)
        Assert.Equal(FakeProvider, f.Mode)

    [<Fact>]
    let ``PERSIST-025-O2`` () =
        let p = live "persist025-o2"
        let f, running = fake ()
        let prefix = Probes.investigationPrefix "fake"
        // expectedHeadOid is enforced, in GitHub's own answer form (checked against the live answer).
        let fakeStale = Probes.staleHead f prefix
        let liveStale = Probes.staleHead p (Probes.investigationPrefix "live")
        Assert.Equal(Scenarios.kind liveStale.Result, Scenarios.kind fakeStale.Result)
        Assert.Equal("StaleHead", Scenarios.kind fakeStale.Result)
        // Real ancestry: each commit's parent is the head it was made at; a simulated other writer's commit stays an
        // ancestor of every later head, on the fake as on GitHub.
        let state = running.Current()
        let (CommitOid resent) = fakeStale.Resent
        let (CommitOid other) = fakeStale.OtherHead
        Assert.Equal(Some other, state.Commits[resent].Parent)
        Assert.True(StatefulFake.isAncestor state other resent)
        Assert.Equal(Ok true, Scenarios.reachable f fakeStale.OtherHead)
        Assert.Equal(Ok true, Scenarios.reachable p liveStale.OtherHead)
        // Commits from simulated other writers outside the request path.
        running.Update(fun s -> StatefulFake.otherWriterCommits "causa-acceptance" "device-2" [ prefix + "-x/a.json", "{}"B ] [] s |> fst)
        let afterOther = Scenarios.readHead f |> Probes.ok "head"
        Assert.Equal(Ok true, Scenarios.reachable f fakeStale.Resent)
        Assert.NotEqual(fakeStale.Resent, afterOther)
        // Losing the answer after applying: the commit is there, carrying the marker.
        running.Update(StatefulFake.injectFault NextMutation LoseAnswerAfterApplying)
        let afterApplying = Probes.lostAnswer f f.Execute prefix
        Assert.Equal(Error(NetworkUnavailable LostAfterSending), afterApplying.Classified)
        Assert.True(afterApplying.MarkerAtHead)
        Assert.NotEqual(afterApplying.Expected, afterApplying.HeadAfter)
        // Losing the answer before applying: nothing was written, the head is unchanged.
        running.Update(StatefulFake.injectFault NextMutation LoseAnswerBeforeApplying)
        let beforeApplying = Probes.lostAnswer f f.Execute prefix
        Assert.Equal(Error(NetworkUnavailable LostAfterSending), beforeApplying.Classified)
        Assert.False(beforeApplying.MarkerAtHead)
        Assert.Equal(beforeApplying.Expected, beforeApplying.HeadAfter)
        // The moved-head race form (KD-022) is GitHub's other stale answer; the fake can give it, and it classifies the same.
        running.Update(StatefulFake.injectFault NextMutation MovedHeadRace)
        let raced = Probes.staleHead f (Probes.investigationPrefix "fake")
        Assert.Equal<string list>([ "FORBIDDEN" ], Scenarios.errorTypes raced.Outcome)
        Assert.Equal("StaleHead", Scenarios.kind raced.Result)
        ProviderEvidence.record p "PERSIST-025-O2" [ liveStale.OtherHead; liveStale.Resent ] [ "liveStaleErrorTypes", String.Join(",", Scenarios.errorTypes liveStale.Outcome) ]
        |> ignore

    [<Fact>]
    let ``PERSIST-025-O3`` () =
        let p = live "persist025-o3"
        let prefix = Probes.investigationPrefix "live"
        // The answers come from GitHub itself (its request id header), not from a double.
        let headRequest = GitHubRequests.branchHead (Scenarios.nextId ()) p.Connection
        let headOutcome = p.Execute headRequest
        Assert.True(hasGitHubRequestId headOutcome, "the live provider's answer does not come from GitHub")
        let head, commit, change, bytes = saveAndReadBack p prefix
        Assert.Equal(Some change.Additions.Head.Contents, bytes)
        // The commit exists on GitHub with this save's marker, as a child of the head it was made at.
        let message = Scenarios.commitMessage p commit |> Probes.ok "commit message"
        Assert.Equal(change.Marker, GitHubRequests.markerOf message)
        Assert.Equal(Ok commit, Scenarios.readHead p)
        Assert.Equal(Ok true, Scenarios.reachable p head)
        ProviderEvidence.record p "PERSIST-025-O3" [ commit ] [] |> ignore

    [<Fact>]
    let ``PERSIST-025-O4`` () =
        let p = live "persist025-o4"
        let f, _ = fake ()
        // A fake answer can never pass as GitHub's: the fake sends no GitHub request id, the live provider does.
        let fakeOutcome = f.Execute(GitHubRequests.branchHead (Scenarios.nextId ()) f.Connection)
        let liveOutcome = p.Execute(GitHubRequests.branchHead (Scenarios.nextId ()) p.Connection)
        Assert.False(hasGitHubRequestId fakeOutcome)
        Assert.True(hasGitHubRequestId liveOutcome)
        // Evidence states the mode: a fake run is labelled fake-only and never "live".
        let fakeLine = ProviderEvidence.line f "PERSIST-025-O4" [] []
        let liveLine = ProviderEvidence.line p "PERSIST-025-O4" [] []
        Assert.Contains("\"mode\":\"fake\"", fakeLine)
        Assert.Contains("fake-only, not live verification", fakeLine)
        Assert.DoesNotContain("\"verified\":\"live\"", fakeLine)
        Assert.Contains("\"verified\":\"live\"", liveLine)
        // Without the credential the live provider is BLOCKED; it never falls back to the fake.
        Assert.Equal(Error LiveGitHub.blockedMessage, LiveGitHub.configurationFrom (fun _ -> None))
        ProviderEvidence.record p "PERSIST-025-O4" [] [ "fakeLabel", "fake-only" ] |> ignore

    [<Fact>]
    let ``PERSIST-025-O5`` () =
        let p = live "persist025-o5"
        let f, _ = fake ()
        let run (provider: GitHubProvider) (label: string) =
            let prefix = Probes.investigationPrefix label
            // PAT-02: a save confirmed by read-back.
            let _, saved, change, bytes = saveAndReadBack provider prefix
            Assert.Equal(Some change.Additions.Head.Contents, bytes)
            // PAT-03: a stale head is refused and nothing is written.
            let stale = Probes.staleHead provider prefix
            Assert.Equal("StaleHead", Scenarios.kind stale.Result)
            Assert.Equal(None, stale.MineAtHeadAfter)
            // PAT-04: an unrelated commit moved the branch; the files of this investigation are unchanged between the
            // base and the fresh head, so the same commit is sent at the fresh head and both commits stay reachable.
            let baseHead = Scenarios.readHead provider |> Probes.ok "pat04 head"
            let unrelated = Probes.otherWriter provider (prefix + "-unrelated/u.json") "{}"
            let fresh = Scenarios.readHead provider |> Probes.ok "pat04 fresh"
            let mine = prefix + "/events.ndjson"
            Assert.Equal(Scenarios.readFile provider baseHead mine, Scenarios.readFile provider fresh mine)
            let rebased =
                Scenarios.commit provider (Scenarios.commitRequest fresh [ mine, "{\"seq\":1}\n{\"seq\":2}\n" ] (Probes.marker "pat04")) |> snd |> Probes.ok "pat04 resend"
            Assert.Equal(Ok true, Scenarios.reachable provider unrelated)
            // PAT-05: the same investigation changed on the branch; its files differ, so nothing is sent (Conflict).
            let conflictBase = Scenarios.readHead provider |> Probes.ok "pat05 base"
            let theirs = Probes.otherWriter provider mine "{\"seq\":1}\n{\"seq\":2}\n{\"theirs\":3}\n"
            let conflictFresh = Scenarios.readHead provider |> Probes.ok "pat05 fresh"
            Assert.NotEqual(Scenarios.readFile provider conflictBase mine, Scenarios.readFile provider conflictFresh mine)
            Assert.Equal(Ok true, Scenarios.reachable provider theirs)
            // PAT-08: a branch that advances before every attempt gets at most four mutations for one save request.
            let rec attempt n acc =
                if n > 4 then acc
                else
                    let h = Scenarios.readHead provider |> Probes.ok "pat08 head"
                    Probes.otherWriter provider (sprintf "%s-busy/%d.json" prefix n) "{}" |> ignore
                    let _, r = Scenarios.commit provider (Scenarios.commitRequest h [ prefix + "/busy.json", "{}" ] (Probes.marker "pat08"))
                    attempt (n + 1) (r :: acc)
            let attempts = attempt 1 []
            Assert.Equal(4, attempts.Length)
            Assert.All(attempts, fun r -> Assert.Equal("StaleHead", Scenarios.kind r))
            [ saved; stale.OtherHead; stale.Resent; unrelated; rebased; theirs ]
        let fakeCommits = run f "fake"
        let liveCommits = run p "live"
        Assert.Equal(fakeCommits.Length, liveCommits.Length)
        // The live evidence names the real commits it created, and each exists and is reachable from the branch head.
        let line = ProviderEvidence.record p "PERSIST-025-O5" liveCommits [ "scenarios", "PAT-02,PAT-03,PAT-04,PAT-05,PAT-08" ]
        for (CommitOid c as commit) in liveCommits do
            Assert.Contains(c, line)
            Assert.Equal(Ok true, Scenarios.reachable p commit)
            Assert.True(Result.isOk (Scenarios.commitMessage p commit))

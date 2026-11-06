// Owner: persistence. PERSIST-006 (live): one createCommitOnBranch per save, with all three files, at the read head.
namespace Causa.Acceptance.Live

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST006Tests =
    let private saveOnce (run: LiveRun.T) =
        let before = run.Session.BaseHead
        let sent = run.Sent.Length
        LiveRun.edit run "PAT-02 recorded mutation" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        before, run.Sent |> List.skip sent

    let o1 (create: string -> LiveRun.T) =
        let run = create "persist006-o1"
        let before, sent = saveOnce run
        Assert.Equal(1, sent |> List.filter SaveKit.isMutation |> List.length)
        // Exactly one new commit on the branch, a child of the head the save expected.
        let commits = LiveRun.commitsSince run before
        Assert.Equal(1, commits.Length)
        Assert.Equal((let (CommitOid h) = run.Session.BaseHead in h), fst commits.Head)
        LiveRun.record run "PERSIST-006-O1" [ run.Session.BaseHead ]

    let o2 (create: string -> LiveRun.T) =
        let run = create "persist006-o2"
        saveOnce run |> ignore
        let prefix = LiveRun.prefix run
        Assert.Equal<string list>(
            [ prefix + "/events.ndjson"; prefix + "/manifest.json"; prefix + "/snapshot.json" ],
            LiveRun.changedPaths run run.Session.BaseHead |> List.sort
        )

    let o3 (create: string -> LiveRun.T) =
        let run = create "persist006-o3"
        let before, sent = saveOnce run
        match sent |> List.filter SaveKit.isMutation |> List.choose GitHubRequests.operationOf with
        | [ CommitOnBranch expected ] -> Assert.Equal(before, expected)
        | other -> failwithf "%A" other

    let o4 (create: string -> LiveRun.T) =
        let run = create "persist006-o4"
        saveOnce run |> ignore
        // Every request was a read (GET or the GraphQL files query) or one createCommitOnBranch: no Contents API
        // write, no ref update, no write without an expected head.
        for r in run.Sent do
            match r.Method, GitHubRequests.operationOf r with
            | Get, _ -> ()
            | Post, Some(ReadInvestigationAt _) -> ()
            | Post, Some(CommitOnBranch(CommitOid e)) -> Assert.Matches("^[0-9a-f]{40}$", e)
            | m, op -> failwithf "unexpected request %A %A %s" m op r.Url
        Assert.DoesNotContain(run.Sent, fun r -> r.Method = Put || r.Method = Patch)

    let o5 (create: string -> LiveRun.T) =
        let run = create "persist006-o5"
        saveOnce run |> ignore
        let prefix = LiveRun.prefix run
        Assert.All(LiveRun.changedPaths run run.Session.BaseHead, fun p -> Assert.StartsWith(prefix + "/", p))
        // A commit touching another path is refused before it is sent.
        let outside =
            { ExpectedHead = run.Session.BaseHead
              Headline = "x"
              Body = ""
              Marker = None
              Additions = [ { Path = RepoPath "workspaces/demo/demonstration-v1/investigations/x/events.ndjson"; Contents = "{}"B } ]
              Deletions = [] }
        Assert.True(Result.isError (GitHubRequests.checkPrefix (RepoPath prefix) outside))

    let o6 (create: string -> LiveRun.T) =
        let run = create "persist006-o6"
        LiveRun.edit run "waits for the other save" |> ignore
        // Another investigation of this tab has a mutation in flight: this save waits in memory, nothing is sent.
        run.Clock.ElsewhereInFlight <- true
        let sent = run.Sent.Length
        LiveRun.input run SaveInput.SaveRequested |> ignore
        Assert.Equal("Unsaved", MachineKit.stateName run.Session.State)
        Assert.Equal(sent, run.Sent.Length)
        // When it settles, the waiting save starts.
        run.Clock.ElsewhereInFlight <- false
        LiveRun.input run SaveInput.QueuedSaveStarted |> ignore
        Assert.Equal("Saved", MachineKit.stateName run.Session.State)

    let o7 (create: string -> LiveRun.T) =
        let run = create "persist006-o7"
        saveOnce run |> ignore
        saveOnce run |> ignore
        let prefix = LiveRun.prefix run
        // Every mutation the application sent: one createCommitOnBranch with an expected head, three files inside the
        // investigation's prefix, and a write marker.
        let mutations = LiveRun.mutations run
        Assert.Equal(3, mutations.Length)
        for m in mutations do
            let body = m.Body |> Option.defaultValue ""
            Assert.Contains("createCommitOnBranch", body)
            Assert.Contains("expectedHeadOid", body)
            Assert.Contains(GitHubRequests.MarkerTrailer, body)
            Assert.Contains(prefix + "/events.ndjson", body)
        LiveRun.record run "PERSIST-006-O7" [ run.Session.BaseHead ]

    [<Fact>]
    let ``PERSIST-006-O1`` () = o1 LiveRun.create

    [<Fact>]
    let ``PERSIST-006-O2`` () = o2 LiveRun.create

    [<Fact>]
    let ``PERSIST-006-O3`` () = o3 LiveRun.create

    [<Fact>]
    let ``PERSIST-006-O4`` () = o4 LiveRun.create

    [<Fact>]
    let ``PERSIST-006-O5`` () = o5 LiveRun.create

    [<Fact>]
    let ``PERSIST-006-O6`` () = o6 LiveRun.create

    [<Fact>]
    let ``PERSIST-006-O7`` () = o7 LiveRun.create

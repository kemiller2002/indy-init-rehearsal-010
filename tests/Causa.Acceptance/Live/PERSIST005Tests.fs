// Owner: persistence. PERSIST-005 (live): GitHub is the only durable store; opening reads and verifies at a head.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST005Tests =
    let o1 (create: string -> LiveRun.T) =
        let run = create "persist005-o1"
        LiveRun.edit run "Recorded and saved to GitHub" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        Assert.Equal("Saved", MachineKit.stateName run.Session.State)
        // A tab with nothing in memory recovers the investigation from the branch alone.
        match LiveRun.openInNewTab run with
        | Ok v -> Assert.Equal(AggregateVersion 2L, v.Investigation.Version)
        | Error f -> failwithf "%A" f
        // Persistence holds no other store: it references no browser or file storage and no HTTP client.
        let refs = typeof<SaveMachine>.Assembly.GetReferencedAssemblies() |> Array.map (fun a -> string a.Name)
        Assert.DoesNotContain("System.Net.Http", refs)
        Assert.DoesNotContain("System.IO.IsolatedStorage", refs)
        Assert.DoesNotContain("Microsoft.JSInterop", refs)
        LiveRun.record run "PERSIST-005-O1" [ run.Session.BaseHead ]

    let o2 (create: string -> LiveRun.T) =
        let run = create "persist005-o2"
        match LiveRun.openInNewTab run with
        | Ok v ->
            // The verified version replays to the investigation that was saved, from GitHub's bytes.
            Assert.Equal(run.Id, v.Investigation.Id)
            Assert.True(Result.isOk (StorageReader.verify Kernel.domain (Some run.Id) v.Address v.ReadAt v.Bytes))
        | Error f -> failwithf "%A" f
        LiveRun.record run "PERSIST-005-O2" [ run.Session.BaseHead ]

    let o3 (create: string -> LiveRun.T) =
        let run = create "persist005-o3"
        let head = LiveRun.head run
        match LiveRun.openInNewTab run with
        | Ok v ->
            Assert.Equal(head, v.ReadAt)
            match Persistence.sessionOf v with
            | Some s -> Assert.Equal(head, s.BaseHead)
            | None -> failwith "a working copy has a session"
        | Error f -> failwithf "%A" f

    let o4 (create: string -> LiveRun.T) =
        let run = create "persist005-o4"
        // The branch head and the files are read by one uncacheable POST that names the branch; every later file
        // read names the commit it reads.
        let r: InvestigationRef = { Address = Writable.address run.Writable; Id = run.Id }
        match saveMachine.StepLoad (SaveKit.context run.Clock) NotLoaded (LoadInput.OpenRequested r) with
        | Ok(_, effects) ->
            let request = (SaveKit.requests effects).Head
            Assert.Equal(Post, request.Method)
            Assert.EndsWith("/graphql", request.Url)
        | Error f -> failwithf "%A" f
        let reads = run.Sent |> List.choose GitHubRequests.operationOf |> List.choose (function ReadInvestigationAt rev -> Some rev | _ -> None)
        Assert.NotEmpty(reads)
        Assert.All(reads, fun rev -> Assert.Matches("^[0-9a-f]{40}$", rev))

    let o5 (create: string -> LiveRun.T) =
        let run = create "persist005-o5"
        LiveRun.edit run "PAT-10 first save" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        let confirmed = run.Session.BaseHead
        match LiveRun.openInNewTab run with
        | Ok v ->
            Assert.Equal(confirmed, v.ReadAt)
            Assert.Equal(AggregateVersion 2L, v.Investigation.Version)
        | Error f -> failwithf "%A" f
        LiveRun.record run "PERSIST-005-O5" [ confirmed ]

    [<Fact>]
    let ``PERSIST-005-O1`` () = o1 LiveRun.create

    [<Fact>]
    let ``PERSIST-005-O2`` () = o2 LiveRun.create

    [<Fact>]
    let ``PERSIST-005-O3`` () = o3 LiveRun.create

    [<Fact>]
    let ``PERSIST-005-O4`` () = o4 LiveRun.create

    [<Fact>]
    let ``PERSIST-005-O5`` () = o5 LiveRun.create

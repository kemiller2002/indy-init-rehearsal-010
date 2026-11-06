// Owner: persistence. PERSIST-007 (live): Saved only from a read-back of the written bytes at the new commit.
namespace Causa.Acceptance.Live

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST007Tests =
    let o1 (create: string -> LiveRun.T) =
        let run = create "persist007-o1"
        LiveRun.edit run "read back" |> ignore
        let sent = run.Sent.Length
        LiveRun.input run SaveInput.SaveRequested |> ignore
        match run.Session.State with
        | SaveState.Saved(Confirmed c) ->
            Assert.Equal(LiveRun.head run, c.CommitOid)
            // The mutation, then a read at exactly the commit it returned.
            match run.Sent |> List.skip sent |> List.choose GitHubRequests.operationOf with
            | CommitOnBranch _ :: ReadInvestigationAt rev :: _ -> Assert.Equal((let (CommitOid h) = c.CommitOid in h), rev)
            | other -> failwithf "%A" other
        | other -> failwithf "%A" other
        LiveRun.record run "PERSIST-007-O1" [ run.Session.BaseHead ]

    let o2 (create: string -> LiveRun.T) =
        let run = create "persist007-o2"
        let before = run.Session.BaseHead
        LiveRun.edit run "not yet confirmed" |> ignore
        Assert.Equal(before, run.Session.BaseHead)
        Assert.Equal("Unsaved", MachineKit.stateName run.Session.State)
        LiveRun.input run SaveInput.SaveRequested |> ignore
        Assert.NotEqual(before, run.Session.BaseHead)
        Assert.Contains(run.Events, fun e -> match e with SaveInput.ReadBackMatched _ -> true | _ -> false)

    let o3 (create: string -> LiveRun.T) =
        let run = create "persist007-o3"
        LiveRun.edit run "saved" |> ignore
        let next, effects = SaveKit.stepOk run.Machine run.Clock run.Session SaveInput.SaveRequested
        let later =
            { CommandId = System.Guid.NewGuid()
              Investigation = run.Id
              ExpectedVersion = None
              Command = Core(RecordObservation { ObservationId = ObservationId(System.Guid.NewGuid()); Original = SaveKit.text "during the save"; ObservedAt = None }) }
        let during, _ = SaveKit.stepOk run.Machine run.Clock next (SaveInput.Edit later)
        let final, _, _ = SaveKit.drive run.Machine run.Clock (LiveRun.execute run) during (SaveKit.requests effects)
        Assert.Equal("Unsaved", MachineKit.stateName final.State)
        Assert.Equal<CommandEnvelope list>([ later ], final.Unsaved)

    let o4 (create: string -> LiveRun.T) =
        let run = create "persist007-o4"
        LiveRun.edit run "PAT-02" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        // Saved was entered only by ReadBackMatched, and a new tab (PAT-10) shows the same commit.
        let savedBy = run.Events |> List.filter (function SaveInput.ReadBackMatched _ | SaveInput.ReconcileMarkerBytesMatch _ -> true | _ -> false)
        Assert.NotEmpty(savedBy)
        match LiveRun.openInNewTab run with
        | Ok v -> Assert.Equal(run.Session.BaseHead, v.ReadAt)
        | Error f -> failwithf "%A" f

    let over1MB (create: string -> LiveRun.T) =
        let run = create "persist007-large"
        // An observation of 1.1 MB: events.ndjson exceeds the 1 MB the contents API and GraphQL return inline.
        LiveRun.edit run (System.String('x', 1_100_000)) |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        match run.Session.State with
        | SaveState.Saved(Confirmed _) -> ()
        | other -> failwithf "%A" other
        match LiveRun.openInNewTab run with
        | Ok v -> Assert.True(v.Bytes.Events.Length > 1_000_000)
        | Error f -> failwithf "%A" f
        LiveRun.record run "PERSIST-007.ReadBackOfFileOver1MB" [ run.Session.BaseHead ]

    [<Fact>]
    let ``PERSIST-007-O1`` () = o1 LiveRun.create

    [<Fact>]
    let ``PERSIST-007-O2`` () = o2 LiveRun.create

    [<Fact>]
    let ``PERSIST-007-O3`` () = o3 LiveRun.create

    [<Fact>]
    let ``PERSIST-007-O4`` () = o4 LiveRun.create

    [<Fact>]
    let ReadBackOfFileOver1MB () = over1MB LiveRun.create

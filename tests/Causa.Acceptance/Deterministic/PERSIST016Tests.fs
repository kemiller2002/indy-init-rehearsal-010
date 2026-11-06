// Owner: persistence. PERSIST-016: automatic network activity of one save request is bounded.
namespace Causa.Acceptance.Deterministic

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST016Tests =
    /// Runs one save request for `seconds` of simulated time: every ScheduleReconcileRead fires at its time
    /// (ReconcileDue), and the network changes at the given instants. Returns the scenario and what was sent when.
    let private simulate (sc: SaveScenario.T) (seconds: float) (networkChanges: (float * SaveInput) list) =
        let start = sc.Clock.Now
        let mutable timers: float list = []
        let record (effects: SaveEffect list) =
            for e in effects do
                match e with
                | ScheduleReconcileRead(_, ms) -> timers <- timers @ [ (sc.Clock.Now - start).TotalSeconds + float ms / 1000.0 ]
                | _ -> ()
        record (SaveScenario.input sc SaveInput.SaveRequested)
        let mutable changes = networkChanges
        let mutable t = 0.0
        while t <= seconds do
            sc.Clock.Now <- start.AddSeconds t
            for (at, input) in changes |> List.filter (fun (at, _) -> at <= t) do
                record (SaveScenario.input sc input)
            changes <- changes |> List.filter (fun (at, _) -> at > t)
            let due = timers |> List.filter (fun at -> at <= t)
            timers <- timers |> List.filter (fun at -> at > t)
            for _ in due do
                match sc.Session.State with
                | SaveState.UnknownOutcome _ -> record (SaveScenario.reconcileDue sc)
                | _ -> ()
            t <- t + 0.5
        sc

    let private everyRequestFails () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        sc.Before <- fun _ -> sc.Fake.Update(StatefulFake.injectFault NextRequest LoseAnswerBeforeApplying)
        sc

    let private reconciliationReads (sc: SaveScenario.T) =
        sc.Sent |> List.filter (fun r -> r.Url.Contains "/compare/" && r.Url.EndsWith "page=1")

    [<Fact>]
    let ``PERSIST-016-O1`` () =
        let sc = simulate (everyRequestFails ()) 60.0 [ 20.0, SaveInput.WentOffline; 21.0, SaveInput.WentOnline ]
        Assert.True((SaveScenario.mutations sc).Length <= 4)
        Assert.True((reconciliationReads sc).Length <= 4)
        Assert.True(sc.Sent.Length <= 8, sprintf "%d requests" sc.Sent.Length)

    [<Fact>]
    let ``PERSIST-016-O2`` () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        let mutable n = 0
        sc.Before <-
            fun r ->
                if SaveKit.isMutation r then
                    n <- n + 1
                    SaveScenario.otherWriter sc [ sprintf "u/%d.json" n, "{}"B ]
        let sc = simulate sc 60.0 []
        Assert.Equal(4, (SaveScenario.mutations sc).Length)
        Assert.Equal(SaveState.SaveFailed RepositoryKeptChanging, sc.Session.State)

    [<Fact>]
    let ``PERSIST-016-O3`` () =
        let sc = simulate (everyRequestFails ()) 40.0 []
        let reads = reconciliationReads sc
        Assert.Equal(3, reads.Length)
        // At least 2, 4 and 8 seconds apart: the reads were made at (about) 2 s, 6 s and 14 s.
        Assert.Equal(SaveState.UnknownOutcome 0, sc.Session.State)
        let times = sc.Session.ReconcileReadsSentAt |> List.rev |> List.map (fun t -> (t - SaveKit.now).TotalSeconds)
        Assert.Equal(3, times.Length)
        let gaps = times |> List.pairwise |> List.map (fun (a, b) -> b - a)
        Assert.True(times.Head >= 2.0)
        Assert.True(gaps[0] >= 4.0 && gaps[1] >= 8.0, sprintf "%A" times)
        // After the automatic reads: one read per offline-to-online change, then only Check again.
        let before = (reconciliationReads sc).Length
        sc.Before <- ignore
        SaveScenario.input sc SaveInput.WentOffline |> ignore
        SaveScenario.input sc SaveInput.WentOnline |> ignore
        Assert.Equal(before + 1, (reconciliationReads sc).Length)

    [<Fact>]
    let ``PERSIST-016-O4`` () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        sc.Fake.Update(StatefulFake.injectFault NextMutation FailBeforeSending)
        let sc = simulate sc 60.0 [ 10.0, SaveInput.WentOnline ]
        Assert.Equal("Unsaved", SaveScenario.stateName sc)
        Assert.Equal(1, (SaveScenario.mutations sc).Length)
        Assert.Contains(SaveInput.FailedBeforeSending, sc.Events)

    [<Fact>]
    let ``PERSIST-016-O5`` () =
        // A network that fails every request, with the browser flapping offline and online: still bounded.
        let flaps = [ for i in 1..10 do yield float (i * 5), SaveInput.WentOffline; yield float (i * 5) + 1.0, SaveInput.WentOnline ]
        let sc = simulate (everyRequestFails ()) 60.0 flaps
        Assert.True((SaveScenario.mutations sc).Length <= 4, sprintf "%d mutations" (SaveScenario.mutations sc).Length)
        Assert.True((reconciliationReads sc).Length <= 4, sprintf "%d reconciliation reads" (reconciliationReads sc).Length)

    [<Fact>]
    let ``PERSIST-016-O6`` () =
        // PAT-08: count every request for 60 seconds under total failure and under constant branch advance.
        let failing = simulate (everyRequestFails ()) 60.0 []
        let busy = SaveScenario.create ()
        SaveScenario.edit busy 1
        let mutable n = 0
        busy.Before <-
            fun r ->
                if SaveKit.isMutation r then
                    n <- n + 1
                    SaveScenario.otherWriter busy [ sprintf "u/%d.json" n, "{}"B ]
        let busy = simulate busy 60.0 []
        for sc in [ failing; busy ] do
            Assert.True((SaveScenario.mutations sc).Length <= 4)
            Assert.True((reconciliationReads sc).Length <= 4)

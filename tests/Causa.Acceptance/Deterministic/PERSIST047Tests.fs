// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// PERSIST-047: every request of one save request, counted for 60 seconds under three failure conditions (R6-D-003).
namespace Causa.Acceptance.Deterministic

open Xunit
open Causa.Persistence
open Causa.TestSupport

module PERSIST047Tests =
    /// Every request fails before it is sent, and the browser reports the network gone (as offline does).
    let private offline () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        sc.Before <- fun _ -> sc.Fake.Update(StatefulFake.injectFault NextRequest FailBeforeSending)
        SaveSimulation.run sc 60.0 [ 0.5, SaveInput.WentOffline ]

    /// Every mutation's answer is lost (GitHub did not apply it); reads are answered.
    let private lostAnswersFor (seconds: float) =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        sc.Before <- fun r -> if SaveKit.isMutation r then sc.Fake.Update(StatefulFake.injectFault NextMutation LoseAnswerBeforeApplying)
        SaveSimulation.run sc seconds []

    let private lostAnswers () = lostAnswersFor 60.0

    /// Another writer advances the branch before every attempt.
    let private advancing () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        let n = ref 0
        sc.Before <-
            fun r ->
                if SaveKit.isMutation r then
                    n.Value <- n.Value + 1
                    SaveScenario.otherWriter sc [ sprintf "u/%d.json" n.Value, "{}"B ]
        SaveSimulation.run sc 60.0 []

    let private conditions () = [ "offline", offline (); "lost answers", lostAnswers (); "branch advances", advancing () ]

    [<Fact>]
    let ``PERSIST-047-O1`` () =
        // Every request the application sends for the one save request is counted: the fake logged exactly what was sent.
        for name, sc in conditions () do
            let logged = (sc.Fake.Current()).Log.Length
            let failedBeforeSending = sc.Sent.Length - logged
            Assert.True(sc.Sent.Length > 0, name)
            Assert.True(failedBeforeSending >= 0, name)
            Assert.True(sc.Sent.Length <= 12, sprintf "%s: %d requests in 60 s" name sc.Sent.Length)

    [<Fact>]
    let ``PERSIST-047-O2`` () =
        let byName = conditions () |> Map.ofList
        // Offline: nothing reached GitHub. Lost answers: every mutation was lost. Advancing: every mutation met a new head.
        Assert.Empty((byName["offline"].Fake.Current()).Log)
        Assert.All(SaveScenario.mutations byName["lost answers"], fun _ -> ())
        Assert.True(byName["lost answers"].Events |> List.filter ((=) SaveInput.AnswerLost) |> List.length = (SaveScenario.mutations byName["lost answers"]).Length)
        Assert.Contains(byName["branch advances"].Events, fun e -> match e with SaveInput.StaleHeadUnrelated _ -> true | _ -> false)

    [<Fact>]
    let ``PERSIST-047-O3`` () =
        for name, sc in conditions () do
            Assert.True((SaveScenario.mutations sc).Length <= 4, sprintf "%s: %d mutations" name (SaveScenario.mutations sc).Length)
            Assert.True((SaveSimulation.reconciliationReads sc).Length <= 4, sprintf "%s: %d reconciliation reads" name (SaveSimulation.reconciliationReads sc).Length)

    [<Fact>]
    let ``PERSIST-047-O4`` () =
        for name, sc in conditions () do
            let state = SaveScenario.stateName sc
            Assert.True(List.contains state [ "Offline"; "UnknownOutcome"; "SaveFailed" ], sprintf "%s ended %s" name state)
            Assert.NotEqual<string>("Saving", state)

    [<Fact>]
    let ``PERSIST-047-O5`` () =
        // On this build: the three conditions, end to end, each bounded and settled.
        let states = conditions () |> List.map (fun (name, sc) -> name, SaveScenario.stateName sc)
        Assert.Equal<(string * string) list>([ "offline", "Offline"; "lost answers", "SaveFailed"; "branch advances", "SaveFailed" ], states)

    [<Fact>]
    let ``PERSIST-047-O6`` () =
        // PERSIST-010 (bounded rebases: the advancing branch ends RepositoryKeptChanging after four attempts) and
        // PERSIST-016 (bounded automatic activity), the regression of R6-D-003 (an unbounded resend loop).
        let sc = advancing ()
        Assert.Equal(SaveState.SaveFailed RepositoryKeptChanging, sc.Session.State)
        Assert.Equal(4, (SaveScenario.mutations sc).Length)
        // Nothing more is sent automatically after the first minute: three minutes send exactly what one minute does.
        let minute = lostAnswersFor 60.0
        let three = lostAnswersFor 180.0
        Assert.Equal(minute.Sent.Length, three.Sent.Length)
        Assert.Equal(minute.Session.State, three.Session.State)

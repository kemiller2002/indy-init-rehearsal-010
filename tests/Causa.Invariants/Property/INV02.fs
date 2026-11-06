// Owner: domain. INV-02 (PERSIST-051-O2): replies to superseded requests are inert, in any delivery order.
namespace Causa.Invariants.Property

open Xunit
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV02 =
    /// Answers in every form a stale request can come back in.
    let private staleReplies =
        [ Answered(RequestId 2L, 200, [], "{\"data\":{\"repository\":{\"ref\":{\"target\":{\"oid\":\"c9\"}}}}}")
          Answered(RequestId 3L, 409, [], "{\"message\":\"conflict\"}")
          NotAnswered(RequestId 4L, LostAfterSending)
          Answered(RequestId 5L, 200, [], "{\"data\":{\"createCommitOnBranch\":{\"commit\":{\"oid\":\"forged\"}}}}") ]

    let rec private permutations (xs: 'a list) : 'a list list =
        match xs with
        | [] -> [ [] ]
        | _ -> xs |> List.collect (fun x -> permutations (List.filter ((<>) x) xs) |> List.map (fun p -> x :: p))

    let private observable (s: InvestigationSession) = s.State, s.BaseHead, s.Unsaved, s.AfterInFlight, s.Outstanding

    [<Fact>]
    let ReorderedRepliesAreInert () =
        let m = machine ()
        let ctx = context 30L
        let orders = permutations staleReplies
        Assert.Equal(24, orders.Length)
        let saveViolations =
            [ for state in [ "Saved"; "Saving"; "UnknownOutcome" ] do
                  let start = sessionIn m state SaveInput.AnswerLost
                  for order in orders do
                      let final =
                          order
                          |> List.fold
                              (fun (s: InvestigationSession) reply ->
                                  match m.Interpret ctx s reply with
                                  | MachineEvent input ->
                                      match m.Step ctx s input with
                                      | Ok(next, _) -> next
                                      | Error _ -> s
                                  | NextRequest(next, _) -> next)
                              start
                      if observable final <> observable start then
                          yield $"SM-SAVE {state}: stale replies in order {order} changed the session" ]
        let loadStart = Loading(RequestId 7L, LoadHead)
        let loadViolations =
            [ for order in orders do
                  let final =
                      order
                      |> List.fold
                          (fun s reply ->
                              match m.InterpretLoad ctx s reply with
                              | LoadEvent input ->
                                  match m.StepLoad ctx s input with
                                  | Ok(next, _) -> next
                                  | Error _ -> s
                              | LoadNextRequest(next, _) -> next)
                          loadStart
                  if final <> loadStart then yield $"SM-LOAD: stale replies in order {order} changed the load" ]
        let conflictStart = ReadingRemote(RequestId 7L)
        let conflictViolations =
            [ for order in orders do
                  let final =
                      order
                      |> List.fold
                          (fun v reply ->
                              match m.InterpretConflict ctx v reply with
                              | ConflictEvent input ->
                                  match m.StepConflict ctx v input with
                                  | Ok(next, _) -> next
                                  | Error _ -> v
                              | ConflictNextRequest(next, _) -> next)
                          conflictStart
                  if final <> conflictStart then yield $"SM-CONFLICT: stale replies in order {order} changed the review" ]
        Assert.Empty(saveViolations @ loadViolations @ conflictViolations)

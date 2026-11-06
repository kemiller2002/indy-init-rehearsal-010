// Owner: domain. INV-03 (PERSIST-015-O3): an uncertain outcome is never classified as failure or conflict.
namespace Causa.Invariants.Property

open Xunit
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV03 =
    /// Inputs that carry a provider or reconciliation result able to justify Conflict or Save failed.
    let private justifying =
        set [ "ReadBackMismatched"; "StaleHeadUnrelated"; "StaleHeadSameInvestigation"; "ProviderRefused"; "CredentialRefused"
              "ReconcileMarkerBytesDiffer"; "ReconcileNothingWrittenHeadUnchanged"; "ReconcileNothingWrittenHeadMoved"
              "ReconcileHistoryRewritten" ]

    let private sequence (seed: uint64) (count: int) =
        Seq.unfold (fun (s: uint64) -> let n = s * 6364136223846793005UL + 1442695040888963407UL in Some(int (n >>> 33), n)) seed
        |> Seq.take count
        |> List.ofSeq

    [<Fact>]
    let LostAnswerNeverConflictOrFailure () =
        let m = machine ()
        let ctx = context 60L
        // A lost answer at any step of a save, before or after GitHub may have applied the mutation, is read as a lost
        // answer (AnswerLost or the next read), never as a stale head, a refusal or a conflict.
        let interpretViolations =
            [ for input in inputs do
                  for state in [ "Saving"; "UnknownOutcome" ] do
                      let session = sessionIn m state input
                      for failure in [ LostAfterSending; TimedOut ] do
                          match m.Interpret ctx session (NotAnswered(outstandingId, failure)) with
                          | MachineEvent e ->
                              let name = inputName e
                              if justifying.Contains name then
                                  yield $"{state} awaiting {session.Outstanding}: {failure} interpreted as {name}"
                          | NextRequest _ -> () ]
        // Over random sequences, Conflict and Save failed are entered only on a justifying result.
        let check (seed: uint64) (session: InvestigationSession, found: string list) (input: SaveInput) =
            match m.Step ctx session input with
            | Error _ -> session, found
            | Ok(next, _) ->
                let before, after, name = stateName session.State, stateName next.State, inputName input
                let entered = after <> before && (after = "Conflict" || after = "SaveFailed")
                next, (if entered && not (justifying.Contains name) then found @ [ $"seed {seed}: {before} --{name}--> {after}" ] else found)
        let stepViolations =
            [ 1UL .. 200UL ]
            |> List.collect (fun seed ->
                sequence seed 40
                |> List.map (fun r -> inputs[abs r % inputs.Length])
                |> List.fold (check seed) (sessionIn m "Saving" SaveInput.AnswerLost, [])
                |> snd)
        Assert.Empty(interpretViolations @ stepViolations)
        // The direct case: a lost mutation answer leads to Unknown commit outcome.
        match m.Step ctx (sessionIn m "Saving" SaveInput.AnswerLost) SaveInput.AnswerLost with
        | Ok(next, _) -> Assert.Equal("UnknownOutcome", stateName next.State)
        | Error f -> failwithf "AnswerLost refused in Saving: %A" f

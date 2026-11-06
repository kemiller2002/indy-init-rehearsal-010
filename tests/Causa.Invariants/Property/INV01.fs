// Owner: domain. INV-01 (PERSIST-023-O2): Saved only after a matching read-back or marker reconciliation.
namespace Causa.Invariants.Property

open Xunit
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV01 =
    /// A deterministic linear congruential sequence (no System.Random: the cases are the same on every run).
    let private sequence (seed: uint64) (count: int) =
        Seq.unfold (fun (s: uint64) -> let n = s * 6364136223846793005UL + 1442695040888963407UL in Some(int (n >>> 33), n)) seed
        |> Seq.take count
        |> List.ofSeq

    /// Inputs a hostile or broken provider could produce, including forged commit oids and mismatched read-backs.
    let private hostile: SaveInput list =
        inputs
        @ [ SaveInput.ReadBackMismatched(CommitOid "forged")
            SaveInput.StaleHeadUnrelated(CommitOid "forged")
            SaveInput.ReconcileMarkerBytesDiffer(CommitOid "forged")
            SaveInput.ReconcileNothingWrittenHeadMoved(CommitOid "forged")
            SaveInput.Edit(edit 7) ]

    [<Fact>]
    let NoSavedWithoutMatchingReadBack () =
        // A mismatched read-back can never yield the confirmation ReadBackMatched carries.
        Assert.Empty(confirmationViolations Confirmation.ofReadBack)
        let m = machine ()
        let check (seed: uint64) (session: InvestigationSession, found: string list) (input: SaveInput) =
            match m.Step (context 50L) session input with
            | Error _ -> session, found
            | Ok(next, _) ->
                let before = stateName session.State
                let name = inputName input
                let problem =
                    match next.State with
                    | SaveState.Saved basis when before <> "Saved" ->
                        if not (savedEntries.Contains name) then [ $"seed {seed}: {before} --{name}--> Saved" ]
                        else
                            match basis, input with
                            | Confirmed c, SaveInput.ReadBackMatched expected
                            | Confirmed c, SaveInput.ReconcileMarkerBytesMatch expected when c = expected -> []
                            | VerifiedAt v, SaveInput.ConflictResolvedDiscarded expected when v = expected -> []
                            | _ -> [ $"seed {seed}: Saved after {name} rests on another basis" ]
                    | _ -> []
                next, found @ problem
        let violations =
            [ 1UL .. 200UL ]
            |> List.collect (fun seed ->
                sequence seed 40
                |> List.map (fun r -> hostile[abs r % hostile.Length])
                |> List.fold (check seed) (m.Initial writable baseVerified, [])
                |> snd)
        Assert.Empty(violations)

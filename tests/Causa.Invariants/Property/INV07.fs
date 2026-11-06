// Owner: domain. INV-07 (PERSIST-031-O21): unsaved work survives every recoverable failure.
namespace Causa.Invariants.Property

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV07 =
    let private sequence (seed: uint64) (count: int) =
        Seq.unfold (fun (s: uint64) -> let n = s * 6364136223846793005UL + 1442695040888963407UL in Some(int (n >>> 33), n)) seed
        |> Seq.take count
        |> List.ofSeq

    /// The inputs after which commands may legitimately leave the tab: a confirmed save of them, a confirmed discard,
    /// and a reapply (whose result replaces them with the engine's re-decision, INV-12).
    let private releasing = set [ "ReadBackMatched"; "ReconcileMarkerBytesMatch"; "ConflictResolvedDiscarded"; "ConflictResolvedReapplied" ]

    let private work (s: InvestigationSession) : CommandEnvelope list = s.Unsaved @ s.AfterInFlight

    [<Fact>]
    let WorkSurvivesRecoverableFailures () =
        let m = machine ()
        let ctx = context 80L
        let check (seed: uint64) (session: InvestigationSession, found: string list) (input: SaveInput) =
            match m.Step ctx session input with
            | Error _ -> session, found
            | Ok(next, _) ->
                let name = inputName input
                let lost = work session |> List.filter (fun c -> not (List.contains c (work next)))
                let problems =
                    [ if not lost.IsEmpty && not (releasing.Contains name) then
                          yield $"seed {seed}: {stateName session.State} --{name}--> {stateName next.State} dropped {lost.Length} unsaved command(s)"
                      match input with
                      | SaveInput.Edit c when not (List.contains c (work next)) -> yield $"seed {seed}: the edit in {stateName session.State} was not kept"
                      | _ -> () ]
                next, found @ problems
        let violations =
            [ 1UL .. 200UL ]
            |> List.collect (fun seed ->
                sequence seed 40
                |> List.mapi (fun i r ->
                    match inputs[abs r % inputs.Length] with
                    | SaveInput.Edit _ -> SaveInput.Edit(edit (100 + i))
                    | other -> other)
                |> List.fold (check seed) (sessionIn m "Unsaved" SaveInput.SaveRequested, [])
                |> snd)
        Assert.Empty(violations)

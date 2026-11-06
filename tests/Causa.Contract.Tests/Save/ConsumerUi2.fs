// Owner: ui-2. The save-state indicator follows SM-SAVE (IF41-SAVE SaveState; IF41-VIEW SaveIndicatorView): exactly one
// of seven named states, the actions each offers, the announcement politeness, and the keys the workspace header binds
// (role=status "Save state" reads exactly the state's name; INV-015, INV-016, PERSIST-030, PERSIST-031).
module Causa.Contract.Tests.Save.ConsumerUi2

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui
open Causa.App.Views
open Causa.TestSupport.UiFixtures

let private value (key: string) (view: (string * ViewValue) list) =
    view |> List.tryFind (fst >> (=) key) |> Option.map snd |> Option.defaultWith (fun () -> failwithf "missing key %s" key)

[<Fact>]
let IndicatorFollowsMachine () =
    let v = Ui2Scenarios.verified [ Ui2Scenarios.startCommand "Pump trip" "Pump P-101 tripped" ]
    let confirmation =
        Confirmation.ofReadBack Ui2Scenarios.investigationId Ui2Scenarios.head Ui2Scenarios.at v.Bytes v.Bytes |> Option.get
    // Every SM-SAVE state, with the words, actions and politeness PERSIST-030/031 give it.
    let cases: (SaveState * string * SaveAction list * Politeness * string) list =
        [ SaveState.Saved(Confirmed confirmation), "Saved", [], Polite, "ok"
          SaveState.Saved(VerifiedAt v), "Saved", [], Polite, "ok"
          SaveState.Unsaved, "Unsaved", [ Save ], Polite, "attention"
          SaveState.Saving, "Saving", [], Polite, "unknown"
          SaveState.Offline, "Offline", [], Polite, "attention"
          SaveState.Conflict RemoteUnavailable, "Conflict", [ Review; ReapplyMine; DiscardMine; ExportMine ], Assertive, "blocked"
          SaveState.UnknownOutcome 2, "Unknown commit outcome", [], Assertive, "unknown"
          SaveState.UnknownOutcome 0, "Unknown commit outcome", [ CheckAgainAction ], Assertive, "unknown"
          SaveState.SaveFailed(Refused(ValidationFailed [ "refused" ])), "Save failed", [ Retry ], Assertive, "blocked"
          SaveState.SaveFailed RepositoryKeptChanging, "Save failed", [ Retry ], Assertive, "blocked"
          SaveState.SaveFailed CredentialNotAccepted, "Save failed", [ Reconnect ], Assertive, "blocked" ]
    let keySets =
        cases
        |> List.map (fun (state, label, actions, politeness, lozenge) ->
            let indicator = SaveIndicator.indicator (Ui2Scenarios.session state [])
            Assert.Equal(label, indicator.Label)
            Assert.Equal<SaveAction list>(actions, indicator.Actions)
            Assert.Equal(politeness, indicator.Politeness)
            Assert.Equal(Ui2Scenarios.investigationId, indicator.InvestigationId)
            Assert.False(System.String.IsNullOrWhiteSpace indicator.Text)
            // The rendered keys over the engine's model with this session.
            let view = SaveIndicator.view (Ui2Scenarios.loaded state "Pump trip" "Pump P-101 tripped" "Why?")
            Assert.Equal(BindingValue.Flag true, value "investigation.save.shown" view)
            Assert.Equal(BindingValue.Text label, value "investigation.save.state" view)
            Assert.Equal(BindingValue.Text lozenge, value "investigation.save.lozenge" view)
            // The Save button is offered exactly where the machine offers Save or Retry (J3-06 retries through it).
            let canSave = List.contains Save actions || List.contains Retry actions
            Assert.Equal(BindingValue.Flag canSave, value "investigation.save.canSave" view)
            Assert.Equal(BindingValue.Flag(List.contains CheckAgainAction actions), value "investigation.save.canCheckAgain" view)
            Assert.Equal(BindingValue.Flag(List.contains Reconnect actions), value "investigation.save.canReconnect" view)
            Assert.Equal(BindingValue.Flag(politeness = Assertive), value "investigation.save.assertive" view)
            view |> List.map fst |> Set.ofList)
    // Exactly seven distinct state names, and the same keys in every state.
    Assert.Equal(7, cases |> List.map (fun (_, l, _, _, _) -> l) |> List.distinct |> List.length)
    Assert.Equal(1, keySets |> List.distinct |> List.length)
    // Saved after a confirmed read-back shows the commit time (Forma freshness); a verified read does not claim one.
    let confirmed = SaveIndicator.indicator (Ui2Scenarios.session (SaveState.Saved(Confirmed confirmation)) [])
    Assert.Equal(Some Ui2Scenarios.at, confirmed.LastSavedCommitTime)
    Assert.Equal(None, (SaveIndicator.indicator (Ui2Scenarios.session (SaveState.Saved(VerifiedAt v)) [])).LastSavedCommitTime)
    // A new investigation the engine created (InitialNew, online) is Unsaved and offers Save; never Saved before a commit.
    let fresh = SaveIndicator.view (Ui2Scenarios.newUnsaved "Pump trip" "Pump P-101 tripped" "Why?" Overview)
    Assert.Equal(BindingValue.Text "Unsaved", value "investigation.save.state" fresh)
    Assert.Equal(BindingValue.Flag true, value "investigation.save.canSave" fresh)
    // A loaded catalog-style investigation with no session shows Saved (a verified read at a commit, ODR-V4-08).
    let loadedOnly =
        let s = Ui2Scenarios.loaded SaveState.Unsaved "Pump trip" "Pump P-101 tripped" "Why?"
        { s with Model = { s.Model with Sessions = Map.empty } }
    Assert.Equal(BindingValue.Text "Saved", value "investigation.save.state" (SaveIndicator.view loadedOnly))
    Assert.Equal(BindingValue.Flag false, value "investigation.save.canSave" (SaveIndicator.view loadedOnly))
    // Off a workspace route nothing is shown, and the key set is unchanged.
    let elsewhere = SaveIndicator.view (Ui2Scenarios.stateAt (Update.initialModel (Routes.route "#/new")) [])
    Assert.Equal(BindingValue.Flag false, value "investigation.save.shown" elsewhere)
    Assert.Equal<Set<string>>(keySets.Head, elsewhere |> List.map fst |> Set.ofList)

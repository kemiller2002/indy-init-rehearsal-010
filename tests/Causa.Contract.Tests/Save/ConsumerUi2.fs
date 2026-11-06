// Owner: ui-2. The save-state indicator follows SM-SAVE (IF41-SAVE SaveState; IF41-VIEW SaveIndicatorView): exactly one
// of seven named states, the actions each offers, the announcement politeness, and the keys the workspace header binds
// (role=status "Save state" reads exactly the state's name; INV-015, INV-016, PERSIST-030, PERSIST-031).
module Causa.Contract.Tests.Save.ConsumerUi2

open System.Text.RegularExpressions
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
          SaveState.Conflict(Reviewable v), "Conflict", [ ReapplyMine; DiscardMine; ExportMine ], Assertive, "blocked"
          SaveState.Conflict RemoteUnavailable, "Conflict", [ ExportMine ], Assertive, "blocked"
          SaveState.Conflict(ReadingRemote(RequestId 9L)), "Conflict", [], Assertive, "blocked"
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
            // A refused credential is shown while SM-CONNECTION is CredentialRefused (T-N04 moves it there).
            let shown = Ui2Scenarios.loaded state "Pump trip" "Pump P-101 tripped" "Why?"
            let shown =
                match state with
                | SaveState.SaveFailed CredentialNotAccepted -> { shown with Model = { shown.Model with Connection = CredentialRefused } }
                | _ -> shown
            let view = SaveIndicator.view shown
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

    // ---- the real machine (Causa.Persistence.saveMachine) drives the indicator: J1-04 and J3-03..07 ------------------
    let text key view = match value key view with BindingValue.Text t -> t | other -> failwithf "%s: %A" key other
    let on (n: int64) (input: SaveInput) (s: InvestigationSession) = Ui2Scenarios.step n input s
    let show (s: InvestigationSession) = Ui2Scenarios.withSession None s
    let problemShown (s: InvestigationSession) = text "investigation.problem" (Frame.view (show s))
    // PERSIST-032: outside Saved no copy uses saved/synced/stored/safe, and nothing says locally/this device/queued/later.
    let forbidden = Regex(@"\b(saved|synced|stored|safe)\b|locally|on this device|queued|later", RegexOptions.IgnoreCase)
    let check (expected: string) (canSave: bool) (s: InvestigationSession) =
        let view = SaveIndicator.view (show s)
        Assert.Equal(expected, text "investigation.save.state" view)
        Assert.Equal(BindingValue.Flag canSave, value "investigation.save.canSave" view)
        if expected <> "Saved" then
            Assert.False(forbidden.IsMatch(text "investigation.save.text" view), text "investigation.save.text" view)
            Assert.NotEqual<string>("Saved", text "investigation.save.state" view)
        // This tab's values are kept in every state (J3-04, J3-05, J3-06).
        Assert.Equal("Pump P-101 tripped", problemShown s)
        view

    // J1-04: a new investigation is Unsaved (never Saved before a commit) and offers Save.
    let created = Ui2Scenarios.initialNew "Pump trip" "Pump P-101 tripped"
    let unsavedView = check "Unsaved" true created
    Assert.Contains("only in this tab", text "investigation.save.text" unsavedView) // PERSIST-032-O3
    // J3-03: an edit keeps Unsaved.
    let edited = created |> on 2L (SaveInput.Edit(Ui2Scenarios.questionCommand "Why did it trip?"))
    check "Unsaved" true edited |> ignore
    // J3-04: offline keeps the values, says so, offers no Save; back online returns to Unsaved.
    let offline = edited |> on 3L SaveInput.WentOffline
    let offlineView = check "Offline" false offline
    Assert.Contains("only in this tab", text "investigation.save.text" offlineView)
    Assert.Equal(BindingValue.Flag false, value "investigation.save.assertive" offlineView)
    let back = offline |> on 4L SaveInput.WentOnline
    check "Unsaved" true back |> ignore
    // Save requested: Saving, nothing offered, not confirmed.
    let saving = back |> on 5L SaveInput.SaveRequested
    let savingView = check "Saving" false saving
    Assert.Contains("not confirmed", text "investigation.save.text" savingView)
    // J3-06: GitHub refuses: Save failed names the cause, keeps the values, offers Save (Retry), announced assertively.
    let failed = saving |> on 6L (SaveInput.ProviderRefused(ValidationFailed [ "refused" ]))
    let failedView = check "Save failed" true failed
    Assert.Contains("refused", text "investigation.save.text" failedView)
    Assert.Contains("kept in this tab", text "investigation.save.text" failedView)
    Assert.Equal("Save failed", text "investigation.save.assertiveText" failedView)
    // PERSIST-030-O1: an operation status explains Save failed and Unknown commit outcome only; freshness is always shown.
    Assert.Equal(BindingValue.Flag true, value "investigation.save.operation" failedView)
    Assert.Equal(BindingValue.Flag false, value "investigation.save.operation" unsavedView)
    Assert.Equal(BindingValue.Flag false, value "investigation.save.operation" offlineView)
    Assert.Equal("None in this tab", text "investigation.save.freshness" unsavedView)
    // J3-07: the answer is lost: Unknown commit outcome, never Saved; Check again only once the automatic reads are spent.
    let unknown = failed |> on 7L SaveInput.SaveRequested |> on 8L SaveInput.AnswerLost
    let unknownView = check "Unknown commit outcome" false unknown
    Assert.Equal(BindingValue.Flag false, value "investigation.save.canCheckAgain" unknownView)
    Assert.Equal(BindingValue.Flag true, value "investigation.save.operation" unknownView)
    Assert.True(Result.isError (Ui2Scenarios.context 9L |> fun ctx -> saveMachine.Step ctx unknown SaveInput.CheckAgain))
    let spent = unknown |> on 9L SaveInput.ReconcileReadsSpent
    let spentView = check "Unknown commit outcome" false spent
    Assert.Equal(BindingValue.Flag true, value "investigation.save.canCheckAgain" spentView)
    Assert.True(Result.isOk (saveMachine.Step (Ui2Scenarios.context 10L) spent SaveInput.CheckAgain))
    // A refused credential: Save failed with Reconnect (PERSIST-031-O22), not Retry.
    // The connection is CredentialRefused meanwhile (T-N04); once a new token is accepted (T-N06) the user may save
    // again: the same Save failed then offers the save, not Reconnect.
    let credential = back |> on 11L SaveInput.SaveRequested |> on 12L SaveInput.CredentialRefused
    let refusedTab = show credential |> fun t -> { t with Model = { t.Model with Connection = CredentialRefused } }
    let credentialView = SaveIndicator.view refusedTab
    Assert.Equal("Save failed", text "investigation.save.state" credentialView)
    Assert.Equal(BindingValue.Flag false, value "investigation.save.canSave" credentialView)
    Assert.Equal(BindingValue.Flag true, value "investigation.save.canReconnect" credentialView)
    let reconnectedView = check "Save failed" true credential
    Assert.Equal(BindingValue.Flag false, value "investigation.save.canReconnect" reconnectedView)
    Assert.True(Result.isOk (saveMachine.Step (Ui2Scenarios.context 13L) credential SaveInput.SaveRequested))

    // ---- J3-05: Conflict keeps this tab's values; only SM-CONFLICT's legal choices are offered; nothing is overwritten
    let remote = Ui2Scenarios.verified [ Ui2Scenarios.startCommand "Pump trip" "Pump P-101 tripped" ]
    let inConflict (view: ConflictView) = { back with State = SaveState.Conflict view }
    let reviewable = ReadingRemote(RequestId 20L) |> Ui2Scenarios.stepConflict 21L (ConflictInput.RemoteVerified remote)
    let offered (s: InvestigationSession) =
        let keys = ConflictReview.view (show s)
        [ "reapply"; "discard"; "discard-confirm"; "discard-cancel"; "export"; "read-again" ]
        |> List.filter (fun c -> value ($"investigation.conflict.{c}.offered") keys = BindingValue.Flag true)
    let reviewState = inConflict reviewable
    check "Conflict" false reviewState |> ignore
    Assert.Equal<string list>([ "reapply"; "discard"; "export"; "read-again" ], offered reviewState)
    let review = ConflictReview.view (show reviewState)
    Assert.Equal(BindingValue.Flag true, value "investigation.conflict.shown" review)
    Assert.Contains("Nothing of this tab's change was written", text "investigation.conflict.explanation" review)
    Assert.Contains("plus 2 changes of this tab", text "investigation.conflict.mine" review)
    Assert.Equal("Version 1 at commit 0123456", text "investigation.conflict.theirs" review)
    match value "investigation.conflict.changes" review with
    | BindingValue.Items rows -> Assert.Equal(2, rows.Length)
    | other -> failwithf "%A" other
    // Each offered choice is a transition the machine accepts; Discard first asks, and Keep returns to the review.
    let confirming = reviewable |> Ui2Scenarios.stepConflict 22L ConflictInput.DiscardChosen
    Assert.Equal<string list>([ "discard-confirm"; "discard-cancel" ], offered (inConflict confirming))
    Assert.Equal(reviewable, confirming |> Ui2Scenarios.stepConflict 23L ConflictInput.DiscardCancelled)
    let reapplied =
        reviewable
        |> Ui2Scenarios.stepConflict 24L ConflictInput.ReapplyChosen
        |> Ui2Scenarios.stepConflict 25L (ConflictInput.ReapplyDone([], [ { Command = Ui2Scenarios.questionCommand "Why?"; Reason = "The unknown already exists." } ]))
    Assert.Empty(offered (inConflict reapplied))
    let reappliedView = ConflictReview.view (show (inConflict reapplied))
    Assert.Equal(BindingValue.Flag true, value "investigation.conflict.hasRefused" reappliedView)
    let unavailable = ReadingRemote(RequestId 26L) |> Ui2Scenarios.stepConflict 27L (ConflictInput.RemoteReadFailed LostAfterSending)
    Assert.Equal<string list>([ "export"; "read-again" ], offered (inConflict unavailable))
    Assert.True(Result.isOk (saveMachine.StepConflict (Ui2Scenarios.context 28L) reviewable ConflictInput.ReadAgain))
    // Outside Conflict the review is not shown and its key set is unchanged.
    let noConflict = ConflictReview.view (show back)
    Assert.Equal(BindingValue.Flag false, value "investigation.conflict.shown" noConflict)
    Assert.Equal<Set<string>>(review |> List.map fst |> Set.ofList, noConflict |> List.map fst |> Set.ofList)

    // ---- PERSIST-033: the header shows the connection and every investigation of the tab GitHub lacks changes of -----
    let otherId = InvestigationId(System.Guid.Parse "11111111-2222-4333-8444-555555555555")
    let otherRef: InvestigationRef = { Address = Ui2Scenarios.workingCopy; Id = otherId }
    let savedOther = { Ui2Scenarios.session (SaveState.Saved(Confirmed confirmation)) [] with Id = otherId }
    let tab =
        let s = show back
        { s with Model = { s.Model with Sessions = s.Model.Sessions.Add(otherRef, savedOther) } }
    let header = SaveIndicator.view tab
    Assert.StartsWith("Connected to harness/causa-test on branch main", text "investigation.connection" header)
    Assert.Equal("1 investigation in this tab has changes GitHub does not have:", text "investigation.notSaved.summary" header)
    match value "investigation.notSaved.items" header with
    | BindingValue.Items [ row ] ->
        Assert.Equal(BindingPrimitive.Text "Pump trip", row.["title"])
        Assert.Equal(BindingPrimitive.Text(Ui2Scenarios.workspaceHash Overview), row.["href"])
    | other -> failwithf "%A" other
    let allSaved =
        let s = Ui2Scenarios.withSession None savedOther
        SaveIndicator.view s
    Assert.Equal(BindingValue.Flag false, value "investigation.notSaved.any" allSaved)
    // Offline with nothing unsent stays Saved and the header says the application is offline (PERSIST-031-O23).
    let offlineSaved =
        let s = Ui2Scenarios.loaded (SaveState.Saved(Confirmed confirmation)) "Pump trip" "Pump P-101 tripped" "Why?"
        { s with Model = { s.Model with Network = NetworkState.Offline } }
    let offlineSavedView = SaveIndicator.view offlineSaved
    Assert.Equal("Saved", text "investigation.save.state" offlineSavedView)
    Assert.Equal(BindingValue.Flag true, value "investigation.save.offlineSaved" offlineSavedView)
    Assert.EndsWith("You are offline.", text "investigation.connection" offlineSavedView)
    // State changes are announced: politely, and assertively for Conflict, Unknown commit outcome and Save failed.
    Assert.Equal(Some(Assertive, "Save state: Save failed"), SaveIndicator.announcement (Some saving) failed)
    Assert.Equal(Some(Polite, "Save state: Offline"), SaveIndicator.announcement (Some edited) offline)
    Assert.Equal(None, SaveIndicator.announcement (Some created) edited)

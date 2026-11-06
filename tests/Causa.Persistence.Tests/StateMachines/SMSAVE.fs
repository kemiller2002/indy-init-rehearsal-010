// Owner: persistence. SM-SAVE transition tests (state-machine-contracts.json): one test per transition, driving the
// published machine through it and asserting the next state and the effects; one test for every illegal pair.
namespace Causa.Persistence.Tests.StateMachines

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport
open Causa.TestSupport.MachineKit

module SMSAVE =
    let private edit = SaveKit.edit
    let private assertState (expected: string) (s: InvestigationSession) = Assert.Equal(expected, stateName s.State)

    [<Fact>]
    let ``T-S01`` () =
        let next, effects = step ctx (saved ()) (SaveInput.Edit(edit 1))
        assertState "Unsaved" next
        Assert.Equal<CommandEnvelope list>([ edit 1 ], next.Unsaved)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S02`` () =
        // Known offline: the engine delivers WentOffline with the edit (the machine holds no network flag); the edit
        // is kept in tab memory only and the investigation shows Offline; nothing is sent.
        let edited, e1 = step ctx (saved ()) (SaveInput.Edit(edit 1))
        let next, e2 = step ctx edited SaveInput.WentOffline
        assertState "Offline" next
        Assert.Contains(edit 1, work next)
        Assert.Empty(sends (e1 @ e2))
        // A new investigation created while known offline starts Offline.
        let created = (m ()).InitialNew SaveKit.writable SaveKit.investigationId baseHead (SaveKit.startCommand SaveKit.investigationId) false
        assertState "Offline" created

    [<Fact>]
    let ``T-S03`` () =
        let next, effects = step ctx (unsaved ()) (SaveInput.Edit(edit 2))
        assertState "Unsaved" next
        Assert.Equal<CommandEnvelope list>([ edit 1; edit 2 ], next.Unsaved)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S04`` () =
        let next, effects = step ctx (unsaved ()) SaveInput.SaveRequested
        assertState "Saving" next
        let sent = mutations effects
        Assert.Equal(1, sent.Length)
        let request = sent.Head
        Assert.Equal(Some(CommitOnBranch baseHead), GitHubRequests.operationOf request)
        let body = request.Body |> Option.defaultValue ""
        Assert.Contains("createCommitOnBranch", body)
        Assert.Contains(SaveKit.dir SaveKit.investigationId + "/events.ndjson", body)
        Assert.Contains(SaveKit.dir SaveKit.investigationId + "/manifest.json", body)
        Assert.Contains(SaveKit.dir SaveKit.investigationId + "/snapshot.json", body)
        Assert.Contains(GitHubRequests.MarkerTrailer, body)
        Assert.Equal(Some(RequestId 40L, MutationAnswer), next.Outstanding)
        Assert.Equal(1, next.Mutations)

    [<Fact>]
    let ``T-S05`` () =
        // Known offline: WentOffline reaches the session first; the save request then sends nothing.
        let off, e1 = step ctx (unsaved ()) SaveInput.WentOffline
        let next, e2 = step ctx off SaveInput.SaveRequested
        assertState "Offline" next
        Assert.Empty(sends (e1 @ e2))

    [<Fact>]
    let ``T-S53`` () =
        let next, effects = step (ctxWith 40L true SaveKit.now) (unsaved ()) SaveInput.SaveRequested
        assertState "Unsaved" next
        Assert.Empty(sends effects)
        Assert.Contains(effects, fun e -> match e with Announce _ -> true | _ -> false)

    [<Fact>]
    let ``T-S06`` () =
        let next, effects = step ctx (unsaved ()) SaveInput.WentOffline
        assertState "Offline" next
        Assert.Contains(edit 1, work next)
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S43`` () =
        let next, effects = step ctx (unsaved ()) SaveInput.WentOnline
        assertState "Unsaved" next
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S54`` () =
        let next, effects = step ctx (unsaved ()) SaveInput.QueuedSaveStarted
        assertState "Saving" next
        Assert.Equal(1, (mutations effects).Length)

    [<Fact>]
    let ``T-S55`` () =
        let off, _ = step ctx (unsaved ()) SaveInput.WentOffline
        let next, effects = step ctx off SaveInput.QueuedSaveStarted
        assertState "Offline" next
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S10`` () =
        let next, effects = step ctx (saving MutationAnswer) (SaveInput.Edit(edit 2))
        assertState "Saving" next
        Assert.Equal<CommandEnvelope list>([ edit 2 ], next.AfterInFlight)
        Assert.Equal<CommandEnvelope list>([ edit 1 ], next.Unsaved)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S61`` () =
        let session = saving MutationAnswer
        let next, effects = step ctx session (SaveInput.MutationSent outstanding)
        Assert.Equal(session, next)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S11`` () =
        let next, effects = step ctx (saving (ReadBackAt(CommitOid "c1"))) (SaveInput.ReadBackMatched(confirmation "c1"))
        match next.State with
        | SaveState.Saved(Confirmed c) -> Assert.Equal(CommitOid "c1", c.CommitOid)
        | other -> failwithf "%A" other
        Assert.Equal(CommitOid "c1", next.BaseHead)
        Assert.Empty(next.Unsaved)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S12`` () =
        let session = { saving (ReadBackAt(CommitOid "c1")) with AfterInFlight = [ edit 2 ] }
        let next, _ = step ctx session (SaveInput.ReadBackMatched(confirmation "c1"))
        assertState "Unsaved" next
        Assert.Equal(CommitOid "c1", next.BaseHead)
        Assert.Equal<CommandEnvelope list>([ edit 2 ], next.Unsaved)

    [<Fact>]
    let ``T-S13`` () =
        let next, _ = step ctx (saving (ReadBackAt(CommitOid "c1"))) (SaveInput.ReadBackMismatched(CommitOid "c1"))
        Assert.Equal(SaveState.SaveFailed(BytesDiffer(CommitOid "c1")), next.State)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S14`` () =
        let next, effects = step ctx (saving (FileComparisonAt(CommitOid "c2"))) (SaveInput.StaleHeadUnrelated(CommitOid "c2"))
        assertState "Saving" next
        let sent = mutations effects
        Assert.Equal(1, sent.Length)
        Assert.Equal(Some(CommitOnBranch(CommitOid "c2")), GitHubRequests.operationOf sent.Head)
        Assert.Equal(CommitOid "c2", next.BaseHead)
        Assert.Equal(1, next.RebaseAttempts)
        Assert.Equal(2, next.Mutations)

    [<Fact>]
    let ``T-S15`` () =
        let session = { saving (FileComparisonAt(CommitOid "c2")) with RebaseAttempts = 3; Mutations = 4 }
        let next, effects = step ctx session (SaveInput.StaleHeadUnrelated(CommitOid "c2"))
        Assert.Equal(SaveState.SaveFailed RepositoryKeptChanging, next.State)
        Assert.Empty(sends effects)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S16`` () =
        let next, effects = step ctx (saving (FileComparisonAt(CommitOid "c2"))) (SaveInput.StaleHeadSameInvestigation(CommitOid "c2"))
        match next.State with
        | SaveState.Conflict(ReadingRemote id) -> Assert.Equal(RequestId 40L, id)
        | other -> failwithf "%A" other
        Assert.Empty(mutations effects)
        Assert.Equal(1, (sends effects).Length)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S17`` () =
        let e = RateLimited(Secondary(Some 60))
        let next, _ = step ctx (saving MutationAnswer) (SaveInput.ProviderRefused e)
        Assert.Equal(SaveState.SaveFailed(Refused e), next.State)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S18`` () =
        let next, _ = step ctx (saving MutationAnswer) SaveInput.CredentialRefused
        Assert.Equal(SaveState.SaveFailed CredentialNotAccepted, next.State)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S19`` () =
        let next, effects = step ctx (saving MutationAnswer) SaveInput.AnswerLost
        assertState "UnknownOutcome" next
        Assert.Empty(sends effects)
        Assert.Equal<int list>([ 2000 ], schedules effects)

    [<Fact>]
    let ``T-S20`` () =
        let next, effects = step ctx (saving MutationAnswer) SaveInput.FailedBeforeSending
        assertState "Offline" next
        Assert.Empty(sends effects)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S45`` () =
        let session = saving MutationAnswer
        let next, effects = step ctx session SaveInput.WentOffline
        Assert.Equal(session, next)
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S46`` () =
        let session = saving MutationAnswer
        let next, effects = step ctx session SaveInput.WentOnline
        Assert.Equal(session, next)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S39`` () =
        let session = saving MutationAnswer
        let next, effects = step ctx session (SaveInput.SupersededReply(RequestId 3L))
        Assert.Equal(session, next)
        Assert.Empty(effects)
        Assert.True(Result.isError ((m ()).Step ctx session (SaveInput.SupersededReply outstanding)))

    [<Fact>]
    let ``T-S57`` () =
        // Known offline on the rebase path: the fresh-head read fails before sending; it is FailedBeforeSending, so
        // the save shows Offline, never Conflict.
        let session = saving FreshHead
        match (m ()).Interpret ctx session (NotAnswered(outstanding, FailedBeforeSending)) with
        | MachineEvent input ->
            let next, _ = step ctx session input
            assertState "Offline" next
        | other -> failwithf "%A" other

    [<Fact>]
    let ``T-S59`` () =
        let next, effects = step ctx (saving FreshHead) SaveInput.RebaseReadFailed
        assertState "UnknownOutcome" next
        Assert.Equal<int list>([ 2000 ], schedules effects)
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S58`` () =
        let next, effects = step ctx (saving (ReadBackAt(CommitOid "c1"))) SaveInput.ReadBackReadFailed
        assertState "UnknownOutcome" next
        Assert.Equal<int list>([ 2000 ], schedules effects)

    [<Fact>]
    let ``T-S07`` () =
        let next, _ = step ctx (offline ()) (SaveInput.Edit(edit 2))
        assertState "Offline" next
        Assert.Equal<CommandEnvelope list>([ edit 1; edit 2 ], next.Unsaved)

    [<Fact>]
    let ``T-S08`` () =
        let next, effects = step ctx (offline ()) SaveInput.SaveRequested
        assertState "Offline" next
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S44`` () =
        let next, effects = step ctx (offline ()) SaveInput.WentOffline
        assertState "Offline" next
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S09`` () =
        let next, effects = step ctx (offline ()) SaveInput.WentOnline
        assertState "Unsaved" next
        Assert.Empty(sends effects)
        Assert.Contains(edit 1, next.Unsaved)

    [<Fact>]
    let ``T-S60`` () =
        let next, effects = step ctx (offline ()) SaveInput.QueuedSaveStarted
        assertState "Offline" next
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S36`` () =
        let next, effects = step ctx (conflict (Reviewable remoteVerified)) (SaveInput.Edit(edit 2))
        assertState "Conflict" next
        Assert.Contains(edit 2, next.Unsaved)
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S48`` () =
        let next, effects = step ctx (conflict (Reviewable remoteVerified)) SaveInput.WentOffline
        assertState "Conflict" next
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S49`` () =
        let next, effects = step ctx (conflict RemoteUnavailable) SaveInput.WentOnline
        match next.State with
        | SaveState.Conflict(ReadingRemote id) -> Assert.Equal(RequestId 40L, id)
        | other -> failwithf "%A" other
        Assert.Equal(1, (sends effects).Length)
        Assert.Empty(mutations effects)
        let unchanged, none = step ctx (conflict (Reviewable remoteVerified)) SaveInput.WentOnline
        assertState "Conflict" unchanged
        Assert.Empty(none)

    [<Fact>]
    let ``T-S37`` () =
        let next, effects = step ctx (conflict (Reapplied(remoteVerified, []))) (SaveInput.ConflictResolvedReapplied [ edit 3 ])
        assertState "Unsaved" next
        Assert.Equal(remoteVerified.ReadAt, next.BaseHead)
        Assert.Equal<CommandEnvelope list>([ edit 3 ], next.Unsaved)
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S38`` () =
        let next, effects = step ctx (conflict (Discarded remoteVerified)) (SaveInput.ConflictResolvedDiscarded remoteVerified)
        Assert.Equal(SaveState.Saved(VerifiedAt remoteVerified), next.State)
        Assert.Equal(remoteVerified.ReadAt, next.BaseHead)
        Assert.Empty(next.Unsaved)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S33`` () =
        let next, _ = step ctx (unknown 2 1 None) (SaveInput.Edit(edit 2))
        assertState "UnknownOutcome" next
        Assert.Equal<CommandEnvelope list>([ edit 2 ], next.AfterInFlight)

    [<Fact>]
    let ``T-S27`` () =
        let next, effects = step ctx (unknown 2 1 (Some(FileComparisonAt(CommitOid "c2")))) (SaveInput.StaleHeadSameInvestigation(CommitOid "c2"))
        assertState "Conflict" next
        Assert.Empty(mutations effects)

    [<Fact>]
    let ``T-S21`` () =
        let next, _ = step ctx (unknown 2 1 (Some(ReconcileFilesAt(CommitOid "c1")))) (SaveInput.ReconcileMarkerBytesMatch(confirmation "c1"))
        match next.State with
        | SaveState.Saved(Confirmed c) -> Assert.Equal(CommitOid "c1", c.CommitOid)
        | other -> failwithf "%A" other
        Assert.Equal(CommitOid "c1", next.BaseHead)

    [<Fact>]
    let ``T-S22`` () =
        let session = { unknown 2 1 (Some(ReconcileFilesAt(CommitOid "c1"))) with AfterInFlight = [ edit 2 ] }
        let next, _ = step ctx session (SaveInput.ReconcileMarkerBytesMatch(confirmation "c1"))
        assertState "Unsaved" next
        Assert.Equal(CommitOid "c1", next.BaseHead)
        Assert.Equal<CommandEnvelope list>([ edit 2 ], next.Unsaved)

    [<Fact>]
    let ``T-S23`` () =
        let next, _ = step ctx (unknown 2 1 (Some(ReconcileFilesAt(CommitOid "c1")))) (SaveInput.ReconcileMarkerBytesDiffer(CommitOid "c1"))
        Assert.Equal(SaveState.SaveFailed(BytesDiffer(CommitOid "c1")), next.State)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S24`` () =
        let next, effects = step ctx (unknown 2 1 (Some(ReconcileCommits 1))) SaveInput.ReconcileNothingWrittenHeadUnchanged
        assertState "Saving" next
        let sent = mutations effects
        Assert.Equal(1, sent.Length)
        Assert.Equal(Some(CommitOnBranch baseHead), GitHubRequests.operationOf sent.Head)
        Assert.Equal(2, next.Mutations)

    [<Fact>]
    let ``T-S25`` () =
        let session = { unknown 2 1 (Some(ReconcileCommits 1)) with Mutations = 4 }
        let next, effects = step ctx session SaveInput.ReconcileNothingWrittenHeadUnchanged
        assertState "SaveFailed" next
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S26`` () =
        let next, effects = step ctx (unknown 2 1 (Some(FileComparisonAt(CommitOid "c2")))) (SaveInput.ReconcileNothingWrittenHeadMoved(CommitOid "c2"))
        assertState "Saving" next
        Assert.Equal(Some(CommitOnBranch(CommitOid "c2")), GitHubRequests.operationOf (mutations effects).Head)
        Assert.Equal(1, next.RebaseAttempts)

    [<Fact>]
    let ``T-S56`` () =
        let session = { unknown 2 1 (Some(FileComparisonAt(CommitOid "c2"))) with RebaseAttempts = 3 }
        let next, effects = step ctx session (SaveInput.ReconcileNothingWrittenHeadMoved(CommitOid "c2"))
        Assert.Equal(SaveState.SaveFailed RepositoryKeptChanging, next.State)
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S28`` () =
        let next, _ = step ctx (unknown 2 1 (Some(ReconcileCommits 1))) SaveInput.ReconcileHistoryRewritten
        Assert.Equal(SaveState.SaveFailed HistoryWasRewritten, next.State)
        Assert.Contains(edit 1, work next)

    [<Fact>]
    let ``T-S29`` () =
        let first, e1 = step ctx (unknown 2 1 (Some(ReconcileCommits 1))) SaveInput.ReconcileReadFailed
        assertState "UnknownOutcome" first
        Assert.Equal<int list>([ 4000 ], schedules e1)
        let second, e2 = step ctx { first with ReconcileReads = 2; Outstanding = Some(outstanding, ReconcileCommits 1) } SaveInput.ReconcileReadFailed
        assertState "UnknownOutcome" second
        Assert.Equal<int list>([ 8000 ], schedules e2)

    [<Fact>]
    let ``T-S64`` () =
        let next, effects = step ctx (unknown 0 3 (Some(ReconcileCommits 1))) SaveInput.ReconcileReadFailed
        Assert.Equal(SaveState.UnknownOutcome 0, next.State)
        Assert.Equal(None, next.Outstanding)
        Assert.Empty(schedules effects)
        Assert.Contains(effects, fun e -> match e with Announce _ -> true | _ -> false)

    [<Fact>]
    let ``T-S30`` () =
        let next, effects = step ctx (unknown 1 2 None) SaveInput.ReconcileReadsSpent
        Assert.Equal(SaveState.UnknownOutcome 0, next.State)
        Assert.Empty(sends effects)
        Assert.Empty(schedules effects)

    [<Fact>]
    let ``T-S31`` () =
        let next, effects = step ctx (unknown 0 3 None) SaveInput.CheckAgain
        assertState "UnknownOutcome" next
        Assert.Equal(1, (sends effects).Length)
        Assert.Equal(Some(CompareFrom baseHead), GitHubRequests.operationOf (sends effects).Head)
        Assert.Equal<DateTimeOffset list>([ SaveKit.now ], next.ReconcileReadsSentAt)

    [<Fact>]
    let ``T-S62`` () =
        let session = { unknown 0 3 None with ReconcileReadsSentAt = [ for i in 1..4 -> SaveKit.now.AddSeconds(float -i * 10.0) ] }
        let next, effects = step ctx session SaveInput.CheckAgain
        Assert.Equal(session, next)
        Assert.Empty(sends effects)
        Assert.Contains(effects, fun e -> match e with Announce _ -> true | _ -> false)

    [<Fact>]
    let ``T-S47`` () =
        let session = unknown 2 1 None
        let next, effects = step ctx session SaveInput.WentOffline
        Assert.Equal(session, next)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S32`` () =
        let next, effects = step ctx (unknown 0 3 None) SaveInput.WentOnline
        Assert.Equal(1, (sends effects).Length)
        Assert.Equal(1, next.ReconcileReadsSentAt.Length)

    [<Fact>]
    let ``T-S63`` () =
        let session = { unknown 0 3 None with ReconcileReadsSentAt = [ for i in 1..4 -> SaveKit.now.AddSeconds(float -i) ] }
        let next, effects = step ctx session SaveInput.WentOnline
        Assert.Equal(session, next)
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S65`` () =
        let session = unknown 2 1 None
        let next, effects = step ctx session SaveInput.WentOnline
        Assert.Equal(session, next)
        Assert.Empty(sends effects)
        Assert.Equal<int list>([ 0 ], schedules effects)

    [<Fact>]
    let ``T-S40`` () =
        let session = unknown 2 1 (Some(ReconcileCommits 1))
        let next, effects = step ctx session (SaveInput.SupersededReply(RequestId 3L))
        Assert.Equal(session, next)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S34`` () =
        let next, _ = step ctx (failed ()) (SaveInput.Edit(edit 2))
        assertState "SaveFailed" next
        Assert.Equal<CommandEnvelope list>([ edit 1; edit 2 ], next.Unsaved)

    [<Fact>]
    let ``T-S35`` () =
        let session = { failed () with Mutations = 4; RebaseAttempts = 3 }
        let next, effects = step ctx session SaveInput.SaveRequested
        assertState "Saving" next
        Assert.Equal(1, next.Mutations)
        Assert.Equal(0, next.RebaseAttempts)
        Assert.Equal(1, (mutations effects).Length)

    [<Fact>]
    let ``T-S52`` () =
        let off, _ = step ctx (failed ()) SaveInput.WentOffline
        assertState "SaveFailed" off
        // Known offline: the engine has stepped the session to Offline through Unsaved; a save sends nothing.
        let next, effects = step ctx (offline ()) SaveInput.SaveRequested
        assertState "Offline" next
        Assert.Empty(sends effects)

    [<Fact>]
    let ``T-S50`` () =
        let next, effects = step ctx (failed ()) SaveInput.WentOffline
        assertState "SaveFailed" next
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S51`` () =
        let next, effects = step ctx (failed ()) SaveInput.WentOnline
        assertState "SaveFailed" next
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S41`` () =
        let session = saved ()
        let next, effects = step ctx session SaveInput.WentOffline
        Assert.Equal(session, next)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-S42`` () =
        let session = saved ()
        let next, effects = step ctx session SaveInput.WentOnline
        Assert.Equal(session, next)
        Assert.Empty(effects)

    let private legal =
        set
            [ "Saved", "Edit"; "Saved", "WentOffline"; "Saved", "WentOnline"; "Unsaved", "Edit"; "Unsaved", "SaveRequested"
              "Unsaved", "WentOffline"; "Unsaved", "WentOnline"; "Unsaved", "QueuedSaveStarted"; "Saving", "Edit"; "Saving", "MutationSent"
              "Saving", "ReadBackMatched"; "Saving", "ReadBackMismatched"; "Saving", "StaleHeadUnrelated"; "Saving", "StaleHeadSameInvestigation"
              "Saving", "ProviderRefused"; "Saving", "CredentialRefused"; "Saving", "AnswerLost"; "Saving", "FailedBeforeSending"
              "Saving", "WentOffline"; "Saving", "WentOnline"; "Saving", "SupersededReply"; "Saving", "RebaseReadFailed"
              "Saving", "ReadBackReadFailed"; "Offline", "Edit"; "Offline", "SaveRequested"; "Offline", "WentOffline"; "Offline", "WentOnline"
              "Offline", "QueuedSaveStarted"; "Conflict", "Edit"; "Conflict", "WentOffline"; "Conflict", "WentOnline"
              "Conflict", "ConflictResolvedReapplied"; "Conflict", "ConflictResolvedDiscarded"; "UnknownOutcome", "Edit"
              "UnknownOutcome", "StaleHeadSameInvestigation"; "UnknownOutcome", "ReconcileMarkerBytesMatch"
              "UnknownOutcome", "ReconcileMarkerBytesDiffer"; "UnknownOutcome", "ReconcileNothingWrittenHeadUnchanged"
              "UnknownOutcome", "ReconcileNothingWrittenHeadMoved"; "UnknownOutcome", "ReconcileHistoryRewritten"
              "UnknownOutcome", "ReconcileReadFailed"; "UnknownOutcome", "ReconcileReadsSpent"; "UnknownOutcome", "CheckAgain"
              "UnknownOutcome", "WentOffline"; "UnknownOutcome", "WentOnline"; "UnknownOutcome", "SupersededReply"; "SaveFailed", "Edit"
              "SaveFailed", "SaveRequested"; "SaveFailed", "WentOffline"; "SaveFailed", "WentOnline" ]

    let private inputs: (string * SaveInput) list =
        [ "Edit", SaveInput.Edit(edit 9)
          "SaveRequested", SaveInput.SaveRequested
          "MutationSent", SaveInput.MutationSent outstanding
          "ReadBackMatched", SaveInput.ReadBackMatched(confirmation "c1")
          "ReadBackMismatched", SaveInput.ReadBackMismatched(CommitOid "c1")
          "StaleHeadUnrelated", SaveInput.StaleHeadUnrelated(CommitOid "c2")
          "StaleHeadSameInvestigation", SaveInput.StaleHeadSameInvestigation(CommitOid "c2")
          "ProviderRefused", SaveInput.ProviderRefused(ValidationFailed [ "x" ])
          "CredentialRefused", SaveInput.CredentialRefused
          "AnswerLost", SaveInput.AnswerLost
          "FailedBeforeSending", SaveInput.FailedBeforeSending
          "ReconcileMarkerBytesMatch", SaveInput.ReconcileMarkerBytesMatch(confirmation "c1")
          "ReconcileMarkerBytesDiffer", SaveInput.ReconcileMarkerBytesDiffer(CommitOid "c1")
          "ReconcileNothingWrittenHeadUnchanged", SaveInput.ReconcileNothingWrittenHeadUnchanged
          "ReconcileNothingWrittenHeadMoved", SaveInput.ReconcileNothingWrittenHeadMoved(CommitOid "c2")
          "ReconcileHistoryRewritten", SaveInput.ReconcileHistoryRewritten
          "ReconcileReadFailed", SaveInput.ReconcileReadFailed
          "ReconcileReadsSpent", SaveInput.ReconcileReadsSpent
          "CheckAgain", SaveInput.CheckAgain
          "WentOffline", SaveInput.WentOffline
          "WentOnline", SaveInput.WentOnline
          "ConflictResolvedReapplied", SaveInput.ConflictResolvedReapplied [ edit 1 ]
          "ConflictResolvedDiscarded", SaveInput.ConflictResolvedDiscarded remoteVerified
          "SupersededReply", SaveInput.SupersededReply(RequestId 3L)
          "QueuedSaveStarted", SaveInput.QueuedSaveStarted
          "RebaseReadFailed", SaveInput.RebaseReadFailed
          "ReadBackReadFailed", SaveInput.ReadBackReadFailed ]

    let private sessions: (string * InvestigationSession) list =
        [ "Saved", saved ()
          "Unsaved", unsaved ()
          "Saving", saving MutationAnswer
          "Offline", offline ()
          "Conflict", conflict (Reviewable remoteVerified)
          "UnknownOutcome", unknown 0 3 (Some(ReconcileCommits 1))
          "SaveFailed", failed () ]

    [<Fact>]
    let EveryIllegalPairFailsClosed () =
        let illegal = [ for s, session in sessions do for i, input in inputs do if not (legal.Contains((s, i))) then yield s, i, session, input ]
        Assert.Equal(139, illegal.Length)
        for s, i, session, input in illegal do
            match (m ()).Step ctx session input with
            | Ok(next, _) -> failwithf "%s + %s was accepted and reached %s" s i (stateName next.State)
            | Error fault ->
                Assert.Equal("SM-SAVE", fault.Machine)
                Assert.Equal(s, fault.State)
                Assert.Equal(i, fault.Input)
                Assert.False(String.IsNullOrWhiteSpace fault.Reason)

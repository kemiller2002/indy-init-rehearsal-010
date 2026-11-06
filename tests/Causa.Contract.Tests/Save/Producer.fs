// Owner: persistence. IF41-SAVE producer tests: the published machines, interpretation and listing.
namespace Causa.Contract.Tests.Save

open System
open System.Reflection
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport
open Causa.TestSupport.MachineKit

module Producer =
    let private eventName (i: SaveInput) = (sprintf "%A" i).Split([| ' '; '\n'; '(' |])[0]

    [<Fact>]
    let ConfirmationNotConstructibleOutside () =
        let t = typeof<Confirmation.T>
        Assert.Empty(t.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance))
        Assert.Empty(t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance) |> Array.filter _.CanWrite)
        let producers =
            (nonNull t.DeclaringType).GetMethods(BindingFlags.Public ||| BindingFlags.Static)
            |> Array.filter (fun mi -> mi.ReturnType = t || mi.ReturnType = typeof<Confirmation.T option>)
            |> Array.map _.Name
        Assert.Equal<string[]>([| "ofReadBack" |], producers)
        let fs = SaveKit.filesOf SaveKit.history
        Assert.True((Confirmation.ofReadBack SaveKit.investigationId (CommitOid "c") SaveKit.now fs { fs with Snapshot = [||] }).IsNone)

    [<Fact>]
    let InterpretCoversEveryReplyEvent () =
        // Drive the published interpretation through every answer form the fake can give and collect the events.
        let seen = Collections.Generic.HashSet<string>()
        let run (setup: SaveScenario.T -> unit) (after: SaveScenario.T -> unit) =
            let sc = SaveScenario.create ()
            SaveScenario.edit sc 1
            setup sc
            SaveScenario.input sc SaveInput.SaveRequested |> ignore
            after sc
            for e in sc.Events do seen.Add(eventName e) |> ignore
        let fault f (sc: SaveScenario.T) = sc.Fake.Update(StatefulFake.injectFault NextMutation f)
        let reconcile (sc: SaveScenario.T) = SaveScenario.reconcileDue sc |> ignore
        let theirs (sc: SaveScenario.T) = SaveScenario.otherWriter sc (SaveKit.repoFiles SaveKit.investigationId (SaveKit.filesOf remoteHistory))
        run ignore ignore
        run (fun sc -> SaveScenario.otherWriter sc [ "u.json", "{}"B ]) ignore
        run theirs ignore
        run (fault (AnswerWith(403, ("retry-after", "5") :: Recordings.standardHeaders 4000, Recordings.secondaryLimitMessage))) ignore
        run (fault (AnswerWith(401, [], Recordings.badCredentials))) ignore
        run (fault FailBeforeSending) ignore
        run (fault LoseAnswerAfterApplying) reconcile
        run (fault LoseAnswerBeforeApplying) reconcile
        run (fault LoseAnswerBeforeApplying) (fun sc -> SaveScenario.otherWriter sc [ "u.json", "{}"B ]; reconcile sc)
        run (fault LoseAnswerBeforeApplying) (fun sc -> theirs sc; reconcile sc)
        run (fault LoseAnswerBeforeApplying) (fun sc -> sc.Fake.Update(fun s -> StatefulFake.rewriteHistory "main" s |> fst); reconcile sc)
        run (fault LoseAnswerBeforeApplying) (fun sc -> sc.Fake.Update(StatefulFake.injectFault NextRequest TimeOutBeforeApplying); reconcile sc)
        run (fault LoseAnswerAfterApplying) (fun sc ->
            let head = (let (CommitOid h) = SaveScenario.head sc in h)
            sc.Fake.Update(fun s -> let c = s.Commits[head] in { s with Commits = s.Commits.Add(head, { c with Tree = c.Tree.Add(SaveKit.dir SaveKit.investigationId + "/snapshot.json", "{}\n"B) }) })
            reconcile sc)
        // Read-back that differs, read-back that fails, and a rebase read that fails.
        run (fun sc ->
                sc.Before <- fun r ->
                    match GitHubRequests.operationOf r with
                    | Some(ReadInvestigationAt rev) when not (rev.StartsWith "refs/") ->
                        sc.Fake.Update(fun s -> let c = s.Commits[rev] in { s with Commits = s.Commits.Add(rev, { c with Tree = c.Tree.Add(SaveKit.dir SaveKit.investigationId + "/events.ndjson", "x\n"B) }) })
                    | _ -> ())
            ignore
        run (fun sc -> sc.Before <- fun r -> if GitHubRequests.operationOf r |> Option.exists (function ReadInvestigationAt _ -> true | _ -> false) then sc.Fake.Update(StatefulFake.injectFault NextRequest TimeOutBeforeApplying)) ignore
        run (fun sc ->
                SaveScenario.otherWriter sc [ "u.json", "{}"B ]
                sc.Before <- fun r -> if GitHubRequests.operationOf r = Some ReadBranchHead then sc.Fake.Update(StatefulFake.injectFault NextRequest TimeOutBeforeApplying))
            ignore
        // An answer to a request that is not outstanding.
        match (m ()).Interpret ctx (saving MutationAnswer) (NotAnswered(RequestId 3L, LostAfterSending)) with
        | MachineEvent e -> seen.Add(eventName e) |> ignore
        | other -> failwithf "%A" other
        let replies =
            [ "ReadBackMatched"; "ReadBackMismatched"; "StaleHeadUnrelated"; "StaleHeadSameInvestigation"; "ProviderRefused"; "CredentialRefused"
              "AnswerLost"; "FailedBeforeSending"; "ReconcileMarkerBytesMatch"; "ReconcileMarkerBytesDiffer"; "ReconcileNothingWrittenHeadUnchanged"
              "ReconcileNothingWrittenHeadMoved"; "ReconcileHistoryRewritten"; "ReconcileReadFailed"; "SupersededReply"; "RebaseReadFailed"
              "ReadBackReadFailed" ]
        for r in replies do
            Assert.True(seen.Contains r, sprintf "no answer was interpreted as %s (seen %A)" r (List.ofSeq seen))

    [<Fact>]
    let SupersededReplyNeverChangesState () =
        let sessions = [ saved (); unsaved (); saving MutationAnswer; offline (); conflict (Reviewable remoteVerified); unknown 0 3 (Some(ReconcileCommits 1)); failed () ]
        for s in sessions do
            match (m ()).Interpret ctx s (Answered(RequestId 3L, 200, [], "{}")) with
            | MachineEvent(SaveInput.SupersededReply id) ->
                Assert.Equal(RequestId 3L, id)
                match (m ()).Step ctx s (SaveInput.SupersededReply id) with
                | Ok(next, effects) ->
                    Assert.Equal(s, next)
                    Assert.Empty(effects)
                | Error f -> Assert.Equal("SM-SAVE", f.Machine)
            | other -> failwithf "%A" other

    [<Fact>]
    let EveryAwaitedReadIsInterpreted () =
        let awaiting =
            [ MutationAnswer; ReadBackAt(CommitOid "c1"); FreshHead; FileComparisonAt(CommitOid "c2"); ReconcileHead; ReconcileCommits 1
              ReconcileFilesAt(CommitOid "c1") ]
        let outcomes =
            [ NotAnswered(outstanding, FailedBeforeSending); NotAnswered(outstanding, LostAfterSending); NotAnswered(outstanding, TimedOut)
              Answered(outstanding, 401, [], Recordings.badCredentials); Answered(outstanding, 502, [], "x"); Answered(outstanding, 200, [], "not json")
              Answered(outstanding, 200, [], "{}") ]
        for a in awaiting do
            for o in outcomes do
                let s = { saving a with State = (match a with ReconcileHead | ReconcileCommits _ | ReconcileFilesAt _ -> SaveState.UnknownOutcome 0 | _ -> SaveState.Saving) }
                match (m ()).Interpret ctx s o with
                | MachineEvent(SaveInput.SupersededReply _) -> failwithf "%A answered as superseded" a
                | MachineEvent _
                | Interpretation.NextRequest _ -> ()

    [<Fact>]
    let ReconcileDueStaysWithinBudget () =
        let s = unknown 2 1 None
        match (m ()).ReconcileDue ctx s with
        | Ok(next, effects) ->
            Assert.Equal(1, (sends effects).Length)
            Assert.Equal(2, next.ReconcileReads)
            Assert.Equal<DateTimeOffset list>([ SaveKit.now ], next.ReconcileReadsSentAt)
        | Error f -> failwithf "%A" f
        // Four reads in the last minute: nothing is sent; the read is rescheduled 60 s after the oldest.
        let busy = { s with ReconcileReadsSentAt = [ for i in 1..4 -> SaveKit.now.AddSeconds(float (-10 * i)) ] }
        match (m ()).ReconcileDue ctx busy with
        | Ok(next, effects) ->
            Assert.Empty(sends effects)
            Assert.Equal<int list>([ 20000 ], schedules effects)
            Assert.Equal(busy, next)
        | Error f -> failwithf "%A" f
        // No automatic read left: ReconcileReadsSpent.
        match (m ()).ReconcileDue ctx (unknown 0 3 None) with
        | Ok(next, effects) ->
            Assert.Equal(SaveState.UnknownOutcome 0, next.State)
            Assert.Empty(sends effects)
        | Error f -> failwithf "%A" f
        Assert.True(Result.isError ((m ()).ReconcileDue ctx (saved ())))

    [<Fact>]
    let RequestIdsAreUniqueAndRecorded () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        SaveScenario.otherWriter sc [ "u.json", "{}"B ]
        let next, effects = SaveKit.stepOk sc.Machine sc.Clock sc.Session SaveInput.SaveRequested
        let first = (SaveKit.requests effects).Head
        Assert.Equal(Some(first.Id, MutationAnswer), next.Outstanding)
        let rec walk (s: InvestigationSession) (r: HttpRequest) (ids: RequestId list) =
            Assert.Equal(Some r.Id, s.Outstanding |> Option.map fst)
            match sc.Machine.Interpret (SaveKit.context sc.Clock) s (sc.Fake.Execute r) with
            | Interpretation.NextRequest(n, req) ->
                sc.Clock.Next <- sc.Clock.Next + 1L
                walk n req (req.Id :: ids)
            | MachineEvent input ->
                let n, e = SaveKit.stepOk sc.Machine sc.Clock s input
                match SaveKit.requests e with
                | req :: _ -> walk n req (req.Id :: ids)
                | [] -> n, ids
        let final, ids = walk next first [ first.Id ]
        Assert.Equal("Saved", stateName final.State)
        Assert.Equal(ids.Length, (List.distinct ids).Length)
        Assert.True(ids.Length >= 4)

    [<Fact>]
    let NewInvestigationSavesThroughExistingTransitions () =
        let running, _ = SaveKit.repository ()
        let machine = SaveMachines.create SaveMachines.support
        let c = SaveKit.clock ()
        let id = InvestigationId(Guid.NewGuid())
        let head = CommitOid((running.Current()).Branches["main"])
        let session = machine.InitialNew SaveKit.writable id head (SaveKit.startCommand id) true
        Assert.Equal(SaveState.Unsaved, session.State)
        let saving, effects = SaveKit.stepOk machine c session SaveInput.SaveRequested
        let final, events, _ = SaveKit.drive machine c running.Execute saving (SaveKit.requests effects)
        Assert.Equal<string list>([ "ReadBackMatched" ], events |> List.map eventName)
        Assert.Equal("Saved", stateName final.State)
        // The new investigation's three files exist at the confirmed commit, and nothing else changed.
        let state = running.Current()
        let commit = state.Commits[state.Branches["main"]]
        let parent = state.Commits[commit.Parent.Value]
        let changed = commit.Tree |> Map.toList |> List.filter (fun (p, b) -> parent.Tree.TryFind p <> Some b) |> List.map fst
        Assert.Equal(3, changed.Length)
        Assert.All(changed, fun p -> Assert.StartsWith(SaveKit.dir id + "/", p))
        // Offline, the new investigation starts Offline and sends nothing.
        Assert.Equal(SaveState.Offline, (machine.InitialNew SaveKit.writable id head (SaveKit.startCommand id) false).State)

    [<Fact>]
    let CatalogLoadCreatesNoSession () =
        let files = Datasets.catalog.Value |> Map.toList
        let running = StatefulFake.start (StatefulFake.initial "owner" "repo" "main" files [ "kit-token", ReadOnly ])
        let r: InvestigationRef =
            { Address = WorkspaceAddress.catalog (Workspace "workspaces/demo/demonstration-v1")
              Id = InvestigationId(Guid "fc3f71a9-4c14-5a31-8196-7ce17a7041c1") }
        let loading, effects = (match (m ()).StepLoad ctx NotLoaded (LoadInput.OpenRequested r) with Ok x -> x | Error f -> failwithf "%A" f)
        match (m ()).InterpretLoad ctx loading (running.Execute (sends effects).Head) with
        | LoadEvent(LoadInput.LoadVerified v) ->
            Assert.Equal(WorkspaceRole.Catalog, v.Address.Role)
            Assert.Equal(None, Persistence.sessionOf v)
            Assert.Equal(Loaded v, fst (match (m ()).StepLoad ctx loading (LoadInput.LoadVerified v) with Ok x -> x | Error f -> failwithf "%A" f))
        | other -> failwithf "%A" other

    let private listAll (running: StatefulFake.Running) (target: ListingTarget) =
        let lister = Persistence.lister
        let step, first = lister.List ctx target
        let rec loop step (request: HttpRequest) sent =
            match lister.InterpretList ctx target step (running.Execute request) with
            | ListingNextRequest(s, r) -> loop s r (r :: sent)
            | other -> other, List.rev sent
        loop step first [ first ]

    [<Fact>]
    let ListingReadsAtOneCommit () =
        let files = Datasets.catalog.Value |> Map.toList
        let running = StatefulFake.start (StatefulFake.initial "owner" "repo" "main" files [ "kit-token", ReadOnly ])
        let head = (running.Current()).Branches["main"]
        match listAll running ActiveCatalog with
        | ListingComplete listing, sent ->
            Assert.Equal(CommitOid head, listing.ReadAt)
            Assert.Equal(4, listing.Investigations.Length)
            Assert.All(listing.Investigations, fun i -> Assert.True(i.Title.IsSome && i.ExpectedStateSha256.IsSome))
            // Every read after the head names that head.
            Assert.All(sent |> List.tail, fun r -> Assert.EndsWith("ref=" + head, r.Url))
        | other, _ -> failwithf "%A" other
        // A working copy that does not exist yet is an empty listing at the head (a clean start).
        let wc = (WorkspaceAddress.workingCopy [] "nobody").Value
        match listAll running (WorkingCopyOf wc) with
        | ListingComplete listing, _ ->
            Assert.Empty(listing.Investigations)
            Assert.Equal(CommitOid head, listing.ReadAt)
        | other, _ -> failwithf "%A" other

    [<Fact>]
    let EveryListingStepIsInterpreted () =
        let steps = [ ListHead; ListActiveCatalogAt(CommitOid "c"); ListManifestAt(CommitOid "c", Workspace "workspaces/demo/demonstration-v1"); ListDirectoryAt(CommitOid "c", Workspace "w") ]
        let outcomes =
            [ NotAnswered(RequestId 1L, LostAfterSending); Answered(RequestId 1L, 401, [], Recordings.badCredentials)
              Answered(RequestId 1L, 200, [], "not json"); Answered(RequestId 1L, 403, Recordings.standardHeaders 0, Recordings.primaryLimitMessage) ]
        for step in steps do
            for o in outcomes do
                match Persistence.lister.InterpretList ctx ActiveCatalog step o with
                | ListingFailed _ -> ()
                | other -> failwithf "%A + %A gave %A" step o other

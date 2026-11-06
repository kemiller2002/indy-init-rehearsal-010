// Owner: ui-2. Real engine states for the ui-2 screens (investigation-start, workspace frame, details, save state):
// the frozen IF41-APP Model and IF41-SAVE sessions, with domain commands the kernel decides. No view is faked: tests
// render the screens' `view` functions over these states.
namespace Causa.TestSupport.UiFixtures

open System
open Causa.Domain
open Causa.Persistence
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Ui2Scenarios =
    let run = "ui2-scenario"
    let workingCopy = WorkspaceAddress.workingCopy [] run |> Option.get
    let writable = Writable.ofAddress workingCopy |> Option.get
    let head = CommitOid "0123456789abcdef0123456789abcdef01234567"
    let repository: Repository = { Owner = "harness"; Name = "causa-test"; Branch = "main" }
    let at = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)

    let investigationId = InvestigationId(Guid.Parse "6f1c2a8e-3b7d-4c55-9a10-2e4f5d6c7b81")
    let reference: InvestigationRef = { Address = workingCopy; Id = investigationId }

    let text s = NonEmptyText.tryCreate s |> Option.get

    /// A model connected to the test repository with write access, the working copy listed.
    let connectedModel (canWrite: bool) (hash: string) : Model =
        { Update.initialModel (Routes.route hash) with
            Connection = Connected canWrite
            Context = Some { Repository = repository; Credential = Credential.create "harness-token" }
            Listings = Map.ofList [ workingCopy, { Address = workingCopy; ReadAt = head; Investigations = [] } ] }

    let stateAt (model: Model) (drafts: (string * string) list) : AppState =
        { Model = model
          Route = UiRoutes.parse (Routes.hashOf model.Route)
          Drafts = Map.ofList drafts
          Credential = None
          Announcements = []
          Build = None }

    let workspaceHash (section: Causa.Ui.Contracts.Section) =
        let (Workspace w) = workingCopy.Workspace
        UiRoutes.href (Causa.Ui.Contracts.WorkspaceRoute({ Workspace = w; Role = WorkspaceRole.WorkingCopy; Id = investigationId }, section))

    let startCommand (title: string) (problem: string) : CommandEnvelope =
        { CommandId = Guid.Parse "a1000000-0000-4000-8000-000000000001"
          Investigation = investigationId
          ExpectedVersion = None
          Command =
            Core(
                StartInvestigation
                    { Title = text title
                      ObservationId = ObservationId(Guid.Parse "b2000000-0000-4000-8000-000000000002")
                      Original = text problem }
            ) }

    /// The primary question as the change request records it: the Unknown whose id is the investigation's id.
    let questionCommand (question: string) : CommandEnvelope =
        let (InvestigationId g) = investigationId
        { CommandId = Guid.Parse "c3000000-0000-4000-8000-000000000003"
          Investigation = investigationId
          ExpectedVersion = None
          Command = Core(IdentifyUnknown { UnknownId = UnknownId g; Question = text question; Materiality = Materiality.Informational }) }

    let session (state: SaveState) (unsaved: CommandEnvelope list) : InvestigationSession =
        { Id = investigationId
          Address = writable
          BaseHead = head
          State = state
          Unsaved = unsaved
          AfterInFlight = []
          Outstanding = None
          Mutations = 0
          RebaseAttempts = 0
          ReconcileReads = 0
          ReconcileReadsSentAt = [] }

    /// The verified (stored) investigation the commands produce, with stored provenance at `at`.
    let verified (commands: CommandEnvelope list) : VerifiedInvestigation =
        let actor: Actor = { ActorId = text "causa-app"; Kind = ActorKind.Human }
        let history =
            commands
            |> List.fold
                (fun (state: Investigation option, events: EventEnvelope list) command ->
                    match Kernel.domain.Decide Policy.core { ActorId = "causa-app" } state command with
                    | Allowed payloads ->
                        payloads
                        |> List.fold
                            (fun (s: Investigation option, es: EventEnvelope list) payload ->
                                let version = AggregateVersion(int64 (List.length es) + 1L)
                                let e: EventEnvelope =
                                    { EventId = EventId(Guid.Parse(sprintf "e%07d-0000-4000-8000-000000000000" (List.length es + 1)))
                                      Investigation = investigationId
                                      Version = version
                                      Provenance =
                                        { OccurredAt = at
                                          Actor = actor
                                          CorrelationId = command.CommandId
                                          FormatVersion = Events.formatVersion
                                          SemanticVersion = Events.semanticVersion }
                                      Payload = payload }
                                Some(Kernel.domain.Evolve s e |> Result.toOption |> Option.get), es @ [ e ])
                            (state, events)
                    | other -> failwithf "the kernel did not allow the scenario command: %A" other)
                (None, [])
            |> snd
        { Investigation = Kernel.domain.Replay history |> Result.toOption |> Option.get
          History = history
          ReadAt = head
          Address = workingCopy
          Bytes = { Manifest = [||]; Events = [||]; Snapshot = [||] } }

    /// The workspace as the engine holds a new, unsaved investigation (CreateRequested -> InitialNew, then the question).
    let newUnsaved (title: string) (problem: string) (question: string) (section: Causa.Ui.Contracts.Section) : AppState =
        let model =
            { connectedModel true (workspaceHash section) with
                Sessions = Map.ofList [ reference, session SaveState.Unsaved [ startCommand title problem; questionCommand question ] ] }
        stateAt model []

    /// The workspace as the engine holds a loaded investigation with a session in `saveState`.
    let loaded (saveState: SaveState) (title: string) (problem: string) (question: string) : AppState =
        let v = verified [ startCommand title problem; questionCommand question ]
        let model =
            { connectedModel true (workspaceHash Causa.Ui.Contracts.Overview) with
                Loads = Map.ofList [ reference, Loaded v ]
                Sessions = Map.ofList [ reference, session saveState [] ] }
        stateAt model []

    // ---- the real SM-SAVE / SM-CONFLICT machine (Causa.Persistence.saveMachine) ----------------------------------------

    let context (n: int64) : RequestContext =
        { Connection = { Repository = repository; Credential = Credential.create "harness-token" }
          NextId = RequestId n
          MutationInFlightElsewhere = false
          Now = at }

    /// A new investigation's session as the machine starts it (CreateRequested -> SaveMachine.InitialNew, online).
    let initialNew (title: string) (problem: string) : InvestigationSession =
        saveMachine.InitialNew writable investigationId head (startCommand title problem) true

    /// One SM-SAVE step; a refused (illegal) step fails the test.
    let step (n: int64) (input: SaveInput) (s: InvestigationSession) : InvestigationSession =
        match saveMachine.Step (context n) s input with
        | Ok(next, _) -> next
        | Error fault -> failwithf "SM-SAVE refused %A: %A" input fault

    /// One SM-CONFLICT step; a refused step fails the test.
    let stepConflict (n: int64) (input: ConflictInput) (view: ConflictView) : ConflictView =
        match saveMachine.StepConflict (context n) view input with
        | Ok(next, _) -> next
        | Error fault -> failwithf "SM-CONFLICT refused %A: %A" input fault

    /// The workspace as the engine holds this session (and, when given, its verified base).
    let withSession (base': VerifiedInvestigation option) (s: InvestigationSession) : AppState =
        let model =
            { connectedModel true (workspaceHash Causa.Ui.Contracts.Overview) with
                Loads = (match base' with Some v -> Map.ofList [ reference, Loaded v ] | None -> Map.empty)
                Sessions = Map.ofList [ reference, s ] }
        stateAt model []

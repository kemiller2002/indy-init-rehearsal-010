// Owner: domain. IF41-DOMAIN (frozen types) and the kernel value every consumer uses.
namespace Causa.Domain

/// IF41-DOMAIN: the F# kernel (Boundaries A-C). Pure: no network, clock, randomness, GitHub or browser types.
type ReplayError =
    | EmptyHistory
    | NonContiguousVersion of expected: AggregateVersion * found: AggregateVersion
    | IllegalEvent of ProjectionError
    | UnknownEventKind of string

type InvestigationProjection =
    { Investigation: Investigation
      Timeline: EventEnvelope list
      Capabilities: string list }

type DomainKernel =
    { Decide: PolicySet -> ActorContext -> Investigation option -> CommandEnvelope -> Decision
      Evolve: Investigation option -> EventEnvelope -> Result<Investigation, ProjectionError>
      Replay: EventEnvelope list -> Result<Investigation, ReplayError>
      Project: Investigation -> InvestigationProjection }

[<RequireQualifiedAccess>]
module Kernel =
    /// Replay: version order is checked first (a gap or duplicate version is NonContiguousVersion), then an event kind
    /// causa-storage v1.0.0 does not have is UnknownEventKind (never skipped), then every event is evolved in order.
    let replay (history: EventEnvelope list) : Result<Investigation, ReplayError> =
        match history with
        | [] -> Error EmptyHistory
        | _ ->
            let gap =
                history
                |> List.mapi (fun i e -> AggregateVersion(int64 (i + 1)), e.Version)
                |> List.tryFind (fun (expected, found) -> expected <> found)
            let unknown =
                history
                |> List.tryPick (fun e ->
                    match e.Payload with
                    | ExtensionEvent(id, _) -> Some $"{id.Namespace.Value}/{id.Name.Value}@{id.Version.Value}"
                    | CoreEvent(ExtensionAttached _) -> Some "ExtensionAttached"
                    | CoreEvent _ -> None)
            match gap, unknown with
            | Some(expected, found), _ -> Error(NonContiguousVersion(expected, found))
            | None, Some kind -> Error(UnknownEventKind kind)
            | None, None ->
                history
                |> List.fold
                    (fun acc e -> acc |> Result.bind (fun s -> Evolve.evolve s e |> Result.map Some |> Result.mapError IllegalEvent))
                    (Ok None)
                |> Result.bind (function
                    | Some s -> Ok s
                    | None -> Error EmptyHistory)

    let project (s: Investigation) : InvestigationProjection =
        { Investigation = s
          Timeline = List.rev s.History
          Capabilities = Capabilities.available s }

    /// The envelopes an Allowed decision would append, with a placeholder provenance (no clock is read): used only to
    /// prove that every event Decide allows also evolves.
    let private rehearse (actor: Actor) (state: Investigation option) (command: CommandEnvelope) (events: EventPayload list) =
        let start = state |> Option.map (fun s -> s.Version) |> Option.defaultValue AggregateVersion.zero
        let at = state |> Option.map (fun s -> s.UpdatedAt) |> Option.defaultValue System.DateTimeOffset.UnixEpoch
        events
        |> List.mapi (fun i payload ->
            { EventId = EventId command.CommandId
              Investigation = command.Investigation
              Version = AggregateVersion(AggregateVersion.value start + int64 (i + 1))
              Provenance =
                { OccurredAt = at
                  Actor = actor
                  CorrelationId = command.CommandId
                  FormatVersion = Events.formatVersion
                  SemanticVersion = Events.semanticVersion }
              Payload = payload })

    /// Decide, with the core invariant firewall: an Allowed decision whose events would not evolve is Blocked.
    let decide (policies: PolicySet) (actor: ActorContext) (state: Investigation option) (command: CommandEnvelope) : Decision =
        match Decide.decide policies actor state command, NonEmptyText.tryCreate actor.ActorId with
        | Allowed events, Some id ->
            let applied =
                rehearse ({ ActorId = id; Kind = ActorKind.Human }: Actor) state command events
                |> List.fold (fun acc e -> acc |> Result.bind (fun s -> Evolve.evolve s e |> Result.map Some)) (Ok state)
            match applied with
            | Ok _ -> Allowed events
            | Error e -> Blocked [ { Code = "invariant.violation"; Message = e.Reason } ]
        | decision, _ -> decision

    /// The IF41-DOMAIN kernel.
    let domain : DomainKernel =
        { Decide = decide
          Evolve = Evolve.evolve
          Replay = replay
          Project = project }

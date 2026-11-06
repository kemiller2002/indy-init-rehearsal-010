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

    /// Decide with the core invariant firewall (Evolve.decide).
    let decide = Evolve.decide

    /// Every capability with the engine's reasons (boundary B), under the composed policy and for this actor.
    let capabilities (policies: PolicySet) (actor: ActorContext) (s: Investigation) : Capability list =
        Capabilities.evaluate policies actor s

    /// The IF41-DOMAIN kernel.
    let domain : DomainKernel =
        { Decide = decide
          Evolve = Evolve.evolve
          Replay = replay
          Project = project }

/// T14 interface freeze: the domain's versioned manifest of the boundaries it produces (REHEARSAL-AGENT-INTERFACE-FREEZE
/// boundaries A-C; D and E belong to persistence and report). A breaking change to any type named here is a new
/// contract version through the coordinator; Causa.Domain.Tests.Identity checks the frozen shapes against this manifest.
[<RequireQualifiedAccess>]
module InterfaceManifest =
    type Operation = { Name: string; Signature: string; Errors: string list }

    type Boundary =
        { Id: string
          Title: string
          Types: string list
          Operations: Operation list }

    /// The frozen package and contract this domain implements.
    let contract = "IF41-DOMAIN"
    let version = "4.1.5"
    /// The causa-storage release whose event payloads the domain types carry field for field.
    let storageContract = "causa-storage-v1.0.0"

    let boundaries: Boundary list =
        [ { Id = "A"
            Title = "Command submission"
            Types = [ "CommandEnvelope"; "Command"; "CoreCommand"; "PolicySet"; "ActorContext"; "Decision"; "Blocker" ]
            Operations =
              [ { Name = "Decide"
                  Signature = "PolicySet -> ActorContext -> Investigation option -> CommandEnvelope -> Decision"
                  Errors = [ "Blocked"; "RequiresReview"; "UnsupportedSemantics"; "Conflict"; "Duplicate" ] } ] }
          { Id = "B"
            Title = "Versioned projection"
            Types = [ "Investigation"; "InvestigationProjection"; "EventEnvelope"; "EventProvenance"; "EventPayload"; "CoreEvent" ]
            Operations =
              [ { Name = "Evolve"
                  Signature = "Investigation option -> EventEnvelope -> Result<Investigation, ProjectionError>"
                  Errors = [ "ProjectionError" ] }
                { Name = "Project"; Signature = "Investigation -> InvestigationProjection"; Errors = [] } ] }
          { Id = "C"
            Title = "Replay"
            Types = [ "ReplayError" ]
            Operations =
              [ { Name = "Replay"
                  Signature = "EventEnvelope list -> Result<Investigation, ReplayError>"
                  Errors = [ "EmptyHistory"; "NonContiguousVersion"; "IllegalEvent"; "UnknownEventKind" ] } ] } ]

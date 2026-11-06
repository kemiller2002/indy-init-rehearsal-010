// Owner: ui-2. The investigation workspace frame (screen "investigation"): the investigation header (identity: title,
// identifier, original problem statement, primary question, domain state; INV-006, INV-007, INV-008, INV-018, INV-023)
// and the "Investigation sections" navigation (UI-029). Pure: it reads the engine's model (Model.Loads, Model.Sessions)
// and lets the domain kernel project the investigation; it never decides legality and never writes state.
namespace Causa.App.Views

open System
open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

/// Where the route's investigation is, as the engine holds it.
type LocatedInvestigation =
    { /// The engine's identity for it (workspace and id), when the engine holds it under the route's target.
      Ref: InvestigationRef option
      /// The verified read (SM-LOAD Loaded), when there is one.
      Verified: VerifiedInvestigation option
      /// The SM-LOAD state, when a load was requested.
      Load: LoadState option
      /// The save session (working copies only).
      Session: InvestigationSession option }

/// The investigation as the domain kernel projects it: the verified history plus the session's unsaved commands.
type WorkingInvestigation =
    { Investigation: Investigation
      /// True when every event shown is in the verified (stored) history; false when unsaved commands contributed.
      Stored: bool }

[<RequireQualifiedAccess>]
module Frame =
    /// The actor the projection of unsaved commands is decided for. Decide requires an identified actor; the value is
    /// never shown and never stored (the engine stamps the stored provenance when it saves).
    let private projectionActor: ActorContext = { ActorId = "causa-app" }

    /// The route's investigation target and section, when the route is a workspace route.
    let target (state: AppState) : (RouteTarget * Section) option =
        match state.Route with
        | WorkspaceRoute(t, s) -> Some(t, s)
        | _ -> None

    let private matches (t: RouteTarget) (r: InvestigationRef) =
        r.Id = t.Id && r.Address.Role = t.Role && r.Address.Workspace = Workspace t.Workspace

    /// Finds the route's investigation in the engine's model (v4.1.5 W1: workspace and id together identify it).
    let locate (state: AppState) : LocatedInvestigation option =
        target state
        |> Option.map (fun (t, _) ->
            let model = state.Model
            let sessionRef = model.Sessions |> Map.tryFindKey (fun r _ -> matches t r)
            let loadRef = model.Loads |> Map.tryFindKey (fun r _ -> matches t r)
            let reference = sessionRef |> Option.orElse loadRef
            let load = loadRef |> Option.bind (fun r -> Map.tryFind r model.Loads)
            { Ref = reference
              Verified =
                match load with
                | Some(Loaded v) -> Some v
                | _ -> None
              Load = load
              Session = sessionRef |> Option.bind (fun r -> Map.tryFind r model.Sessions) })

    /// The commands this tab holds that are not yet confirmed on GitHub, oldest first.
    let pending (session: InvestigationSession option) : CommandEnvelope list =
        match session with
        | Some s -> s.Unsaved @ s.AfterInFlight
        | None -> []

    /// The kernel's projection: the verified investigation, then every pending command decided by the kernel and
    /// evolved onto it. A command the kernel does not allow contributes nothing. Unsaved events carry a provisional
    /// provenance that is never displayed (CreatedAt is shown only from the stored history).
    let working (verified: Investigation option) (commands: CommandEnvelope list) : Investigation option =
        let apply (current: Investigation option) (command: CommandEnvelope) =
            match Kernel.domain.Decide Policy.core projectionActor current command with
            | Allowed payloads ->
                let version0 = current |> Option.map (fun s -> s.Version) |> Option.defaultValue AggregateVersion.zero
                let occurredAt = current |> Option.map (fun s -> s.UpdatedAt) |> Option.defaultValue DateTimeOffset.UnixEpoch
                payloads
                |> List.indexed
                |> List.fold
                    (fun (acc: Investigation option) (i, payload) ->
                        let envelope: EventEnvelope =
                            { EventId = EventId(if i = 0 then command.CommandId else Guid.Empty)
                              Investigation = command.Investigation
                              Version = AggregateVersion(AggregateVersion.value version0 + int64 i + 1L)
                              Provenance =
                                { OccurredAt = occurredAt
                                  Actor = { ActorId = NonEmptyText.tryCreate projectionActor.ActorId |> Option.get; Kind = ActorKind.System }
                                  CorrelationId = command.CommandId
                                  FormatVersion = Events.formatVersion
                                  SemanticVersion = Events.semanticVersion }
                              Payload = payload }
                        match Kernel.domain.Evolve acc envelope with
                        | Ok next -> Some next
                        | Error _ -> acc)
                    current
            | _ -> current
        commands |> List.fold apply verified

    /// The route's investigation as the kernel projects it.
    let investigation (located: LocatedInvestigation) : WorkingInvestigation option =
        let verified = located.Verified |> Option.map (fun v -> v.Investigation)
        let commands = pending located.Session
        working verified commands
        |> Option.map (fun i -> { Investigation = i; Stored = List.isEmpty commands && verified.IsSome })

    /// INV-008: the original problem statement, as entered (the InvestigationStarted `original`).
    let problemStatement (i: Investigation) : string option =
        i.History
        |> List.tryPick (fun e ->
            match e.Payload with
            | CoreEvent(InvestigationStarted p) -> Some p.Original.Value
            | _ -> None)

    /// INV-023: the primary investigative question. causa-storage v1.0.0 has no field for it (DATA-REPOSITORY-CONTRACT
    /// C5), so it is the investigation's Unknown whose id is the investigation's own id (UnknownIdentified, recorded by
    /// the creation; see the change request). Any other unknown is not the primary question.
    let question (i: Investigation) : string option =
        let (InvestigationId id) = i.Id
        i.Unknowns |> Map.tryFind (UnknownId id) |> Option.map (fun u -> u.Unknown.Question.Value)

    /// INV-018: the domain lifecycle in words.
    let lifecycle (l: Lifecycle) : string =
        match l with
        | Lifecycle.Draft -> "Draft"
        | Lifecycle.Active -> "Active"
        | Lifecycle.AwaitingEvidence -> "Awaiting evidence"
        | Lifecycle.AwaitingVerification -> "Awaiting verification"
        | Lifecycle.Resolved -> "Resolved"
        | Lifecycle.Closed -> "Closed"
        | Lifecycle.Archived -> "Archived"

    /// INV-007: the creation time from the stored history; an investigation not yet saved has none.
    let createdAt (located: LocatedInvestigation) : DateTimeOffset option =
        located.Verified |> Option.map (fun v -> v.Investigation.CreatedAt)

    let formatTime (t: DateTimeOffset) = t.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")

    /// The sections the navigation offers (design contract investigationSections; Corrective Actions only where
    /// supported, and causa-storage v1.0.0 has no corrective-action workflow screen).
    let sections = [ Overview; CausalAnalysis; EvidenceSection; HypothesesSection; TimelineSection ]

    /// The section links of the route's investigation (UI-029): label, href and aria-current.
    let sectionLinks (t: RouteTarget) (current: Section) : NavLink<Section> list =
        sections
        |> List.map (fun s ->
            { Destination = s
              Label = UiRoutes.sectionLabel s
              Href = UiRoutes.href (WorkspaceRoute(t, s))
              Current = (s = current) })

    /// Why the frame has no investigation to show (Remote/LoadState as the engine holds it).
    let private absence (located: LocatedInvestigation) : string =
        match located.Load with
        | Some(Loading _) -> "Opening investigation…"
        | Some UnavailableOffline -> "This investigation is not available offline. It opens when the connection returns."
        | Some(LoadFailed(Unverifiable _)) -> "This investigation could not be opened: its stored files did not verify. Nothing was changed."
        | Some(LoadFailed(ProviderFailed _)) -> "This investigation could not be opened: GitHub did not return it. Nothing was changed."
        | _ -> "This investigation is not open in this tab."

    let private copyKind (role: WorkspaceRole) =
        match role with
        | WorkspaceRole.WorkingCopy -> "Working copy"
        | WorkspaceRole.Catalog -> "Catalog (read-only)"

    /// The frame's keys (screen "investigation"). Every key is always present.
    let view (state: AppState) : (string * ViewValue) list =
        let located = locate state
        let working = located |> Option.bind investigation
        let t = target state
        let identifier =
            match t with
            | Some(target, _) -> let (InvestigationId g) = target.Id in g.ToString("D")
            | None -> ""
        let i = working |> Option.map (fun w -> w.Investigation)
        let q = i |> Option.bind question
        let links =
            match t with
            | Some(target, s) -> sectionLinks target s
            | None -> []
        let link (s: Section) = links |> List.tryFind (fun l -> l.Destination = s)
        [ Bind.flag "investigation.found" i.IsSome
          Bind.flag "investigation.absent" (t.IsSome && i.IsNone)
          Bind.text "investigation.absence" (located |> Option.map absence |> Option.defaultValue "")
          Bind.text "investigation.title" (i |> Option.map (fun x -> x.Title.Value) |> Option.defaultValue "")
          Bind.text "investigation.identifier" identifier
          Bind.text "investigation.problem" (i |> Option.bind problemStatement |> Option.defaultValue "")
          Bind.flag "investigation.hasQuestion" q.IsSome
          Bind.text "investigation.question" (q |> Option.defaultValue "")
          Bind.text "investigation.state" (i |> Option.map (fun x -> lifecycle x.Lifecycle) |> Option.defaultValue "")
          Bind.text "investigation.copy" (t |> Option.map (fun (target, _) -> copyKind target.Role) |> Option.defaultValue "")
          Bind.text "investigation.created" (located |> Option.bind createdAt |> Option.map formatTime |> Option.defaultValue "Not on GitHub until the first commit")
          Bind.text "investigation.version" (i |> Option.map (fun x -> string (AggregateVersion.value x.Version)) |> Option.defaultValue "")
          for s in sections do
              let slug = UiRoutes.sectionSlug s
              Bind.text ("investigation.section." + slug + ".href") (link s |> Option.map (fun l -> l.Href) |> Option.defaultValue "#/investigations")
              Bind.current ("investigation.section." + slug + ".current") (link s |> Option.exists (fun l -> l.Current)) ]

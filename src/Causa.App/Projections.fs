// Owner: integration. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
namespace Causa.App

open Causa.Domain
open Causa.Persistence

/// IF41-VIEW: projections the views render (ui and ui-2). Views read only these; they never decide legality.
type SaveAction =
    | Save
    | Retry
    | Reconnect
    | CheckAgainAction
    | Review
    | ReapplyMine
    | DiscardMine
    | ExportMine

type SaveIndicatorView =
    { InvestigationId: InvestigationId
      Label: string
      Text: string
      Glyph: string
      Actions: SaveAction list
      Politeness: Politeness
      LastSavedCommitTime: System.DateTimeOffset option }

type WorkspaceHeaderView =
    { Connection: ConnectionState
      Offline: bool
      NotSaved: (InvestigationId * string) list }

type ScreenView =
    { Route: Route
      Title: string
      Header: WorkspaceHeaderView
      Indicator: SaveIndicatorView option }

type Projections =
    { SaveIndicator: InvestigationSession -> SaveIndicatorView
      Header: Model -> WorkspaceHeaderView
      Screen: Model -> ScreenView }

// ---- IF-UI (CF-09 A, APR-CFUI-OWNER-1): the presentation contract types for screens, folded into IF41-VIEW so no new
// contract file is added (V41Package.fs:1790). Types and signatures only, plus the boundary members that must sit beside
// a private representation (FixtureLabel, Visible, Understood, SafeUrl). The functions over these types are the
// producer's (proposals/r10-contract-first-ui/contracts/Presentation.fs). Identities are InvestigationRef; a route
// carries an unresolved RouteTarget (v4.1.5 W1, rev 3).

// -- Ids.fs --
namespace Causa.Ui.Contracts

/// Stable identifiers. Catalog entries keep their CAT-CAUSA-INVESTIGATION id (INV-0xx) as their field, action or region id,
/// so a UI element, its manifest entry and its Dokimos/catalog check share one name. Entries no catalog item names (the
/// connection form, journey-named actions) get a UIF-/UIA-/UIR- id and cite the source that requires them
/// (SCREEN-MANIFEST.json). The id is also the Limen binding name (data-text, data-event) of the element.
type ScreenId = ScreenId of string
type FieldId = FieldId of string
type ActionId = ActionId of string
type RegionId = RegionId of string

// -- Types.fs --

open System
open System.Text.RegularExpressions
open Causa.Domain
open Causa.Persistence

/// A fixture's label. Every fixture value carries one, so a fixture-backed screen can never be reported as an
/// integrated one (VERIFICATION-STRATEGY.md, Layer A/B versus Layer C). Construct only through FixtureLabel.create.
type FixtureLabel = private FixtureLabel of string

[<RequireQualifiedAccess>]
module FixtureLabel =
    let create (name: string) : FixtureLabel = FixtureLabel("FIXTURE: " + name)
    let value (FixtureLabel s) = s

/// Where a stored record was read: the repository commit when the provider supplied one.
type StoredRef = { Path: string; Commit: string option }

// ---- The three capability boundaries (README.md section 3) ----------------------------------------------------------

/// What the UI is entitled to claim about a value.
type Capability =
    /// Repository status, investigation summaries, metadata and stored records exactly as read. No replay is claimed.
    | DataVisible
    /// Validated projections the domain engine supplied (hypotheses, evidence, relationships, timeline, conclusions,
    /// state). The UI never replays events itself.
    | DataUnderstood
    /// Inputs, commands, validation results and save progress. Persistence and the domain engine decide correctness.
    | DataEditable

/// A value shown as stored (Data Visible). There is no Visible -> Understood conversion. Understood.projected is public,
/// though, so "stored data is never presented as the kernel's interpretation" rests on an ownership rule (only the
/// integration adapter calls Understood.projected; screens and Presentation.fs never do), checked in review, not on
/// the types.
type Visible<'a> =
    private
    | StoredValue of 'a * StoredRef
    | VisibleFixture of 'a * FixtureLabel

/// A value the domain engine projected at a version (Data Understood).
type Understood<'a> =
    private
    | Projected of 'a * AggregateVersion
    | UnderstoodFixture of 'a * FixtureLabel

[<RequireQualifiedAccess>]
module Visible =
    let stored (at: StoredRef) (value: 'a) = StoredValue(value, at)
    let fixture (label: FixtureLabel) (value: 'a) = VisibleFixture(value, label)
    let value v = match v with StoredValue(x, _) | VisibleFixture(x, _) -> x
    let fixtureLabel v = match v with VisibleFixture(_, l) -> Some l | StoredValue _ -> None
    let map f v = match v with StoredValue(x, s) -> StoredValue(f x, s) | VisibleFixture(x, l) -> VisibleFixture(f x, l)

[<RequireQualifiedAccess>]
module Understood =
    /// Only the integration adapter (Ui/UiAdapter.fs), holding a domain projection, may call this. That is an ownership
    /// and review rule: the type does not stop another caller.
    let projected (version: AggregateVersion) (value: 'a) = Projected(value, version)
    let fixture (label: FixtureLabel) (value: 'a) = UnderstoodFixture(value, label)
    let value u = match u with Projected(x, _) | UnderstoodFixture(x, _) -> x
    let version u = match u with Projected(_, v) -> Some v | UnderstoodFixture _ -> None
    let fixtureLabel u = match u with UnderstoodFixture(_, l) -> Some l | Projected _ -> None
    let map f u = match u with Projected(x, v) -> Projected(f x, v) | UnderstoodFixture(x, l) -> UnderstoodFixture(f x, l)

/// A displayed fact, either as stored or as projected; the boundary stays visible to the view.
type Shown<'a> =
    | AsStored of Visible<'a>
    | AsProjected of Understood<'a>

/// A count that may be unknown. Unknown is never rendered as zero (UI-022, UI-001 "where supported").
type Count =
    | Known of int
    | UnknownCount
    | NotSupported

/// Whether an action is offered. The domain engine decides (UI-004); the UI renders only what it is given.
type Availability =
    | Available
    /// Shown disabled with the engine's reason, only when the engine supplies one.
    | Unavailable of reason: string
    /// Not rendered at all.
    | NotOffered

// ---- Validation (catalog validation blocks) ----------------------------------------------------------------------
// Close to Limen.Forms' FieldSpec, but NOT identical: Limen.Forms has Required: bool and no Pattern, counts runes, does
// not trim and reports one error per field; this checks UTF-16 length, trims for MinLength and lists every failed rule.
// Which semantics the product uses is CF-12.

type Rule =
    | Required
    | MinLength of int
    | MaxLength of int
    | Pattern of string
    | OneOf of string list

type ComponentKind =
    | TextInput
    | TextArea
    | UrlInput
    | PasswordInput
    | RadioGroup
    | Select
    | Display
    | Button
    | Link
    | Region

type FieldSpec =
    { Field: FieldId
      /// The accessible name the governed journeys look the field up by (catalog `name`).
      Label: string
      Component: ComponentKind
      Rules: Rule list }

type ValidationError = { Field: FieldId; Rule: Rule; Message: string }

/// An absolute http(s) URL, the only form INV-029 allows to be rendered as a link (UI-005 sanitising).
type SafeUrl = private SafeUrl of string

[<RequireQualifiedAccess>]
module SafeUrl =
    let pattern = @"^https?://[^\s]+$"
    let tryCreate (s: string) =
        let schemeOk = s.StartsWith("http://", StringComparison.Ordinal) || s.StartsWith("https://", StringComparison.Ordinal)
        if schemeOk && Uri.IsWellFormedUriString(s, UriKind.Absolute) && Regex.IsMatch(s, pattern) && s.Length <= 2000 then Some(SafeUrl s)
        else None
    let value (SafeUrl s) = s

/// What a route asks for, exactly as the URL states it. It is not an address: v4.1.5 addresses are derived, never
/// asserted (WorkspaceAddress.catalog / workingCopy, 02-IF41-STORAGE.fs), so whoever holds the listings resolves a target
/// against the workspaces it actually listed, and an unresolved target is not found.
type RouteTarget = { Workspace: string; Role: WorkspaceRole; Id: InvestigationId }

// ---- Failure and loading ---------------------------------------------------------------------------------------------

/// Why data is not shown. Each case has its own plain-language copy (PresentationLanguage.failure, UI-010, UI-028).
/// Qualified access: CredentialRefused would otherwise collide with IF41-APP ConnectionState.CredentialRefused.
[<RequireQualifiedAccess>]
type UiFailure =
    /// Keyed by workspace and id (v4.1.5 W1): the same case can be stored in two workspaces.
    | InvestigationNotFound of RouteTarget
    /// The stored files did not verify (schema, digest, replay refusal). Fails closed: nothing partial is shown.
    | InvalidData of detail: string
    | NotAuthorized
    | CredentialRefused
    | ProviderUnreachable
    | ProviderRateLimited
    /// The provider answered something no case classifies.
    | ProviderError of detail: string
    /// The capability the screen needs is not provided by the current backend or by the frozen contracts.
    | CapabilityAbsent of needed: Capability * because: string

/// Data a screen is waiting for.
type Remote<'a> =
    | NotRequested
    /// The copy describes the user's task (UI-024 "Opening investigation"), never a fabricated progress figure.
    | Waiting of activity: string
    | Ready of 'a
    | Failed of UiFailure

// -- UiStates.fs --

/// The UI states every screen declares (SCREEN-MANIFEST.json `states`). A screen view is in exactly one.
type ScreenState =
    | ScreenLoading
    | ScreenReady
    | ScreenEmpty
    | ScreenFailed of UiFailure

/// The seven save states (PERSIST-031, UI-006). Presentation-only: the frozen SaveState (IF41-SAVE) decides which one
/// applies, and an adapter maps it with a total match (real-adapter-sketch/RealAdapter.fs), so an eighth state or a
/// missing one does not compile.
type SaveStateKind =
    | SavedKind
    | UnsavedKind
    | SavingKind
    | OfflineKind
    | ConflictKind
    | UnknownOutcomeKind
    | SaveFailedKind

/// What a failure tells the user (UI-010: what happened, what was preserved, a safe next step).
type FailureCopy =
    { What: string
      Preserved: string
      NextStep: string
      /// Offered only when it performs a real action (UI-010).
      Retry: bool }

// -- Navigation.fs --

open System
open Causa.Domain
open Causa.Persistence

/// The investigation sections (INV-042, UI-029). Corrective Actions only "where supported".
type Section =
    | Overview
    | CausalAnalysis
    | EvidenceSection
    | HypothesesSection
    | TimelineSection
    | CorrectiveActions

/// Every route the in-scope requirements define. Hash routes (SCREEN-CONTRACTS URL contract, D-203).
type UiRoute =
    /// `#/` investigation-list (catalog screens[0]); before Connect it holds the connection region (UI-001, EXT-DATA-002).
    | InvestigationsRoute
    /// `#/investigations/new` (catalog screens[1]).
    | StartRoute
    /// `#/investigations/{id}?ws=<workspace>&role=<catalog|working-copy>[&tab=<section>]`. The workspace and its role
    /// are part of the identity (v4.1.5 W1: the same case can be stored in two workspaces); `tab` (S02, default
    /// overview) selects the section.
    | WorkspaceRoute of RouteTarget * Section
    /// `#/administration`: UI-018 requires the page; no document fixes its path (OWNER-DECISIONS CF-05).
    | AdministrationRoute
    /// `#/settings` (S24).
    | SettingsRoute
    | NotFoundRoute of requested: string

type PrimaryDestination =
    | Investigations
    | Administration
    | Settings

/// One navigation link as the view renders it; `Current` becomes aria-current="page" (J2-08, J3-01).
type NavLink<'d> = { Destination: 'd; Label: string; Href: string; Current: bool }

// -- Connection.fs --

open Causa.App

/// The Connect form's text fields as entered. The credential is not here: it reaches the backend as an opaque frozen
/// Credential.T (UiIntent.Secret) and is never part of any view model, binding or log (PF-015, INV-21, UI-019).
type ConnectForm = { Owner: string; Repository: string; Branch: string; CredentialEntered: bool }

/// What the connected repository allows, as the provider stated it (EXT-DATA-005). Frozen: ConnectResult.ConnectAccepted.
type WriteScope =
    | ReadOnlyConnection
    | Writable
    /// Not yet known (before Connect succeeds).
    | WriteScopeUnknown

/// The connection region of investigation-list (UI-001 before Connect; EXT-DATA-002/003/007).
type ConnectionView =
    { /// SM-CONNECTION's state, frozen (IF41-APP ConnectionState); the view never invents a sixth.
      State: ConnectionState
      Form: ConnectForm
      Errors: ValidationError list
      Connect: Availability
      Disconnect: Availability
      /// "owner/repository on branch", shown after Connect (UI-022: not in the global header as a commit hash).
      Connected: string option
      WriteScope: WriteScope }

// -- InvestigationList.fs --

open System
open Causa.Domain
open Causa.Persistence

/// UI-014-O3 and UI-001: frozen demonstrations are told apart from the user's writable copies.
type CopyKind =
    | FrozenDemonstration
    | WorkingCopy
    | CopyKindUnknown

/// The counts a card shows "where supported" (UI-001). Each may be unknown; none defaults to zero.
type SummaryCounts = { Hypotheses: Count; Evidence: Count; UnresolvedContradictions: Count }

/// One investigation card (UI-001, UI-022). Title and last activity are Data Visible; lifecycle and counts are shown
/// with their boundary, so a stored value is never presented as the kernel's interpretation (UI-023).
type InvestigationSummary =
    { /// Where the investigation is and its id (v4.1.5 W1); opening it dispatches with this, never with the id alone.
      Ref: InvestigationRef
      Title: string
      /// The domain lifecycle (UI-022 "actual domain lifecycle/status"), never an "expected" one.
      Status: Shown<string> option
      LastActivity: DateTimeOffset option
      Counts: Shown<SummaryCounts> option
      Copy: CopyKind
      /// The canonical workspace route (INV-040 activate); also the row key the open action carries.
      Href: string }

type ListSort =
    | ByUpdated
    | ByTitle

/// investigation-list: before Connect the connection region; after Connect the Investigations region (INV-039).
type InvestigationListView =
    { Connection: ConnectionView
      /// NotRequested before Connect; Ready [] is the empty catalog (UI-010 "no investigations"), distinct from Failed.
      Investigations: Remote<Visible<InvestigationSummary list>>
      Sort: ListSort
      /// Opens investigation-start; offered only on a writable connection (J1-02 S8 is open: OWNER-DECISIONS CF-04).
      NewInvestigation: Availability
      State: ScreenState }

// -- InvestigationDetail.fs --

open System
open Causa.Domain
open Causa.Persistence

/// One stored event as read (Data Visible): the wire record, uninterpreted. Shown, for example, when the kernel cannot
/// yet project a history; never presented as the investigation's state (UI-010 "fail closed", UI-023).
type StoredEvent =
    { Version: int64
      EventType: string
      OccurredAt: DateTimeOffset
      ActorId: string
      /// The payload JSON exactly as stored. Rendered as text only (UI-005 sanitising).
      PayloadJson: string }

/// The read-only record of an investigation as stored (Data Visible). No replay is claimed.
type StoredRecordView =
    { Ref: InvestigationRef
      Title: string
      CreatedAt: DateTimeOffset option
      UpdatedAt: DateTimeOffset option
      EventCount: int
      /// A page of stored events, oldest first.
      Events: StoredEvent list }

/// The workspace frame every section shares (INV-007, INV-008, INV-018, INV-042, UI-013, UI-014).
type WorkspaceFrame =
    { /// The identity every section dispatches with (v4.1.5 W1).
      Ref: InvestigationRef
      /// The investigation identifier as text (INV-006 read-only after Start; INV-034 keeps it).
      Identifier: string
      Title: string
      /// INV-008: the original problem statement, as entered.
      ProblemStatement: Shown<string>
      /// INV-023: optional; shown in the header.
      Question: Shown<string> option
      /// INV-007.
      CreatedAt: Shown<DateTimeOffset>
      /// INV-018: the domain state, from the domain only.
      InvestigationState: Shown<string>
      Copy: CopyKind
      Sections: NavLink<Section> list
      /// Which aggregate version every section of this frame refers to (UI-013); None for stored-only data.
      Version: AggregateVersion option }

// -- InvestigationEditor.fs --

open Causa.App

// ---- investigation-start (S01; INV-001..006, INV-019..023) -----------------------------------------------------------

/// Where a Start will be written (INV-005), shown before any remote write.
type Destination = { Repository: string; Branch: string; Writable: bool }

type StartForm = { ProblemStatement: string; Title: string; Question: string; Identifier: string }

/// The progress of one submitted command. Correctness is the engine's and persistence's; the UI shows the outcome.
/// Qualified access: Refused would otherwise collide with IF41-SAVE SaveFailure.Refused.
[<RequireQualifiedAccess>]
type Submission =
    | NotSubmitted
    | Submitting
    | Refused of reasons: string list
    | Accepted

type StartView =
    { Form: StartForm
      Errors: ValidationError list
      Destination: Remote<Destination>
      /// INV-003 applies only when a domain profile is selected (catalog fact domain-profile-selected).
      Profile: string option
      Start: Availability
      Cancel: Availability
      /// Data Editable progress of the Start command (submit, refusal, accepted).
      Submission: Submission }

// ---- Edit investigation details (INV-034, INV-035) --------------------------------------------------------------------

type DetailsForm = { Title: string; ProblemStatement: string; Question: string }

type DetailsEditorView =
    { Open: bool
      Form: DetailsForm
      Errors: ValidationError list
      /// INV-034 is offered only where the engine authorizes it (UI-004); under causa-data v1.0.0 there is no rename
      /// event (DATA-REPOSITORY-CONTRACT C5), so a real backend reports Unavailable until ODR-R10-26 is decided.
      Edit: Availability
      SaveDetails: Availability
      Cancel: Availability }

// ---- Save-state presentation (INV-015, INV-016, INV-043, UI-006, PERSIST-030..033) ------------------------------------

/// SM-CONFLICT review as the user sees it (S23): this tab's version, GitHub's version, the unsaved changes kept, and
/// the safe choices the frozen machine offers (IF41-VIEW SaveAction).
type ConflictPresentation =
    { ThisTabVersion: string
      GitHubVersion: string
      UnsavedChanges: int
      Explanation: string
      Choices: SaveAction list }

type SavePresentation =
    { Kind: SaveStateKind
      /// The frozen IF41-VIEW projection; Label equals PresentationLanguage.saveState Kind (contract test).
      Indicator: SaveIndicatorView
      Save: Availability
      Conflict: ConflictPresentation option
      /// PERSIST-033: investigations in this tab with unsaved changes.
      NotSaved: string list }

// -- InvestigationAnalysis.fs --

open System
open Causa.Domain

/// A record the UI can link to (UI-033): its kind and its stable id as text. Not a domain type.
type EntityKind =
    | ObservationEntity
    | HypothesisEntity
    | EvidenceEntity
    | RelationshipEntity
    | ConclusionEntity
    | ContradictionEntity
    | CorrectiveActionEntity
    | OtherEntity of string

type EntityRef = { Kind: EntityKind; Id: string }

/// INV-031's options (OD-INDY-23). "Not assessed" is the absence of a relationship, never an option.
type RelationshipKind =
    | Supports
    | Contradicts
    | ConsistentWith
    | Inconclusive

/// A kind as the projection supplies it: one of INV-031's options, or a stored kind no option names (causa-data's
/// `discriminates`, `insufficient`: DATA-REPOSITORY-CONTRACT C5), shown verbatim and never mapped by the UI.
type KindShown =
    | CatalogKind of RelationshipKind
    | UninterpretedKind of string

type ObservationCard = { Id: string; Text: string; SourceReference: string option; ObservedAt: DateTimeOffset option }

/// One evidence-hypothesis assessment, shown on both the evidence and the hypothesis (INV-044).
type LinkCard =
    { Id: string
      Evidence: EntityRef
      EvidenceLabel: string
      Hypothesis: EntityRef
      HypothesisStatement: string
      Kind: KindShown
      Rationale: string
      /// At most one current assessment per pair; earlier ones stay in the history (INV-030, INV-035).
      Current: bool }

type HypothesisCard =
    { Id: string
      Statement: string
      /// The hypothesis's lifecycle word as the domain states it.
      State: string
      Explanation: string option
      Links: LinkCard list }

type EvidenceCard =
    { Id: string
      /// INV-027. Option because causa-data v1.0.0 has no label field (C5); None renders as "No label recorded".
      Label: string option
      Content: string
      SourceReference: string option
      /// Only a SafeUrl is rendered as a link (INV-029, UI-005).
      SourceUrl: SafeUrl option
      Category: string option
      CapturedAt: DateTimeOffset option
      Links: LinkCard list }

type TimelineItem =
    { Version: int64
      EventType: string
      Summary: string
      OccurredAt: DateTimeOffset
      Actor: string option
      Affects: EntityRef list }

type ConclusionCard = { Id: string; Statement: string; ResidualUncertainty: string option; RecordedAtVersion: int64 option }

/// UI-032: a domain-derived next step linked to its record; never invented.
type NextStep = { Text: string; Target: EntityRef; Action: ActionId option }

/// The projection the analysis sections render (Data Understood). One value, one version, for every section (UI-013).
type AnalysisProjection =
    { Observations: ObservationCard list
      Hypotheses: HypothesisCard list
      Evidence: EvidenceCard list
      Relationships: LinkCard list
      Timeline: TimelineItem list
      Conclusions: ConclusionCard list
      NextSteps: NextStep list
      UnresolvedContradictions: Count
      CorrectiveActionsSupported: bool
      /// Which content actions the engine authorizes now (UI-004). Keys are ActionIds.
      Actions: Map<ActionId, Availability> }

// ---- Content editors (Data Editable) -----------------------------------------------------------------------------------

/// The text of the open content editors and their validation (one map: the editor never holds a second store).
type EditorDrafts = { Values: Map<FieldId, string>; Errors: ValidationError list; OpenEditor: ActionId option }

// ---- investigation-workspace -------------------------------------------------------------------------------------------

/// The content of the current section. Analysis sections need Data Understood; without it they say so (CapabilityAbsent),
/// never "not supported" as a substitute for content (J2-08) and never a partial replay.
type SectionView =
    | OverviewSection of Remote<Understood<AnalysisProjection>>
    | CausalAnalysisView of Remote<Understood<AnalysisProjection>>
    | EvidenceView of Remote<Understood<AnalysisProjection>>
    | HypothesesView of Remote<Understood<AnalysisProjection>>
    | TimelineView of Remote<Understood<AnalysisProjection>>
    | CorrectiveActionsView of Remote<Understood<AnalysisProjection>>

type WorkspaceView =
    { Frame: Remote<WorkspaceFrame>
      /// The stored record (Data Visible), available even where no projection is.
      Stored: Remote<Visible<StoredRecordView>>
      Section: SectionView
      Drafts: EditorDrafts
      Details: DetailsEditorView
      Save: SavePresentation option
      State: ScreenState }

// -- System.fs --

/// A system fact on Administration (UI-018): unknown, unavailable, failed and verified are distinct, and nothing
/// unverified is shown as passed.
type SystemFactStatus =
    | FactKnown of string
    | FactUnknown
    | FactUnavailable
    | FactFailed of string

type SystemFact = { Label: string; Status: SystemFactStatus }

/// Administration (UI-018, UI-031): read-only; system detail moves here from investigation screens (UI-017, UI-022).
type AdministrationView =
    { Provider: SystemFact
      Repository: SystemFact
      Connection: SystemFact
      Catalog: SystemFact
      StorageContract: SystemFact
      ApplicationVersion: SystemFact
      DeployedCommit: SystemFact
      DataValidation: SystemFact
      Diagnostics: string list }

/// Where a preference lives (UI-019 "define persistence and scope of each setting").
type PreferenceScope =
    | SessionScope
    | DeviceScope

/// One supported preference. Which preferences exist is an owner decision (OWNER-DECISIONS CF-06); fixtures label theirs.
type Preference =
    { Id: string
      Label: string
      Scope: PreferenceScope
      Options: string list
      Effective: string }

type SettingsView =
    { Preferences: Preference list
      /// "Distinguish a preference from a verified external connection" (UI-019).
      ConnectionPreference: SystemFact }

type NotFoundView = { Requested: string; Copy: FailureCopy }

// -- UiActions.fs --

open Causa.Domain
open Causa.Persistence

/// The screen a route shows.
type ScreenView =
    | ListScreen of InvestigationListView
    | StartScreen of StartView
    | WorkspaceScreen of WorkspaceView
    | AdministrationScreen of AdministrationView
    | SettingsScreen of SettingsView
    | NotFoundScreen of NotFoundView

/// Who produced a view: a claim, not a proof. IntegratedBackend is a public case; that only the integration adapter
/// over the real engine produces it, and that fixtures always say FixtureBackend, are ownership rules checked in review
/// and by the fixture tests, not type guarantees. Acceptance evidence is never this value: only Layer C (slice-gate,
/// the governed journeys) judges an integrated feature (VERIFICATION-STRATEGY.md).
type BackendOrigin =
    | FixtureBackend of FixtureLabel
    | IntegratedBackend

type AppView =
    { Route: UiRoute
      Primary: NavLink<PrimaryDestination> list
      Screen: ScreenView
      /// Polite/assertive announcements for the live region (frozen Politeness).
      Announcements: (Politeness * string) list
      Origin: BackendOrigin }

/// What the user did, in Limen's SemanticEvent shape: an element id, an optional row key, an optional value. Views emit
/// only these; they never call a backend function, decide legality or build a domain command. Qualified access: its
/// Navigate would otherwise collide with IF41-APP Msg.Navigate (X04).
[<RequireQualifiedAccess>]
type UiIntent =
    | Navigate of UiRoute
    /// A field's value changed (data-on="input").
    | Input of field: FieldId * value: string * key: string option
    /// The credential field: turned into the opaque frozen Credential.T before it reaches any backend.
    | Secret of field: FieldId * Credential.T
    /// A button or link was activated.
    | Activate of action: ActionId * key: string option

/// The one substitutable interface. The fixture backend (fixtures/) and the integration adapter (real-adapter-sketch/)
/// both implement it; screens depend only on AppView and UiIntent. Pure: effects are data the host performs.
type UiBackend<'state, 'effect> =
    { /// The boundaries this backend actually provides; a screen needing more shows CapabilityAbsent.
      Provides: Capability list
      Init: UiRoute -> 'state * 'effect list
      Dispatch: UiIntent -> 'state -> 'state * 'effect list
      Project: 'state -> AppView }

/// An action's contract, as data (SCREEN-MANIFEST.json `actions` is checked against these).
type ActionContract =
    { Action: ActionId
      Screen: ScreenId
      Label: string
      Component: ComponentKind
      Needs: Capability
      /// The fields validated before the action is sent.
      Validates: FieldId list
      Outcome: string }

// -- Bindings.fs --

/// Limen 0.9.1's view-state shape (limen.core contract, guests/fsharp/Limen.Contract Core.fs ViewValue/ViewPrimitive),
/// mirrored case for case so this library compiles before the integration lane adds EchelonFoundry.Limen.Contract at
/// T10. Integration replaces these two types with aliases of Limen.Contract.Core.ViewValue/ViewPrimitive; no screen
/// code changes. A screen module's whole output is a Bindings map.
[<RequireQualifiedAccess>]
type BindingPrimitive =
    | Text of string
    | Number of float
    | Flag of bool

[<RequireQualifiedAccess>]
type BindingValue =
    | Text of string
    | Number of float
    | Flag of bool
    | Items of Map<string, BindingPrimitive> list

type Bindings = Map<string, BindingValue>

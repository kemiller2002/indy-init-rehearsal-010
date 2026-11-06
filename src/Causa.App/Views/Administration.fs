// Owner: ui (written by ui-2 acting as ui for T29). Administration (UI-018, UI-020-O2, UI-031, VER-007): read-only facts
// about the connection, the data and this build. Versions are the ones actually loaded: the running .NET runtime and
// the loaded domain assembly's contract constants, and the application's build record (dist/build-info.json, written
// by build.fsx from the installed packages) as the engine fetched it from the serving origin. Each fact says whether
// it is verified, known (stated, not verified), unknown, unavailable or failed; nothing unverified is shown as
// verified, and no secret is ever shown. Keys start with "administration.".
namespace Causa.App.Views

open System
open System.Text.Json
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

/// The build record as build.fsx writes it (schema causa.build-info/1): the commit built, the build gate's verdict and
/// the installed package versions.
type BuildRecord =
    { Commit: string
      GateVerdict: string
      /// name -> version, as installed when the build ran (package-lock / node_modules).
      Packages: (string * string) list }

/// What the engine read of the build record: not yet, read, or the read failed (with why).
type BuildRead = Result<string, string> option

/// How far a fact is established (UI-018-O3/O4): verified by a check that ran, stated by a source, or absent.
type FactClaim =
    | VerifiedFact of string
    | StatedFact of string
    | UnknownFact
    | UnavailableFact of string
    | FailedFact of string

[<RequireQualifiedAccess>]
module Administration =
    /// The storage contract and domain contract the loaded domain assembly implements (not a requirement's number).
    let storageContract = Causa.Domain.InterfaceManifest.storageContract
    let domainContract = $"{Causa.Domain.InterfaceManifest.contract} {Causa.Domain.InterfaceManifest.version}"

    /// The packages Administration names as Echelon components, with the names people use for them.
    let echelonComponents =
        [ "@echelon-foundry/limen", "Limen (browser kernel)"
          "@echelon-foundry/design-system", "Forma (design system)"
          "@echelon-foundry/print-components", "Folio (print components)" ]

    /// Parses dist/build-info.json. Error says what is wrong with it; nothing is guessed.
    let parseBuild (json: string) : Result<BuildRecord, string> =
        try
            use doc = JsonDocument.Parse json
            let root = doc.RootElement
            let str (e: JsonElement) (name: string) =
                match e.TryGetProperty name with
                | true, v when v.ValueKind = JsonValueKind.String -> Option.ofObj (v.GetString())
                | _ -> None
            match str root "schema" with
            | Some "causa.build-info/1" ->
                let verdict =
                    match root.TryGetProperty "gate" with
                    | true, g -> str g "verdict" |> Option.defaultValue ""
                    | _ -> ""
                let packages =
                    match root.TryGetProperty "packages" with
                    | true, p when p.ValueKind = JsonValueKind.Array ->
                        [ for e in p.EnumerateArray() do
                              match str e "name", str e "version" with
                              | Some n, Some v -> yield n, v
                              | _ -> () ]
                    | _ -> []
                match str root "commit" with
                | Some c when c.Length >= 7 -> Ok { Commit = c; GateVerdict = verdict; Packages = packages }
                | _ -> Error "the build record names no commit"
            | other -> Error $"the build record has schema %A{other}, not causa.build-info/1"
        with e -> Error("the build record is not JSON: " + e.Message)

    let private short (commit: string) = if commit.Length > 7 then commit.Substring(0, 7) else commit

    let connectionClaim (model: Model) =
        match model.Connection with
        | Disconnected -> StatedFact "Not connected"
        | Connecting _ -> UnknownFact
        | Connected true -> VerifiedFact "Connected; GitHub confirmed read and write access"
        | Connected false -> VerifiedFact "Connected; GitHub confirmed read access only"
        | CredentialRefused -> FailedFact "GitHub refused the token"
        | Reauthenticating -> FailedFact "The token needs re-entering"

    /// Authentication without the secret: whether a token was entered and whether GitHub accepted it.
    let private authenticationClaim (state: AppState) =
        match state.Model.Connection, state.Credential with
        | Connected _, _ -> VerifiedFact "Token accepted by GitHub (the token is never shown)"
        | CredentialRefused, _ -> FailedFact "Token refused by GitHub"
        | _, Some _ -> UnknownFact
        | _, None -> StatedFact "No token entered"

    /// A diagnostic code for a verification failure (the technical detail investigation pages leave out, UI-017-O4).
    let private failureCode (f: VerificationFailure) =
        match f with
        | SchemaInvalid(file, _) -> $"SCHEMA_INVALID {file}"
        | DigestMismatch field -> $"DIGEST_MISMATCH {field}"
        | ContractValueMismatch(field, _, _) -> $"CONTRACT_VALUE_MISMATCH {field}"
        | SnapshotDiffersFromReplay paths ->
            let joined = String.concat ", " paths
            $"SNAPSHOT_DIFFERS_FROM_REPLAY {joined}"
        | MissingRequiredField(file, field) -> $"MISSING_REQUIRED_FIELD {file} {field}"
        | ReplayRejected e -> $"REPLAY_REJECTED %A{e}"
        | ReaderStage(code, file, _, _) -> $"{code} {file}"

    /// UI-031-O2: the outcome of every investigation load, and a build record that could not be read, as diagnostics
    /// (never the credential).
    let diagnostics (build: BuildRead) (model: Model) : string list =
        [ for KeyValue(r, load) in model.Loads do
              let (Causa.Domain.InvestigationId id) = r.Id
              let (Workspace w) = r.Address.Workspace
              match load with
              | LoadFailed(Unverifiable failures) ->
                  for f in failures do
                      yield $"{w}/{id:D}: {failureCode f}"
              | LoadFailed(ProviderFailed e) -> yield $"{w}/{id:D}: PROVIDER %A{e}"
              | _ -> ()
          match build with
          | Some(Error why) -> yield $"build-info.json: {why}"
          | Some(Ok json) ->
              match parseBuild json with
              | Error why -> yield $"build-info.json: {why}"
              | Ok _ -> ()
          | None -> () ]

    /// UI-023-O3: data validation and domain replay are technical facts, kept apart from investigation verification.
    let private validationClaim (model: Model) =
        let loaded = model.Loads |> Map.filter (fun _ l -> match l with Loaded _ -> true | _ -> false) |> Map.count
        let refused = model.Loads |> Map.filter (fun _ l -> match l with LoadFailed(Unverifiable _) -> true | _ -> false) |> Map.count
        match loaded, refused with
        | 0, 0 -> UnknownFact
        | l, 0 -> VerifiedFact $"{l} investigation history(ies) verified and replayed on opening; none refused"
        | l, r -> FailedFact $"{r} investigation history(ies) refused on opening; {l} verified"

    /// The active catalog and its source reference, as listed from GitHub (EXT-DATA-013).
    let private catalogClaims (model: Model) =
        match model.Listings |> Map.toList |> List.tryFind (fun (a, _) -> a.Role = WorkspaceRole.Catalog) with
        | Some(address, listing) ->
            let (Workspace w) = address.Workspace
            let (CommitOid c) = listing.ReadAt
            StatedFact(w.Split('/') |> Array.last), StatedFact $"catalog/active.json → {w} at commit {short c}"
        | None when not model.ListingsInFlight.IsEmpty -> UnknownFact, UnknownFact
        | None -> UnknownFact, UnknownFact

    /// The facts the build record states, or why they are not known.
    let private buildClaims (build: BuildRead) =
        match build |> Option.map (Result.bind parseBuild) with
        | None -> UnknownFact, UnknownFact, UnknownFact, [ for _, label in echelonComponents -> label, UnknownFact ]
        | Some(Error why) ->
            let failed = FailedFact("The build record could not be read: " + why)
            failed, failed, failed, [ for _, label in echelonComponents -> label, failed ]
        | Some(Ok b) ->
            let gate =
                match b.GateVerdict with
                | "PASS" -> VerifiedFact "The build's stack-lock gate passed"
                | "" -> UnavailableFact "The build record has no gate verdict"
                | v -> FailedFact $"The build's stack-lock gate reported {v}"
            let components =
                [ for name, label in echelonComponents ->
                      label,
                      match b.Packages |> List.tryFind (fst >> (=) name) with
                      | Some(_, v) when v <> "" -> StatedFact v
                      | _ -> UnavailableFact "Not in this build" ]
            StatedFact $"Causa build {short b.Commit}", StatedFact b.Commit, gate, components

    /// Every fact, in order: (key, label, claim).
    let facts (build: BuildRead) (state: AppState) : (string * string * FactClaim) list =
        let model = state.Model
        let catalog, catalogSource = catalogClaims model
        let application, commit, gate, components = buildClaims build
        [ "provider", "Provider", StatedFact "GitHub (api.github.com)"
          "repository", "Repository", (Shell.repository model |> Option.map StatedFact |> Option.defaultValue UnknownFact)
          "connection", "Connection", connectionClaim model
          "authentication", "Authentication", authenticationClaim state
          "catalog", "Active catalog", catalog
          "catalog-source", "Catalog source", catalogSource
          "storage-contract", "Storage contract", StatedFact storageContract
          "domain-contract", "Domain contract", StatedFact domainContract
          "application-version", "Application version", application
          "deployed-commit", "Deployed commit", commit
          "build-gate", "Build gate", gate
          for label, claim in components do
              "component-" + (label.Split(' ').[0]).ToLowerInvariant(), label, claim
          "runtime", ".NET runtime", StatedFact $".NET {Environment.Version}"
          "data-validation", "Data validation and replay", validationClaim model ]

    /// A claim's value, its status word and its tone. Verified and known are different words (UI-018-O3); an absence
    /// is neutral, never a success colour (UI-016-O17).
    let present (claim: FactClaim) : string * string * States.Tone =
        match claim with
        | VerifiedFact v -> v, "Verified", States.Ok
        | StatedFact v -> v, "Known", States.Ok
        | UnknownFact -> "Not known yet", "Unknown", States.Neutral
        | UnavailableFact why -> why, "Unavailable", States.Neutral
        | FailedFact why -> why, "Failed", States.Blocked

    /// The frozen IF-UI AdministrationView of the same facts.
    let project (build: BuildRead) (state: AppState) : AdministrationView =
        let all = facts build state
        let fact key =
            let _, label, claim = all |> List.find (fun (k, _, _) -> k = key)
            { Label = label
              Status =
                match claim with
                | VerifiedFact v
                | StatedFact v -> FactKnown v
                | UnknownFact -> FactUnknown
                | UnavailableFact _ -> FactUnavailable
                | FailedFact why -> FactFailed why }
        { Provider = fact "provider"
          Repository = fact "repository"
          Connection = fact "connection"
          Catalog = fact "catalog"
          StorageContract = fact "storage-contract"
          ApplicationVersion = fact "application-version"
          DeployedCommit = fact "deployed-commit"
          DataValidation = fact "data-validation"
          Diagnostics = diagnostics build state.Model }

    /// The page's keys for a build record read (integration passes AppState's build read; see the change request).
    let viewWith (build: BuildRead) (state: AppState) : (string * ViewValue) list =
        let notConnected = match state.Model.Connection with Disconnected -> true | _ -> false
        let diagnostics = diagnostics build state.Model
        [ Bind.text "administration.heading" "Administration"
          Bind.text "administration.intro" Language.administrationIntro
          Bind.flag "administration.not-connected" notConnected
          Bind.text "administration.not-connected-text" Language.notConnected
          Bind.flag "administration.diagnostics.empty" diagnostics.IsEmpty
          Bind.items "administration.diagnostics" [ for i, d in List.indexed diagnostics -> [ Cell.text "id" (string i); Cell.text "text" d ] ]
          Bind.items
              "administration.facts"
              [ for key, label, claim in facts build state ->
                    let value, word, tone = present claim
                    // Not being connected needs the user's attention (amber, with its word): it is the one next step.
                    let tone = if key = "connection" && notConnected then States.Attention else tone
                    let word = if key = "connection" && notConnected then "Needs attention" else word
                    [ Cell.text "key" key
                      Cell.text "label" label
                      Cell.text "value" value
                      Cell.text "status" word
                      Cell.text "tone" (States.tone tone) ] ] ]

    /// Until integration carries the build read in AppState (change request CR-T29-BUILD), the build facts are Unknown.
    let view (state: AppState) : (string * ViewValue) list = viewWith None state

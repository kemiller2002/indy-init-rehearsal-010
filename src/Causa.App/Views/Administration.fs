// Owner: ui. Administration (UI-018, UI-020-O2, UI-031): read-only facts about the connection and this build. Every
// fact says whether it is known; nothing unverified is shown as verified. Keys start with "administration.".
namespace Causa.App.Views

open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Administration =
    /// The storage contract this build reads and writes (CAUSA-STORAGE-RELEASE-v1.0.0).
    [<Literal>]
    let StorageContract = "causa-storage v1.0.0"

    let private connectionFact (model: Model) =
        match model.Connection with
        | Disconnected -> FactKnown "Not connected"
        | Connecting _ -> FactKnown "Connecting"
        | Connected true -> FactKnown "Connected, read and write"
        | Connected false -> FactKnown "Connected, read only"
        | CredentialRefused -> FactFailed "The token was refused"
        | Reauthenticating -> FactFailed "The token needs re-entering"

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

    /// UI-031-O2: the outcome of every investigation load, as diagnostics (never the credential).
    let diagnostics (model: Model) : string list =
        [ for KeyValue(r, load) in model.Loads do
              let (Causa.Domain.InvestigationId id) = r.Id
              let (Workspace w) = r.Address.Workspace
              match load with
              | LoadFailed(Unverifiable failures) ->
                  for f in failures do
                      yield $"{w}/{id:D}: {failureCode f}"
              | LoadFailed(ProviderFailed e) -> yield $"{w}/{id:D}: PROVIDER %A{e}"
              | _ -> () ]

    /// UI-023-O3: data validation is a technical fact, kept apart from any investigation-level verification.
    let private validationFact (model: Model) =
        let loaded = model.Loads |> Map.filter (fun _ l -> match l with Loaded _ -> true | _ -> false) |> Map.count
        let refused = model.Loads |> Map.filter (fun _ l -> match l with LoadFailed(Unverifiable _) -> true | _ -> false) |> Map.count
        match loaded, refused with
        | 0, 0 -> FactUnknown
        | l, 0 -> FactKnown $"{l} investigation history(ies) checked on opening; none refused"
        | l, r -> FactFailed $"{r} investigation history(ies) refused on opening; {l} checked"

    /// The facts, from the connection context and this build only.
    let project (state: AppState) : AdministrationView =
        let model = state.Model
        { Provider = { Label = "Provider"; Status = FactKnown "GitHub (api.github.com)" }
          Repository =
            { Label = "Repository"
              Status = Shell.repository model |> Option.map FactKnown |> Option.defaultValue FactUnknown }
          Connection = { Label = "Connection"; Status = connectionFact model }
          Catalog = { Label = "Catalog"; Status = if model.Listings.IsEmpty then FactUnknown else FactKnown(sprintf "%d workspace(s) listed" model.Listings.Count) }
          StorageContract = { Label = "Storage contract"; Status = FactKnown StorageContract }
          ApplicationVersion = { Label = "Application version"; Status = FactUnavailable }
          DeployedCommit = { Label = "Deployed commit"; Status = FactUnavailable }
          DataValidation = { Label = "Data validation"; Status = validationFact model }
          Diagnostics = diagnostics model }

    let facts (v: AdministrationView) =
        [ "provider", v.Provider
          "repository", v.Repository
          "connection", v.Connection
          "catalog", v.Catalog
          "storage-contract", v.StorageContract
          "application-version", v.ApplicationVersion
          "deployed-commit", v.DeployedCommit
          "data-validation", v.DataValidation ]

    let view (state: AppState) : (string * ViewValue) list =
        let v = project state
        let notConnected = match state.Model.Connection with Disconnected -> true | _ -> false
        [ Bind.text "administration.heading" "Administration"
          Bind.text "administration.intro" Language.administrationIntro
          Bind.flag "administration.not-connected" notConnected
          Bind.text "administration.not-connected-text" Language.notConnected
          Bind.flag "administration.diagnostics.empty" v.Diagnostics.IsEmpty
          Bind.items "administration.diagnostics" [ for i, d in List.indexed v.Diagnostics -> [ Cell.text "id" (string i); Cell.text "text" d ] ]
          Bind.items
              "administration.facts"
              [ for key, fact in facts v ->
                    let value, word, tone = States.fact fact.Status
                    // Not being connected needs the user's attention (amber, with its word): it is the one next step.
                    let tone = if key = "connection" && notConnected then States.Attention else tone
                    let word = if key = "connection" && notConnected then "Needs attention" else word
                    [ Cell.text "key" key
                      Cell.text "label" fact.Label
                      Cell.text "value" value
                      Cell.text "status" word
                      Cell.text "tone" (States.tone tone) ] ] ]

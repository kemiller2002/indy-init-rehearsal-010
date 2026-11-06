// Owner: ui. Administration (UI-018, UI-020-O2, UI-031): read-only facts about the connection and this build. Every
// fact says whether it is known; nothing unverified is shown as verified. Keys start with "administration.".
namespace Causa.App.Views

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
          DataValidation = { Label = "Data validation"; Status = FactUnknown }
          Diagnostics = [] }

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

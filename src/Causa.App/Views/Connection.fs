// Owner: ui (written by the integration lane acting as ui for T17). The data repository region (connection.*): the
// Connect form while disconnected or refused, and an honest summary of the connection (EXT-DATA-002..005, UI-010).
// The token is never a view value.
namespace Causa.App.Views

open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Connection =
    let summary (state: AppState) : string =
        let model = state.Model
        let repo =
            model.Context
            |> Option.map (fun c -> sprintf "%s/%s on branch %s" c.Repository.Owner c.Repository.Name c.Repository.Branch)
            |> Option.defaultValue ""
        match model.Connection with
        | Disconnected -> "Not connected. Connect a data repository to list and save investigations."
        | Connecting _ -> "Connecting to " + repo + "…"
        | Connected true -> "Connected to " + repo + " with read and write access."
        | Connected false -> "Connected to " + repo + " with read-only access: investigations can be opened but not saved."
        | CredentialRefused -> "GitHub no longer accepts the token for " + repo + ". Enter a new token to reconnect; nothing in this tab is lost."
        | Reauthenticating -> "Checking the new token for " + repo + "…"

    let view (state: AppState) : (string * ViewValue) list =
        let model = state.Model
        let form =
            match model.Connection with
            | Disconnected
            | CredentialRefused -> true
            | _ -> false
        let busy =
            match model.Connection with
            | Connecting _
            | Reauthenticating -> true
            | _ -> false
        [ Bind.text "connection.summary" (summary state)
          Bind.flag "connection.form" form
          Bind.flag "connection.busy" busy
          Bind.text ConnectForm.Owner (Drafts.get state ConnectForm.Owner)
          Bind.text ConnectForm.Repository (Drafts.get state ConnectForm.Repository)
          Bind.text ConnectForm.Branch (Drafts.get state ConnectForm.Branch) ]

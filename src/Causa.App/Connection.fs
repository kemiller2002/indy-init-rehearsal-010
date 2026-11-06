// Owner: ui (written by the integration lane acting as ui for T17). SM-CONNECTION and SM-NETWORK
// (requirements-compiled/v4.1.5/state-machine-contracts.json) as pure transition functions, and the Connect form's
// field names. Every (state, event) pair the contract calls illegal fails closed: the state is unchanged and the fault
// names the pair.
namespace Causa.App

open Causa.Persistence

/// The fail-closed answer to an illegal (state, event) pair: "SM-CONNECTION/<state>/<event>".
type MachineFault = { Machine: string; State: string; Input: string }

[<RequireQualifiedAccess>]
module ConnectionMachine =
    let stateName (s: ConnectionState) =
        match s with
        | Disconnected -> "Disconnected"
        | Connecting _ -> "Connecting"
        | Connected _ -> "Connected"
        | CredentialRefused -> "CredentialRefused"
        | Reauthenticating -> "Reauthenticating"

    let inputName (i: ConnectionInput) =
        match i with
        | ConnectionInput.ConnectSubmitted _ -> "ConnectSubmitted"
        | ConnectionInput.ConnectSucceeded _ -> "ConnectSucceeded"
        | ConnectionInput.ConnectRefused _ -> "ConnectRefused"
        | ConnectionInput.AuthFailed -> "AuthFailed"
        | ConnectionInput.ReconnectSubmitted _ -> "ReconnectSubmitted"
        | ConnectionInput.DisconnectConfirmed -> "DisconnectConfirmed"

    let fault (s: ConnectionState) (i: ConnectionInput) =
        { Machine = "SM-CONNECTION"; State = stateName s; Input = inputName i }

    /// The fault's contract identity, e.g. "SM-CONNECTION/Disconnected/ConnectSucceeded".
    let faultId (f: MachineFault) = sprintf "%s/%s/%s" f.Machine f.State f.Input

    /// Guard of T-N01 and T-N05: the credential is not blank. The credential stays opaque: only the header persistence
    /// would send is inspected, never kept.
    let isBlank (credential: Credential.T) =
        let _, header = Credential.authorization credential
        header.Substring("Bearer ".Length).Trim() = ""

    /// One SM-CONNECTION event. `request` is the identity the Connect read will carry (T-N01: Connecting names it).
    /// A guard that does not hold is a fault as well: nothing changes.
    let step (request: RequestId) (state: ConnectionState) (input: ConnectionInput) : Result<ConnectionState, MachineFault> =
        match state, input with
        // T-N01: the credential is kept in memory only and sent only in the Authorization header.
        | Disconnected, ConnectionInput.ConnectSubmitted(_, credential) when not (isBlank credential) -> Ok(Connecting request)
        // T-N02: the permissions GitHub states; write is never inferred from a successful read.
        | Connecting _, ConnectionInput.ConnectSucceeded canWrite -> Ok(Connected canWrite)
        // T-N03: the cause is stated by the caller; no secret in any message.
        | Connecting _, ConnectionInput.ConnectRefused _ -> Ok Disconnected
        // T-N04: everything loaded and unsaved is kept.
        | Connected _, ConnectionInput.AuthFailed -> Ok CredentialRefused
        // T-N08, T-N09: confirmation was asked by the view before this event is sent.
        | Connected _, ConnectionInput.DisconnectConfirmed
        | CredentialRefused, ConnectionInput.DisconnectConfirmed -> Ok Disconnected
        // T-N05: the new credential is checked for the same repository.
        | CredentialRefused, ConnectionInput.ReconnectSubmitted credential when not (isBlank credential) -> Ok Reauthenticating
        // T-N06: same repository (ReconnectSubmitted carries no repository, so it cannot name another).
        | Reauthenticating, ConnectionInput.ConnectSucceeded canWrite -> Ok(Connected canWrite)
        // T-N07: everything stays in memory.
        | Reauthenticating, ConnectionInput.ConnectRefused _ -> Ok CredentialRefused
        | _ -> Error(fault state input)

[<RequireQualifiedAccess>]
module NetworkMachine =
    let stateName (s: NetworkState) =
        match s with
        | NetworkState.Online -> "Online"
        | NetworkState.Offline -> "Offline"

    let inputName (i: NetworkInput) =
        match i with
        | NetworkInput.OfflineObserved -> "OfflineObserved"
        | NetworkInput.OnlineObserved -> "OnlineObserved"

    /// T-W01 Online -> Offline, T-W02 Offline -> Online; the other two pairs fail closed.
    let step (state: NetworkState) (input: NetworkInput) : Result<NetworkState, MachineFault> =
        match state, input with
        | NetworkState.Online, NetworkInput.OfflineObserved -> Ok NetworkState.Offline
        | NetworkState.Offline, NetworkInput.OnlineObserved -> Ok NetworkState.Online
        | _ -> Error { Machine = "SM-NETWORK"; State = stateName state; Input = inputName input }

/// The Connect form (EXT-DATA-002, EXT-DATA-003, EXT-DATA-004): its event names, which are also its draft names.
/// The token field's last segment is `token`, so its value becomes the opaque credential and never a draft.
[<RequireQualifiedAccess>]
module ConnectForm =
    [<Literal>]
    let Owner = "connection.owner"

    [<Literal>]
    let Repository = "connection.repository"

    [<Literal>]
    let Branch = "connection.branch"

    [<Literal>]
    let Token = "connection.token"

    [<Literal>]
    let Connect = "connection.connect"

    [<Literal>]
    let Disconnect = "connection.disconnect"

    /// Set while the user is asked to confirm a disconnect that would drop unsaved changes.
    [<Literal>]
    let ConfirmDisconnect = "connection.confirm-disconnect"

    [<Literal>]
    let KeepConnection = "connection.keep"

    /// The branch used when none is entered.
    let defaultBranch = "main"

    /// The repository the form names, when owner and repository are entered.
    let repository (owner: string) (name: string) (branch: string) : Repository option =
        let owner, name, branch = owner.Trim(), name.Trim(), branch.Trim()
        if owner = "" || name = "" then None
        else Some { Owner = owner; Name = name; Branch = (if branch = "" then defaultBranch else branch) }

/// HELD until the owner records ODR-COMP-P13 / ODR-R10-36. The test data connection the deploy build injected
/// (./deploy-connection.json, written by the deploy build (CAUSA_DEPLOY_CONNECTION=1) from four Actions secrets; same origin, read
/// once after the build record). Every other build writes the file without a connection, and the Connect form is
/// shown exactly as before. The key becomes the opaque credential at once and is never kept as text.
[<RequireQualifiedAccess>]
module DeployedConnection =
    /// The correlation id of the read; never an http-<n> id, so it never reaches the GitHub machines.
    [<Literal>]
    let Correlation = "deploy-connection"

    [<Literal>]
    let Url = "./deploy-connection.json"

    [<Literal>]
    let Schema = "causa.deploy-connection/1"

    /// The repository and credential the file names, when it names one completely; otherwise None.
    let parse (text: string) : (Repository * Credential.T) option =
        try
            use doc = System.Text.Json.JsonDocument.Parse text
            let root = doc.RootElement
            let str (e: System.Text.Json.JsonElement) (name: string) =
                match e.TryGetProperty name with
                | true, v when v.ValueKind = System.Text.Json.JsonValueKind.String -> v.GetString() |> Option.ofObj |> Option.map (fun s -> s.Trim()) |> Option.filter (fun s -> s <> "")
                | _ -> None
            match root.ValueKind, (if root.ValueKind = System.Text.Json.JsonValueKind.Object then str root "schema" else None) with
            | System.Text.Json.JsonValueKind.Object, Some Schema ->
                match root.TryGetProperty "connection" with
                | true, c when c.ValueKind = System.Text.Json.JsonValueKind.Object ->
                    match str c "owner", str c "repository", str c "branch", str c "key" with
                    | Some owner, Some name, Some branch, Some key ->
                        ConnectForm.repository owner name branch |> Option.map (fun repo -> repo, Credential.create key)
                    | _ -> None
                | _ -> None
            | _ -> None
        with _ -> None

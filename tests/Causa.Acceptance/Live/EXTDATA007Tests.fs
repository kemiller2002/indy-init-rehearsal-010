// Owner: integration. EXT-DATA-007: the interface tells disconnected, connecting, authorized-empty, loading,
// incompatible, access-denied, offline and conflict apart; a supplied token is never shown as a connection.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport
open Causa.App
open Causa.App.Ui
open Causa.Ui.Contracts
open Causa.App.Views

module EXTDATA007Tests =
    let private stateOf (model: Model) (hash: string) =
        let s, _ = Update.init hash
        { s with Model = { model with Route = Routes.route hash }; Route = UiRoutes.parse hash }

    let private text (state: AppState) (key: string) =
        match (View.render state |> Map.ofList)[key] with
        | BindingValue.Text t -> t
        | other -> failwithf "%s is %A" key other

    let o1 (create: string -> GitHubProvider) =
        let p = create "extdata007-o1"
        let t = AppDriver.tab p
        let disconnected = text (stateOf t.Model "#/investigations") "connection.summary"
        // Connecting: the state between the submit and GitHub's answer.
        let connecting, _ = Update.stepAt DateTimeOffset.UtcNow (Connection(ConnectionInput.ConnectSubmitted(p.Connection.Repository, p.Connection.Credential))) t.Model
        let connectingText = text (stateOf connecting "#/investigations") "connection.summary"
        AppDriver.connect t |> ignore
        let connected = text (stateOf t.Model "#/investigations") "connection.summary"
        // Authorized and empty: a fresh branch whose working copy holds nothing yet.
        let emptyState = stateOf t.Model "#/investigations"
        Assert.Equal(BindingValue.Flag true, (View.render emptyState |> Map.ofList)["investigations.empty"])
        let empty = text emptyState "investigations.emptyText"
        Assert.Contains("no investigations", empty)
        // Loading, then incompatible: a forged investigation read and refused.
        let forged = Guid.NewGuid()
        let dir = sprintf "workspaces/demo/causa-app-r10/investigations/%s" (forged.ToString("D"))
        AppDriver.commitFiles p [ dir + "/manifest.json", "{}"; dir + "/events.ndjson", "{}\n"; dir + "/snapshot.json", "{}" ] |> ignore
        let reader = AppDriver.tab p
        AppDriver.connect reader |> ignore
        let route = sprintf "#/investigation/%s/%s/overview" (Uri.EscapeDataString "workspaces/demo/causa-app-r10") (forged.ToString("D"))
        let r = reader.Model.Loads |> Map.findKey (fun k _ -> k.Id = Causa.Domain.InvestigationId forged)
        let loading = stateOf { reader.Model with Loads = Map.add r (Loading(RequestId 1L, LoadHead)) reader.Model.Loads } route
        let incompatible = stateOf reader.Model route
        let loadingText = text loading "investigation.absence"
        let incompatibleText = text incompatible "investigation.absence"
        // Access denied: GitHub refuses the credential.
        let denied = AppDriver.tab { p with Connection = { p.Connection with Credential = Credential.create "not-a-valid-token" } }
        AppDriver.connect denied |> ignore
        let deniedText = AppDriver.announcements denied |> String.concat " "
        // Offline, and conflict, as the shell and the save state say them.
        let offline, _ = Update.stepAt DateTimeOffset.UtcNow (Network NetworkInput.OfflineObserved) t.Model
        Assert.Equal(BindingValue.Flag true, (View.render (stateOf offline "#/investigations") |> Map.ofList)["app.offline"])
        Assert.Equal("Conflict", SaveIndicator.label Causa.Ui.Contracts.ConflictKind)
        let all = [ disconnected; connectingText; connected; empty; loadingText; incompatibleText; deniedText ]
        Assert.Equal(all.Length, (List.distinct all).Length)
        Assert.All(all, fun s -> Assert.False(String.IsNullOrWhiteSpace s))
        Assert.Contains("Not connected", deniedText)

    let o2 (create: string -> GitHubProvider) =
        let p = create "extdata007-o2"
        // A token was supplied, but GitHub has not answered: not connected.
        let t = AppDriver.tab p
        let submitted, _ = Update.stepAt DateTimeOffset.UtcNow (Connection(ConnectionInput.ConnectSubmitted(p.Connection.Repository, p.Connection.Credential))) t.Model
        Assert.False(match submitted.Connection with Connected _ -> true | _ -> false)
        Assert.Equal("Connecting…", Shell.connection submitted)
        // A token GitHub refuses: back to not connected, with the cause, never "Connected".
        let bad = AppDriver.tab { p with Connection = { p.Connection with Credential = Credential.create "not-a-valid-token" } }
        AppDriver.connect bad |> ignore
        Assert.Equal(Disconnected, bad.Model.Connection)
        Assert.Equal("Not connected", Shell.connection bad.Model)
        Assert.True(bad.Model.Listings.IsEmpty)

    [<Fact>]
    let ``EXT-DATA-007-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-007-O2`` () = o2 AppDriver.live

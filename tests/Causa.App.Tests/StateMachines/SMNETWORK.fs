// Owner: ui (written by the integration lane acting as ui for T17). SM-NETWORK transition tests
// (state-machine-contracts.json) through the engine: each session hears the change; nothing is sent automatically.
namespace Causa.App.Tests.StateMachines

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.App

module SMNETWORK =
    let now = DateTimeOffset(2026, 11, 6, 18, 0, 0, TimeSpan.Zero)
    let step msg model = Update.stepAt now msg model
    let https (effects: HostEffect list) = effects |> List.choose (function Http r -> Some r | _ -> None)

    /// A connected model holding one new investigation's session (Unsaved) in the working copy.
    let withSession () =
        let address = (WorkspaceAddress.workingCopy [] "r10").Value
        let writable = (Writable.ofAddress address).Value
        let id = InvestigationId(Guid "3f2504e0-4f89-41d3-9a0c-0305e82c3301")
        let title = (NonEmptyText.tryCreate "Pump trips").Value
        let start =
            { CommandId = Guid "0d1c4f39-9a35-4f3a-8f0b-6e0c9f0b7a11"
              Investigation = id
              ExpectedVersion = None
              Command = Core(StartInvestigation { Title = title; ObservationId = ObservationId(Guid "6b2e3b0e-37c5-4b0a-9a8f-2d4a1c5e9f10"); Original = title }) }
        let session = Persistence.createInvestigation writable (CommitOid(String('a', 40))) start true
        let r = { Address = address; Id = id }
        { Update.initialModel (Route "#/investigations") with
            Connection = Connected true
            Context = Some { Repository = { Owner = "o"; Name = "r"; Branch = "main" }; Credential = Credential.create "t" }
            Sessions = Map.ofList [ r, session ] },
        r

    [<Fact>]
    let ``T-W01`` () =
        let model, r = withSession ()
        Assert.Equal(SaveState.Unsaved, model.Sessions[r].State)
        let next, effects = step (Network NetworkInput.OfflineObserved) model
        Assert.Equal(NetworkState.Offline, next.Network)
        // SM-SAVE WentOffline for each investigation.
        Assert.Equal(SaveState.Offline, next.Sessions[r].State)
        Assert.Equal<CommandEnvelope list>(model.Sessions[r].Unsaved, next.Sessions[r].Unsaved)
        Assert.Empty(https effects)
        // The browser's connectivity signal is the same event.
        let viaBrowser, _ = step (Browser(ConnectivityChanged false)) model
        Assert.Equal(NetworkState.Offline, viaBrowser.Network)

    [<Fact>]
    let ``T-W02`` () =
        let model, r = withSession ()
        let offline, _ = step (Network NetworkInput.OfflineObserved) model
        let next, effects = step (Network NetworkInput.OnlineObserved) offline
        Assert.Equal(NetworkState.Online, next.Network)
        Assert.Equal(SaveState.Unsaved, next.Sessions[r].State)
        // No save is sent automatically.
        Assert.Empty(https effects)

    [<Fact>]
    let EveryIllegalPairFailsClosed () =
        for state, input, name in
            [ NetworkState.Online, NetworkInput.OnlineObserved, "SM-NETWORK/Online/OnlineObserved"
              NetworkState.Offline, NetworkInput.OfflineObserved, "SM-NETWORK/Offline/OfflineObserved" ] do
            match NetworkMachine.step state input with
            | Ok next -> failwithf "%s was accepted (-> %A)" name next
            | Error f -> Assert.Equal(name, ConnectionMachine.faultId f)
            let model, r = withSession ()
            let model = { model with Network = state }
            let next, effects = step (Network input) model
            Assert.Equal(state, next.Network)
            Assert.Equal(model.Sessions[r], next.Sessions[r])
            Assert.Empty(https effects)

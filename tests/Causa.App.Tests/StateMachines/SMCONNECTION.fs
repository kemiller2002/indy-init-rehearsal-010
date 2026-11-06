// Owner: ui (written by the integration lane acting as ui for T17). SM-CONNECTION transition tests
// (state-machine-contracts.json): every legal transition through the engine (Update.stepAt) with its effects, and
// every illegal pair failing closed.
namespace Causa.App.Tests.StateMachines

open System
open Xunit
open Causa.Persistence
open Causa.App

module SMCONNECTION =
    let now = DateTimeOffset(2026, 11, 6, 18, 0, 0, TimeSpan.Zero)
    let repo: Repository = { Owner = "harness"; Name = "causa-test"; Branch = "main" }
    let secret = "s3cret-token-value"
    let credential = Credential.create secret
    let step msg model = Update.stepAt now msg model
    let fresh = Update.initialModel (Route "#/investigations")

    let https (effects: HostEffect list) = effects |> List.choose (function Http r -> Some r | _ -> None)
    let said (effects: HostEffect list) = effects |> List.choose (function LiveAnnounce(_, t) -> Some t | _ -> None)

    let connecting () =
        let model, effects = step (Connection(ConnectionInput.ConnectSubmitted(repo, credential))) fresh
        model, (https effects).Head

    let answer (request: HttpRequest) status body model = step (Browser(HttpCompleted(Answered(request.Id, status, [], body)))) model

    let canPush = """{"full_name":"harness/causa-test","permissions":{"pull":true,"push":true}}"""
    let readOnly = """{"full_name":"harness/causa-test","permissions":{"pull":true,"push":false}}"""

    let connected () =
        let model, request = connecting ()
        fst (answer request 200 canPush model)

    let refused () = fst (step (Connection ConnectionInput.AuthFailed) (connected ()))

    let reauthenticating () =
        let model, effects = step (Connection(ConnectionInput.ReconnectSubmitted(Credential.create "new-token"))) (refused ())
        model, (https effects).Head

    [<Fact>]
    let ``T-N01`` () =
        let model, effects = step (Connection(ConnectionInput.ConnectSubmitted(repo, credential))) fresh
        let request = (https effects) |> List.exactlyOne
        Assert.Equal(Connecting request.Id, model.Connection)
        // The credential is sent only in the Authorization header, to the GitHub API over HTTPS.
        Assert.StartsWith("https://api.github.com/", request.Url)
        Assert.Contains(("Authorization", "Bearer " + secret), request.Headers)
        Assert.DoesNotContain(secret, request.Url)
        Assert.True(request.Body.IsNone || not (request.Body.Value.Contains secret))
        Assert.Equal("<credential>", string model.Context.Value.Credential)
        // Guard: a blank credential is not submitted.
        let blank, none = step (Connection(ConnectionInput.ConnectSubmitted(repo, Credential.create "  "))) fresh
        Assert.Equal(Disconnected, blank.Connection)
        Assert.Empty(https none)

    [<Fact>]
    let ``T-N02`` () =
        let model, request = connecting ()
        let writable, effects = answer request 200 canPush model
        Assert.Equal(Connected true, writable.Connection)
        // Connected lists the catalog and the working copy.
        Assert.Equal(2, (https effects).Length)
        // Write access only from the permissions GitHub states, never from a successful read.
        let model, request = connecting ()
        let readable, _ = answer request 200 readOnly model
        Assert.Equal(Connected false, readable.Connection)
        let model, request = connecting ()
        let unstated, _ = answer request 200 """{"full_name":"harness/causa-test"}""" model
        Assert.NotEqual(Connected true, unstated.Connection)

    [<Fact>]
    let ``T-N03`` () =
        let model, request = connecting ()
        let next, effects = answer request 401 """{"message":"Bad credentials"}""" model
        Assert.Equal(Disconnected, next.Connection)
        Assert.True(next.Context.IsNone)
        let messages = said effects
        Assert.NotEmpty(messages)
        Assert.All(messages, fun m -> Assert.DoesNotContain(secret, m))

    [<Fact>]
    let ``T-N04`` () =
        let before = connected ()
        let next, effects = step (Connection ConnectionInput.AuthFailed) before
        Assert.Equal(CredentialRefused, next.Connection)
        // Everything loaded and unsaved is kept.
        Assert.True((before.Sessions = next.Sessions), "Sessions changed")
        Assert.True((before.Loads = next.Loads), "Loads changed")
        Assert.True((before.ListingsInFlight = next.ListingsInFlight), "ListingsInFlight changed")
        Assert.Equal(repo, next.Context.Value.Repository)
        Assert.Empty(https effects)

    [<Fact>]
    let ``T-N05`` () =
        let model, request = reauthenticating ()
        Assert.Equal(Reauthenticating, model.Connection)
        // The new credential is checked for the same repository.
        Assert.Equal("https://api.github.com/repos/harness/causa-test", request.Url)
        Assert.Contains(("Authorization", "Bearer new-token"), request.Headers)
        Assert.Equal(repo, model.Context.Value.Repository)
        let blank, effects = step (Connection(ConnectionInput.ReconnectSubmitted(Credential.create ""))) (refused ())
        Assert.Equal(CredentialRefused, blank.Connection)
        Assert.Empty(https effects)

    [<Fact>]
    let ``T-N06`` () =
        let model, request = reauthenticating ()
        let next, effects = answer request 200 canPush model
        Assert.Equal(Connected true, next.Connection)
        // Everything in the tab is kept: no new listing, the same sessions.
        Assert.True((model.Sessions = next.Sessions), "Sessions changed")
        Assert.True((model.ListingsInFlight = next.ListingsInFlight), "ListingsInFlight changed")
        Assert.Empty(https effects)

    [<Fact>]
    let ``T-N07`` () =
        let model, request = reauthenticating ()
        let next, _ = answer request 401 """{"message":"Bad credentials"}""" model
        Assert.Equal(CredentialRefused, next.Connection)
        Assert.True((model.Sessions = next.Sessions), "Sessions changed")
        Assert.Equal(repo, next.Context.Value.Repository)

    [<Fact>]
    let ``T-N08`` () =
        let next, effects = step (Connection ConnectionInput.DisconnectConfirmed) (connected ())
        Assert.Equal(Disconnected, next.Connection)
        // The credential and the tab's state are dropped.
        Assert.True(next.Context.IsNone)
        Assert.True(next.Sessions.IsEmpty && next.Loads.IsEmpty && next.Listings.IsEmpty && next.ListingsInFlight.IsEmpty)
        Assert.Empty(https effects)

    [<Fact>]
    let ``T-N09`` () =
        let next, _ = step (Connection ConnectionInput.DisconnectConfirmed) (refused ())
        Assert.Equal(Disconnected, next.Connection)
        Assert.True(next.Context.IsNone)
        Assert.True(next.Sessions.IsEmpty && next.Listings.IsEmpty)

    let states: (string * ConnectionState) list =
        [ "Disconnected", Disconnected
          "Connecting", Connecting(RequestId 1L)
          "Connected", Connected true
          "CredentialRefused", CredentialRefused
          "Reauthenticating", Reauthenticating ]

    let inputs: (string * ConnectionInput) list =
        [ "ConnectSubmitted", ConnectionInput.ConnectSubmitted(repo, credential)
          "ConnectSucceeded", ConnectionInput.ConnectSucceeded true
          "ConnectRefused", ConnectionInput.ConnectRefused AuthenticationFailed
          "AuthFailed", ConnectionInput.AuthFailed
          "ReconnectSubmitted", ConnectionInput.ReconnectSubmitted credential
          "DisconnectConfirmed", ConnectionInput.DisconnectConfirmed ]

    let legal =
        set [ "Disconnected", "ConnectSubmitted"; "Connecting", "ConnectSucceeded"; "Connecting", "ConnectRefused"
              "Connected", "AuthFailed"; "Connected", "DisconnectConfirmed"; "CredentialRefused", "ReconnectSubmitted"
              "CredentialRefused", "DisconnectConfirmed"; "Reauthenticating", "ConnectSucceeded"; "Reauthenticating", "ConnectRefused" ]

    [<Fact>]
    let EveryIllegalPairFailsClosed () =
        let illegal =
            [ for (sn, s) in states do
                  for (iname, i) in inputs do
                      if not (legal.Contains(sn, iname)) then yield sn, s, iname, i ]
        Assert.Equal(21, illegal.Length)
        for (sn, s, iname, i) in illegal do
            match ConnectionMachine.step (RequestId 9L) s i with
            | Ok next -> failwithf "SM-CONNECTION/%s/%s was accepted (-> %A)" sn iname next
            | Error f -> Assert.Equal(sprintf "SM-CONNECTION/%s/%s" sn iname, ConnectionMachine.faultId f)
            // Through the engine: the state is unchanged and nothing is sent.
            let model = { fresh with Connection = s; Context = Some { Repository = repo; Credential = credential } }
            let next, effects = step (Connection i) model
            Assert.Equal(s, next.Connection)
            Assert.Equal(model.Context, next.Context)
            Assert.Empty(https effects)

    // ---- T-N04 reached through a save (PERSIST-031, INV-25) --------------------------------------------------------
    // The AuthFailed event must come from somewhere: a save whose credential GitHub refuses (401) while connected. The
    // tab runs against the stateful fake; every request the engine asks for is answered until nothing is in flight.

    let rec private pump (execute: Execute) (model: Model) (pending: HttpRequest list) : Model =
        match pending with
        | [] -> model
        | request :: rest ->
            let model, effects = step (Browser(HttpCompleted(execute request))) model
            pump execute model (rest @ https effects)

    let private run (execute: Execute) (msg: Msg) (model: Model) : Model =
        let model, effects = step msg model
        pump execute model (https effects)

    let private started (execute: Execute) =
        let model = run execute (Connection(ConnectionInput.ConnectSubmitted(repo, credential))) fresh
        let w = model.Listings |> Map.toList |> List.pick (fun (a, _) -> Writable.ofAddress a)
        let id = Causa.Domain.InvestigationId(Guid.NewGuid())
        let text s = (Causa.Domain.NonEmptyText.tryCreate s).Value
        let command: Causa.Domain.CommandEnvelope =
            { CommandId = Guid.NewGuid()
              Investigation = id
              ExpectedVersion = None
              Command =
                Causa.Domain.Core(
                    Causa.Domain.StartInvestigation
                        { Title = text "Refused credential"; ObservationId = Causa.Domain.ObservationId(Guid.NewGuid()); Original = text "kept" }) }
        let model = run execute (CreateRequested(w, command)) model
        model, ({ Address = Writable.address w; Id = id }: InvestigationRef)

    [<Fact>]
    let ``T-N04 a save refused for its credential leaves the connection CredentialRefused`` () =
        let fake =
            Causa.TestSupport.StatefulFake.start (
                Causa.TestSupport.StatefulFake.initial repo.Owner repo.Name repo.Branch [ "README.md", "r"B ] [ secret, Causa.TestSupport.ReadWrite ])
        let model, r = started fake.Execute
        Assert.Equal(Connected true, model.Connection)
        let unsaved = model.Sessions[r]
        fake.Update(
            Causa.TestSupport.StatefulFake.injectFault
                Causa.TestSupport.NextMutation
                (Causa.TestSupport.AnswerWith(401, [ "content-type", "application/json; charset=utf-8" ], Causa.TestSupport.Recordings.badCredentials)))
        let refused = run fake.Execute (SessionInput(r, SaveInput.SaveRequested)) model
        Assert.Equal(SaveState.SaveFailed CredentialNotAccepted, refused.Sessions[r].State)
        Assert.Equal(CredentialRefused, refused.Connection)
        // Everything in the tab is kept: the unsaved change and the repository.
        Assert.Equal<Causa.Domain.CommandEnvelope list>(unsaved.Unsaved, refused.Sessions[r].Unsaved)
        Assert.NotEmpty(refused.Sessions[r].Unsaved)
        Assert.Equal(repo, refused.Context.Value.Repository)
        // T-N05, T-N06: a new token reconnects to the same repository and the change can be saved.
        let back = run fake.Execute (Connection(ConnectionInput.ReconnectSubmitted(Credential.create secret))) refused
        Assert.Equal(Connected true, back.Connection)
        let saved = run fake.Execute (SessionInput(r, SaveInput.SaveRequested)) back
        Assert.True((match saved.Sessions[r].State with SaveState.Saved _ -> true | _ -> false), sprintf "%A" saved.Sessions[r].State)

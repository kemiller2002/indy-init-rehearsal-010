// Owner: integration. EXT-DATA-002: a fresh launch is an empty, disconnected workspace with an explicit Connect data
// repository action; nothing is imported implicitly.
namespace Causa.Acceptance.Live

open System
open System.IO
open Xunit
open Causa.Persistence
open Causa.TestSupport
open Causa.App
open Causa.App.Ui
open Causa.Ui.Contracts

module EXTDATA002Tests =
    let private viewOf (state: AppState) = View.render state |> Map.ofList

    let o1 (create: string -> GitHubProvider) =
        let state, effects = Update.init ""
        let view = viewOf state
        Assert.Equal(BindingValue.Flag true, view["screen.investigations"])
        Assert.Equal(BindingValue.Flag true, view["connection.form"])
        Assert.Equal(BindingValue.Flag false, view["investigations.connected"])
        Assert.Equal(BindingValue.Text "Not connected", view["shell.connection"])
        Assert.Equal(BindingValue.Items [], view["investigations.items"])
        // Only the page's own build record is read at start (same origin, no credential); nothing from GitHub.
        Assert.Equal<Causa.App.Ui.AppEffect list>([ Causa.App.Ui.ReadBuildInfo ], effects)
        // The explicit action, as the page names it.
        let fragment = File.ReadAllText(Repo.at "web/screens/connection.html")
        Assert.Contains(">Connect data repository</button>", fragment)
        // And against the provider: connecting is what lists, and only then (one connect read first).
        let t = AppDriver.tab (create "extdata002-o1")
        AppDriver.connect t |> ignore
        Assert.EndsWith("/repos/" + t.Provider.Connection.Repository.Owner + "/" + t.Provider.Connection.Repository.Name, t.Sent.Head.Url)
        Assert.Equal(Connected true, t.Model.Connection)

    let o2 (create: string -> GitHubProvider) =
        // No implicit demo import: before Connect nothing is requested, listed or loaded, whatever the route.
        for hash in [ ""; "#/investigations"; "#/new"; "#/investigation/workspaces%2Fdemo%2Fhistorical-v1/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview" ] do
            let state, effects = Update.init hash
            // Only the page's own build record is read at start (same origin, no credential); nothing from GitHub.
            Assert.Equal<Causa.App.Ui.AppEffect list>([ Causa.App.Ui.ReadBuildInfo ], effects)
            let model, more = Update.stepAt DateTimeOffset.UtcNow (Navigate(Routes.route hash)) state.Model
            Assert.Empty(more |> List.filter (function Http _ -> true | _ -> false))
            Assert.True(model.Loads.IsEmpty && model.Listings.IsEmpty)
        // After Connect, everything listed comes from the connected repository's answers, never from bundled data.
        let t = AppDriver.tab (create "extdata002-o2")
        AppDriver.connect t |> ignore
        Assert.All(t.Sent, fun r -> Assert.StartsWith("https://api.github.com/", r.Url))
        Assert.NotEmpty(t.Model.Listings)
        let answered = t.Answers |> List.choose (function Answered(_, _, _, body) -> Some body | _ -> None) |> String.concat "\n"
        for KeyValue(_, listing) in t.Model.Listings do
            let (CommitOid c) = listing.ReadAt
            Assert.Contains(c, answered)

    [<Fact>]
    let ``EXT-DATA-002-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-002-O2`` () = o2 AppDriver.live

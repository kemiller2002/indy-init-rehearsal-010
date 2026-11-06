// Owner: integration. EXT-DATA-012: the data repository is configured by the user and separate from the application
// repository; its content is untrusted even when authenticated; data is never served through the Pages assets.
namespace Causa.Acceptance.Live

open System
open System.IO
open Xunit
open Causa.Persistence
open Causa.TestSupport
open Causa.App

module EXTDATA012Tests =
    let o1 (create: string -> GitHubProvider) =
        let p = create "extdata012-o1"
        let repo = p.Connection.Repository
        // The test repository is not the application repository.
        Assert.NotEqual<string>("indy-init-rehearsal-010", repo.Name)
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        Assert.All(t.Sent, fun r -> Assert.True(r.Url = "https://api.github.com/graphql" || r.Url.Contains(sprintf "/repos/%s/%s" repo.Owner repo.Name), r.Url))
        // The application names no data repository of its own: none is configured in what ships.
        for f in Repo.own () |> List.filter (fun f -> f.EndsWith ".fs" || f.EndsWith ".js" || f.EndsWith ".html" || f.EndsWith ".json") do
            Assert.DoesNotContain("causa-acceptance", File.ReadAllText f)
        Assert.True((ConnectForm.repository "" "" "").IsNone, "a repository was assumed without the user naming one")

    let o2 (create: string -> GitHubProvider) =
        // Untrusted even when authenticated: a path outside the investigation's own prefix, or an investigation
        // directory with extra files, is refused.
        let p = create "extdata012-o2"
        let id = Guid.NewGuid().ToString("D")
        let dir = sprintf "workspaces/demo/causa-app-r10/investigations/%s" id
        AppDriver.commitFiles p [ dir + "/manifest.json", "{\"x\":1}"; dir + "/events.ndjson", "\n"; dir + "/snapshot.json", "{}"; dir + "/payload.js", "alert(1)" ] |> ignore
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        let r = t.Model.Loads |> Map.findKey (fun k _ -> let (Causa.Domain.InvestigationId g) = k.Id in g.ToString("D") = id)
        Assert.True(match t.Model.Loads[r] with LoadFailed _ -> true | _ -> false)
        Assert.False(t.Model.Sessions.ContainsKey r)
        // A save never writes outside the investigation's own directory.
        Assert.True(Result.isError (GitHubRequests.checkPrefix (RepoPath dir) (Scenarios.commitRequest (CommitOid(String('a', 40))) [ "catalog/active.json", "{}" ] "m")))

    let o3 (create: string -> GitHubProvider) =
        // No data through public Pages assets: what ships holds no investigation file set, and every data read the
        // application makes goes to the GitHub API with the user's credential, never to its own origin.
        for f in Repo.shipped () do
            let name = Path.GetFileName f
            Assert.False(name = "events.ndjson" || name = "snapshot.json", f)
        let t = AppDriver.tab (create "extdata012-o3")
        AppDriver.connect t |> ignore
        Assert.All(t.Sent, fun r ->
            Assert.StartsWith("https://api.github.com/", r.Url)
            Assert.Contains(r.Headers, fun (k, _) -> k = "Authorization"))

    [<Fact>]
    let ``EXT-DATA-012-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-012-O2`` () = o2 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-012-O3`` () = o3 AppDriver.live

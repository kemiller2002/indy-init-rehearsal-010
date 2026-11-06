// Owner: integration. EXT-DATA-004: the credential is never committed, bundled, logged, put in a URL, shown in an
// error, stored in the browser or written into an investigation's files; it lives in the tab's memory only.
namespace Causa.Acceptance.Live

open System
open System.IO
open Xunit
open Causa.Persistence
open Causa.TestSupport
open Causa.App
open Causa.App.Ui
open Causa.Ui.Contracts

module EXTDATA004Tests =
    /// Connect, start and save one investigation; everything the tab sent, rendered and announced, and the files
    /// GitHub then holds.
    let private session (create: string -> GitHubProvider) label =
        let t = AppDriver.tab (create label)
        AppDriver.connect t |> ignore
        let r = AppDriver.start t "Credential handling" "The token must never leave the Authorization header."
        AppDriver.save t r |> ignore
        Assert.Equal("Saved", AppDriver.saveState t r)
        t, r

    let private secretOf (p: GitHubProvider) = p.Secrets |> List.tryHead |> Option.defaultWith (fun () -> snd (Credential.authorization p.Connection.Credential) |> fun h -> h.Substring 7)

    let o1 (create: string -> GitHubProvider) =
        let t, r = session create "extdata004-o1"
        let secret = secretOf t.Provider
        for request in t.Sent do
            Assert.DoesNotContain(secret, request.Url)
            Assert.False(request.Body |> Option.exists (fun b -> b.Contains secret), "a request body carries the token")
            Assert.Equal(1, request.Headers |> List.filter (fun (_, v) -> v.Contains secret) |> List.length)
            Assert.Contains(("Authorization", "Bearer " + secret), request.Headers)
        // Never in an announcement, the rendered view, or the serialized model.
        Assert.All(AppDriver.announcements t, fun a -> Assert.DoesNotContain(secret, a))
        let state = { (fst (Update.init "")) with Model = t.Model }
        Assert.DoesNotContain(secret, Program.encode (View.render state) [] 0)
        Assert.DoesNotContain(secret, sprintf "%A" t.Model)
        // Never in the investigation's files on GitHub.
        match LiveRun.openInNewTab { Machine = saveMachine; Provider = t.Provider; Clock = SaveKit.clock (); Id = r.Id; Writable = (Writable.ofAddress r.Address).Value; Session = t.Model.Sessions[r]; Sent = []; Events = []; Before = ignore } with
        | Ok v ->
            for bytes in [ v.Bytes.Manifest; v.Bytes.Events; v.Bytes.Snapshot ] do
                Assert.DoesNotContain(secret, Text.Encoding.UTF8.GetString bytes)
        | Error f -> failwithf "%A" f
        // Never stored in the browser, never bundled: no browser storage use and no token literal in what ships.
        Assert.Empty(t.Effects |> List.filter (function Download _ -> true | _ -> false))
        for f in Repo.own () |> List.filter (fun f -> f.EndsWith ".js" || f.EndsWith ".fs" || f.EndsWith ".html") do
            let text = File.ReadAllText f
            Assert.DoesNotContain("localStorage", text)
            Assert.DoesNotContain("sessionStorage", text)
            Assert.False(Text.RegularExpressions.Regex.IsMatch(text, "gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}"), sprintf "%s holds a token literal" f)

    let o2 (create: string -> GitHubProvider) =
        // Least privilege: the token is used only for the GitHub API of the named repository (and GraphQL), never sent
        // anywhere else, and the application asks for no broader access than reading and committing files.
        let t, _ = session create "extdata004-o2"
        let repo = t.Provider.Connection.Repository
        Assert.All(t.Sent, fun r ->
            Assert.True(r.Url = "https://api.github.com/graphql" || r.Url.StartsWith(sprintf "https://api.github.com/repos/%s/%s" repo.Owner repo.Name), r.Url))
        Assert.All(t.Sent, fun r -> Assert.True(r.Method = Get || r.Method = Post, sprintf "%A %s" r.Method r.Url))
        let writes = t.Sent |> List.filter (fun r -> r.Method = Post && not (r.Body |> Option.exists (fun b -> b.StartsWith "{\"query\":\"query")))
        Assert.All(writes, fun r -> Assert.Contains("createCommitOnBranch", r.Body.Value))

    let o3 (create: string -> GitHubProvider) =
        let t, _ = session create "extdata004-o3"
        Assert.True(t.Model.Context.IsSome)
        // A reload is a new engine: no credential, no connection, nothing restored from storage.
        let reloaded, effects = Update.init "#/investigations"
        Assert.True(reloaded.Credential.IsNone && reloaded.Model.Context.IsNone)
        Assert.Equal(Disconnected, reloaded.Model.Connection)
        Assert.Empty(effects)
        Assert.Equal(BindingValue.Flag true, (View.render reloaded |> Map.ofList)["connection.form"])

    let o4 (create: string -> GitHubProvider) =
        // No token by default: the application ships no credential and connects only with the one the user enters.
        let state, _ = Update.init ""
        Assert.True(state.Credential.IsNone)
        let env: HostEnv = { Now = DateTimeOffset.UtcNow; Fresh = [ Guid.NewGuid() ] }
        let named =
            [ ConnectForm.Owner, "o"; ConnectForm.Repository, "r" ]
            |> List.fold (fun s (k, v) -> fst (Update.updateAt env (DraftChanged(k, v)) s)) state
        Assert.Empty(Program.Intents.handle env named { Name = ConnectForm.Connect; Key = None; Value = None } |> Option.defaultValue [] |> List.filter (function EngineMsg _ -> true | _ -> false))
        // The credential the suite uses is the scoped one the environment supplies for the test repository.
        let t = AppDriver.tab (create "extdata004-o4")
        AppDriver.connect t |> ignore
        Assert.Contains(("Authorization", "Bearer " + secretOf t.Provider), t.Sent.Head.Headers)

    [<Fact>]
    let ``EXT-DATA-004-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-004-O2`` () = o2 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-004-O3`` () = o3 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-004-O4`` () = o4 AppDriver.live

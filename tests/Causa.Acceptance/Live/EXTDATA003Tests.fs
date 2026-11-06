// Owner: integration. EXT-DATA-003: the user names the repository and branch; a token alone names no target.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport
open Causa.App
open Causa.App.Ui

module EXTDATA003Tests =
    let env: HostEnv = { Now = DateTimeOffset.UtcNow; Fresh = List.init 4 (fun _ -> Guid.NewGuid()) }
    let connectEvent = { Name = ConnectForm.Connect; Key = None; Value = None }

    let private engineMessages (msgs: AppMsg list) = msgs |> List.choose (function EngineMsg m -> Some m | _ -> None)

    let o1 (create: string -> GitHubProvider) =
        let p = create "extdata003-o1"
        let repo = p.Connection.Repository
        let state, _ = Update.init ""
        // The form's owner, repository and branch, and the token, become exactly the connect target.
        let state =
            [ ConnectForm.Owner, repo.Owner; ConnectForm.Repository, repo.Name; ConnectForm.Branch, repo.Branch ]
            |> List.fold (fun s (k, v) -> fst (Update.updateAt env (DraftChanged(k, v)) s)) state
        let state, _ = Update.updateAt env (CredentialEntered p.Connection.Credential) state
        match Program.Intents.handle env state connectEvent |> Option.map engineMessages with
        | Some [ Connection(ConnectionInput.ConnectSubmitted(target, _)) ] -> Assert.Equal(repo, target)
        | other -> failwithf "Connect did not submit the named repository: %A" other
        // Against the provider every read names that repository, and every commit read is on that branch.
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        let prefix = sprintf "https://api.github.com/repos/%s/%s" repo.Owner repo.Name
        Assert.All(t.Sent |> List.filter (fun r -> r.Method = Get), fun r -> Assert.StartsWith(prefix, r.Url))
        Assert.Contains(t.Sent, fun r -> r.Url.EndsWith("/git/ref/heads/" + Uri.EscapeDataString repo.Branch))

    let o2 (create: string -> GitHubProvider) =
        let p = create "extdata003-o2"
        let state, _ = Update.init ""
        // A token with no repository named sends nothing and says what is missing.
        let state, _ = Update.updateAt env (CredentialEntered p.Connection.Credential) state
        let msgs = Program.Intents.handle env state connectEvent |> Option.defaultValue []
        Assert.Empty(engineMessages msgs)
        Assert.Contains(msgs, fun m -> match m with Announced(_, text) -> text.Contains "repository" | _ -> false)
        // A repository with no token sends nothing either.
        let named, _ = Update.updateAt env (DraftChanged(ConnectForm.Owner, p.Connection.Repository.Owner)) (fst (Update.init ""))
        let named, _ = Update.updateAt env (DraftChanged(ConnectForm.Repository, p.Connection.Repository.Name)) named
        Assert.Empty(Program.Intents.handle env named connectEvent |> Option.defaultValue [] |> engineMessages)
        // The working copy is never inferred from the token: it is derived inside the named repository.
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        let (Workspace w) = (Writable.address (AppDriver.workingCopy t)).Workspace
        Assert.StartsWith("workspaces/demo/causa-app-", w)

    [<Fact>]
    let ``EXT-DATA-003-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-003-O2`` () = o2 AppDriver.live

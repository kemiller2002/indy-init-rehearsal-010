// Owner: integration. EXT-DATA-005: permissions are checked, paths and manifests validated, and only data that passes
// the storage contract and the kernel's replay is loaded; anything else fails closed with a safe diagnostic.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport
open Causa.App

module EXTDATA005Tests =
    let o1 (create: string -> GitHubProvider) =
        let p = create "extdata005-o1"
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        // Write access is what GitHub states for this credential (permissions.push), read from the connect answer.
        let stated =
            match t.Answers.Head with
            | Answered(_, 200, _, body) ->
                use doc = Text.Json.JsonDocument.Parse body
                doc.RootElement.GetProperty("permissions").GetProperty("push").GetBoolean()
            | other -> failwithf "connect answer %A" other
        Assert.Equal(Connected stated, t.Model.Connection)
        // A saved investigation, opened in a fresh tab, is loaded only through the storage contract and replay.
        let r = AppDriver.start t "Permission and validation" "Loaded only after verification."
        AppDriver.save t r |> ignore
        Assert.Equal("Saved", AppDriver.saveState t r)
        let fresh = AppDriver.tab p
        AppDriver.connect fresh |> ignore
        match Map.tryFind r fresh.Model.Loads with
        | Some(Loaded v) ->
            Assert.True(Result.isOk (StorageReader.verify Kernel.domain (Some r.Id) v.Address v.ReadAt v.Bytes))
            Assert.True(Result.isOk (Kernel.replay (List.rev v.Investigation.History)))
        | other -> failwithf "the saved investigation did not load: %A" other

    let o2 (create: string -> GitHubProvider) =
        let p = create "extdata005-o2"
        let secret = p.Secrets |> List.tryHead |> Option.defaultValue "<none>"
        // A forged investigation (a working-copy directory whose files are not causa-storage) and a forged catalog
        // pointer are written by another party.
        let forged = Guid.NewGuid().ToString("D")
        let dir = sprintf "workspaces/demo/causa-app-r10/investigations/%s" forged
        AppDriver.commitFiles p
            [ dir + "/manifest.json", """{"schema":"forged","investigationId":"not-a-uuid"}"""
              dir + "/events.ndjson", "{\"eventType\":\"InvestigationStarted\"}\n"
              dir + "/snapshot.json", "{}"
              "catalog/active.json", """{"workspace":"../../etc","manifest":"catalog/../../etc/manifest.json"}""" ]
        |> ignore
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        // The forged investigation fails closed: listed by its identity, never loaded, no save session.
        let r = t.Model.Listings |> Map.toList |> List.collect (fun (_, l) -> l.Investigations) |> List.find (fun i -> let (InvestigationId g) = i.Ref.Id in g.ToString("D") = forged)
        match t.Model.Loads[r.Ref] with
        | LoadFailed(Unverifiable failures) -> Assert.NotEmpty(failures)
        | other -> failwithf "the forged investigation was not refused: %A" other
        Assert.False(t.Model.Sessions.ContainsKey r.Ref)
        // The forged catalog pointer lists nothing.
        Assert.DoesNotContain(t.Model.Listings |> Map.toList, fun (a, _) -> a.Role = WorkspaceRole.Catalog)
        // Diagnostics are safe: no credential in anything said.
        Assert.All(AppDriver.announcements t, fun a -> Assert.DoesNotContain(secret, a))
        Assert.Contains(AppDriver.announcements t, fun a -> a.Contains "catalog")

    [<Fact>]
    let ``EXT-DATA-005-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-005-O2`` () = o2 AppDriver.live

// Owner: integration. EXT-DATA-006: connecting and reading write nothing; creating is an explicit action and a write
// is one commit conditional on the head the tab read, never over unread history.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport
open Causa.App

module EXTDATA006Tests =
    let o1 (create: string -> GitHubProvider) =
        let p = create "extdata006-o1"
        // Something to read: one investigation saved by another tab.
        let writer = AppDriver.tab p
        AppDriver.connect writer |> ignore
        let r = AppDriver.start writer "Read only" "Connecting and reading never write."
        AppDriver.save writer r |> ignore
        let before = AppDriver.head p
        let reader = AppDriver.tab p
        AppDriver.connect reader |> ignore
        AppDriver.step reader (Navigate(Route(Causa.App.Ui.UiRoutes.href (Causa.Ui.Contracts.WorkspaceRoute({ Workspace = (let (Workspace w) = r.Address.Workspace in w); Role = WorkspaceRole.WorkingCopy; Id = r.Id }, Causa.Ui.Contracts.Overview))))) |> ignore
        Assert.True(match reader.Model.Loads[r] with Loaded _ -> true | _ -> false)
        Assert.Empty(AppDriver.mutations reader)
        Assert.Empty(reader.Sent |> List.filter (fun q -> q.Method <> Get && not (q.Body |> Option.exists (fun b -> b.StartsWith "{\"query\":\"query"))))
        Assert.Equal(before, AppDriver.head p)

    let o2 (create: string -> GitHubProvider) =
        let p = create "extdata006-o2"
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        let before = AppDriver.head p
        // Creating is the explicit CreateRequested; it writes nothing until the user saves.
        let r = AppDriver.start t "Explicit creation" "Created only when asked; written only on Save."
        Assert.Equal("Unsaved", AppDriver.saveState t r)
        Assert.Empty(AppDriver.mutations t)
        Assert.Equal(before, AppDriver.head p)
        // The save is one commit, conditional on the head the tab read.
        AppDriver.save t r |> ignore
        let mutation = AppDriver.mutations t |> List.exactlyOne
        let (CommitOid expected) = before
        Assert.Contains(sprintf "\"expectedHeadOid\":\"%s\"" expected, mutation.Body.Value)
        Assert.Equal("Saved", AppDriver.saveState t r)
        // Another writer moves the branch: the next save never overwrites what this tab has not read.
        let other = AppDriver.commitFiles p [ "workspaces/demo/other-writer.txt", "another writer" ]
        AppDriver.step t (DomainCommand { CommandId = Guid.NewGuid(); Investigation = r.Id; ExpectedVersion = None; Command = Causa.Domain.Core(Causa.Domain.RecordObservation { ObservationId = Causa.Domain.ObservationId(Guid.NewGuid()); Original = AppDriver.text "after the other writer"; ObservedAt = None }) }) |> ignore
        AppDriver.save t r |> ignore
        Assert.True(Scenarios.reachable p other |> Probes.ok "reachable", "the other writer's commit was overwritten")
        // The unrelated commit is rebased over (PERSIST stale-head handling), never discarded.
        Assert.Contains(AppDriver.saveState t r, [ "Saved"; "Conflict" ])

    [<Fact>]
    let ``EXT-DATA-006-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-006-O2`` () = o2 AppDriver.live

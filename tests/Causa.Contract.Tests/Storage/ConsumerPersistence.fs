// Owner: persistence. IF41-STORAGE consumed by persistence: every case of the active catalog verifies and replays.
namespace Causa.Contract.Tests.Storage

open System
open System.Text
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module ConsumerPersistence =
    [<Fact>]
    let CatalogCasesVerifyAndReplay () =
        let files = Datasets.catalog.Value
        let manifest = Canonical.parse (Encoding.UTF8.GetString(files["catalog/demonstration-v1/manifest.json"]).TrimEnd('\n')) |> Result.defaultWith failwith
        let cases = match Canonical.field "cases" manifest with Some(Canonical.JArray xs) -> xs | _ -> []
        Assert.Equal(4, cases.Length)
        let workspace = Workspace "workspaces/demo/demonstration-v1"
        for c in cases do
            let path = Canonical.str "path" c |> Option.get
            let id = Canonical.str "investigationId" c |> Option.get
            let fs = { Manifest = files[path + "/manifest.json"]; Events = files[path + "/events.ndjson"]; Snapshot = files[path + "/snapshot.json"] }
            match StorageReader.verify Kernel.domain (Some(InvestigationId(Guid id))) (WorkspaceAddress.catalog workspace) (CommitOid "c") fs with
            | Error f -> failwithf "%s: %A" id f
            | Ok v ->
                Assert.Equal(AggregateVersion(Canonical.integer "expectedVersion" c |> Option.get), v.Investigation.Version)
                Assert.Equal(Canonical.str "eventsSha256" c, Some(Canonical.sha256Hex fs.Events))
                Assert.Equal(Canonical.str "snapshotSha256" c, Some(Canonical.sha256Hex fs.Snapshot.[.. fs.Snapshot.Length - 2]))
                Assert.Equal(Canonical.str "manifestSha256" c, Some(Canonical.sha256Hex fs.Manifest.[.. fs.Manifest.Length - 2]))
                Assert.Equal(Ok v.Investigation, Kernel.domain.Replay v.History)
                Assert.Equal(WorkspaceRole.Catalog, v.Address.Role)

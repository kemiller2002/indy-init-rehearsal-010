// Owner: persistence. IF41-STORAGE producer tests: the causa-storage v1.0.0 codec against the frozen dataset.
namespace Causa.Contract.Tests.Storage

open System
open System.Text
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module Producer =
    let private kernel = Kernel.domain
    let private codec = StorageReader.codec kernel

    let fixtureSets () =
        let files = Datasets.fixtures.Value
        files
        |> Map.toList
        |> List.choose (fun (p, _) ->
            if p.StartsWith "fixtures/v1.0.0/investigations/" && p.EndsWith "/manifest.json" then Some(p.Substring(0, p.Length - "/manifest.json".Length))
            else None)
        |> List.map (fun dir ->
            let id = dir.Split('/') |> Array.last
            id, { Manifest = files[dir + "/manifest.json"]; Events = files[dir + "/events.ndjson"]; Snapshot = files[dir + "/snapshot.json"] })

    let private address = WorkspaceAddress.catalog (Workspace "fixtures/v1.0.0")

    let private verify (id: string) (fs: FileSet) =
        StorageReader.verify kernel (Some(InvestigationId(Guid id))) address (CommitOid "c") fs

    let private code (r: Result<VerifiedInvestigation, VerificationFailure list>) =
        match r with
        | Ok _ -> "OK"
        | Error fs -> fs |> List.tryPick (function VerificationFailure.ReaderStage(c, _, _, _) -> Some c | _ -> None) |> Option.defaultValue "?"

    [<Fact>]
    let GoldenFixturesRoundTrip () =
        let sets = fixtureSets ()
        Assert.Equal(5, sets.Length)
        let root = Canonical.parse (Encoding.UTF8.GetString(Datasets.fixtures.Value["fixtures/v1.0.0/manifest.json"]).TrimEnd('\n')) |> Result.defaultWith failwith
        let entries = match Canonical.field "investigations" root with Some(Canonical.JArray xs) -> xs | _ -> []
        for id, fs in sets do
            match verify id fs with
            | Error f -> failwithf "%s does not verify: %A" id f
            | Ok v ->
                // Encoding the decoded history reproduces every byte.
                match FileSets.ofHistory v.History with
                | Ok again ->
                    Assert.Equal<byte[]>(fs.Events, again.Events)
                    Assert.Equal<byte[]>(fs.Manifest, again.Manifest)
                    Assert.Equal<byte[]>(fs.Snapshot, again.Snapshot)
                | Error e -> failwithf "%A" e
                // The projected state equals the golden state and the root manifest's digest.
                let entry = entries |> List.find (fun e -> Canonical.str "investigationId" e = Some id)
                let key = Canonical.str "scenarioKey" entry |> Option.get
                let golden = Datasets.fixtures.Value[sprintf "contract/v1.0.0/golden/%s.state.json" key]
                let wire = Encoding.UTF8.GetString(fs.Events).TrimEnd('\n').Split('\n') |> Array.map (Canonical.parse >> Result.defaultWith failwith) |> List.ofArray
                let state = Projection.state wire |> Result.defaultWith failwith
                Assert.Equal(Encoding.UTF8.GetString(golden), Canonical.canonical state + "\n")
                Assert.Equal(Canonical.str "expectedStateSha256" entry, Some(Canonical.digest state))

    [<Fact>]
    let EachIntegrityCheckRefusesItsForgery () =
        let id, fs = fixtureSets () |> List.find (fun (i, _) -> i = "6fd2ddc9-bedd-5fff-9093-5506c4d68cb3")
        Assert.Equal("OK", code (verify id fs))
        let text (b: byte[]) = Encoding.UTF8.GetString b
        let bytes (s: string) = Encoding.UTF8.GetBytes s
        let rehash (f: FileSet) =
            // Recompute the manifest digests so only the forged check can fail.
            let m = Canonical.parse ((text f.Manifest).TrimEnd('\n')) |> Result.defaultWith failwith
            let snap = Canonical.parse ((text f.Snapshot).TrimEnd('\n')) |> Result.defaultWith failwith
            let set k v (o: Canonical.JsonValue) =
                match o with
                | Canonical.JObject ms -> Canonical.JObject(ms |> List.map (fun (k', v') -> if k' = k then k', v else k', v'))
                | other -> other
            let m = m |> set "eventsSha256" (Canonical.JString(Canonical.sha256Hex f.Events)) |> set "snapshotSha256" (Canonical.JString(Canonical.digest snap))
            { f with Manifest = Canonical.fileOf m }
        let forgeries =
            [ "CAUSA.ENCODING", { fs with Events = bytes ((text fs.Events).Replace("\n", "\r\n")) }
              "CAUSA.JSON_MALFORMED", { fs with Manifest = bytes "{\"a\":1,\"a\":2}\n" }
              "CAUSA.NON_CANONICAL", { fs with Manifest = bytes ((text fs.Manifest).Replace(",", ", ")) }
              "CAUSA.UNKNOWN_EVENT_TYPE", rehash { fs with Events = bytes ((text fs.Events).Replace("\"ObservationRecorded\"", "\"ObservationRecordeX\"")) }
              "CAUSA.EVENTS_HASH_MISMATCH",
              (let t = text fs.Events
               let i = t.IndexOf("\"original\":\"") + 12
               { fs with Events = bytes (t.Substring(0, i) + (if t[i] = 'Z' then "Y" else "Z") + t.Substring(i + 1)) })
              "CAUSA.SNAPSHOT_HASH_MISMATCH",
              (let t = text fs.Snapshot
               let i = t.IndexOf("\"original\":\"") + 12
               { fs with Snapshot = bytes (t.Substring(0, i) + (if t[i] = 'Z' then "Y" else "Z") + t.Substring(i + 1)) })
              "CAUSA.SNAPSHOT_MISMATCH",
              (let t = text fs.Snapshot
               let i = t.IndexOf("\"original\":\"") + 12
               rehash { fs with Snapshot = bytes (t.Substring(0, i) + (if t[i] = 'Z' then "Y" else "Z") + t.Substring(i + 1)) }) ]
        for expected, forged in forgeries do
            let got = code (verify id forged)
            Assert.True((got = expected), sprintf "%s forgery: got %s" expected got)
        // A replay the domain refuses is refused (stage 15), whatever the hashes say.
        let reordered =
            let lines = (text fs.Events).TrimEnd('\n').Split('\n')
            rehash { fs with Events = bytes (String.Join("\n", Array.append [| lines[0] |] (Array.rev lines[1..])) + "\n") }
        Assert.NotEqual<string>("OK", code (verify id reordered))

    [<Fact>]
    let EveryCatalogEventRoundTrips () =
        let lines =
            Datasets.catalog.Value
            |> Map.toList
            |> List.filter (fun (p, _) -> p.EndsWith "/events.ndjson")
            |> List.collect (fun (_, b) -> Encoding.UTF8.GetString(b).TrimEnd('\n').Split('\n') |> List.ofArray)
        Assert.True(lines.Length > 1000)
        for line in lines do
            let wire = Canonical.parse line |> Result.defaultWith failwith
            match WireDecode.envelope wire with
            | Error e -> failwithf "%s: %s" e line
            | Ok envelope ->
                match WireEvents.envelope envelope with
                | Ok again -> Assert.Equal(line, Canonical.canonical again)
                | Error e -> failwithf "%A" e

    [<Fact>]
    let EncodeNewVerifiesAndRoundTrips () =
        let id = InvestigationId(Guid "0b5c1f8e-0000-4000-8000-000000000001")
        let correlation = Guid "0b5c1f8e-0000-4000-8000-000000000002"
        let started: EventEnvelope =
            { EventId = EventId(Canonical.eventId (Guid "0b5c1f8e-0000-4000-8000-000000000001") 1L correlation)
              Investigation = id
              Version = AggregateVersion 1L
              Provenance =
                { OccurredAt = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)
                  Actor = { ActorId = SaveKit.text "causa-web"; Kind = ActorKind.Human }
                  CorrelationId = correlation
                  FormatVersion = Events.formatVersion
                  SemanticVersion = Events.semanticVersion }
              Payload = CoreEvent(InvestigationStarted { Title = SaveKit.text "New"; ObservationId = ObservationId(Guid.NewGuid()); Original = SaveKit.text "Problem" }) }
        match codec.EncodeNew id [ started ] with
        | Ok fs ->
            match StorageReader.verify kernel (Some id) address (CommitOid "c") fs with
            | Ok v -> Assert.Equal<EventEnvelope list>([ started ], v.History)
            | Error e -> failwithf "%A" e
        | Error e -> failwithf "%A" e
        // A history that would not verify (sub-millisecond time has no wire form) is refused before anything is sent.
        let bad = { started with Provenance = { started.Provenance with OccurredAt = started.Provenance.OccurredAt.AddTicks 1L } }
        Assert.True(Result.isError (codec.EncodeNew id [ bad ]))

    [<Fact>]
    let WritableRefusesCatalog () =
        Assert.Equal(None, Writable.ofAddress (WorkspaceAddress.catalog (Workspace "workspaces/demo/demonstration-v1")))
        match WorkspaceAddress.workingCopy [] "run-1" with
        | Some a -> Assert.True((Writable.ofAddress a).IsSome)
        | None -> failwith "a working copy is writable"

    [<Fact>]
    let WorkingCopyNeverNamesACatalogWorkspace () =
        let catalogs = [ Workspace "workspaces/demo/demonstration-v1"; Workspace "workspaces/demo/historical-v1" ]
        for run in [ "demonstration-v1"; "historical-v1"; "r1"; "a" ] do
            match WorkspaceAddress.workingCopy catalogs run with
            | Some a ->
                let (Workspace w) = a.Workspace
                Assert.StartsWith("workspaces/demo/causa-app-", w)
                Assert.DoesNotContain(a.Workspace, catalogs)
            | None -> failwithf "%s refused" run
        for bad in [ ""; "Upper"; "../x"; "a/b"; String('a', 60) ] do
            Assert.Equal(None, WorkspaceAddress.workingCopy catalogs bad)
        Assert.Equal(None, WorkspaceAddress.workingCopy [ Workspace "workspaces/demo/causa-app-x" ] "x")

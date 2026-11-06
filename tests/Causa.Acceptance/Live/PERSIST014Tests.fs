// Owner: persistence. PERSIST-014 (live): every mutation carries a unique write marker (tab session id, sequence).
namespace Causa.Acceptance.Live

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST014Tests =
    let private markersOf (run: LiveRun.T) =
        LiveRun.mutations run
        |> List.choose (fun m ->
            use doc = System.Text.Json.JsonDocument.Parse(m.Body.Value)
            let body = doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("message").GetProperty("body").GetString()
            GitHubRequests.markerOf (string body))

    let o1 (create: string -> LiveRun.T) =
        let a = create "persist014-o1a"
        let b = create "persist014-o1b"
        let session (marker: string) = marker.Split('.')[0]
        let sa = markersOf a |> List.map session |> List.distinct
        let sb = markersOf b |> List.map session |> List.distinct
        // One random session identifier per tab (32 hex digits), different between tabs.
        Assert.Equal(1, sa.Length)
        Assert.Equal(1, sb.Length)
        Assert.Matches("^[0-9a-f]{32}$", sa.Head)
        Assert.NotEqual<string>(sa.Head, sb.Head)

    let o2 (create: string -> LiveRun.T) =
        let run = create "persist014-o2"
        LiveRun.edit run "marked" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        // The commit on GitHub carries the trailer `Causa-Write-Marker: <session>.<sequence>`.
        let message = Scenarios.commitMessage run.Provider run.Session.BaseHead |> Probes.ok "message"
        match GitHubRequests.markerOf message with
        | Some marker ->
            Assert.Matches("^[0-9a-f]{32}\\.[0-9]+$", marker)
            Assert.Equal(marker, List.last (markersOf run))
        | None -> failwithf "no marker in %s" message
        LiveRun.record run "PERSIST-014-O2" [ run.Session.BaseHead ]

    let o3 (create: string -> LiveRun.T) =
        let a = create "persist014-o3"
        LiveRun.edit a "one" |> ignore
        LiveRun.input a SaveInput.SaveRequested |> ignore
        // A reload is a new tab memory: a new machine with a new session identifier.
        let reloaded = { a with Machine = SaveMachines.create SaveMachines.support; Sent = []; Events = [] }
        match LiveRun.openInNewTab a with
        | Ok v ->
            reloaded.Session <- reloaded.Machine.Initial a.Writable v
            LiveRun.edit reloaded "after reload" |> ignore
            LiveRun.input reloaded SaveInput.SaveRequested |> ignore
        | Error f -> failwithf "%A" f
        let all = markersOf a @ markersOf reloaded
        Assert.Equal(all.Length, (List.distinct all).Length)
        Assert.NotEqual<string>((List.head (markersOf a)).Split('.')[0], (List.head (markersOf reloaded)).Split('.')[0])

    let o4 (create: string -> LiveRun.T) =
        let tabs = [ for i in 1..3 -> create (sprintf "persist014-o4-%d" i) ]
        for t in tabs do
            LiveRun.edit t "concurrent" |> ignore
        tabs |> List.map (fun t -> System.Threading.Tasks.Task.Run(fun () -> LiveRun.input t SaveInput.SaveRequested |> ignore)) |> List.iter (fun t -> t.Wait())
        let all = tabs |> List.collect markersOf
        Assert.Equal(all.Length, (List.distinct all).Length)
        for t in tabs do
            Assert.Equal("Saved", MachineKit.stateName t.Session.State)

    [<Fact>]
    let ``PERSIST-014-O1`` () = o1 LiveRun.create

    [<Fact>]
    let ``PERSIST-014-O2`` () = o2 LiveRun.create

    [<Fact>]
    let ``PERSIST-014-O3`` () = o3 LiveRun.create

    [<Fact>]
    let ``PERSIST-014-O4`` () = o4 LiveRun.create

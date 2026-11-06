// Owner: persistence. PERSIST-021 (live): reopening loads the latest confirmed commit and verifies it.
namespace Causa.Acceptance.Live

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST021Tests =
    let private observations (v: VerifiedInvestigation) =
        v.History |> List.choose (fun e -> match e.Payload with CoreEvent(ObservationRecorded o) -> Some o.Original.Value | _ -> None)

    let o1 (create: string -> LiveRun.T) =
        let run = create "persist021-o1"
        LiveRun.edit run "confirmed observation" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        match LiveRun.openInNewTab run with
        | Ok v ->
            Assert.Equal(run.Session.BaseHead, v.ReadAt)
            Assert.True(Result.isOk (StorageReader.verify Kernel.domain (Some run.Id) v.Address v.ReadAt v.Bytes))
        | Error f -> failwithf "%A" f

    let o2 (create: string -> LiveRun.T) =
        let run = create "persist021-o2"
        match LiveRun.openInNewTab run with
        | Ok v ->
            match Persistence.sessionOf v with
            | Some s -> Assert.Equal(SaveState.Saved(VerifiedAt v), s.State)
            | None -> failwith "no session"
        | Error f -> failwithf "%A" f

    let o3 (create: string -> LiveRun.T) =
        let run = create "persist021-o3"
        LiveRun.edit run "saved one" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        LiveRun.edit run "never saved" |> ignore
        match LiveRun.openInNewTab run with
        | Ok v ->
            let seen = observations v
            Assert.Contains("saved one", seen)
            Assert.DoesNotContain("never saved", seen)
        | Error f -> failwithf "%A" f

    let o4 (create: string -> LiveRun.T) =
        let run = create "persist021-o4"
        LiveRun.edit run "tab A" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        // A second tab opens, adds and saves; the first tab reopens and sees both confirmed saves.
        let tabB = { run with Machine = SaveMachines.create SaveMachines.support; Sent = []; Events = [] }
        match LiveRun.openInNewTab run with
        | Ok v ->
            tabB.Session <- tabB.Machine.Initial run.Writable v
            LiveRun.edit tabB "tab B" |> ignore
            LiveRun.input tabB SaveInput.SaveRequested |> ignore
        | Error f -> failwithf "%A" f
        match LiveRun.openInNewTab run with
        | Ok v ->
            Assert.Equal(tabB.Session.BaseHead, v.ReadAt)
            let seen = observations v
            Assert.Contains("tab A", seen)
            Assert.Contains("tab B", seen)
        | Error f -> failwithf "%A" f
        LiveRun.record run "PERSIST-021-O4" [ run.Session.BaseHead; tabB.Session.BaseHead ]

    [<Fact>]
    let ``PERSIST-021-O1`` () = o1 LiveRun.create

    [<Fact>]
    let ``PERSIST-021-O2`` () = o2 LiveRun.create

    [<Fact>]
    let ``PERSIST-021-O3`` () = o3 LiveRun.create

    [<Fact>]
    let ``PERSIST-021-O4`` () = o4 LiveRun.create

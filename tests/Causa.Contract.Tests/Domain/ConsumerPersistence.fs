// Owner: persistence. IF41-DOMAIN consumed by persistence: the kernel signatures it calls and the stored fixtures replay.
namespace Causa.Contract.Tests.Domain

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module ConsumerPersistence =
    [<Fact>]
    let KernelSignaturesAndFixtures () =
        // The fields persistence calls, with the frozen signatures.
        let k: DomainKernel = Kernel.domain
        let _: PolicySet -> ActorContext -> Investigation option -> CommandEnvelope -> Decision = k.Decide
        let _: Investigation option -> EventEnvelope -> Result<Investigation, ProjectionError> = k.Evolve
        let _: EventEnvelope list -> Result<Investigation, ReplayError> = k.Replay
        // Every frozen fixture's decoded history replays to its last version, and replay refuses an empty history.
        for dir in [ "6fd2ddc9-bedd-5fff-9093-5506c4d68cb3"; "abd5f9b5-e335-5245-b951-802f01d5a2e5"; "a6747bd3-2794-5ac7-83b8-5ea2a0c50f5e"; "c57e4407-dd43-5d63-8dbe-9d6ceac6c31a"; "d588748e-8df9-5f27-859e-5d36d9a2b53d" ] do
            let events = Datasets.fixtures.Value[sprintf "fixtures/v1.0.0/investigations/%s/events.ndjson" dir]
            let history =
                Text.Encoding.UTF8.GetString(events).TrimEnd('\n').Split('\n')
                |> Array.map (fun l -> Canonical.parse l |> Result.defaultWith failwith |> WireDecode.envelope |> Result.defaultWith failwith)
                |> List.ofArray
            match k.Replay history with
            | Ok inv ->
                Assert.Equal(AggregateVersion(int64 history.Length), inv.Version)
                Assert.Equal(InvestigationId(Guid dir), inv.Id)
            | Error e -> failwithf "%s: %A" dir e
        Assert.Equal(Error EmptyHistory, k.Replay [])

module App.Engine.Tests.EngineTests

open Xunit
open IndyInitRootCauseInvestigator.Engine

[<Fact>]
let ``the scaffold engine starts uninitialized`` () =
    Assert.Equal(State.Uninitialized, State.initial)

[<Fact>]
let ``the Aegis configuration validates at startup`` () =
    Assert.True(Result.isOk (Operational.validateConfiguration ()))

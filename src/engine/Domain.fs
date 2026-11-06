namespace IndyInitRootCauseInvestigator.Engine

type State =
    | Uninitialized

module State =
    let initial = Uninitialized

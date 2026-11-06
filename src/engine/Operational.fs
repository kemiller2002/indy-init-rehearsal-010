namespace IndyInitRootCauseInvestigator.Engine

open Aegis

module Operational =
    let aegis = Aegis.configure "indy-init-root-cause-investigator" None [ Sinks.standardError ]

    let validateConfiguration () =
        Bootstrap.validate None aegis

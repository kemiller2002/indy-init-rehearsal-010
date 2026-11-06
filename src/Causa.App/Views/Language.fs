// Owner: ui. The shell's plain-language copy (UI-017, UI-010): one place for what the destinations say.
namespace Causa.App.Views

open Causa.Ui.Contracts

[<RequireQualifiedAccess>]
module Language =
    let administrationIntro = "How this application is connected and what it has verified. Nothing here is a preference."
    let settingsIntro = "Preferences for this browser tab. They change how Causa looks, not what is stored."
    let notConnected = "Not connected. Connect a data repository on Investigations."
    let investigationsNotConnected = "Connect a data repository to list its investigations."

    /// UI-010: what happened, what was preserved, and a safe next step.
    let notFound (requested: string) : FailureCopy =
        { What = sprintf "There is no page at %s." requested
          Preserved = "Nothing was changed or saved."
          NextStep = "Go to Investigations to continue."
          Retry = false }

/// The not-found screen (UI-010): an unknown route says what happened, that nothing changed, and where to go.
[<RequireQualifiedAccess>]
module NotFound =
    open Causa.App.Ui

    let view (state: AppState) : (string * ViewValue) list =
        let requested = match state.Route with NotFoundRoute r -> r | _ -> ""
        let copy = Language.notFound requested
        [ Bind.text "not-found.heading" "Page not found"
          Bind.text "not-found.what" copy.What
          Bind.text "not-found.preserved" copy.Preserved
          Bind.text "not-found.next" copy.NextStep ]

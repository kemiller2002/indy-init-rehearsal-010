// Owner: ui. Settings (UI-019, UI-020-O2): the preferences this build supports, each with its scope, kept apart from
// the verified connection. Keys start with "settings.". Choices arrive as field events (Ui/Events.fs keeps them as
// drafts); the shell applies them (Shell.motion, Shell.textSize).
namespace Causa.App.Views

open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Settings =
    let project (state: AppState) : SettingsView =
        { Preferences =
            [ { Id = Shell.MotionPreference
                Label = "Motion"
                Scope = SessionScope
                Options = [ "system"; "reduce" ]
                Effective = Shell.motion state }
              { Id = Shell.TextSizePreference
                Label = "Text size"
                Scope = SessionScope
                Options = [ "default"; "large" ]
                Effective = Shell.textSize state } ]
          ConnectionPreference =
            { Label = "Data repository"
              Status = Shell.repository state.Model |> Option.map FactKnown |> Option.defaultValue FactUnknown } }

    let view (state: AppState) : (string * ViewValue) list =
        let v = project state
        let effective id = v.Preferences |> List.find (fun p -> p.Id = id) |> fun p -> p.Effective
        let value, word, _ = States.fact v.ConnectionPreference.Status
        [ Bind.text "settings.heading" "Settings"
          Bind.text "settings.intro" Language.settingsIntro
          Bind.flag "settings.motion.system" (effective Shell.MotionPreference = "system")
          Bind.flag "settings.motion.reduce" (effective Shell.MotionPreference = "reduce")
          Bind.flag "settings.text-size.default" (effective Shell.TextSizePreference = "default")
          Bind.flag "settings.text-size.large" (effective Shell.TextSizePreference = "large")
          Bind.text "settings.scope" "These preferences apply to this browser tab only and are not stored."
          Bind.text "settings.connection" (if word = "Known" then value else "Not connected: a connection is verified on Investigations, not set here.") ]

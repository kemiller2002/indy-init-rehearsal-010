// Owner: ui (written by ui-2 acting as ui for T29). Settings (UI-019, UI-020-O2): only the preferences this build
// honours (motion and text size, applied by the shell), each with its scope and effective value, kept apart from the
// data repository connection, which is shown as a fact GitHub verified, never as a preference. No credential is a
// setting: the token is entered on Investigations, kept only in this tab's memory and never shown or stored.
// Keys start with "settings.". Choices arrive as field events (Ui/Events.fs keeps them as drafts).
namespace Causa.App.Views

open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Settings =
    let private optionLabel (id: string) (value: string) =
        match id, value with
        | _, "system" -> "Follow the device setting"
        | _, "reduce" -> "Reduce motion"
        | _, "default" -> "Default"
        | _, "large" -> "Larger"
        | _, v -> v

    let scopeText (scope: PreferenceScope) =
        match scope with
        | SessionScope -> "This browser tab only; not stored, so a reload returns to the default."
        | DeviceScope -> "This device."

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
        let pref id = v.Preferences |> List.find (fun p -> p.Id = id)
        let effective id = (pref id).Effective
        let connection, word, tone = Administration.present (Administration.connectionClaim state.Model)
        let repository = Shell.repository state.Model |> Option.defaultValue "None"
        [ Bind.text "settings.heading" "Settings"
          Bind.text "settings.intro" Language.settingsIntro
          Bind.flag "settings.motion.system" (effective Shell.MotionPreference = "system")
          Bind.flag "settings.motion.reduce" (effective Shell.MotionPreference = "reduce")
          Bind.flag "settings.text-size.default" (effective Shell.TextSizePreference = "default")
          Bind.flag "settings.text-size.large" (effective Shell.TextSizePreference = "large")
          for p in v.Preferences do
              Bind.text ($"{p.Id}.effective") (optionLabel p.Id p.Effective)
              Bind.text ($"{p.Id}.scope") (scopeText p.Scope)
          Bind.text "settings.scope" "These preferences apply to this browser tab only and are not stored."
          Bind.text "settings.connection.repository" repository
          Bind.text "settings.connection" connection
          Bind.text "settings.connection.status" word
          Bind.text "settings.connection.tone" (States.tone tone)
          Bind.text "settings.connection.note"
              "The connection is set and verified on Investigations, not here. The token is kept only in this tab's memory and is never shown or stored." ]

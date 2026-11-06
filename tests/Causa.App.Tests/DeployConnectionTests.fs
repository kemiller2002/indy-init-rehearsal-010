// Owner: integration. HELD until the owner records ODR-COMP-P13 / ODR-R10-36. The deploy connection: its generation
// from the four Actions secrets (build/DeployConnection.fs, the code build.fsx runs), the credential scan of the
// deploy artifact, and the application connecting from the file without the user typing the key. Positive and
// negative: only the one injected key, only in deploy-connection.json, only for the designated repository.
namespace Causa.App.Tests

open System
open Xunit
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

module DeployConnectionTests =
    let key = "github_pat_11TESTONLY0000000000_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOP"
    let otherToken = "ghp_" + String('Z', 36)
    let secrets =
        Map.ofList
            [ DeployConnection.OwnerSecret, "kemiller2002"
              DeployConnection.RepositorySecret, "causa-data"
              DeployConnection.BranchSecret, "causa-test"
              DeployConnection.KeySecret, key ]
    let lookupOf (m: Map<string, string>) = fun n -> Map.tryFind n m
    let settings: DeployConnection.Settings = { Owner = "kemiller2002"; Repository = "causa-data"; Branch = "causa-test"; Key = key }
    let env: HostEnv = { Now = DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero); Fresh = [ Guid.NewGuid() ] }

    // ---- generation -------------------------------------------------------------------------------------------------

    [<Fact>]
    let ``no secret set: no connection, nothing bundled (every local and CI build)`` () =
        Assert.Equal(Ok None, DeployConnection.fromLookup (fun _ -> None))
        let text = DeployConnection.render None
        Assert.DoesNotContain("\"connection\"", text)
        Assert.DoesNotContain("key", text)
        Assert.Contains(DeployConnection.Schema, text)

    [<Fact>]
    let ``all four secrets: the one connection, rendered for the application`` () =
        match DeployConnection.fromLookup (lookupOf secrets) with
        | Ok(Some s) ->
            Assert.Equal(settings, s)
            match DeployedConnection.parse (DeployConnection.render (Some s)) with
            | Some(repo, credential) ->
                Assert.Equal(({ Owner = "kemiller2002"; Name = "causa-data"; Branch = "causa-test" }: Repository), repo)
                Assert.Equal(("Authorization", "Bearer " + key), Credential.authorization credential)
            | None -> failwith "the rendered file does not parse"
        | other -> failwithf "%A" other

    [<Fact>]
    let ``some secrets: the build fails naming the missing secrets, never a value`` () =
        match DeployConnection.fromLookup (lookupOf (Map.remove DeployConnection.BranchSecret secrets)) with
        | Error why ->
            Assert.Contains(DeployConnection.BranchSecret, why)
            Assert.DoesNotContain(key, why)
        | other -> failwithf "expected an error, got %A" other

    [<Fact>]
    let ``a malformed secret fails naming the secret, never the value`` () =
        for name, bad in [ DeployConnection.OwnerSecret, "kem/iller"; DeployConnection.RepositorySecret, "a b"; DeployConnection.BranchSecret, "x..y"; DeployConnection.KeySecret, "two words here" ] do
            match DeployConnection.fromLookup (lookupOf (Map.add name bad secrets)) with
            | Error why ->
                Assert.Contains(name, why)
                Assert.DoesNotContain(bad, why)
                Assert.DoesNotContain(key, why)
            | other -> failwithf "%s=%s: expected an error, got %A" name bad other

    [<Fact>]
    let ``the settings never print the key`` () =
        Assert.DoesNotContain(key, sprintf "%A" settings)
        Assert.DoesNotContain(key, string settings)
        Assert.DoesNotContain(key, sprintf "%A" (Some settings))

    // ---- the credential scan ----------------------------------------------------------------------------------------

    let site (config: string) extra =
        [ "index.html", "<html><body>Causa</body></html>"
          "boundary.js", "export const x = 1;"
          "build-info.json", "{\"schema\":\"causa.build-info/1\"}"
          DeployConnection.FileName, config ]
        @ extra

    [<Fact>]
    let ``scan passes: a build without secrets ships no credential`` () =
        Assert.Empty(DeployConnection.scan None (site (DeployConnection.render None) []))

    [<Fact>]
    let ``scan passes: the deploy build ships the one key, in deploy-connection.json only`` () =
        Assert.Empty(DeployConnection.scan (Some settings) (site (DeployConnection.render (Some settings)) []))

    [<Fact>]
    let ``scan fails: any other token literal, in any build`` () =
        for injected in [ None; Some settings ] do
            let config = DeployConnection.render injected
            let findings = DeployConnection.scan injected (site config [ "_framework/Causa.App.dll", "...." + otherToken + "...." ])
            Assert.Equal<string list>([ "_framework/Causa.App.dll" ], findings |> List.map (fun f -> f.Path))

    [<Fact>]
    let ``scan fails: the injected key outside deploy-connection.json`` () =
        let findings = DeployConnection.scan (Some settings) (site (DeployConnection.render (Some settings)) [ "app.js", "const k = \"" + key + "\";" ])
        Assert.Contains(findings, fun f -> f.Path = "app.js")

    [<Fact>]
    let ``scan fails: a deploy-connection.json not at the artifact root is not the allowed file`` () =
        let findings = DeployConnection.scan (Some settings) (site (DeployConnection.render (Some settings)) [ "nested/" + DeployConnection.FileName, DeployConnection.render (Some settings) ])
        Assert.Contains(findings, fun f -> f.Path = "nested/" + DeployConnection.FileName)

    [<Fact>]
    let ``scan fails: a connection in a build that has no deploy secrets`` () =
        let findings = DeployConnection.scan None (site (DeployConnection.render (Some settings)) [])
        Assert.NotEmpty(findings)
        Assert.All(findings, fun f -> Assert.Equal(DeployConnection.FileName, f.Path))

    [<Fact>]
    let ``scan fails: another token or another repository in deploy-connection.json`` () =
        let swapped = DeployConnection.render (Some { settings with Key = otherToken })
        Assert.NotEmpty(DeployConnection.scan (Some settings) (site swapped []))
        let elsewhere = DeployConnection.render (Some { settings with Repository = "production-data" })
        Assert.NotEmpty(DeployConnection.scan (Some settings) (site elsewhere []))

    [<Fact>]
    let ``scan findings never carry a credential value`` () =
        let findings =
            DeployConnection.scan (Some settings) (site (DeployConnection.render (Some settings)) [ "a.js", key; "b.js", otherToken ])
        Assert.NotEmpty(findings)
        for f in findings do
            Assert.DoesNotContain(key, f.Reason + f.Path)
            Assert.DoesNotContain(otherToken, f.Reason + f.Path)

    // ---- the application --------------------------------------------------------------------------------------------

    let loaded () =
        let state, effects = Update.init ""
        Assert.Equal<AppEffect list>([ ReadBuildInfo ], effects |> List.filter (function ReadBuildInfo | ReadDeployConnection -> true | _ -> false))
        let state, effects = Update.updateAt env (BuildRead(Ok "{}")) state
        Assert.Equal<AppEffect list>([ ReadDeployConnection ], effects)
        state

    [<Fact>]
    let ``the deploy connection is read once, after the build record`` () =
        let state = loaded ()
        let _, again = Update.updateAt env (BuildRead(Ok "{}")) state
        Assert.Empty(again)

    [<Fact>]
    let ``without a connection in the file the app is exactly as before: the Connect form, no credential`` () =
        let before = loaded ()
        for read in [ Ok(DeployConnection.render None); Error "deploy-connection.json answered 404"; Ok "not json" ] do
            let after, effects = Update.updateAt env (DeployConnectionRead read) before
            Assert.Empty(effects)
            Assert.True(after.Credential.IsNone)
            Assert.Equal(Disconnected, after.Model.Connection)
            Assert.Equal(BindingValue.Flag true, (View.render after |> Map.ofList)["connection.form"])

    [<Fact>]
    let ``with the injected connection the app connects to that repository with that key, untyped`` () =
        let after, effects = Update.updateAt env (DeployConnectionRead(Ok(DeployConnection.render (Some settings)))) (loaded ())
        Assert.True(after.Credential.IsSome)
        Assert.NotEqual(Disconnected, after.Model.Connection)
        let requests = effects |> List.choose (function HostEffect(Http r) -> Some r | _ -> None)
        Assert.NotEmpty(requests)
        Assert.All(requests, fun r ->
            Assert.StartsWith("https://api.github.com/repos/kemiller2002/causa-data", r.Url)
            Assert.DoesNotContain(key, r.Url)
            Assert.Contains(("Authorization", "Bearer " + key), r.Headers))
        // Never rendered, announced or printed.
        Assert.DoesNotContain(key, Program.encode (View.render after) [] 0)
        Assert.DoesNotContain(key, sprintf "%A" after.Model)
        Assert.All(after.Announcements, fun (_, a) -> Assert.DoesNotContain(key, a))

    [<Fact>]
    let ``a connection the user already made is not replaced`` () =
        let connected, _ = Update.updateAt env (DeployConnectionRead(Ok(DeployConnection.render (Some settings)))) (loaded ())
        let again, effects = Update.updateAt env (DeployConnectionRead(Ok(DeployConnection.render (Some { settings with Key = otherToken })))) connected
        Assert.Empty(effects)
        Assert.Equal(connected.Model.Connection, again.Model.Connection)

    [<Fact>]
    let ``the read is a same-origin GET without credentials, and its answer is decoded`` () =
        let text = Program.encode [] [ ReadDeployConnection ] 0
        Assert.Contains("\"url\":\"./deploy-connection.json\"", text)
        Assert.Contains("\"credentials\":\"omit\"", text)
        Assert.DoesNotContain("Authorization", text)
        let body = DeployConnection.render None
        let message =
            sprintf """{"kind":"EffectResult","result":{"kind":"HttpResult","correlationId":"%s","outcome":{"kind":"Success","status":200,"body":%s}}}"""
                DeployedConnection.Correlation (Text.Json.JsonSerializer.Serialize body)
        Assert.Equal(Program.DeployConnection(Ok body), Program.decode message)
        let missing =
            sprintf """{"kind":"EffectResult","result":{"kind":"HttpResult","correlationId":"%s","outcome":{"kind":"Success","status":404,"body":""}}}""" DeployedConnection.Correlation
        match Program.decode missing with
        | Program.DeployConnection(Error _) -> ()
        | other -> failwithf "%A" other

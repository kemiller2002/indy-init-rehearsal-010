module App.Engine.Tests.RouteTests

open System
open Xunit
open IndyInitRootCauseInvestigator.Engine

// SAF-URL-9: format (parse url) is the canonical url, and parse (format route)
// is the route, for generated input.
let private generated count =
    let random = Random 20261008
    let alphabet = "abcdefghijklmnopqrstuvwxyz0123456789-"
    let segment () = String(Array.init (random.Next(1, 9)) (fun _ -> alphabet[random.Next alphabet.Length]))

    List.init count (fun _ -> "/" + String.Join("/", Array.init (random.Next(1, 4)) (fun _ -> segment ())))

[<Fact>]
let ``the home route is the empty and the root hash`` () =
    Assert.Equal(Home, Routes.parse "")
    Assert.Equal(Home, Routes.parse "#/")
    Assert.Equal("#/", Routes.format Home)

[<Fact>]
let ``format (parse url) is the canonical url`` () =
    for path in generated 500 do
        Assert.Equal("#" + path, Routes.format (Routes.parse ("#" + path + "?undeclared=1")))

[<Fact>]
let ``parse (format route) is the route`` () =
    for path in generated 500 do
        let route = NotFound path
        Assert.Equal(route, Routes.parse (Routes.format route))

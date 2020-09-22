#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp 
open Akka.Remote

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 9001
            }
        }"

[<EntryPoint>]
let main args =
    use system = System.create "remote-system" config
    System.Console.ReadLine() |> ignore
    0
open System.Threading
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp



let rnd = System.Random()
let serverip = fsi.CommandLineArgs.[1] |> string
let serverport = fsi.CommandLineArgs.[2] |>string
let simid = fsi.CommandLineArgs.[3] |>string
//"akka.tcp://RemoteFSharp@localhost:8777/user/server"
let addr = "akka.tcp://TwitterEngine@" + serverip + ":" + serverport + "/user/RequestHandler"

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                
            }
            remote {
                helios.tcp {
                    port = 8779
                    hostname = 192.168.0.96
                }
            }
        }")

let system = ActorSystem.Create("Sampl", configuration)
let twitterServer = system.ActorSelection(addr)
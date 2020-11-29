open System.Threading
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let addr = "akka.tcp://TwitterEngine@192.168.0.96:8777/user/RequestHandler"

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

let system = ActorSystem.Create("Temp", configuration)
let twitterServer = system.ActorSelection(addr)
// Thread.Sleep(10000)
twitterServer <! "Done|"
twitterServer <! "BackupDB|"

system.WhenTerminated.Wait()
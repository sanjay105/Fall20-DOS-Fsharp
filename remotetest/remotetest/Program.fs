
open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp 
open Akka.Remote

let address = "akka.tcp://system2@localhost:9001"


let config =Configuration.parse """
        akka {  
            actor {
                provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
            }
            remote.helios.tcp {
            port = 9001
            hostname = 192.168.0.96
        }
    }"""

// let remoteDeploy sysPath = 
//     let addr = 
//         match ActorPath.TryParseAddress sysPath with
//         |false,_ -> failwith "biscuit"
//         |true, a -> a
//     Deploy(RemoteScope(addr))



let depl sys = 
    let ad = 
        match  ActorPath.TryParseAddress sys with
        |false,_ -> failwith "biscuit"
        |true, a -> a
    Deploy(RemoteScope ad)

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"
    0 // return an integer exit code

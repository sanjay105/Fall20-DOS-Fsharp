#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp 
open Akka.Remote

let address = "akka.tcp://system2@localhost:9001"


let config =
    Configuration.parse """
        akka {  
            actor {
                provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
            }
            remote.helios.tcp {
            port = 9001
            hostname = 192.168.0.96
        }
    }"""

// let remoteDeploy systemPath =
//     let address =
//         match ActorPath.TryParseAddress systemPath with
//         | false, _ -> failwith "Actor Path addr failed to be parsed"
//         | true, a -> a
//     SpawnOption.Deploy (Deploy(RemoteScope address))

let deployRemotely address = Deploy(RemoteScope (Address.Parse address))

let spawnRemote systemOrContext remoteSystemAddress actorName expr =
    spawne systemOrContext actorName expr [SpawnOption.Deploy (deployRemotely remoteSystemAddress)]

let addr = "akka.tcp://remote-system@192.168.0.109:9001/"

let system = System.create "local-system" config

let temp = spawne system "hello" <@ actorOf (fun msg -> printfn "received " ) @> [remoteDeploy addr]
let aref =
    spawnRemote system addr "hello"
       // actorOf wraps custom handling function with message receiver logic
       <@ actorOf (fun msg -> printfn "received '%s'" msg) @>

// send example message to remotely deployed actor
aref <! "Hello world"
// temp <! "Hello World"
// thanks to location transparency, we can select 
// remote actors as if they where existing on local node
let sref = select "akka://local-system/user/hello" system
sref <! "Hello again"

// we can still create actors in local system context
let lref = spawn system "local" (actorOf (fun msg -> printfn "local '%s'" msg))
// this message should be printed in local application console
lref <! "Hello locally"
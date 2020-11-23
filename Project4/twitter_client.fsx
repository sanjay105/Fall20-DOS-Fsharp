#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 8779
                    hostname = 192.168.0.96
                }
            }
        }")

let system = ActorSystem.Create("TwitterClient", configuration)

let serveraddr = "akka.tcp://TwitterEngine@192.168.0.96:8778/user/server"

type ServerMsg =
    | StartServer
    | ClientReq

type ClientMsg =
    | StartClient
    | ServerAck

type SimulatorMsg =
    | Start

let Client (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | StartClient ->    printfn "Client Started"
                            let server = system.ActorSelection(serveraddr)
                            printfn "%A" server.Path
                            server <! "ClientReq"
                            
                            printfn "Request Sent to Server"
        | ServerAck ->      printfn "Received Ack from Server"
                      
        return! loop()
    }
    loop()

let clientList=[for a in 1 .. 5 do yield(spawn system ("Client" + (string a)) Client)]



let Simulator (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | Start ->  for i in 1..5 do
                        clientList.[i-1]<!StartClient

        return! loop()
    }
    loop()

let simulatorRef = spawn system "Simulator" Simulator

simulatorRef <! Start



system.WhenTerminated.Wait()
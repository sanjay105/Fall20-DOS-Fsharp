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
                    port = 8778
                    hostname = 192.168.0.96
                }
            }
        }")

let system = ActorSystem.Create("TwitterEngine", configuration)

type ServerMsg =
    | StartServer
    | ClientReq

type ClientMsg =
    | StartClient
    | ServerAck

type SimulatorMsg =
    | Start


let Server (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        printfn "%A" msg
        match msg with 
        | StartServer ->    printfn "Server Started"
        | ClientReq ->      printfn "Received request from client"
                            mailbox.Sender()<!ServerAck
                      

        return! loop()
    }
    loop()

let serverRef = spawn system "server" Server

printfn "%A" serverRef.Path

serverRef <! StartServer

system.WhenTerminated.Wait()
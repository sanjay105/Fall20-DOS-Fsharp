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
                    port = 8777
                    hostname = 192.168.0.229
                }
            }
        }")

type ActorMsg =
    | WorkerMsg of int64*int64*int64*int64
    | DispatcherMsg of int64*int64*int64
    | EndMsg of int64*int64
    | ResultMsg of int64
let mutable count=0L //to keep track of the workers
//let workers = System.Environment.ProcessorCount |> int64
let workers = 8L
let system = ActorSystem.Create("RemoteFSharp", configuration)
let mutable ref = null

let checkLucasSquarePyramid startIndex k  =
    let mutable sum = 0L
    for i in startIndex|>int64 .. startIndex+k-1L do
        sum <- sum + (i * i)
    let mutable num = startIndex+k
    // while (num*num) < sum do
    //     num <- num+1L
    // if (num * num)=sum then 
    //     printfn "%i" startIndex
    let squareRoot = sum |> double |> sqrt |> int64
    if sum = squareRoot * squareRoot then
        ref <! (startIndex|>string)
        printfn "%i " startIndex
        // let echoClient = system.ActorSelection("akka.tcp://ClientFsharp@localhost:8778/user/serverPrintMessage")
        // echoClient <! startIndex
        // printfn "Message %i sent" startIndex


let FindLucasPyramid (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | WorkerMsg(start,endI,k,offset) -> for i in start .. workers .. endI do
                                                checkLucasSquarePyramid (i+offset) k 
                                            mailbox.Sender() <! EndMsg(start,offset)
        | _ -> printfn "Worker Received Wrong message"
    }
    loop()
let workersList=[for a in 1L .. workers do yield(spawn system ("Job" + (string a)) FindLucasPyramid)]
let Dispatcher (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | DispatcherMsg(startN,endN,k) ->   printfn "In Dispatcher"
                                            
                                            printfn "Received Dispatcher command from server"
                                            for i in 0L .. (workers-1L) do
                                                workersList.Item(i|>int) <! WorkerMsg( startN, endN , k , i )
                                                                
        | EndMsg(index,workerid) -> count <- count+1L
                                    //printfn "Worker %i finished its job" workerid
                                    if count = workers then
                                        ref <! "ProcessingDone"
        | _ -> printfn "Dispatcher Received Wrong message"
        return! loop()
    }
    loop()
let localDispatcherRef = spawn system "localDisp" Dispatcher

let commlink = 
    spawn system "server"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! msg = mailbox.Receive()
                printfn "%s" msg 
                let command = (msg|>string).Split ','
                if command.[0].CompareTo("Job")=0 then
                    
                    localDispatcherRef <! DispatcherMsg(command.[1]|>int64,command.[2]|>int64,command.[3]|>int64)
                    ref <- mailbox.Sender()

                return! loop() 
            }
        loop()

// let echoClient = system.ActorSelection(
//                             "akka.tcp://RemoteFSharp@localhost:8778/user/EchoServer")

// let task = echoClient <? "F#!"

// let response = Async.RunSynchronously (task, 1000)

// printfn "Reply from remote %s" (string(response))


system.WhenTerminated.Wait()

//system.Terminate()
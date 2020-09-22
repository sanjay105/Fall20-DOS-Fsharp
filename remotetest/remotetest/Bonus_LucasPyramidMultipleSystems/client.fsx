#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp



let N = fsi.CommandLineArgs.[1] |> int64
let k = fsi.CommandLineArgs.[2] |> int64
// let N = 100000000L
// let k = 2L
let serverip = fsi.CommandLineArgs.[3] |> string
let s_port = fsi.CommandLineArgs.[4] |>string
//"akka.tcp://RemoteFSharp@localhost:8777/user/server"
let addr = "akka.tcp://RemoteFSharp@" + serverip + ":" + s_port + "/user/server"


let mutable count=0L //to keep track of the workers
let workers = System.Environment.ProcessorCount |> int64

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                
            }
            remote {
                helios.tcp {
                    port = 8778
                    hostname = localhost
                }
            }
        }")

let system = ActorSystem.Create("ClientFsharp", configuration)

let echoServer = 
    spawn system "EchoServer"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? string -> 
                        printfn "super!"
                        sender <! sprintf "Hello %s remote" message
                        return! loop()
                | _ ->  failwith "unknown message"
            } 
        loop()




type ActorMsg =
    | WorkerMsg of int64*int64*int64*int64
    | DispatcherMsg of int64*int64
    | EndMsg of int64*int64


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
        printfn "%i" startIndex


let FindLucasPyramid (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | WorkerMsg(start,endI,k,offset) -> for i in start .. workers .. endI do
                                                //printfn "Worker %i received job %i" offset (i+offset)
                                                checkLucasSquarePyramid (i+offset) k 
                                            mailbox.Sender() <! EndMsg(start,offset)
        | _ -> printfn "Worker Received Wrong message"
    }
    loop()

let mutable localWorkDone = false
let Dispatcher (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | DispatcherMsg(N,k) ->
                                        let workersList=[for a in 1L .. workers do yield(spawn system ("Job" + (string a)) FindLucasPyramid)]
                                        //printfn "Received Dispatcher command from server"
                                        let effort = N/(workers-1L)
                                        for i in 0L .. (workers-1L) do
                                            workersList.Item(i|>int) <! WorkerMsg( 1L, N , k , i )
                                        
                                            
        | EndMsg(index,workerid) -> count <- count+1L
                                    //printfn "Worker %i finished its job" workerid
                                    if count = workers then
                                        //mailbox.Context.System.Terminate() |> ignore
                                        localWorkDone <- true
        | _ -> printfn "Dispatcher Received Wrong message"
        return! loop()
    }
    loop()

// let ereff =  echoServer
//DispatcherRef <! DispatcherMsg(2000L,2L)
let mutable remoteWorkDone = false
let commlink = 
    spawn system "client"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! msg = mailbox.Receive()
                printfn "%s" msg 
                let response =msg|>string
                let command = (response).Split ','
                if command.[0].CompareTo("init")=0 then
                    let endN = N/2L
                    printfn "EndN : %i" endN
                    let echoClient = system.ActorSelection(addr)
                    let msgToServer = "Job," + (endN|>string) + "," + (N|>string) + "," + (k|>string)
                    echoClient <! msgToServer
                    let DispatcherRef = spawn system "Dispatcher" Dispatcher
                    DispatcherRef <! DispatcherMsg( endN-1L , k)
                elif response.CompareTo("")=0 then
                    system.Terminate() |> ignore
                    remoteWorkDone <- true
                else
                    printfn "-%s-" msg

                return! loop() 
            }
        loop()



commlink <! "init"
while (not localWorkDone && not remoteWorkDone) do

//Console.ReadLine()|>ignore


system.WhenTerminated.Wait()

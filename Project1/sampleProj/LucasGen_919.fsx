#time "on"
#r "nuget: Akka.FSharp" 

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp 

let N = fsi.CommandLineArgs.[1] |> int64
let k = fsi.CommandLineArgs.[2] |> int64
let mutable count=0

let system = ActorSystem.Create("Master")

type ActorMsg =
    | WorkerMsg of int64*int64*int*IActorRef
    | WorkerMsg2 of int64*int64*int64*int*IActorRef
    | DispatcherMsg of int64*int64*IActorRef
    | EndMsg of int64*int

let add1 x= x + 1L 
let times2 x = x * 2L 
let timesn n x = x * n 
let divideby6 x = x / 6L 

let sumOfSquares n = timesn n >> timesn (add1 n) >> (times2>>add1) >> divideby6

let checkLucasSquarePyramid startIndex k =
    let total = sumOfSquares (startIndex+k-1L) (startIndex+k-1L) - sumOfSquares (startIndex-1L) (startIndex-1L)
    let squareRoot = total |> double |> sqrt |> int64
    if total = squareRoot * squareRoot then
        printfn "%d" startIndex

let FindLucasPyramid (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | WorkerMsg(index,k,selfid,bossRef) ->  checkLucasSquarePyramid index k
                                                bossRef <! EndMsg(index,selfid)
        | WorkerMsg2(start,endI,k,selfid,bossRef) ->    for i in start .. endI do
                                                            checkLucasSquarePyramid i k
                                                        bossRef <! EndMsg(start,selfid)
    }
    loop()

let Dispatcher (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | DispatcherMsg(N,k,selfRef) ->
                                        let workersList=[for a in 1 .. 10 do yield(spawn system ("Job" + (string a)) FindLucasPyramid)]
                                        let effort = N/9L
                                        for i in 0L .. 9L do
                                            workersList.Item(i|>int) <! WorkerMsg2( ((effort*i)+1L) , effort*(i + 1L) , k , i|>int , selfRef)                    
        | EndMsg(index,workerid) -> count <- count+1
                                    printfn "finished %i job by worker %i" index workerid
        //while count <> 0 do     
        //finish <- true
        return! loop()
    }
    loop()

let DispatcherRef = spawn system "Dispatcher" Dispatcher

DispatcherRef <! DispatcherMsg(N,k,DispatcherRef)

while count <> 10 do

system.Terminate()
#time "on"
#r "nuget: Akka.FSharp" 

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let N = 1000000
let k = 256
let mutable count=0
let mutable finish=false
let system = ActorSystem.Create("Master")

type ActorMsg =
    | WorkerMsg of int*int*int*IActorRef
    | WorkerMsg2 of int*int*int*int*IActorRef
    | DispatcherMsg of int*int*IActorRef
    | EndMsg of int*int

let SquareOfNumber num = num*num

let checkLucasSquarePyramid startIndex k =
    let mutable sum = 0
    for i in startIndex .. startIndex+k-1 do
        sum <- sum + (i * i)
    let squareRoot = sum |> float |> sqrt |> int
    //printfn "%i %i %f %d " startIndex sum squareRoot (int squareRoot)
    if sum |> int = squareRoot * squareRoot then
        printfn "%d" startIndex
    //count <- count - 1

let FindLucasPyramid (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | WorkerMsg(index,k,selfid,bossRef) ->  checkLucasSquarePyramid index k
                                                bossRef <! EndMsg(index,selfid)
        | WorkerMsg2(start,endI,k,selfid,bossRef) ->    for i in start .. endI do
                                                            checkLucasSquarePyramid i k
                                                        bossRef <! EndMsg(start,selfid)
        // | EndMsg -> printfn "Actor queue empty"
        // | DispatcherMsg(N,k) -> printfn "Dispatcher"
    }
    loop()
let rnd = System.Random()
let Dispatcher (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | DispatcherMsg(N,k,selfRef) ->
                                        let workersList=[for a in 1 .. 10 do yield(spawn system ("Job" + (string a)) FindLucasPyramid)]
                                        let effort = N/9
                                        for i in 0 .. 9 do
                                            // let workerid = rnd.Next()%10
                                            // workersList.Item(workerid) <! WorkerMsg(i,k,workerid,selfRef)
                                            workersList.Item(i) <! WorkerMsg2( ((effort*i)+1) , effort*(i+1) , k , i , selfRef)


                                            
        | EndMsg(index,workerid) -> count <- count+1
                                    printfn "finished %i job by worker %i" index workerid
        //while count <> 0 do     
        //finish <- true
        return! loop()
    }
    loop()

let DispatcherRef = spawn system "Dispatcher" Dispatcher

DispatcherRef <! DispatcherMsg(N,k,DispatcherRef)
while count<>10 do
    //printfn "count %i" count
//while not finish do
// let WorkersList=[for a in 1 .. 10 do yield(spawn system ("Job" + (string a)) FindLucasPyramid)]
// for i in 1 .. N do
//     WorkersList.Item(i%10) <! WorkerMsg(i,k)

// let interval = TimeSpan 1000L
// let tasklist= [for i in 0 .. 9 do yield(WorkersList.Item(i).GracefulStop interval )]
// for i in 0 .. 9 do
//     let temp = WorkersList.Item(i).GracefulStop interval
//     printfn "%s" (string temp.Status)

// // for i in 0 .. 9 do
//     WorkersList.Item(i) <! EndMsg

//Thread.Sleep(10000)
system.Terminate()



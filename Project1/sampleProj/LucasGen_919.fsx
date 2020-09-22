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
let coreCount = System.Environment.ProcessorCount
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

//let sumOfSquares n = timesn n >> timesn (add1 n) >> (times2>>add1) >> divideby6

let sumOfSquares n = ((2L * n * n *n)+(3L * n * n) + n)/6L

// let checkLucasSquarePyramid startIndex k =
//     let temp=startIndex
//     let total = sumOfSquares (startIndex+k-1L) (startIndex+k-1L) - sumOfSquares (startIndex-1L) (startIndex-1L)
//     let squareRoot = total |> double |> sqrt |> int64
//     if total = squareRoot * squareRoot then
//         printfn "%i" temp

let checkLucasSquarePyramid startIndex k =
    let mutable sum = 0L
    for i in startIndex|>int64 .. startIndex+k-1L do
        sum <- sum + (i * i)
    //let sum = sumOfSquares (startIndex+k-1L) - sumOfSquares (startIndex-1L)
    //printfn "%i   %i" startIndex sum
    let mutable num = startIndex+k
    while (num*num) < sum do
        num <- num+1L
    if (num * num)=sum then 
        printfn "%d" startIndex
    // let squareRoot = sum |> double |> sqrt |> int64
    // //printfn "%i %i %f %d " startIndex sum squareRoot (int squareRoot)
    // if sum = squareRoot * squareRoot then
    //     printfn "%d" startIndex
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
    }
    loop()

// type watchClass = 
//     interface ICanWatch with
//         member this.Watch(a)=a

let Dispatcher (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | DispatcherMsg(N,k,selfRef) ->
                                        let workersList=[for a in 1 .. 100 do yield(spawn system ("Job" + (string a)) FindLucasPyramid)]
                                        let effort = N/99L
                                        for i in 0L .. 99L do
                                            workersList.Item(i|>int) <! WorkerMsg2( ((effort*i)+1L) , effort*(i + 1L) , k , i|>int , selfRef)
                                            //let interval = TimeSpan 10000L
                                            // let x = watchClass()
                                            // (x:>ICanWatch).Watch(workersList.Item(i|>int))
                                            
                                            
        | EndMsg(index,workerid) -> count <- count+1
                                    if count = 100 then
                                        mailbox.Context.System.Terminate() |> ignore
                                        //system.Terminate()
                                    //printfn "finished %i job by worker %i" index workerid
        //while count <> 0 do     
        //finish <- true
        return! loop()
    }
    loop()

let DispatcherRef = spawn system "Dispatcher" Dispatcher

DispatcherRef <! DispatcherMsg(N,k,DispatcherRef)

//while count <> 10 do

//system.Terminate()

system.WhenTerminated.Wait()




#time "on"
#r "nuget: Akka.FSharp" 

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp 
//command line arguments
let N = fsi.CommandLineArgs.[1] |> int64
let k = fsi.CommandLineArgs.[2] |> int64
let mutable count=0L //to keep track of the workers
let workers = 8L
//let workers = System.Environment.ProcessorCount |> int64
//let workers = fsi.CommandLineArgs.[3] |> int64
//creating an actor system
let system = ActorSystem.Create("Master")

//union of messages to an actor
type ActorMsg =
    | WorkerMsg of int64*int64*int64*int64
    | DispatcherMsg of int64*int64
    | EndMsg of int64*int64

//function to check the Lucas square pyramid from start index
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
        printfn "%d" startIndex

//worker actor
let FindLucasPyramid (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | WorkerMsg(start,endI,k,offset) -> for i in start .. workers .. endI do
                                                        //printfn "Worker %i received job %i" offset (i+offset)
                                                checkLucasSquarePyramid (i+offset) k 
                                            mailbox.Sender()<! EndMsg(start,offset) //send back the finish message to boss
        | _ -> printfn "Worker Received Wrong message"
    }
    loop()

//boss actor- it distrbutes the tasks to workers
let Dispatcher (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | DispatcherMsg(N,k) ->
                                let workersList=[for a in 1L .. workers do yield(spawn system ("Job" + (string a)) FindLucasPyramid)] //creating workers
                                let effort = N/(workers-1L)
                                for i in 0L .. (workers-1L) do //distributing work to the workers
                                    workersList.Item(i|>int) <! WorkerMsg( 1L, N , k , i) //sending message to worker
                                            
        | EndMsg(index,workerid) -> count <- count+1L //recieves end msg from worker
                                    //printfn "Worker %i finished its job" workerid
                                    if count = workers then //checking if all workers have already sent the end message
                                        mailbox.Context.System.Terminate() |> ignore //terminating the actor system
        | _ -> printfn "Dispatcher Received Wrong message"
        return! loop()
    }
    loop()
//creating boss actor
let DispatcherRef = spawn system "Dispatcher" Dispatcher

//sending message to boss actor
DispatcherRef <! DispatcherMsg(N,k)
//waiting for boss actor to terminate the actor system
system.WhenTerminated.Wait()




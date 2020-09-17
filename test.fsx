// ActorSayHello.fsx
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
// #load "Bootstrap.fsx"

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

let system = ActorSystem.Create("system")

type processorMessage = ProcessJob of int*int*int
type squareGenMessage = SquareGenJob of int
type lucasGenMessage = LucasGenJob of int*int

let square x = 
    //printfn "%i square is %i" x x*x
    x*x

let checkLucas ind k : bool=
    let mutable sum = 0
    //printfn "%i index k %i" ind k
    for i=ind to ind+k-1 do
        sum <- sum + (i * i)
        
    let squareroot = sqrt (float sum)
    //printfn "%i \n" sum
    int sum = int squareroot * int squareroot

let processor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! ProcessJob(x,y,z) = mailbox.Receive ()
        printfn "Message received  %i %i %i " x y z
        return! loop ()
    }
    loop ()

let squareGen (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! SquareGenJob(message)= mailbox.Receive()
        let result = square message
        printfn "%i square is %i \n" message result
        return! loop()
    }
    loop()

let printresref = spawn system "printres"  (actorOf (fun msg -> printfn "%i" msg))

let lucasGen (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! LucasGenJob(index, k)= mailbox.Receive()
        let result = checkLucas index k
        if result = true then
            printresref <! index
        return! loop()
    }
    loop()

let processorRef = spawn system "processor" processor

let squareGenRef = spawn system "squareGen" squareGen

let lucasGenRef1 = spawn system "lucasGen1" lucasGen

let lucasGenRef2 = spawn system "lucasGen2" lucasGen

let lucasGenRef3 = spawn system "lucasGen3" lucasGen



// processorRef <! ProcessJob(1,3,5)

// squareGenRef <! SquareGenJob(25)
let int2String (x: int) = string x
let l = seq{1 .. 10000-4}
let k= 20
for i in l do
    if i%3 = 0 then
        lucasGenRef1 <! LucasGenJob(i,k)
    else if i%3 = 1 then
        lucasGenRef2 <! LucasGenJob(i,k)
    if i%3 = 2 then
        lucasGenRef3 <! LucasGenJob(i,k)
    // let res = checkLucas i k
    // printfn "%b" res
    

Thread.Sleep(1000)

//let res = checkLucas 3 2

//printfn "%b" res

//let square25 = square 25000

//printfn "%d" square25
system.Terminate()
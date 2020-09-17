#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

let system = ActorSystem.Create("system")

type lucasGenMessage = LucasGenJob of int*int

let square x = x*x

let checkLucas ind k : bool=
    let mutable sum = 0
    for i=ind to ind+k-1 do
        sum <- sum + (i * i)
    let squareroot = sqrt (float sum)
    int sum = int squareroot * int squareroot

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

let lucasGenRef1 = spawn system "lucasGen1" lucasGen

let lucasGenRef2 = spawn system "lucasGen2" lucasGen

let lucasGenRef3 = spawn system "lucasGen3" lucasGen

let l = seq{1 .. 10000-4}
let k= 20
for i in l do
    if i%3 = 0 then
        lucasGenRef1 <! LucasGenJob(i,k)
    else if i%3 = 1 then
        lucasGenRef2 <! LucasGenJob(i,k)
    if i%3 = 2 then
        lucasGenRef3 <! LucasGenJob(i,k)


printfn "Sanjay"
 
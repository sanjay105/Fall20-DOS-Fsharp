// Learn more about F# at http://fsharp.org

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
//open Akka.TestKit

let system = ActorSystem.Create("system")

type lucasGenMessage = LucasGenJob of int*int

let square x = x*x

let checkLucas ind k : bool=
    let mutable sum = 0
    for i=ind to ind+k-1 do
        sum <- sum + (i * i)
    let squareroot = sqrt (float sum)
    int sum = int squareroot * int squareroot


let lucasGen (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! LucasGenJob(index, k)= mailbox.Receive()
        let result = checkLucas index k
        if result then
            let printresref = spawn system ("print"+(string index)) (actorOf (fun msg -> printfn "%i" msg))
            printresref <! index
        return! loop()
    }
    loop()

let runup total k =
    let l = seq{1 .. total-k}
    let list1=[for a in 1 .. total/10 do yield(spawn system (string a) lucasGen)]
    for i in l do
        list1.Item(i%(total/10)) <! LucasGenJob(i,k)
        //lucasGenRef1 <! LucasGenJob(i,k)
    0



[<EntryPoint>]
let main argv =
    printfn "Hello World from F#! %A" argv
    let x=runup (int argv.[0]) (int argv.[1])
    0 // return an integer exit code

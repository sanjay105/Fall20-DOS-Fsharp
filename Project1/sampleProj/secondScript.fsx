#time "on"
#r "nuget: Akka.FSharp" 

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let system = ActorSystem.Create("Master")

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
        //printfn "Job %i started" index
        let result = checkLucas index k
        if result then
            //let printresref = spawn system ("print"+(string index)) (actorOf (fun msg -> printfn "%i" msg))
            //printresref <! index
            printfn "%i" index
        return! loop()
    }
    loop()


let total = fsi.CommandLineArgs.[1] |> int
let k = fsi.CommandLineArgs.[2] |> int
// let total=100000
// let k= 2
let l = seq{1 .. total-k}
printfn "%i %i" total k 
let list1=[for a in 1 .. 10 do yield(spawn system (string a) lucasGen)]
for i in l do
    //printfn "%i" i
    // let actorRef=spawn system ("calculate"+(string i)) lucasGen
    // actorRef<!LucasGenJob(i,k)
    list1.Item(i%(10)) <! LucasGenJob(i,k)

//system.Serialization

System.Console.ReadLine() |> ignore
system.Terminate()
printfn "%A" fsi.CommandLineArgs
open System.Threading
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp



let rnd = System.Random()
let serverip = fsi.CommandLineArgs.[1] |> string
let serverport = fsi.CommandLineArgs.[2] |>string
let simid = "Simulator1"
let N = fsi.CommandLineArgs.[3] |> int
//"akka.tcp://RemoteFSharp@localhost:8777/user/server"
let addr = "akka.tcp://TwitterEngine@" + serverip + ":" + serverport + "/user/RequestHandler"

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

let system = ActorSystem.Create("TwitterClient", configuration)
let twitterServer = system.ActorSelection(addr)
// let N = 100
let mutable activeStatus = Array.create N false

let Print (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        printfn "%s" msg
        return! loop()
    }
    loop()

let mutable workersList = []
let hashtags = [|"sanjay";"DOS";"COP5615";"UF";"Gainesville";"#AKKA";"#FSharp";"#twitter";"#Fall2020";"#deepthi";|]

let printRef = spawn system "Print" Print

let addSubscriber rank (workers: list<IActorRef>) =
    for i in 0 .. rank-1 do
        twitterServer <! "Subscribe|"+workers.[rank-1].Path.Name+"|"+workers.[i].Path.Name

let sendTweetToServer (myref: IActorRef) rank (workers: list<IActorRef>) login= 
    let mutable retweet = false
    let mutable nlogin = login
    let tmp = rnd.Next(0,100)
    if tmp%50 = 25 then
        if login then
            nlogin <- false
            twitterServer <! "Logout|"+myref.Path.Name+"|"
        else
            nlogin <- true
            twitterServer <! "Login|"+myref.Path.Name+"|"
    elif tmp = 31 then
        twitterServer <! "GetTweets|"+myref.Path.Name+"|"
    elif tmp = 32 then
        twitterServer <! "GetMentions|"+myref.Path.Name+"|"
    elif tmp = 33 then
        twitterServer <! "GetHashTags|"+myref.Path.Name+"|"+hashtags.[rnd.Next(0,10)]+"|"
    else
        if tmp%10 =0 then
            retweet <- true
        if retweet then
            let tweetmsg = "ReTweet|"+myref.Path.Name+"|"
            twitterServer <! tweetmsg
        else
            let tweetmsg = "Tweet|"+myref.Path.Name+"|" + "Hello @"+workers.[rnd.Next(0,N)].Path.Name+" "+ hashtags.[rnd.Next(0,10)]    
            twitterServer <! tweetmsg
    let tspan = rank|>int
    system.Scheduler.ScheduleTellOnce(tspan ,myref,"TweetSch",myref)
    nlogin

type SimulatorMsg =
    | Start
    | StartAck
    | SubsAck
    

let Client (mailbox:Actor<_>)=
    let mutable sim = null
    let mutable serveruserid = ""
    let mutable login = true
    let mutable cid = 0
    let mutable rank = 0
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        // printRef <! msg 
        let response =msg|>string
        
        let command = (response).Split '|'
        if command.[0].CompareTo("initZipf") = 0 then 
            rank <- command.[1] |> int
            twitterServer<!"Register|"+mailbox.Self.Path.Name
            twitterServer<!"Login|"+mailbox.Self.Path.Name
            sim <- mailbox.Sender()
            // Add scheduler
            // system.Scheduler.ScheduleTellOnce()
        elif command.[0].CompareTo("Ack") = 0 then
            if command.[1].CompareTo("Register") = 0 then
                sim <! StartAck
            elif command.[1].CompareTo("Subscribe") = 0 then
                sim <! SubsAck

        elif command.[0].CompareTo("AddSubscribers") = 0 then
            addSubscriber rank workersList
            // sendTweetToServer mailbox.Self rank workersList
        elif command.[0].CompareTo("TweetSch") = 0 then
            cid <- cid + 1
            login <- sendTweetToServer mailbox.Self rank workersList login
        elif command.[0].CompareTo("QUIT") = 0 then
            mailbox.Context.System.Terminate() |> ignore 
        // elif command.[0].CompareTo("GetResponse") = 0 then
        //     printfn "%A" msg
        
        elif command.[0].CompareTo("GetResponse") = 0 then

            printfn "%A" msg
        return! loop()
    }
    loop()


workersList <- [for a in 1 .. N do yield(spawn system (simid + "_Client_" + (string a)) Client)]



let Simulator (mailbox:Actor<_>)=
    let mutable startackcnt = 0
    let mutable tweetsack = 0
    let mutable subackcnt = 0
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | Start ->      for i in 0 .. N-1 do
                            workersList.[i] <! "initZipf|"+(string (i+1))
        | StartAck ->   startackcnt <- startackcnt + 1
                        // printfn "%A" startackcnt
                        if startackcnt = N then
                            printfn "Initialization Done"
                            for i in 0 .. N-1 do
                                workersList.[i] <! "AddSubscribers|"
        | SubsAck ->    subackcnt <- subackcnt + 1
                        if subackcnt = N then
                            printfn "Subscribe activities Done"
                            for i in 0 .. N-1 do
                                workersList.[i] <! "TweetSch|"
         
        return! loop();
    }
    loop()

let simRef = spawn system "simulator" Simulator

simRef <! Start


// let ZipfDistributuion = 
//     for i in 0 .. N-1 do
//         workersList.[i] <! "initZipf|"+(string (i+1))
//     Thread.Sleep(2000)
//     for i in 0 .. N-1 do
//         workersList.[i] <! "AddSubs"
//     Thread.Sleep(10000)
//     workersList.[0] <! "GetTweets"
//     Thread.Sleep(10000)
//     workersList.[10] <! "GetMentions"
//     Thread.Sleep(10000)
//     workersList.[20] <! "GetHashTags"
    

system.WhenTerminated.Wait()


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
let simid = fsi.CommandLineArgs.[3] |>string
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
                    hostname = 192.168.0.96
                }
            }
        }")

let system = ActorSystem.Create("TwitterClient", configuration)
let twitterServer = system.ActorSelection(addr)
let N = 100
let mutable activeStatus = Array.create N false
type SimulatorMsg =
    | Start
    | ACK
    | SimulateTweets
    | Done
    | ACKSIMTWEET
    | ACKSIMSUBSCRIBE
    | Reports

let Print (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        printfn "%s" msg
        return! loop()
    }
    loop()

let mutable workersList = []
let hashtags = [|"sanjay";"DOS";"COP5615";"UF";"Gainesville"|]

let printRef = spawn system "Print" Print

let addSubscriber rank (workers: list<IActorRef>) =
    for i in 0 .. rank-1 do
        twitterServer <! "Subscribe|"+workers.[rank-1].Path.Name+"|"+workers.[i].Path.Name

let sendTweetToServer (myref: IActorRef) rank (workers: list<IActorRef>) = 
    let tweetmsg = "Tweet|"+myref.Path.Name+"|" + "Hello @"+workers.[rnd.Next(0,N)].Path.Name+" #"+ hashtags.[rnd.Next(0,5)]    
    twitterServer <! tweetmsg
    let tspan = (rank|>int) * 10
    system.Scheduler.ScheduleTellOnce(tspan ,myref,"TweetSch",myref)



let Client (mailbox:Actor<_>)=
    let mutable sim = null
    let mutable serveruserid = ""
    let mutable login = false
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
            // Add scheduler
            // system.Scheduler.ScheduleTellOnce()
        elif command.[0].CompareTo("AddSubs") = 0 then
            addSubscriber rank workersList
            sendTweetToServer mailbox.Self rank workersList
        elif command.[0].CompareTo("TweetSch") = 0 then
            sendTweetToServer mailbox.Self rank workersList
        elif command.[0].CompareTo("QUIT") = 0 then
            mailbox.Context.System.Terminate() |> ignore 


        // elif command.[0].CompareTo("Register")=0 then
        //     cid <- command.[1] |> int
        //     sim <- mailbox.Sender()
        //     twitterServer <! "Register|"+mailbox.Self.Path.Name
        // elif command.[0].CompareTo("Login") = 0 then
        //     twitterServer <! "Login|"+serveruserid
        // elif command.[0].CompareTo("Logout") = 0 then
        //     twitterServer <! "Logout|"+serveruserid
        // elif command.[0].CompareTo("ACK")=0 then
        //     if command.[1].CompareTo("Register") = 0 then
        //         serveruserid <- command.[2]
        //         sim <! ACK
        //     elif command.[1].CompareTo("Login") = 0 then
        //         printRef <! "User "+serveruserid+" Logged in" 
        //         activeStatus.[cid] <- true
        //     elif command.[1].CompareTo("Logout") = 0 then
        //         printRef <!  "User "+serveruserid+" Logged out" 
        //         activeStatus.[cid] <- false
        //     elif command.[1].CompareTo("Tweet") = 0 then
        //         sim <! ACKSIMTWEET
        //     elif command.[1].CompareTo("Subscribe") = 0 then
        //         sim <! ACKSIMSUBSCRIBE
        //     // system.Terminate() |> ignore
        // elif command.[0].CompareTo("Tweet")=0 then
        //     let tweetmsg = "Tweet|"+mailbox.Self.Path.Name+"|"+"Hello @"+command.[1]+" #"+command.[2]
            
        //     twitterServer <! tweetmsg
        //     // if rnd.Next(0,5) = 2 then
        //     //     mailbox.Self <! "Logout|"
        // elif command.[0].CompareTo("Subscribe") = 0 then
        //     let req = "Subscribe|"+mailbox.Self.Path.Name+"|"+command.[1]
        //     twitterServer <! req
        // elif command.[0].CompareTo("GetTweets") = 0 then
        //     let req = "GetTweets|"+mailbox.Self.Path.Name
        //     twitterServer <! req
        // elif command.[0].CompareTo("GetMentions") = 0 then
        //     let req = "GetMentions|"+mailbox.Self.Path.Name
        //     twitterServer <! req
        // elif command.[0].CompareTo("Response") = 0 then
        //     if command.[1].CompareTo("GetTweets") = 0 then
        //         printRef <! (mailbox.Self.Path.Name+"\n"+command.[2]+"\n")
        //     elif command.[1].CompareTo("GetMentions") = 0 then
        //         printfn "%s" command.[2]
        //         printRef <! (mailbox.Self.Path.Name+"\n"+command.[2]+"\n")

        
        // else
        //     printRef <! msg
        return! loop()
    }
    loop()


workersList <- [for a in 1 .. N do yield(spawn system (simid + "_Client_" + (string a)) Client)]



let Simulator (mailbox:Actor<_>)=
    let mutable ackcnt = 0
    let mutable tweetsack = 0
    let mutable suback = 0
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | Start ->  for i in 0 .. N-1 do
                        workersList.[i] <! "Register|"+(i|>string)
                    
        | ACK ->    ackcnt <- ackcnt + 1
                    if ackcnt = N then
                        printRef <! "Total ACK Received"
                        mailbox.Self <! SimulateTweets
        | ACKSIMTWEET ->    tweetsack <- tweetsack + 1
                            printRef <!  ((tweetsack/2)+suback)
                            if (tweetsack/2)+suback = N*10 then
                                mailbox.Self <! Reports
        | ACKSIMSUBSCRIBE ->    suback <- suback + 1
                                printRef <!  ((tweetsack/2)+suback)
                                if (tweetsack/2)+suback = N*10 then
                                    mailbox.Self <! Reports
        | SimulateTweets -> for i in 0 .. N-1 do
                                workersList.[i]<!"Login|"
                            for i in 1 .. 10*N do
                                let n = rnd.Next(0,N)
                                // if activeStatus.[n] then
                                if n%5 = 0 then
                                    let ind = rnd.Next(0,N)
                                    let t = rnd.Next(0,N)
                                    let temp = workersList.[t].Path.Name
                                    workersList.[ind] <! "Subscribe|"+temp
                                else
                                    let ind = rnd.Next(0,N)
                                    let t = rnd.Next(0,N)
                                    let temp = workersList.[t].Path.Name
                                    workersList.[ind] <! "Tweet|"+temp+"|"+hashtags.[t%5]

        | Reports ->
                        for i in 1 .. N do
                            workersList.[i-1] <! "GetMentions|"
                        for i in 1 .. N do
                            workersList.[i-1] <! "GetTweets|"
                        mailbox.Self <! Done
        | Done ->   
                    twitterServer <! "BackupDB|"
                    twitterServer <! "Done|"
                    Thread.Sleep(60000)
                    mailbox.Context.System.Terminate() |> ignore 
        return! loop();
    }
    loop()

let simRef = spawn system "simulator" Simulator

// simRef <! Start


let ZipfDistributuion = 
    for i in 0 .. N-1 do
        workersList.[i] <! "initZipf|"+(string (i+1))
    Thread.Sleep(1000)
    for i in 0 .. N-1 do
        workersList.[i] <! "AddSubs"

system.WhenTerminated.Wait()


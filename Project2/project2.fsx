
#r "nuget: Akka"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Remote"

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

let rnd = System.Random(1)
let system = ActorSystem.Create("Gossip-Protocol")
let mutable nodecount = fsi.CommandLineArgs.[1] |> int
let topology = fsi.CommandLineArgs.[2] |> string
let algo = fsi.CommandLineArgs.[3] |> string
let gossipreceivelimit = 10
let gossipsendlimit = 1000


let timer = System.Diagnostics.Stopwatch()

type DispatcherMsg =
    | InitTopology of string
    | TopologyBuildDone
    | NodeExhausted
    | PrintPsumResults

type TopologyMsg =
    | BuildTopology of string*list<IActorRef>
    | BuildDone

type NodeMsg =
    | AddTopology of string*IActorRef*list<IActorRef>
    | Rumor
    | Spread
    | Exhausted

type PNodeMsg =
    | AddTopologyP of string*IActorRef*list<IActorRef>
    | Send
    | Receive of float*float
    | Forward of float*float
    | ExhaustedP
    | PrintRatio



let Topology(mailbox:Actor<_>)=
    let mutable recorddone=0
    let mutable dispatcherref = null
    let rec loop() =actor{
        let! message = mailbox.Receive()
        //printfn "%A" message
        match message with
        | BuildTopology(topology,nodelist)->    let mutable nlist=[]
                                                dispatcherref<-mailbox.Sender()
                                                if(topology="line") then
                                                    for i in 0 .. nodecount-1 do
                                                        nlist <- []
                                                        if i <> 0 then
                                                            nlist <- nlist @ [nodelist.Item(i-1)]
                                                        if i <> nodecount-1 then
                                                            nlist <- nlist @ [nodelist.Item(i+1)]
                                                        if algo = "gossip" then
                                                            nodelist.Item(i)<!AddTopology(topology,dispatcherref,nlist)
                                                        else
                                                            nodelist.Item(i)<!AddTopologyP(topology,dispatcherref,nlist)
                                                elif topology="full" then
                                                    for i in 0 .. nodecount-1 do
                                                        //nodelist.Item(i)<!AddTopology(topology,dispatcherref,[])
                                                        if algo = "gossip" then
                                                            nodelist.Item(i)<!AddTopology(topology,dispatcherref,[])
                                                        else
                                                            nodelist.Item(i)<!AddTopologyP(topology,dispatcherref,[])
                                                elif topology="2D" || topology="imp2D" then
                                                    let n= sqrt(nodecount|>float) |> int
                                                    nodecount<-n*n
                                                    for i in 0 .. nodecount-1 do
                                                        nlist <- []
                                                        if i%n <> 0 then
                                                            nlist <- nlist @ [nodelist.Item(i-1)]
                                                        if i%n <> n-1 then
                                                            nlist <- nlist @ [nodelist.Item(i+1)]
                                                        if i/n <> 0 then 
                                                            nlist <- nlist @ [nodelist.Item(i-n)]
                                                        if i/n <> n-1 then 
                                                            nlist <- nlist @ [nodelist.Item(i+n)]
                                                        if topology = "imp2D" then 
                                                            nlist <- nlist @ [nodelist.Item(rnd.Next()%nodecount)]
                                                        //nodelist.Item(i)<!AddTopology(topology,dispatcherref,nlist)
                                                        if algo = "gossip" then
                                                            nodelist.Item(i)<!AddTopology(topology,dispatcherref,nlist)  
                                                        else
                                                            nodelist.Item(i)<!AddTopologyP(topology,dispatcherref,nlist)                                             
        | BuildDone ->  
                        if recorddone=nodecount-1 then
                            dispatcherref<!TopologyBuildDone
                        recorddone<-recorddone+1
                        
        return! loop()
    }
    loop()
let topologyref= spawn system "topology" Topology
let mutable Nodelist = []
let Node(mailbox:Actor<_>)=
    let mutable neigbhours=[]
    let mutable rumorheard = 0
    let mutable hadrumor = false
    let mutable spreadcnt = 0
    let mutable nodetopology=""
    let mutable exhausted = false
    let mutable nexhausted = 0
    let mutable dispatcherref = null
    let id = mailbox.Self.Path.Name |> int
    let rec loop() =actor{
        let! message = mailbox.Receive()
        //printfn "%A %i" message id
        match message with
        | AddTopology(topology,dref,nodelist)->
                                                neigbhours<-nodelist
                                                nodetopology<-topology
                                                dispatcherref<-dref
                                                mailbox.Sender()<!BuildDone
        | Rumor ->  
                    if not exhausted then
                        if rumorheard = 0 then
                            hadrumor <- true
                        rumorheard<-rumorheard+1
                        if rumorheard = gossipreceivelimit then 
                            exhausted <- true
                            dispatcherref<!NodeExhausted
                            if topology = "full" then
                                for i in 0 .. nodecount-1 do 
                                    if i <> id then
                                        Nodelist.Item(i)<!Exhausted
                            else
                                for i in 0 .. neigbhours.Length-1 do
                                    neigbhours.Item(i)<!Exhausted
                        else
                            mailbox.Self<!Spread
                        
        | Spread -> //printfn "Spread %i exhausted %b" id exhausted
                    if not exhausted then
                        let mutable next=rnd.Next()
                        if topology = "full" then
                            while next%nodecount=id do
                                    next<-rnd.Next()
                            Nodelist.Item(next%nodecount)<!Rumor
                        else
                            neigbhours.Item(next%neigbhours.Length)<!Rumor
                        spreadcnt <- spreadcnt + 1
                        if spreadcnt = gossipsendlimit then
                            exhausted <- true
                            dispatcherref<!NodeExhausted
                            if topology = "full" then
                                for i in 0 .. nodecount-1 do 
                                    if i <> id then
                                        Nodelist.Item(i)<!Exhausted
                            else
                                for i in 0 .. neigbhours.Length-1 do
                                    Nodelist.Item(i)<!Exhausted
                        else
                            mailbox.Self<!Spread

        | Exhausted ->  if not hadrumor then
                            mailbox.Self<!Rumor
                        if not exhausted then
                            nexhausted <- nexhausted + 1
                            if topology = "full" then 
                                if nexhausted = nodecount-1 then 
                                    exhausted <- true
                                    dispatcherref<!NodeExhausted
                            else
                                if nexhausted = neigbhours.Length then
                                    exhausted <- true
                                    dispatcherref<!NodeExhausted
        return! loop()
    }
    loop()


let PsumNode(mailbox:Actor<_>)=
    let mutable neigbhours=[]
    let mutable nodetopology=""
    let mutable exhausted = false
    let mutable nexhausted = 0
    let mutable ecnt = 0
    let mutable dispatcherref = null
    let id = mailbox.Self.Path.Name |> int
    let mutable s = id|>float
    let mutable w = 1.0
    let rec loop() =actor{
        let! message = mailbox.Receive()
        // printfn "%A %i" message id
        match message with
        | AddTopologyP(topology,dref,nodelist)->
                                                neigbhours<-nodelist
                                                nodetopology<-topology
                                                dispatcherref<-dref
                                                mailbox.Sender()<!BuildDone
        | Receive(rs,rw) ->  
                            if not exhausted then
                                let ns = s+rs
                                let nw = w+rw
                                if abs((ns/nw)-(s/w))<0.0000000001 then
                                    ecnt <- ecnt + 1
                                else
                                    ecnt <- 0
                                if ecnt = 3 then 
                                    exhausted <- true
                                    dispatcherref<!NodeExhausted
                                    if topology = "full" then
                                        for i in 0 .. nodecount-1 do 
                                            if i <> id then
                                                Nodelist.Item(i)<!ExhaustedP
                                    else
                                        for i in 0 .. neigbhours.Length-1 do
                                            neigbhours.Item(i)<!ExhaustedP
                                    mailbox.Self<!Forward(rs,rw)
                                else
                                    s <- ns
                                    w <- nw
                                    mailbox.Self<!Send
                            else
                                mailbox.Self<!Forward(rs,rw)
        
        | Forward(rs,rw) -> let mutable next=rnd.Next()
                            if topology = "full" then
                                while next%nodecount=id do
                                        next<-rnd.Next()
                                Nodelist.Item(next%nodecount)<!Receive(rs,rw)
                            else
                                neigbhours.Item(next%neigbhours.Length)<!Receive(rs,rw)
                        
        | Send ->
                    if not exhausted then
                        let mutable next=rnd.Next()
                        s <- s/2.0
                        w <- w/2.0
                        if topology = "full" then
                            while next%nodecount=id do
                                    next<-rnd.Next()
                            Nodelist.Item(next%nodecount)<!Receive(s,w)
                        else
                            neigbhours.Item(next%neigbhours.Length)<!Receive(s,w)

        | ExhaustedP -> 
                        if not exhausted then
                            nexhausted <- nexhausted + 1
                            if topology = "full" then 
                                if nexhausted = nodecount-1 then 
                                    exhausted <- true
                                    dispatcherref<!NodeExhausted
                            else
                                if nexhausted = neigbhours.Length then
                                    exhausted <- true
                                    dispatcherref<!NodeExhausted
        | PrintRatio -> printfn "Node: %i ratio: %f" id (s/w)
        return! loop()
    }
    loop()



if algo = "gossip" then
    Nodelist <- [for a in 0 .. nodecount-1 do yield(spawn system (string a) Node)] 
else
    Nodelist <- [for a in 0 .. nodecount-1 do yield(spawn system (string a) PsumNode)] 



let Dispatcher(mailbox:Actor<_>)=
    let mutable spread = 0  
    let rec loop() =actor{
        let! message = mailbox.Receive()
        //printfn "%A" message
        match message with
        | InitTopology(topology) ->     
                                        topologyref <! BuildTopology(topology,Nodelist)
        | TopologyBuildDone ->  
                                if algo = "gossip" then
                                    Nodelist.Item(rnd.Next()%nodecount)<!Rumor
                                    timer.Start()
                                else
                                    let ind = rnd.Next()%nodecount |> float
                                    Nodelist.Item(rnd.Next()%nodecount)<!Receive(ind,1.0)
                                    timer.Start()

        | NodeExhausted ->  spread <- spread + 1
                            if spread = nodecount then 
                                mailbox.Context.System.Terminate() |> ignore
                                printfn "%s,%s,%i,%i" algo topology nodecount timer.ElapsedMilliseconds
        | PrintPsumResults ->   for i in 0 .. nodecount-1 do
                                    Nodelist.Item(i)<!PrintRatio
                                

                                
        return! loop()
    }
    loop()

let Dispatcherref = spawn system "Dispatcher" Dispatcher  

Dispatcherref<!InitTopology(topology)

system.WhenTerminated.Wait()
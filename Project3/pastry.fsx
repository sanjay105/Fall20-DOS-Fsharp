#time "on"
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

let mutable nodecount = fsi.CommandLineArgs.[1] |> int
let mutable numRequests = fsi.CommandLineArgs.[2] |> int
let mutable b = 4
let mutable KeyLen = 7 
let rnd = System.Random()
let rnd1 = System.Random()
let MaxValPossible = Math.Pow(2.0,(b*KeyLen)|>float)|>int
let SetSize = Math.Pow(2.0,b|>float)|>int

let system = ActorSystem.Create("Pastry")

type NodeMsg =
    | Init of int
    | Join of IActorRef*string
    | Print
    | Update of Set<string>*Set<string>*array<string>*int*list<IActorRef>*string*string
    | SendMessage
    | ForwardMessage of string*int*IActorRef
    | ACK of int
    | FindKey

type BossMsg =
    | InitDone
    | Start
    | JoinDone of string
    | RequestsDone of float*float

let mutable Nodelist = []

let PrintAct(mailbox:Actor<_>)=
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        printfn "%A" msg
        return! loop()
    }
    loop()

let printref = spawn system "print" PrintAct


let mutable temp = Map.empty
let mutable temp2 = []

let genNodeId x =
    // Generates Unique NodeID for each Node
    let mutable num = rnd.Next(1,MaxValPossible)
    while temp.TryFind num <> None do
        num <- rnd.Next(1,MaxValPossible)
    temp <- temp.Add(num,true)
    let res = System.Convert.ToString(num,SetSize)
    let key = res.PadLeft(KeyLen,'0')
    temp2 <- temp2@[key]
    key

let commonPrefix (a:string) (b:string) : int=
    // Returns the size of common prefix
    let mutable cnt = 0
    let mutable breakSt = false
    for i in 0 .. a.Length-1 do
        if (a.Chars(i)=b.Chars(i)) && ( not breakSt) then
            cnt <- cnt+1
        else
            breakSt <- true
    cnt

let hexDiff a b =
    // Returns difference between the two keys
    let x = Convert.ToInt32(a, SetSize)
    let y = Convert.ToInt32(b, SetSize)
    abs(x-y) 

let addToRoutingTable a b (table:string array array)=
    // Adds key into the routing table
    let prf = commonPrefix a b
    if prf <> KeyLen then
        let temp = a.Chars(prf)|>int
        if temp > 96 then 
            table.[prf].[temp-87] <- a
        else
            table.[prf].[temp-48] <- a

let broadcastRoutingInfo nodeID (smallerleafset:Set<string>) (largerleafset:Set<string>) (rtable:string array array) = 
    // Broadcasts it's routing table , leafset to all the nodes that it knows
    for i in smallerleafset do
        if i.Length <> 0 then
            let prf = commonPrefix nodeID i
            if prf <> KeyLen then
                system.ActorSelection("user/"+i)<!Update(smallerleafset,largerleafset,rtable.[prf],prf,[],nodeID,"")
    for i in largerleafset do
        if i.Length <> 0 then
            let prf = commonPrefix nodeID i
            if prf <> KeyLen then
                system.ActorSelection("user/"+i)<!Update(smallerleafset,largerleafset,rtable.[prf],prf,[],nodeID,"")
    for j in rtable do
        for i in j do
            if i.Length <> 0 then
                let prf = commonPrefix nodeID i
                if prf <> KeyLen then
                    system.ActorSelection("user/"+i)<!Update(smallerleafset,largerleafset,rtable.[prf],prf,[],nodeID,"")
    
let getNextAddress key nodeID (smallerleafset:Set<string>) (largerleafset:Set<string>) (rtable:string array) = 
    // returns the closest node to the key
    let mutable diff = hexDiff key nodeID
    let mutable res = nodeID
    let mutable sleafset = Set.empty
    sleafset <- smallerleafset
    sleafset<- sleafset.Add(nodeID)
    let mutable lleafset = Set.empty
    lleafset <- largerleafset
    lleafset<- lleafset.Add(nodeID)
    if key >= sleafset.MinimumElement && key <= lleafset.MaximumElement then
        // search nearest in leaf set
        for i in sleafset do
            let tmp = hexDiff key i
            if  tmp < diff then
                diff <- tmp
                res <- i
        for i in lleafset do
            let tmp = hexDiff key i
            if  tmp < diff then
                diff <- tmp
                res <- i
        res
    else
        // search for nearest node in routing table
        for k in rtable do
            if k.Length <> 0 then
                let mutable tmp = hexDiff key k
                if tmp < diff then
                    diff <- tmp
                    res <- k
        res

let Node(mailbox:Actor<_>)=
    // Node Actor
    let mutable neighbours = [] // Has the reference to the physically closest nodes
    let mutable smallerleafset = Set.empty // Has the reference to the smaller value nodes closer to this node
    let mutable largerleafset = Set.empty  // Has the reference to the larger value nodes closer to this node
    let mutable ackcnt = 0.0 // Keeps track of Acknowledgement received for the message sent 
    let mutable ackhpcnt = 0.0 // Keeps track of hops made to reach the quered key
    let mutable scnt = 0        // Keeps track of number of messages originated by the node
    let mutable routingtable = Array.create KeyLen [||] // Has the reference to the nodes in an organised manner 
    for i in 0 .. KeyLen-1 do
        routingtable.[i] <- Array.create (SetSize) ""
    let mutable nodeID = mailbox.Self.Path.Name  // Stores the key of the Current node
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        // printfn "%A" msg
        match msg with 
            | Init(id) ->   neighbours <- [Nodelist.[(nodecount+id-1)%nodecount];Nodelist.[(nodecount+id-2)%nodecount];Nodelist.[(id+1)%nodecount];Nodelist.[(id+2)%nodecount]]
                            mailbox.Sender()<!InitDone
            | Join(nref,key) ->     // Adds the new key into the network
                                    let prf = commonPrefix key nodeID
                                    if prf = KeyLen then
                                        nref <! Update(Set.empty,Set.empty,Array.empty,0,[],nodeID,"Done")
                                    else 
                                        if nodeID<key  then
                                            if largerleafset.Count < SetSize/2 then
                                                largerleafset <- largerleafset.Add(key)
                                            else
                                                let mutable toroutingtable = key
                                                if largerleafset.MaximumElement > key then
                                                    toroutingtable <- largerleafset.MaximumElement
                                                    largerleafset <- largerleafset.Remove(toroutingtable)
                                                    largerleafset <- largerleafset.Add(key)
                                                addToRoutingTable toroutingtable nodeID routingtable
                                        else
                                            if smallerleafset.Count < SetSize/2 then
                                                smallerleafset <- smallerleafset.Add(key)
                                            else
                                                let mutable toroutingtable = key
                                                if smallerleafset.MinimumElement < key then
                                                    toroutingtable <- smallerleafset.MinimumElement
                                                    smallerleafset <- smallerleafset.Remove(toroutingtable)
                                                    smallerleafset <- smallerleafset.Add(key)
                                                addToRoutingTable toroutingtable nodeID routingtable
                                        let nxt = getNextAddress key nodeID smallerleafset largerleafset routingtable.[prf]
                                        if nxt.CompareTo(nodeID)=0 then 
                                            nref<!Update(smallerleafset,largerleafset,routingtable.[prf],prf,neighbours,nodeID,"Done")
                                        else 
                                            system.ActorSelection("user/"+nxt)<!Join(nref,key)
                                            nref<!Update(smallerleafset,largerleafset,routingtable.[prf],prf,neighbours,nodeID,"")
            | Update(sleafset,lleafset,rtable,prefix,neighbours,key,status) ->  // Updates the leafsets, routing tables received from other nodes
                                                                                if nodeID < key then
                                                                                   if largerleafset.Count < SetSize/2 then
                                                                                            largerleafset <- largerleafset.Add(key)
                                                                                        else
                                                                                            let mutable toroutingtable = key
                                                                                            if largerleafset.MaximumElement > key then
                                                                                                toroutingtable <- largerleafset.MaximumElement
                                                                                                largerleafset <- largerleafset.Remove(toroutingtable)
                                                                                                largerleafset <- largerleafset.Add(key)
                                                                                            addToRoutingTable toroutingtable nodeID routingtable
                                                                                elif nodeID > key then
                                                                                    if smallerleafset.Count < SetSize/2 then
                                                                                            smallerleafset <- smallerleafset.Add(key)
                                                                                        else
                                                                                            let mutable toroutingtable = key
                                                                                            if smallerleafset.MinimumElement < key then
                                                                                                toroutingtable <- smallerleafset.MinimumElement
                                                                                                smallerleafset <- smallerleafset.Remove(toroutingtable)
                                                                                                smallerleafset <- smallerleafset.Add(key)
                                                                                            addToRoutingTable toroutingtable nodeID routingtable
                                                                                for i in sleafset do
                                                                                    if nodeID<i  then
                                                                                        if largerleafset.Count < SetSize/2 then
                                                                                            largerleafset <- largerleafset.Add(i)
                                                                                        else
                                                                                            let mutable toroutingtable = i
                                                                                            if largerleafset.MaximumElement > i then
                                                                                                toroutingtable <- largerleafset.MaximumElement
                                                                                                largerleafset <- largerleafset.Remove(toroutingtable)
                                                                                                largerleafset <- largerleafset.Add(i)
                                                                                            addToRoutingTable toroutingtable nodeID routingtable
                                                                                    elif nodeID > i then
                                                                                        if smallerleafset.Count < SetSize/2 then
                                                                                            smallerleafset <- smallerleafset.Add(i)
                                                                                        else
                                                                                            let mutable toroutingtable = i
                                                                                            if smallerleafset.MinimumElement < i then
                                                                                                toroutingtable <- smallerleafset.MinimumElement
                                                                                                smallerleafset <- smallerleafset.Remove(toroutingtable)
                                                                                                smallerleafset <- smallerleafset.Add(i)
                                                                                            addToRoutingTable toroutingtable nodeID routingtable
                                                                                for i in lleafset do
                                                                                    if nodeID<i  then
                                                                                        if largerleafset.Count < SetSize/2 then
                                                                                            largerleafset <- largerleafset.Add(i)
                                                                                        else
                                                                                            let mutable toroutingtable = i
                                                                                            if largerleafset.MaximumElement > i then
                                                                                                toroutingtable <- largerleafset.MaximumElement
                                                                                                largerleafset <- largerleafset.Remove(toroutingtable)
                                                                                                largerleafset <- largerleafset.Add(i)
                                                                                            addToRoutingTable toroutingtable nodeID routingtable
                                                                                    elif nodeID > i then
                                                                                        if smallerleafset.Count < SetSize/2 then
                                                                                            smallerleafset <- smallerleafset.Add(i)
                                                                                        else
                                                                                            let mutable toroutingtable = i
                                                                                            if smallerleafset.MinimumElement < i then
                                                                                                toroutingtable <- smallerleafset.MinimumElement
                                                                                                smallerleafset <- smallerleafset.Remove(toroutingtable)
                                                                                                smallerleafset <- smallerleafset.Add(i)
                                                                                            addToRoutingTable toroutingtable nodeID routingtable
                                                                                for j in rtable do
                                                                                    if j.CompareTo("") <> 0 then
                                                                                        let i = j
                                                                                        if nodeID<i  then
                                                                                            if largerleafset.Count < SetSize/2 then
                                                                                                largerleafset <- largerleafset.Add(i)
                                                                                            else
                                                                                                let mutable toroutingtable = i
                                                                                                if largerleafset.MaximumElement > i then
                                                                                                    toroutingtable <- largerleafset.MaximumElement
                                                                                                    largerleafset <- largerleafset.Remove(toroutingtable)
                                                                                                    largerleafset <- largerleafset.Add(i)
                                                                                                addToRoutingTable toroutingtable nodeID routingtable
                                                                                        elif nodeID > i then
                                                                                            if smallerleafset.Count < SetSize/2 then
                                                                                                smallerleafset <- smallerleafset.Add(i)
                                                                                            else
                                                                                                let mutable toroutingtable = i
                                                                                                if smallerleafset.MinimumElement < i then
                                                                                                    toroutingtable <- smallerleafset.MinimumElement
                                                                                                    smallerleafset <- smallerleafset.Remove(toroutingtable)
                                                                                                    smallerleafset <- smallerleafset.Add(i)
                                                                                                addToRoutingTable toroutingtable nodeID routingtable
                                                                                if status = "Done" then
                                                                                    broadcastRoutingInfo nodeID smallerleafset largerleafset routingtable
                                                                                    system.ActorSelection("user/Boss")<!JoinDone(nodeID)
                                                                                    mailbox.Self<!SendMessage
            | SendMessage ->    // Schedules the message to be sent
                                system.Scheduler.ScheduleTellOnce(TimeSpan(1000L),mailbox.Self,FindKey)
            | FindKey ->    // Generates a random key in the network
                            let key = temp2.[rnd1.Next(1,temp2.Length)]
                            mailbox.Self<!ForwardMessage(key,0,mailbox.Self)
                            scnt <- scnt + 1
                            if scnt < numRequests then
                                mailbox.Self <! SendMessage
            | ForwardMessage(key,hopcount,pref) ->  // Forwards the key to the closest node to the key with the information this node had
                                                    let pfx = commonPrefix key nodeID
                                                    if pfx <> KeyLen then 
                                                        let dst = getNextAddress key nodeID smallerleafset largerleafset routingtable.[pfx]
                                                        if dst = nodeID || dst = key then
                                                            pref<!ACK(hopcount)
                                                        else
                                                            system.ActorSelection("user/"+dst)<!ForwardMessage(key, hopcount+1 , pref)
                                                    else
                                                        pref<!ACK(hopcount)
            | ACK(hopcount) ->      // Handles the acknowledgement 
                                    ackcnt <- ackcnt + 1.0
                                    ackhpcnt <- ackhpcnt + (hopcount|>float)
                                    if ackcnt = (numRequests|>float) then
                                        // printfn "NodeID - %A done with sending messages Ack Received: %A and Total hops : %A with average hop ratio %A" nodeID ackcnt ackhpcnt (ackhpcnt/ackcnt)
                                        system.ActorSelection("user/Boss")<!RequestsDone(ackhpcnt/ackcnt,ackcnt)
            | Print -> printref<!("Node id",nodeID,"Smaller Leaf Set:",smallerleafset,"Larger Leaf Set:",largerleafset,"Routing Table:",routingtable) 
        return! loop()
    }
    loop()

let Dispatcher(mailbox:Actor<_>)=
    // Main actor which is responsible for adding the Nodes into network
    let mutable cnt = 0
    let mutable jcnt = 0
    let mutable rcnt = 0
    let mutable hopsum = 0.0
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
            | Start ->      
                            for i in 0 .. nodecount-1 do
                                Nodelist.Item(i)<!Init(i)
            | InitDone->    cnt <- cnt + 1 
                            if cnt = nodecount then
                                for i in 1 .. nodecount-1 do
                                    Nodelist.Item((i-1)%100)<! Join(Nodelist.Item(i),Nodelist.Item(i).Path.Name)
            | JoinDone(nid)->       jcnt <- jcnt+1
                                    // printfn "Join Done by %d Nodes and NodeID %s" jcnt nid
                                    if jcnt = nodecount - 1 then
                                        printfn "Join Done"
                                        // for i in Nodelist do
                                        //     i<!SendMessage
            | RequestsDone(avgHops,hops) ->     rcnt <- rcnt + 1
                                                hopsum <- hopsum + avgHops
                                                // printfn "Hops for Node %s is %f on %f messages and %d " (mailbox.Sender().Path.Name) avgHops hops rcnt
                                                if rcnt = nodecount - 1 then
                                                    printfn "Final Avg Hop count %f" (hopsum/(rcnt|>float))
                                                    mailbox.Context.System.Terminate() |> ignore


        return! loop()
    }
    loop()

Nodelist <- [for a in 0 .. nodecount-1 do yield(spawn system (genNodeId a) Node)] 

let BossRef = spawn system "Boss" Dispatcher

BossRef <! Start

system.WhenTerminated.Wait()
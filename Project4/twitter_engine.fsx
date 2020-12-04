open System.Threading
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Data
open System.Collections.Generic

// open System.Linq
let rnd = System.Random()
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 8777
                    hostname = localhost
                }
            }
        }")


let system = ActorSystem.Create("TwitterEngine", configuration)

let mutable users = Map.empty
let mutable activeUsers = Map.empty
let mutable tweets = Map.empty
let mutable tweetOwner = Map.empty
let mutable followers = Map.empty
let mutable subscribersTweets = Map.empty
let mutable mentions = Map.empty
let mutable hashTags = Map.empty
// let mutable clientRef = Map.empty

let mutable masterID = 1234
let ticksPerMicroSecond = (TimeSpan.TicksPerMillisecond |> float )/(1000.0)
let ticksPerMilliSecond = TimeSpan.TicksPerMillisecond |> float

let registerUser username = 
    users <- users.Add(username,masterID|>string)
    let userID = masterID |> string
    masterID <- masterID + rnd.Next(1,32)
    userID

let getUserID username =
    let found = users.TryFind username
    found

let userLoggedIn username clientref= 
    let mutable userid = getUserID username 
    let mutable uid = ""
    if userid = None then
        uid <- (registerUser username) 
    else
        uid <- userid.Value
    let found = activeUsers.TryFind uid
    if found = None then
        activeUsers <- activeUsers.Add(uid,clientref)
    // clientRef <- clientRef.Add(uid,clientref)

let userLoggedOut username = 
    let mutable userid = getUserID username
    if userid <> None then
        activeUsers <- activeUsers.Remove(userid.Value)

let addTweet tweetID tweet = 
    tweets <- tweets.Add(tweetID,tweet)

let addTweetToUser userID tweet =
    let find = tweetOwner.TryFind userID
    if find = None then
        let lst = new List<string>()
        lst.Add(tweet)
        tweetOwner <- tweetOwner.Add(userID,lst)
    else
        find.Value.Add(tweet)

let isActiveUser userid = 
    let res = activeUsers.TryFind userid
    res <> None

let addFollower username followerUsername req=
    let userid = getUserID username
    if userid <> None then
        if (isActiveUser userid.Value) then
            let fid = getUserID followerUsername
            if fid<>None && userid <> None then
                let fset = followers.TryFind fid.Value
                if fset = None then
                    let followerList = new List<string>()
                    followerList.Add(userid.Value)
                    followers <- followers.Add(fid.Value,followerList)
                else 
                    fset.Value.Add(userid.Value)
                req <! "Ack|Subscribe|"
        else
            req <! "Ack|NotLoggedIn|"

let getFollowers userid = 
    let followerList = followers.TryFind userid
    if followerList <> None then
        followerList.Value
    else
        let elist = new List<string>()
        elist

let addSubscribersTweet tweet ownerid =
    let followerList = getFollowers ownerid
    for i in followerList do
        let subs = subscribersTweets.TryFind i        
        if subs = None then
            let stweets = new List<string>()
            stweets.Add(tweet)
            subscribersTweets <- subscribersTweets.Add(i,stweets)
        else
            subs.Value.Add(tweet)

let addMentions userid tweet mentionusername =
    let taggedid = getUserID mentionusername
    if taggedid <> None then
        let mentions_ = mentions.TryFind taggedid.Value
        if mentions_ = None then
            let mutable mp = Map.empty
            let tlist = new List<string>()
            tlist.Add(tweet)
            mp <- mp.Add(userid,tlist)
            mentions <- mentions.Add(taggedid.Value,mp)
        else
            let user = mentions_.Value.TryFind userid
            if user = None then
                let tlist = new List<string>()
                tlist.Add(tweet)
                let mutable mp = mentions_.Value
                mp <- mp.Add(userid,tlist)
                mentions <- mentions.Add(taggedid.Value,mp)
            else
                user.Value.Add(tweet)

let sendTweetAck userid tweet = 
    let userStatus = activeUsers.TryFind userid
    if userStatus <> None then
        userStatus.Value <! "Tweet|Self|"+tweet

// let sendReTweetACK userid = 


let sendTweetsToActiveFollowers userid tweet =
    // printfn "Send Tweets to Active users %A %s" userid tweet  
    let flist = getFollowers userid
    // printfn "followers list %A" flist
    for i in flist do
        let userStatus = activeUsers.TryFind i
        if userStatus <> None then
            // printfn "Sent to active client %A" i
            userStatus.Value <! "Tweet|Subscribe|"+tweet

let addHashTag hashtag tweet =
    let hfind = hashTags.TryFind hashtag
    if hfind = None then
        let tlist = new List<string>()
        tlist.Add(tweet)
        hashTags <- hashTags.Add(hashtag,tlist)
    else
        hfind.Value.Add(tweet)

let getTweets userid =
    let tlist = tweetOwner.TryFind userid
    let resp = new List<string>()
    if tlist <> None then
        tlist.Value
    else
        resp

let getMentions userid = 
    let tlist = mentions.TryFind userid
    let res = new List<string>()
    if tlist <> None then
        for i in tlist.Value do
            for j in i.Value do
                res.Add(j)
    res

let getHashTags hashtag =
    let hlist = hashTags.TryFind hashtag
    let res = new List<string>()
    
    if hlist <> None then 
        // printfn "%i" hlist.Value.Count
        for i in hlist.Value do
            // printfn "%s" i
            res.Add(i)
    res

let concatList (list: List<string>) =
    let mutable resp = ""
    let len = Math.Min(100,list.Count)
    for i in 0 .. len-1 do
        resp <- resp+list.[i]+"|"
    resp

type RegisterMsg =
    | RegisterUser of string*IActorRef
    | Login of string*IActorRef
    | Logout of string*IActorRef

let RegistrationHandler (mailbox:Actor<_>)=
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        // printfn "%A" msg
        try
            match msg with 
            | RegisterUser(username,userref) ->     let uid = getUserID username
                                                    let mutable cid = ""
                                                    if uid = None then
                                                        cid <- registerUser username
                                                    else
                                                        cid <- uid.Value
                                                    userref <! ("Ack|Register|"+(string cid))
            | Login(username,userref) ->    userLoggedIn username userref
                                            userref <! "Ack|Login|"
            | Logout(username,userref) ->   userLoggedOut username
                                            // printfn "User %A logged out" username
                                            userref <! "Ack|Logout|"
        finally
            let a = "Ignore"
            a |> ignore
        return! loop()
    }
    loop()

type TweetParserMsg = 
    | Parse of string*string*string*IActorRef

let TweetParser (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        try
            match msg with 
            | Parse(userid,tweetid,tweet,req) ->    let splits = (tweet).Split ' '
                                                    for i in splits do
                                                        if i.StartsWith "@" then
                                                            let temp = i.Split '@'
                                                            addMentions userid tweet temp.[1]
                                                        elif i.StartsWith "#" then
                                                            addHashTag i tweet
                                                    req <! "Ack|Tweet|"
        finally 
            let a = "ignore"
            a |> ignore
        return! loop()
    }
    loop()

let tweetParserRef = spawn system "TweetParser" TweetParser


type FollowersMsg = 
    | Add of string*string*IActorRef
    | Update of string*string*IActorRef

let FollowersHandler (mailbox:Actor<_>) = 
    
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        try
            match msg with
            | Add(user,follower,req) ->     addFollower user follower req
                                            
            | Update (ownerid,tweet,req)->    addSubscribersTweet tweet ownerid
        finally
            let a = "ignore"
            a |> ignore                                     
        return! loop()
    }
    loop()

let followersRef = spawn system "Follower" FollowersHandler

type TweetHandlerMsg =
    | AddTweet of string*string*string*IActorRef
    | ReTweet of string*IActorRef

let TweetHandler (mailbox:Actor<_>) =
    let mutable cnt = 0L
    let mutable timetaken = 0.0
    let mutable millisecs = 0
    let timer = System.Diagnostics.Stopwatch()
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        // printfn "Tweet Handler %A" msg
        try
            match msg with
            | AddTweet(username,reqid,tweet,req) -> timer.Restart()
                                                    let userid = getUserID username
                                                    cnt <- cnt + 1L
                                                    if userid <> None then
                                                        if (isActiveUser userid.Value) then
                                                            let tweetid = cnt|>string
                                                            addTweet tweetid tweet
                                                            addTweetToUser userid.Value tweet
                                                            tweetParserRef <! Parse(userid.Value, tweetid, tweet, req)
                                                            followersRef <! Update(userid.Value, tweet, req)
                                                            sendTweetAck userid.Value tweet
                                                            sendTweetsToActiveFollowers userid.Value tweet
                                                        else
                                                            req <! "Ack|NotLoggedIn|"
                                                    timetaken <- timetaken + (timer.ElapsedTicks|>float)
                                                    millisecs <- millisecs + (timer.ElapsedMilliseconds|>int)
                                                    // printfn "%A ticks %A milliseconds %A timespan" timer.ElapsedTicks timer.ElapsedMilliseconds timer.Elapsed
                                                    if cnt % 10000L = 0L then
                                                        // printfn "%A" followers
                                                        // printfn "%i %A" cnt timetaken
                                                        printfn "Average Tweet processing after %i tweets is %A microseconds and total %A milliseconds" cnt (timetaken/(ticksPerMicroSecond*(cnt|>float))) (timetaken/ticksPerMilliSecond) 
                                                               
            | ReTweet(username, req) -> let ind = rnd.Next(0,cnt|>int)|>string
                                        let tweet = tweets.TryFind ind
                                        // printfn "%A" tweet
                                        if tweet <> None then
                                            mailbox.Self<! AddTweet(username,ind,tweet.Value,req)
                                            // req<!"TWEET|RETWEET|"+tweet.Value
        finally
            let a = "Ignore "
            a |> ignore
                    

        return! loop()
    }
    loop()

type GetHandlerMsg =
    | GetTweets of string*IActorRef
    | GetTags of string*IActorRef
    | GetHashTags of string * string * IActorRef

exception Error1 of string

let GetHandler(mailbox:Actor<_>)=
    let timer = System.Diagnostics.Stopwatch()
    let mutable gettweetstime = 0
    let mutable gettweetscnt =0
    let mutable gettagstime = 0
    let mutable gettagscnt =0
    let mutable gethashtagstime = 0
    let mutable gethashtagscnt =0
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        try
            match msg with
            | GetTweets(username,req) ->    timer.Restart()
                                            gettweetscnt <- gettweetscnt + 1
                                            let userid = getUserID username
                                            if userid <> None then
                                                if isActiveUser userid.Value then
                                                    let resp = (getTweets userid.Value) 
                                                    req <! "GetResponse|GetTweets|"+(concatList resp)+"\n"
                                                else
                                                    req <! "Ack|NotLoggedIn|"
                                            gettweetstime <- gettweetstime + (timer.ElapsedTicks|>int)
                                            // printfn "%i gettweets count" gettweetscnt
                                            if gettweetscnt%100 = 0 then
                                                printfn "Average time taken to process get tweets is %A microseconds after %i gettweets Requests and total %A milliseconds" ((gettweetstime|>float)/(ticksPerMicroSecond*(gettweetscnt|>float))) gettweetscnt ((gettweetstime|>float)/ticksPerMilliSecond)
            | GetTags(username,req)->   timer.Restart()
                                        gettagscnt <- gettagscnt + 1
                                        let userid = getUserID username
                                        if userid <> None then
                                            if isActiveUser userid.Value then
                                                let resp = (getMentions userid.Value)
                                                req <! "GetResponse|GetMentions|"+(concatList resp)+"\n"
                                            else
                                                    req <! "Ack|NotLoggedIn|"
                                        gettagstime <- gettagstime + (timer.ElapsedTicks|>int)
                                        if gettagscnt%100 = 0 then
                                                printfn "Average time taken to process get mentions is %A microseconds after %i getmentions Requests and total %A milliseconds" ((gettagstime|>float)/(ticksPerMicroSecond*(gettagscnt|>float))) gettagscnt ((gettagstime|>float)/ticksPerMilliSecond)
            | GetHashTags(username,hashtag,req)->   timer.Restart()
                                                    gethashtagscnt <- gethashtagscnt + 1
                                                    let userid = getUserID username
                                                    if userid <> None then
                                                        if isActiveUser userid.Value then
                                                            let resp = (getHashTags hashtag)
                                                            // printfn "%s" (concatList resp)
                                                            req <! "GetResponse|GetHashTags|"+(concatList resp)+"\n"
                                                        else
                                                                req <! "Ack|NotLoggedIn|"
                                                    gethashtagstime <- gethashtagstime + (timer.ElapsedTicks|>int)
                                                    if gethashtagscnt%100 = 0 then
                                                        printfn "Average time taken to process get hashtags is %A microseconds after %i gethashtags Requests and total %A milliseconds" ((gethashtagstime|>float)/(ticksPerMicroSecond*(gethashtagscnt|>float))) gethashtagscnt ((gethashtagstime|>float)/ticksPerMilliSecond)
        with
            | :? System.InvalidOperationException as ex ->  let b = "ignore"
                                                            // printfn "exception"
                                                            b |>ignore


        return! loop()
    }
    loop()

let tweetHandlerRef = spawn system "tweetHandler" TweetHandler
let registrationHandlerRef = spawn system "registrationHandler" RegistrationHandler
let getHandlerRef = spawn system "GetHandler" GetHandler


let commlink = 
    spawn system "RequestHandler"
    <| fun mailbox ->
        let mutable reqid = 0
        let mutable ticks = 0L
        let timer = System.Diagnostics.Stopwatch()
        
        let rec loop() =
            actor {
                
                let! msg = mailbox.Receive()
                reqid <- reqid + 1
                timer.Restart()
                // printfn "%s" msg 
                let command = (msg|>string).Split '|'
                if command.[0].CompareTo("Register") = 0 then
                    registrationHandlerRef <! RegisterUser(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Login") = 0 then
                    registrationHandlerRef <! Login(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Logout") = 0 then
                    registrationHandlerRef <! Logout(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Tweet") = 0 then
                    tweetHandlerRef <! AddTweet(command.[1],string reqid,command.[2],mailbox.Sender())
                elif command.[0].CompareTo("ReTweet") = 0 then
                    tweetHandlerRef <! ReTweet(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("GetTweets") = 0 then
                    getHandlerRef <! GetTweets(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Subscribe") = 0 then 
                    followersRef <! Add(command.[1],command.[2],mailbox.Sender())
                elif command.[0].CompareTo("GetMentions") = 0 then
                    getHandlerRef <! GetTags(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("GetHashTags") = 0 then
                    getHandlerRef <! GetHashTags(command.[1],command.[2],mailbox.Sender())
                
                ticks <- ticks + timer.ElapsedTicks
                if reqid%10000 = 0 then
                    printfn "Time taken to handle %i requests from clients is %A milliseconds" reqid ((ticks|>float)/ticksPerMilliSecond)

                return! loop() 
            }
            
        loop()


printfn "Server Started"



system.WhenTerminated.Wait()

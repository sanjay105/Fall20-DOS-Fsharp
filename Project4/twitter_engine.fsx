open System.Threading
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Data

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
                    hostname = 192.168.0.96
                }
            }
        }")


let system = ActorSystem.Create("TwitterEngine", configuration)

let dataSet = new DataSet()

let userTable = new DataTable("User Table")
userTable.Columns.Add("username")
userTable.Columns.Add("userid")
userTable.PrimaryKey = [|userTable.Columns.["userid"]|]

let tweetTableRelations = new DataTable("Tweet Relations Table")
tweetTableRelations.Columns.Add("userid")
tweetTableRelations.Columns.Add("ownerid")
tweetTableRelations.Columns.Add("tweetid")

let tweetTable = new DataTable("Tweets Table")
tweetTable.Columns.Add("tweetid")
tweetTable.Columns.Add("tweet")
tweetTable.Columns.Add("tweettime")
tweetTable.PrimaryKey = [|tweetTable.Columns.["tweetid"]|]

let mentionTable = new DataTable("Mentions Table")
mentionTable.Columns.Add("userid")
mentionTable.Columns.Add("mentionedby")
mentionTable.Columns.Add("tweetid")

let hashTagTable = new DataTable("HashTag Table")
hashTagTable.Columns.Add("hashtag")
hashTagTable.Columns.Add("userid")
hashTagTable.Columns.Add("tweetid")

let followerTable = new DataTable("Followers Table")
followerTable.Columns.Add("userid")
followerTable.Columns.Add("followerid")

dataSet.Tables.Add(userTable)
dataSet.Tables.Add(tweetTable)
dataSet.Tables.Add(tweetTableRelations)
dataSet.Tables.Add(mentionTable)
dataSet.Tables.Add(hashTagTable)
dataSet.Tables.Add(followerTable)
dataSet.AcceptChanges()

let getUsersList temp =
    let res = query{
        for i in userTable.AsEnumerable() do
        select(i)
    }
    let result = Seq.toList res
    result

let userAlreadyRegistered username  =
    let res = query{
        for i in userTable.AsEnumerable() do
        where (i.["username"] |> string=username)
        select (i.["userid"] |> string) 
    }
    let result = Seq.toList res
    result

let getUserName userid = 
    let res = query{
        for i in userTable.AsEnumerable() do
        where (i.["userid"]|>string = userid)
        select (i.["username"]|>string)
    }
    let result = Seq.toList res
    if result.Length > 0 then
        result.Item(0)
    else
        ""

let getTweets userid =
    let res = query{
        for i in tweetTable.AsEnumerable() do
        join j in tweetTableRelations.AsEnumerable() on
            ((i.["tweetid"] |> string) = (j.["tweetid"] |> string))
        where (j.["userid"] |> string=userid)
        select("Tweet ID "+(i.["tweetid"] |> string)+" -> Tweet - '"+(i.["tweet"] |> string)+"', "+(i.["tweettime"] |> string)+" tweeted by '"+(getUserName (j.["ownerid"]|>string))+"'\n")
    }
    let result = Seq.toList res
    result

let getTweetMentions userid = 
    let res = query {
        for i in tweetTable.AsEnumerable() do
        join j in mentionTable.AsEnumerable() on
            ((i.["tweetid"] |> string) = (j.["tweetid"] |> string))
        where (j.["userid"] |> string =userid)
        select ("Mentioned by "+(getUserName (j.["mentionedby"]|> string))+" on tweet -> "+(i.["tweet"]|> string)+" - tweet id "+(j.["tweetid"] |> string)+"\n") 
    }
    let result = Seq.toList res
    // printfn "%A" result
    result

let getFollowers userid =
    let res = query{
        for i in followerTable.AsEnumerable() do
        where (i.["followerid"] |> string = userid)
        select (i.["userid"] |> string)
    }
    let result = Seq.toList res
    result

let getUserID username:string =
    let uid = userAlreadyRegistered username
    if uid.Length <> 0 then
        uid.Item(0)
    else
        ""

let isAlreadyFollower userid followerid : bool =
    let res = query{
        for i in followerTable.AsEnumerable() do
        where (i.["followerid"]|>string = followerid && (i.["userid"]|>string = userid))
        select (i)
    }
    let result = Seq.toList res
    if result.Length = 0 then 
        false
    else
        true

let addTweet (tweetid:string) (tweet:string) =
    tweetTable.Rows.Add(tweetid,tweet,DateTime.Now) |> ignore
    tweetTable.AcceptChanges()

let addTweetRelation (userid:string) (ownerid:string) (tweetid:string) =
    tweetTableRelations.Rows.Add(userid,ownerid,tweetid) |> ignore
    // tweetTableRelations.AcceptChanges()

let addMention (user:string) (tweetid:string) (muser:string) =
    let musername = (muser).Split '@'
    let muid = getUserID musername.[1]
    // printfn "%A - %s -%s" musername muid user
    mentionTable.Rows.Add(muid,user,tweetid) |> ignore
    mentionTable.AcceptChanges()

let addHashTag (user:string) (tweetid:string) (hashtag:string) = 
    hashTagTable.Rows.Add(hashtag,user,tweetid) |> ignore
    hashTagTable.AcceptChanges()

let addFollower (user:string) (follower:string) =
    let fid = getUserID follower
    let uid = getUserID user
    if not (isAlreadyFollower uid fid) then
        followerTable.Rows.Add(uid,fid) |> ignore
        followerTable.AcceptChanges()





    
type RegisterMsg =
    | RegisterUser of string*IActorRef
    | Login of string*IActorRef
    | Logout of string*IActorRef

let RegistrationHandler (mailbox:Actor<_>)=
    let mutable userid = 23345
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        // printfn "%A" msg
        match msg with 
        | RegisterUser(username,userref) ->     let ulist = userAlreadyRegistered username
                                                // printfn "%A" ulist
                                                let mutable cid = ""
                                                if ulist.Length = 0 then
                                                    userTable.Rows.Add(username,userid)|>ignore
                                                    cid <- userid|>string
                                                    userid <- userid + rnd.Next(1,32)
                                                    userTable.AcceptChanges()
                                                    // printfn "Created User %A with user id %A" username cid 
                                                else
                                                    cid <- ulist.Item(0)
                                                    // printfn "Got Here"
                                                    // cid <- ulist.[0].["userid"].ToString() |> int
                                                    // printfn "%A" ulist
                                                // printfn "User Logged in %A" cid
                                                userref <! ("ACK|Register|"+(string cid))
        | Login(userid,userref) ->  userref <! "ACK|Login|"
        | Logout(userid,userref) ->     printfn "User %A logged out" userid
                                        userref <! "ACK|Logout|"

                                        


        return! loop()
    }
    loop()

type TweetParserMsg = 
    | Parse of string*string*string*IActorRef

let TweetParser (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | Parse(user,tweetid,tweet,req) ->  let splits = (tweet).Split ' '
                                            for i in splits do
                                                if i.StartsWith '@' then
                                                    // printfn "parse - %s" i
                                                    addMention user tweetid i
                                                elif i.StartsWith '#' then
                                                    addHashTag user tweetid i
                                            req <! "ACK|Tweet|"
                                        // mailbox.Context.Stop(mailbox.Self)
                                        // mailbox.Self.Tell(PoisonPill.Instance)
        return! loop()
    }
    loop()

let tweetParserRef = spawn system "TweetParser" TweetParser

type FollowersMsg = 
    | Add of string*string*IActorRef
    | Update of string*string*IActorRef

let Followers (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | Add(user,follower,req) ->     addFollower user follower
                                        req <! "ACK|Subscribe|"
        | Update (ownerid,tweetid,req)->    let followers = getFollowers ownerid
                                            addTweetRelation ownerid ownerid tweetid
                                            for i in followers do
                                                addTweetRelation i ownerid tweetid
                                            tweetTableRelations.AcceptChanges()
                                            req <! "ACK|Tweet|"
        return! loop()
    }
    loop()

let followersRef = spawn system "Follower" Followers

type TweetHandlerMsg =
    | AddTweet of string*string*string*IActorRef
    | GetTweets of string*IActorRef
    | GetMentions of string*IActorRef

let TweetHandler (mailbox:Actor<_>) =
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        // printfn "Tweet Handler %A" msg
        match msg with
        | AddTweet(username,tweetid,tweet,req) ->   let userid = getUserID username
                                                    addTweet tweetid tweet 
                                                    // addTweetRelation userid userid tweetid
                                                    tweetParserRef <! Parse(userid, tweetid, tweet, req)
                                                    followersRef <! Update(userid, tweetid, req)
                                                    // req <! "ACK|Tweet|"
                                            // printfn "Added tweet from %A with tweet id %A -> %A" user tweetid tweet
                                            // tweetTable.Rows.Add(tweetid,user,tweet,DateTime.Now)|>ignore  
                                            // tweetTable.AcceptChanges()  
                                            // printfn "Added tweet %A to datatable" tweetid 
        | GetTweets(username,req) ->    let userid = getUserID username
                                        let resp = (getTweets userid) |> List.fold (+) ""
                                        req <! "Response|GetTweets|"+resp
                                    // for i in 0 .. tweetTable.Rows.Count-1 do
                                    //     printfn "%A-%A-%A-%A" tweetTable.Rows.[i].["userid"] tweetTable.Rows.[i].["tweetid"] tweetTable.Rows.[i].["tweet"] tweetTable.Rows.[i].["tweet_time"]
        | GetMentions(username,req)->   let userid = getUserID username
                                        
                                        let resp = (getTweetMentions userid) |> List.fold (+) ""
                                        req <! "Response|GetMentions|"+resp



                                            

        return! loop()
    }
    loop()

let tweetHandlerRef = spawn system "tweetHandler" TweetHandler
let registrationHandlerRef = spawn system "registrationHandler" RegistrationHandler


let commlink = 
    spawn system "RequestHandler"
    <| fun mailbox ->
        let mutable reqid = 0
        let rec loop() =
            actor {
                
                let! msg = mailbox.Receive()
                reqid <- reqid + 1
                // printfn "%s" msg 
                let command = (msg|>string).Split '|'
                if command.[0].CompareTo("Register") = 0 then
                    // printfn "Message Received from %A" command.[1]
                    // mailbox.Sender()<!"ACK|Yipee"
                    registrationHandlerRef <! RegisterUser(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Login") = 0 then
                    registrationHandlerRef <! Login(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Logout") = 0 then
                    registrationHandlerRef <! Logout(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Tweet") = 0 then
                    // printfn "Received Tweet from %A tweet id - %A -> %A" command.[1] reqid command.[2]
                    tweetHandlerRef <! AddTweet(command.[1],string reqid,command.[2],mailbox.Sender())
                elif command.[0].CompareTo("GetTweets") = 0 then
                    tweetHandlerRef <! GetTweets(command.[1],mailbox.Sender())
                elif command.[0].CompareTo("Subscribe") = 0 then //Subscribe|myid|<follower name>
                    followersRef <! Add(command.[1],command.[2],mailbox.Sender())
                elif command.[0].CompareTo("GetMentions") = 0 then
                    tweetHandlerRef <! GetMentions(command.[1],mailbox.Sender())
                 
                elif command.[0].CompareTo("BackupDB") = 0 then
                    dataSet.WriteXml("twitterserver.xml")
                    


                return! loop() 
            }
        loop()


printfn "Server Started"



system.WhenTerminated.Wait()

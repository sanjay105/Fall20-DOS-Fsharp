// Model
type Details={
    Name: string
    Description:string
}

type Item={
    Details:Details
}

type RoomId=
    | RoomId of string

type Exit=
    | PassableExit of Details * destination:RoomId
    | LockedExit of Details * key:Item *next:Exit
    | NoExit of string option

and Exits = {
    North:Exit
    South:Exit
    East:Exit
    West:Exit
}

and Room={
    Details:Details
    Items: Item list
    Exits:Exits
}

type Player={
    Details:Details
    Location:RoomId
    Inventory:Item list
}

type World={
    Rooms:Map<RoomId,Room>
    Player:Player
}

let firstRoom={
    Details={
        Name="First Room";
        Description="You are standing in a room"
    };
    Items=[];
    Exits={
        North=NoExit(None);
        South=NoExit(None);
        East=NoExit(None);
        West=NoExit(None);
    }
}

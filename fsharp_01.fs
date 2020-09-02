//Variables
let mutable a=10
a <- 20

let items = [1..5]

List.append items [6] //immutable

items

//Functions

let prefix prefixStr baseStr =
    prefixStr+", "+baseStr

prefix "Hello" "Sanjay"

let names=["zz";"aa";"bb";"cc"]

names
|>Seq.map (prefix "Holo")

let prefixWithHello = prefix "Hello"

let exclaim s=
    s+"!"

names
|>Seq.map prefixWithHello
|>Seq.map exclaim
|>Seq.sort


let bigHello = prefixWithHello >> exclaim

names
|>Seq.map bigHello
|>Seq.sort

let hellos=
    names
    |>Seq.map (fun x -> printfn "Mapped over %s" x; bigHello x)
    |>Seq.sort
//hellos will be empty at this point
hellos//now the above routine is executed and hellos will have data

let hellos1=
    names
    |>Seq.map (fun x -> printfn "Mapped over %s" x; bigHello x)
    |>Seq.sort
    |>Seq.iter(printfn "%s")//will iterate 
hellos1 //will be empty
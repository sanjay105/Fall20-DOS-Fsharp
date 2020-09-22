#time "on"
open System

let N = fsi.CommandLineArgs.[1] |> int64
let k = fsi.CommandLineArgs.[2] |> int64
let mutable sum=0L
for i in 1L .. k do
    sum <- sum + (i * i)
for i in k+1L .. N+1L do
    let squareRoot = sqrt (double sum) 
    if (int64 sum) = (int64 squareRoot) * (int64 squareRoot) then
        printfn "%i" (i-k) 
    sum <- sum - ((i - k)*(i - k))
    sum <- sum + (i * i)


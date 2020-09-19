#time "on"
let rec func num ind sum : int= 
    match ind with
    | 0 -> sum
    | _ -> func (num+1) (ind-1) (sum + num * num)

let add1 x= x + 1L 
let times2 x = x * 2L 
let timesn n x = x * n 
let divideby6 x = x / 6L 
//let n=5L

let sumOfSquares n = timesn n >> timesn (add1 n) >> (times2>>add1) >> divideby6

let res = sumOfSquares 5L 5L

printfn "%i" res

// // let result = (sumOfSquares 1000000000) - sumOfSquares 999999999

// // printfn "%i" result

// let canFormLucasPyramid index k =
//     let total = sumOfSquares (index+k-1) - sumOfSquares (index-1)
//     let squareRoot = total |> double |> sqrt |> int64
//     if total = squareRoot * squareRoot then
//         printfn "%i" index
//     0
// let N = 1000000000
// let k = 24
// for i in 1 .. N do
//     canFormLucasPyramid i k |> ignore

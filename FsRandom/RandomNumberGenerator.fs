﻿module FsRandom.RandomNumberGenerator

open System

type Prng<'s> = 's -> uint64 * 's
type PrngState =
   abstract Next64Bits : unit -> uint64 * PrngState
type GeneratorFunction<'a> = GF of (PrngState -> 'a * PrngState)

let rec createState (prng:Prng<'s>) (seed:'s) = {
   new PrngState with
      member __.Next64Bits () =
         let r, next = prng seed
         r, createState prng next
}

let inline bindRandom (GF m) f =
   GF (fun s0 -> let v, s' = m s0 in match f v with GF (g) -> g s')
let inline returnRandom x = GF (fun s -> x, s)
let inline runRandom (GF m) x = m x
let inline evaluateRandom (GF m) x = m x |> fst
let inline executeRandom (GF m) x = m x |> snd

let inline (|>>) m f = bindRandom m f
let inline (&>>) m b = bindRandom m (fun _ -> b)

type RandomBuilder () =
   member this.Bind (m, f) = m |>> f
   member this.Combine (a, b) = a &>> b
   member this.Return (x) = returnRandom x
   member this.ReturnFrom (m : GeneratorFunction<_>) = m
   member this.Zero () = GF (fun s -> (), s)
   member this.Delay (f) = returnRandom () |>> f
   member this.While (condition, m) =
      if condition () then
         m |>> (fun () -> this.While (condition, m))
      else
         this.Zero ()
   member this.For (source : seq<'a>, f) =
      use e = source.GetEnumerator ()
      this.While (e.MoveNext, this.Delay (fun () -> f e.Current))
let random = RandomBuilder ()

let buffer = Array.zeroCreate sizeof<uint64>
let systemrandom (random : Random) = 
   random.NextBytes (buffer)
   BitConverter.ToUInt64 (buffer, 0), random
   
let inline xor128 (x:uint32, y:uint32, z:uint32, w:uint32) =
   let t = x ^^^ (x <<< 11)
   let (_, _, _, w') as s = y, z, w, (w ^^^ (w >>> 19)) ^^^ (t ^^^ (t >>> 8))
   w', s
let xorshift s =
   let lower, s = xor128 s
   let upper, s = xor128 s
   to64bit lower upper, s

let rawBits = GF (fun s -> s.Next64Bits ())
[<Literal>]
let ``1 / 2^52`` = 2.22044604925031308084726333618e-16
[<Literal>]
let ``1 / 2^53`` = 1.11022302462515654042363166809e-16
[<Literal>]
let ``1 / (2^53 - 1)`` = 1.1102230246251566636831481e-16
let ``(0, 1)`` = GF (fun s0 -> let r, s' = s0.Next64Bits () in (float (r >>> 12) + 0.5) * ``1 / 2^52``, s')
let ``[0, 1)`` = GF (fun s0 -> let r, s' = s0.Next64Bits () in float (r >>> 11) * ``1 / 2^53``, s')
let ``(0, 1]`` = GF (fun s0 -> let r, s' = s0.Next64Bits () in (float (r >>> 12) + 1.0) * ``1 / 2^52``, s')
let ``[0, 1]`` = GF (fun s0 -> let r, s' = s0.Next64Bits () in float (r >>> 11) * ``1 / (2^53 - 1)``, s')
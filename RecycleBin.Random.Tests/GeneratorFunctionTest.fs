﻿module RecycleBin.Random.Tests.GeneratorFunctionTest

open System
open RecycleBin.Random
open RecycleBin.Random.Statistics
open RecycleBin.Random.Utility
open MathNet.Numerics
open MathNet.Numerics.Distributions
open MathNet.Numerics.Statistics
open NUnit.Framework

let n = 500
let level = 0.01
let rec generate f seed =
   seq {
      let r, s = f seed
      yield r
      yield! generate f s
   }
let sample f seed = generate f seed |> Seq.take n |> Seq.toList

[<Literal>]
let iteration = 1000
let ks x = 1.0 - 2.0 * (Seq.init iteration (fun i -> let k = float (i + 1) in (if i % 2 = 0 then 1.0 else -1.0) * exp (-2.0 * k * k * x * x)) |> Seq.sum)
let testContinuous (prng : 's -> RandomBuilder<'s>, seed) generator cdf =
   let observed =
      let f seed = prng seed { return! generator }
      in sample f seed
   let empirical x = List.sumBy (fun t -> if t <= x then 1.0 / float n else 0.0) observed
   let epsilon = List.sort observed |> Seq.pairwise |> Seq.map (fun (a, b) -> b - a) |> Seq.min |> ((*) 0.1)
   let diff x = empirical x - cdf x |> abs
   let d = observed |> List.collect (fun x -> [diff x; diff (x - epsilon)]) |> List.max
   Assert.That (ks (sqrt (float n) * d), Is.GreaterThanOrEqualTo(level))

let testDiscrete (prng : 's -> RandomBuilder<'s>, seed) generator cdf parameterCount =
   let observed =
      let f seed = prng seed { return! generator }
      in sample f seed
   let binCount = int <| ceil (2.0 * (float n ** 0.4))
   let histogram = Histogram(List.map float observed, binCount)
   let p =
      let nonEmptyCell = ref 0
      let mutable sum = 0.0
      for index = 0 to binCount - 1 do
         let bin = histogram.[index]
         if floor bin.UpperBound <> floor bin.LowerBound  // ensures at least one integer exists in the bin
         then
            let o = float bin.Count
            let e = float n * (cdf bin.UpperBound - cdf bin.LowerBound)
            sum <- sum + (o - e) ** 2.0 / e
            if bin.Count <> 0.0 then incr nonEmptyCell
      let df = !nonEmptyCell - (parameterCount + 1)
      ChiSquare(float df).CumulativeDistribution (sum)
   Assert.That (p, Is.GreaterThanOrEqualTo(level))

let testBinary (prng : 's -> RandomBuilder<'s>, seed) generator cdf probability =
   let observed =
      let f seed = prng seed { return! generator }
      in sample f seed
   let o0, o1 = observed |> List.partition ((=) 0) |> (fun (zero, one) -> float (List.length zero), float (List.length one))
   let e0, e1 = let one = float n * probability in (float n - one, one)
   let chisq = (o0 - e0) ** 2.0 / e0 + (o1 - e1) ** 2.0 / e1
   let p = ChiSquare(1.0).CumulativeDistribution (chisq)
   Assert.That (p, Is.GreaterThanOrEqualTo(level))
   
let cdfUniform (a, b) = ContinuousUniform(a, b).CumulativeDistribution
let testUniform tester parameter =
   let generator = uniform parameter
   let cdf = cdfUniform parameter
   testContinuous tester generator cdf
   
let cdfLoguniform (a, b) x =
   if x < a
   then
      0.0
   elif a <= x && x <= b
   then
      1.0 / (x * (log b - log a))
   else
      1.0
let testLoguniform tester parameter =
   let generator = loguniform parameter
   let cdf = cdfLoguniform parameter
   testContinuous tester generator cdf
   
let cdfTriangular (a, b, c) x =
   if x < a
   then
      0.0
   elif a <= x && x <= b
   then
      if x < c
      then
         (x - a) * (x - a) / ((b - a) * (c - a))
      else
         1.0 - (b - x) * (b - x) / ((b - a) * (b - c))
   else
      1.0
let testTriangular tester parameter =
   let generator = triangular parameter
   let cdf = cdfTriangular parameter
   testContinuous tester generator cdf
   
let cdfNormal (mean, sd) = Normal(mean, sd).CumulativeDistribution
let testNormal tester parameter =
   let generator = normal parameter
   let cdf = cdfNormal parameter
   testContinuous tester generator cdf
   
let cdfLognormal (mu, sigma) = LogNormal(mu, sigma).CumulativeDistribution
let testLognormal tester parameter =
   let generator = lognormal parameter
   let cdf = cdfLognormal parameter
   testContinuous tester generator cdf
   
let cdfGamma (shape, scale) =
   // Gamma.CumulativeDistribution (x) (x < 0) throws an exception.
   let distribution = Gamma (shape, 1.0 / scale)
   fun x -> if x < 0.0 then 0.0 else distribution.CumulativeDistribution (x)
let testGamma tester parameter =
   let generator = gamma parameter
   let cdf = cdfGamma parameter
   testContinuous tester generator cdf
   
let cdfExponential rate = Exponential(rate).CumulativeDistribution
let testExponential tester rate =
   let generator = exponential rate
   let cdf = cdfExponential rate
   testContinuous tester generator cdf
   
let cdfBeta (a, b) = Beta(a, b).CumulativeDistribution
let testBeta tester parameter =
   let generator = beta parameter
   let cdf = cdfBeta parameter
   testContinuous tester generator cdf
   
let cdfCauchy (location, scale) = Cauchy(location, scale).CumulativeDistribution
let testCauchy tester parameter =
   let generator = cauchy parameter
   let cdf = cdfCauchy parameter
   testContinuous tester generator cdf
   
let cdfChisq df = ChiSquare(float df).CumulativeDistribution
let testChiSquare tester df =
   let generator = chisquare df
   let cdf = cdfChisq df
   testContinuous tester generator cdf
   
let cdfT df = StudentT(0.0, 1.0, float df).CumulativeDistribution
let testT tester df =
   let generator = t df
   let cdf = cdfT df
   testContinuous tester generator cdf
   
let cdfUniformDiscrete (a, b) = DiscreteUniform(a, b).CumulativeDistribution
let testUniformDiscrete tester parameter =
   let generator = uniformDiscrete parameter
   let cdf = cdfUniformDiscrete parameter
   testDiscrete tester generator cdf 2
   
let cdfPoisson lambda = Poisson(lambda).CumulativeDistribution
let testPoisson tester lambda =
   let generator = poisson lambda
   let cdf = cdfPoisson lambda
   testDiscrete tester generator cdf 1
   
let cdfGeometric p = Geometric(p).CumulativeDistribution
let testGeometric tester p =
   let generator = geometric p
   let cdf = cdfGeometric p
   testDiscrete tester generator cdf 1
   
let cdfBernoulli p = Bernoulli(p).CumulativeDistribution
let testBernoulli tester p =
   let generator = bernoulli p
   let cdf = cdfBernoulli p
   testBinary tester generator cdf p
   
let cdfBinomial (n, p) = Binomial(p, n).CumulativeDistribution
let testBinomial tester parameter =
   let generator = binomial parameter
   let cdf = cdfBinomial parameter
   testDiscrete tester generator cdf 2
   
let testDirichlet tester parameter =
   Assert.Inconclusive ("Not implemented.")
   
let testMultinomial tester parameter =
   Assert.Inconclusive ("Not implemented.")

let testFlipCoin tester p =
   let generator = getRandomBy (fun b -> if b then 1 else 0) <| flipCoin p
   let cdf = cdfBernoulli p
   testBinary tester generator cdf p

[<Test>]
let ``Validates uniform`` () =
   testUniform (getDefaultTester ()) (-10.0, 10.0)

[<Test>]
let ``Validates loguniform`` () =
   testUniform (getDefaultTester ()) (1.0, 100.0)

[<Test>]
let ``Validates triangular`` () =
   testTriangular (getDefaultTester ()) (-3.3, 10.7, 2.1)

[<Test>]
let ``Validates normal (-5.0, 3.0)`` () =
   testNormal (getDefaultTester ()) (-5.0, 3.0)

[<Test>]
let ``Validates lognormal`` () =
   testLognormal (getDefaultTester ()) (3.1, 7.2)

[<Test>]
let ``Validates gamma (shape < 1)`` () =
   testGamma (getDefaultTester ()) (0.3, 2.0)

[<Test>]
let ``Validates gamma (shape > 1)`` () =
   testGamma (getDefaultTester ()) (5.6, 0.4)

[<Test>]
let ``Validates gamma (shape is integer)`` () =
   testGamma (getDefaultTester ()) (3.0, 7.9)

[<Test>]
let ``Validates exponential`` () =
   testExponential (getDefaultTester ()) (1.5)

[<Test>]
let ``Validates beta`` () =
   testBeta (getDefaultTester ()) (1.5, 0.4)

[<Test>]
let ``Validates cauchy`` () =
   testCauchy (getDefaultTester ()) (-1.5, 0.1)

[<Test>]
let ``Validates chisquare`` () =
   testChiSquare (getDefaultTester ()) (10)

[<Test>]
let ``Validates t`` () =
   testT (getDefaultTester ()) (3)

[<Test>]
let ``Validates uniformDiscrete`` () =
   testUniformDiscrete (getDefaultTester ()) (-10, 10)

[<Test>]
let ``Validates poisson`` () =
   testPoisson (getDefaultTester ()) (5.2)

[<Test>]
let ``Validates geometric`` () =
   testGeometric (getDefaultTester ()) (0.2)

[<Test>]
let ``Validates bernoulli`` () =
   testBernoulli (getDefaultTester ()) (0.7)

[<Test>]
let ``Validates binomial`` () =
   testBinomial (getDefaultTester ()) (20, 0.3)

[<Test>]
let ``Validates dirichlet`` () =
    testDirichlet (getDefaultTester ()) [1.0; 2.0; 2.5; 0.5]

[<Test>]
let ``Validates multinomial`` () =
    testMultinomial (getDefaultTester ()) [1.0; 2.0; 2.5; 0.5]

[<Test>]
let ``Validates flipCoin`` () =
   testFlipCoin (getDefaultTester ()) (0.2)

[<Test>]
let ``Validates sample`` () =
   let array = Array.init 10 id
   let builder, seed = getDefaultTester ()
   let result, _ = builder seed { return! Utility.sample 8 array }
   Assert.That (Array.length result, Is.EqualTo(8))
   Assert.That (Array.forall (fun x -> Array.exists ((=) x) array) result, Is.True)
   Assert.That (Seq.length (Seq.distinct result), Is.EqualTo(8))

[<Test>]
let ``Validates sampleWithReplacement`` () =
   let array = Array.init 5 id
   let builder, seed = getDefaultTester ()
   let result, _ = builder seed { return! Utility.sampleWithReplacement 8 array }
   Assert.That (Array.length result, Is.EqualTo(8))
   Assert.That (Array.forall (fun x -> Array.exists ((=) x) array) result, Is.True)

[<Test>]
let ``Validates Array.randomCreate`` () =
   let builder, seed = getDefaultTester ()
   let result, _ = builder seed { return! Array.randomCreate 8 ``[0, 1)`` }
   Assert.That (Array.length result, Is.EqualTo(8))
   let head = result.[0]
   Assert.That (Array.forall ((=) head) result, Is.False)

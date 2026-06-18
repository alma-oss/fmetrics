module Alma.Metrics.Tests.Utils

open System
open Expecto
open FsCheck
open Alma.ServiceIdentification

let instance = {
    Domain = Domain "consents"
    Context = Context "example"
    Purpose = Purpose "common"
    Version = Version "stable"
}

let testBox =
    Box.ofInstance instance (Zone "data") (Bucket "lmc")

let okOrFail = function
    | Ok value -> value
    | Error error -> failtestf "Expected Ok, got %A" error

let finiteFloatGen =
    Arb.generate<float>
    |> Gen.filter (fun value ->
        not (Double.IsNaN value)
        && not (Double.IsInfinity value)
    )

let finiteFloatArb = Arb.fromGen finiteFloatGen

let finiteIntFloatPairArb =
    gen {
        let! intValue = Arb.generate<int>
        let! floatValue = finiteFloatGen
        return (intValue, floatValue)
    }
    |> Arb.fromGen

let finiteFloatPairArb =
    gen {
        let! left = finiteFloatGen
        let! right = finiteFloatGen
        return (left, right)
    }
    |> Arb.fromGen

let noisyBucketValueGen =
    Gen.frequency [
        8, finiteFloatGen
        1, Gen.constant Double.NaN
        1, Gen.constant Double.PositiveInfinity
        1, Gen.constant Double.NegativeInfinity
    ]

let duplicateHeavyBucketBoundsArb =
    gen {
        let! seedValues = Gen.listOfLength 4 noisyBucketValueGen
        let! duplicateValues = Gen.listOfLength 8 (Gen.elements seedValues)
        let! extraValues = Gen.listOfLength 4 noisyBucketValueGen
        let combined = seedValues @ duplicateValues @ extraValues
        let! shuffled = Gen.shuffle (combined |> List.toArray)
        return shuffled |> Array.toList
    }
    |> Arb.fromGen

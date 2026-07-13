module Alma.Metrics.Tests.PrometheusPureTests

open System
open Expecto
open FsCheck
open Alma.Metrics
open Alma.Metrics.Tests.Utils

let private expectException (action: unit -> unit) =
    try
        action ()
        failtest "Expected exception to be thrown"
    with _ ->
        ()

type private MetricValueAdditionTestCase = {
    Description: string
    A: MetricValue
    B: MetricValue
    Expected: MetricValue
}

type private UnsupportedMetricValueAdditionTestCase = {
    Description: string
    A: MetricValue
    B: MetricValue
}

let private provideMetricValueAdditionCases : MetricValueAdditionTestCase list = [
    {
        Description = "Int 2 + Int 3"
        A = Int 2
        B = Int 3
        Expected = Int 5
    }
    {
        Description = "Int 2 + Float 0.5"
        A = Int 2
        B = Float 0.5
        Expected = Float 2.5
    }
    {
        Description = "Float 2.5 + Int 3"
        A = Float 2.5
        B = Int 3
        Expected = Float 5.5
    }
    {
        Description = "Float 2.5 + Float 0.5"
        A = Float 2.5
        B = Float 0.5
        Expected = Float 3.0
    }
    {
        Description = "Infinite + Int 1"
        A = Infinite
        B = Int 1
        Expected = Infinite
    }
    {
        Description = "NegativeInfinite + Int 1"
        A = NegativeInfinite
        B = Int 1
        Expected = NegativeInfinite
    }
    {
        Description = "NotANumber + NotANumber"
        A = NotANumber
        B = NotANumber
        Expected = NotANumber
    }
]

let private provideUnsupportedMetricValueAdditionCases : UnsupportedMetricValueAdditionTestCase list = [
    {
        Description = "Infinite and NegativeInfinite"
        A = Infinite
        B = NegativeInfinite
    }
    {
        Description = "NegativeInfinite and Infinite"
        A = NegativeInfinite
        B = Infinite
    }
]

[<Tests>]
let prometheusPureTests =
    testList "Prometheus" [
        testList "MetricName" [
            testCase "should return EmptyName error when metric name is empty" <| fun _ ->
                match MetricName.create "" with
                | Error (MetricNameError.NameError EmptyName) -> ()
                | actual -> failtestf "Expected EmptyName error, got %A" actual

            testCase "should return TooShortName error when metric name has only one character" <| fun _ ->
                match MetricName.create "x" with
                | Error (MetricNameError.NameError (TooShortName _)) -> ()
                | actual -> failtestf "Expected TooShortName error, got %A" actual

            testCase "should normalize dashes and spaces to underscores" <| fun _ ->
                let name = MetricName.createOrFail "my-metric value"
                Expect.equal (MetricName.value name) "my_metric_value" "Metric name should normalize"
        ]

        testList "HistogramMetric" [
            testCase "should return EmptyName error when histogram metric name is empty" <| fun _ ->
                match HistogramMetric.create "" (HistogramBuckets.create [ 1.0 ]) with
                | Error (MetricNameError.NameError EmptyName) -> ()
                | actual -> failtestf "Expected EmptyName error, got %A" actual

            testCase "should return TooShortName error when histogram metric name has only one character" <| fun _ ->
                match HistogramMetric.create "x" (HistogramBuckets.create [ 1.0 ]) with
                | Error (MetricNameError.NameError (TooShortName _)) -> ()
                | actual -> failtestf "Expected TooShortName error, got %A" actual

            testCase "should preserve buckets from construction" <| fun _ ->
                let buckets = HistogramBuckets.create [ 2.0; 1.0 ]
                let metric = HistogramMetric.createOrFail "request_duration_seconds" buckets

                Expect.equal (HistogramMetric.buckets metric |> HistogramBuckets.value) [ 1.0; 2.0; Double.PositiveInfinity ] "Histogram metric should preserve normalized buckets"
        ]

        testList "Label" [
            testCase "should return EmptyName error when label name is empty" <| fun _ ->
                match Label.create ("", "v") with
                | Error (LabelNameError.NameError EmptyName) -> ()
                | actual -> failtestf "Expected EmptyName error, got %A" actual

            testCase "should return TooShortName error when label name has only one character" <| fun _ ->
                match Label.create ("x", "value") with
                | Error (LabelNameError.NameError (TooShortName _)) -> ()
                | actual -> failtestf "Expected TooShortName error, got %A" actual
        ]

        testList "MetricValue addition" [
            yield!
                provideMetricValueAdditionCases
                |> List.map (fun testCaseData ->
                    testCase $"should add {testCaseData.Description}" <| fun _ ->
                        let actual = testCaseData.A + testCaseData.B
                        Expect.equal actual testCaseData.Expected testCaseData.Description
                )

            yield!
                provideUnsupportedMetricValueAdditionCases
                |> List.map (fun testCaseData ->
                    testCase $"should throw when adding {testCaseData.Description}" <| fun _ ->
                        expectException (fun () -> ignore (testCaseData.A + testCaseData.B))
                )
        ]

        testList "MetricValue properties" [
            testProperty "should be commutative for finite Int and Float values" <| 
                Prop.forAll finiteIntFloatPairArb (fun (x, y) ->
                    Int x + Float y = Float y + Int x
                )

            testProperty "should treat NotANumber as identity on either side" <| fun (x: int) ->
                let value = Int x
                let leftIdentity = (NotANumber + value) = value
                let rightIdentity = (value + NotANumber) = value
                leftIdentity && rightIdentity

            testProperty "should treat NotANumber as identity for finite Float values" <|
                Prop.forAll finiteFloatArb (fun x ->
                    let value = Float x
                    let leftIdentity = (NotANumber + value) = value
                    let rightIdentity = (value + NotANumber) = value
                    leftIdentity && rightIdentity
                )

            testProperty "should be commutative for finite Float values" <|
                Prop.forAll finiteFloatPairArb (fun (x, y) ->
                    Float x + Float y = Float y + Float x
                )

            testProperty "should keep Infinite when paired with finite values" <| fun (x: int) ->
                (Infinite + Int x) = Infinite && (Int x + Infinite) = Infinite

            testProperty "should keep Infinite when paired with finite Float values" <|
                Prop.forAll finiteFloatArb (fun x ->
                    (Infinite + Float x) = Infinite
                    && (Float x + Infinite) = Infinite
                )

            testProperty "should treat finite integers as identity for NegativeInfinite" <| fun (x: int) ->
                (NegativeInfinite + Int x) = NegativeInfinite && (Int x + NegativeInfinite) = NegativeInfinite

            testProperty "should treat finite floats as identity for NegativeInfinite" <|
                Prop.forAll finiteFloatArb (fun x ->
                    (NegativeInfinite + Float x) = NegativeInfinite
                    && (Float x + NegativeInfinite) = NegativeInfinite
                )

            testCase "should treat NotANumber as identity for Infinite" <| fun _ ->
                Expect.equal (NotANumber + Infinite) Infinite "Left identity"
                Expect.equal (Infinite + NotANumber) Infinite "Right identity"

            testCase "should treat NotANumber as identity for NegativeInfinite" <| fun _ ->
                Expect.equal (NotANumber + NegativeInfinite) NegativeInfinite "Left identity"
                Expect.equal (NegativeInfinite + NotANumber) NegativeInfinite "Right identity"

            testCase "should keep Infinite when added to itself" <| fun _ ->
                Expect.equal (Infinite + Infinite) Infinite "Infinite + Infinite"

            testCase "should keep NegativeInfinite when added to itself" <| fun _ ->
                Expect.equal (NegativeInfinite + NegativeInfinite) NegativeInfinite "NegativeInfinite + NegativeInfinite"

            testProperty "should produce Float when adding Int to finite Float" <|
                Prop.forAll finiteIntFloatPairArb (fun (x, y) ->
                    match Int x + Float y with
                    | Float _ -> true
                    | _ -> false
                )

            testProperty "should produce Float when adding finite Float to Int" <|
                Prop.forAll finiteIntFloatPairArb (fun (x, y) ->
                    match Float y + Int x with
                    | Float _ -> true
                    | _ -> false
                )

            testProperty "should produce Float when adding two finite Float values" <|
                Prop.forAll finiteFloatPairArb (fun (x, y) ->
                    match Float x + Float y with
                    | Float _ -> true
                    | _ -> false
                )
        ]

        testList "HistogramBuckets" [
            testCase "should normalize sort and append positive infinity" <| fun _ ->
                let buckets =
                    HistogramBuckets.create [ 1.0; 0.5; 1.0; Double.NaN; Double.PositiveInfinity; 0.25 ]
                    |> HistogramBuckets.value

                Expect.equal buckets [ 0.25; 0.5; 1.0; Double.PositiveInfinity ] "Buckets should normalize"

            testProperty "should always return sorted distinct bounds ending with +Inf" <|
                Prop.forAll duplicateHeavyBucketBoundsArb (fun bounds ->
                    let normalized =
                        HistogramBuckets.create bounds
                        |> HistogramBuckets.value

                    let withoutInf =
                        normalized
                        |> List.filter (fun bound -> not (Double.IsPositiveInfinity bound))

                    let sortedDistinct =
                        withoutInf = (withoutInf |> List.sort |> List.distinct)

                    let noNaN = withoutInf |> List.forall (fun bound -> not (Double.IsNaN bound))
                    let noInfinity = withoutInf |> List.forall (fun bound -> not (Double.IsInfinity bound))
                    let lastIsInf = normalized |> List.last |> Double.IsPositiveInfinity

                    sortedDistinct && noNaN && noInfinity && lastIsInf
                )
        ]

        testList "DataSetKey" [
            testCase "should include service labels when created from instance" <| fun _ ->
                match DataSetKey.createFromInstance instance [ ("custom", "v") ] with
                | Error e -> failtestf "Expected dataset key, got %A" e
                | Ok key ->
                    let metric =
                        Metric.createMetric
                            (MetricName.createOrFail "dataset_key")
                            None
                            None
                            [ DataSet.createFromTuple (key, Int 1) ]

                    let formatted = Metric.format metric
                    Expect.stringContains formatted "svc_domain=\"consents\"" "Missing svc_domain"
                    Expect.stringContains formatted "svc_context=\"example\"" "Missing svc_context"
                    Expect.stringContains formatted "svc_purpose=\"common\"" "Missing svc_purpose"
                    Expect.stringContains formatted "svc_version=\"stable\"" "Missing svc_version"
                    Expect.stringContains formatted "custom=\"v\"" "Missing custom"

            testCase "should return label error when custom label name is invalid" <| fun _ ->
                match DataSetKey.createFromInstance instance [ ("", "value") ] with
                | Error (LabelError (LabelNameError.NameError EmptyName)) -> ()
                | actual -> failtestf "Expected LabelError EmptyName, got %A" actual
        ]

        testList "HistogramDataSet" [
            testCase "should compute cumulative counts sum and count from observations" <| fun _ ->
                let simple =
                    SimpleHistogramDataSet.create
                        []
                        (HistogramBuckets.create [ 1.0; 2.0 ])
                        [ 0.5; 1.5; 3.0 ]

                match HistogramDataSet.createFromSimple simple with
                | Error e -> failtestf "Expected histogram dataset, got %A" e
                | Ok dataSet ->
                    let counts = dataSet.Buckets |> List.map (fun b -> b.CumulativeCount)
                    Expect.equal counts [ 1; 2; 3 ] "Cumulative counts mismatch"
                    Expect.equal dataSet.Sum 5.0 "Sum mismatch"
                    Expect.equal dataSet.Count 3 "Count mismatch"

            testCase "should include value exactly on bucket boundary in bucket count" <| fun _ ->
                let simple =
                    SimpleHistogramDataSet.create
                        []
                        (HistogramBuckets.create [ 1.0 ])
                        [ 1.0 ]

                match HistogramDataSet.createFromSimple simple with
                | Error e -> failtestf "Expected histogram dataset, got %A" e
                | Ok dataSet ->
                    let counts = dataSet.Buckets |> List.map (fun b -> b.CumulativeCount)
                    Expect.equal counts [ 1; 1 ] "Boundary value should be included in le=1 and le=+Inf buckets"
        ]

        testList "Metric.format" [
            testCase "should include help type and single value" <| fun _ ->
                let metric =
                    Metric.createSingle "requests_total" (Int 3) (Some "Request count") (Some MetricType.Counter)
                    |> okOrFail

                let formatted = Metric.format metric
                let expected = "# HELP requests_total Request count\n# TYPE requests_total counter\nrequests_total 3\n\n"

                Expect.equal formatted expected "Metric format mismatch"

            testCase "should end with newline when metric has no datasets" <| fun _ ->
                let metric =
                    Metric.createMetric
                        (MetricName.createOrFail "empty_datasets")
                        None
                        None
                        []

                let formatted = Metric.format metric
                Expect.stringEnds formatted "\n" "Format should end with newline"

            testCase "should format unix timestamp when dataset has timestamp" <| fun _ ->
                let ts = DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                let dataSet =
                    SimpleDataSet.createWithTimestamp [] (Int 42) (Some ts)
                    |> DataSet.createFromSimple
                    |> okOrFail

                let metric =
                    Metric.createMetric
                        (MetricName.createOrFail "ts_metric")
                        None
                        None
                        [ dataSet ]

                let formatted = Metric.format metric
                Expect.stringContains formatted "1704067200" "Timestamp should appear in formatted output"

            testCase "should format float values with invariant culture" <| fun _ ->
                let metric =
                    Metric.createSingle "float_culture_check" (Float 2.5) None None
                    |> okOrFail

                let formatted = Metric.format metric
                Expect.stringContains formatted "2.5" "Float must use dot decimal separator"
                Expect.isFalse (formatted.Contains "2,5") "Float must not use comma decimal separator"

            testCase "should format all datasets for non-histogram metric" <| fun _ ->
                let getSet =
                    SimpleDataSet.create [ ("method", "GET") ] (Int 3)
                    |> DataSet.createFromSimple
                    |> okOrFail

                let postSet =
                    SimpleDataSet.create [ ("method", "POST") ] (Int 5)
                    |> DataSet.createFromSimple
                    |> okOrFail

                let metric =
                    Metric.createMetric
                        (MetricName.createOrFail "http_requests_total")
                        None
                        None
                        [ getSet; postSet ]

                let formatted = Metric.format metric
                Expect.stringContains formatted "http_requests_total {method=\"GET\"} 3" "Missing GET dataset line"
                Expect.stringContains formatted "http_requests_total {method=\"POST\"} 5" "Missing POST dataset line"

            testCase "should keep blank-line separators between multiple metric sections" <| fun _ ->
                let first =
                    Metric.createSingle "metric_a" (Int 1) None None
                    |> okOrFail
                    |> Metric.format

                let second =
                    Metric.createSingle "metric_b" (Int 2) None None
                    |> okOrFail
                    |> Metric.format

                let exposition = first + second
                let expected = "metric_a 1\n\nmetric_b 2\n\n"

                Expect.equal exposition expected "Multiple metrics should remain separated by a blank line"
        ]

        testList "Histogram.format" [
            testCase "should include buckets sum and count" <| fun _ ->
                let simple =
                    SimpleHistogramDataSet.create
                        []
                        (HistogramBuckets.create [ 1.0 ])
                        [ 0.5; 2.0 ]

                let histogram =
                    Histogram.createWithSimpleDataSets "request_duration_seconds" (Some "Duration") [ simple ]
                    |> okOrFail

                let formatted = Histogram.format histogram

                Expect.stringContains formatted "# HELP request_duration_seconds Duration" "Missing HELP"
                Expect.stringContains formatted "# TYPE request_duration_seconds histogram" "Missing TYPE"
                Expect.stringContains formatted "request_duration_seconds_bucket{le=\"1\"} 1" "Missing bucket 1"
                Expect.stringContains formatted "request_duration_seconds_bucket{le=\"+Inf\"} 2" "Missing +Inf bucket"
                Expect.stringContains formatted "request_duration_seconds_sum 2.5" "Missing sum"
                Expect.stringContains formatted "request_duration_seconds_count 2" "Missing count"

            testCase "should preserve dataset labels in bucket sum and count lines" <| fun _ ->
                let simple =
                    SimpleHistogramDataSet.create
                        [ ("method", "GET") ]
                        (HistogramBuckets.create [ 0.5 ])
                        [ 0.1; 1.0 ]

                let histogram =
                    Histogram.createWithSimpleDataSets "http_duration_seconds" None [ simple ]
                    |> okOrFail

                let formatted = Histogram.format histogram
                Expect.stringContains formatted "method=\"GET\"" "Missing label in bucket"
                Expect.stringContains formatted "http_duration_seconds_bucket" "Missing bucket metric name"
                Expect.stringContains formatted "http_duration_seconds_sum" "Missing sum metric name"
                Expect.stringContains formatted "http_duration_seconds_count" "Missing count metric name"

            testCase "should include all datasets when histogram has multiple datasets" <| fun _ ->
                let simpleA =
                    SimpleHistogramDataSet.create
                        [ ("method", "GET") ]
                        (HistogramBuckets.create [ 1.0 ])
                        [ 0.5 ]

                let simpleB =
                    SimpleHistogramDataSet.create
                        [ ("method", "POST") ]
                        (HistogramBuckets.create [ 1.0 ])
                        [ 0.8 ]

                let histogram =
                    Histogram.createWithSimpleDataSets "multi_duration_seconds" None [ simpleA; simpleB ]
                    |> okOrFail

                let formatted = Histogram.format histogram
                Expect.stringContains formatted "method=\"GET\"" "Missing GET label"
                Expect.stringContains formatted "method=\"POST\"" "Missing POST label"
        ]
    ]

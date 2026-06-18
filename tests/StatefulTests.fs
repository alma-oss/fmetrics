module Alma.Metrics.Tests.StatefulTests

open Expecto
open Alma.Metrics
open Alma.Metrics.Tests.Utils

let private getMetricLine metricName (formatted: string) =
    formatted.Split '\n'
    |> Array.tryFind (fun line -> line.StartsWith(metricName + " "))

[<Tests>]
let statefulTests =
    testList "Stateful" [
        testList "State metric values" [
            testCase "should return stored value when metric is set then read" <| fun _ ->
                let reg = Registry.create ()
                let name = MetricName.createOrFail "state_set_get"
                State.setMetricValueIn reg (Int 10) name

                match State.getMetricIn reg name with
                | None -> failtest "Metric should exist"
                | Some metric ->
                    Expect.equal (Metric.singleValue metric) (Some (Int 10)) "Single value mismatch"

            testCase "should accumulate value when metric is incremented twice" <| fun _ ->
                let reg = Registry.create ()
                let name = MetricName.createOrFail "state_increment"
                State.incrementMetricValueIn reg (Int 2) name |> ignore
                State.incrementMetricValueIn reg (Int 3) name |> ignore

                match State.getMetricIn reg name with
                | None -> failtest "Metric should exist"
                | Some metric ->
                    Expect.equal (Metric.singleValue metric) (Some (Int 5)) "Incremented value mismatch"

            testCase "should accumulate metric-set value when incremented twice" <| fun _ ->
                let reg = Registry.create ()
                let name = MetricName.createOrFail "state_set_inc"
                State.incrementMetricSetValueIn reg (Int 3) name DataSetKey.empty |> ignore
                State.incrementMetricSetValueIn reg (Int 4) name DataSetKey.empty |> ignore

                match State.getMetricIn reg name with
                | None -> failtest "Metric should exist"
                | Some metric ->
                    Expect.equal (Metric.singleValue metric) (Some (Int 7)) "Incremented set value mismatch"

            testCase "should overwrite metric-set value when set twice" <| fun _ ->
                let reg = Registry.create ()
                let name = MetricName.createOrFail "state_set_set"
                State.setMetricSetValueIn reg (Int 10) name DataSetKey.empty
                State.setMetricSetValueIn reg (Int 20) name DataSetKey.empty

                match State.getMetricIn reg name with
                | None -> failtest "Metric should exist"
                | Some metric ->
                    Expect.equal (Metric.singleValue metric) (Some (Int 20)) "Set overwrites previous value"

            testCase "should include newly set metric in bulk metric read" <| fun _ ->
                let reg = Registry.create ()
                let name = MetricName.createOrFail "bulk_read"
                State.setMetricValueIn reg (Int 99) name

                let metrics = State.getMetricsIn reg ()
                let found =
                    metrics
                    |> List.tryFind (fun m -> MetricName.value m.Name = MetricName.value name)

                match found with
                | None -> failtest "getMetrics should include newly set metric"
                | Some metric ->
                    Expect.equal (Metric.singleValue metric) (Some (Int 99)) "Bulk metric value mismatch"
        ]

        testList "State histograms" [
            testCase "should compute expected histogram counts sum and count after observations" <| fun _ ->
                let reg = Registry.create ()
                let histogramMetric =
                    HistogramMetric.createOrFail "state_hist" (HistogramBuckets.create [ 1.0 ])

                State.observeHistogramSetValueIn reg histogramMetric 0.5 DataSetKey.empty
                State.observeHistogramSetValueIn reg histogramMetric 2.0 DataSetKey.empty

                match State.getHistogramIn reg histogramMetric with
                | None -> failtest "Histogram should exist"
                | Some histogram ->
                    let dataSet = histogram.DataSets |> List.head
                    let counts = dataSet.Buckets |> List.map (fun b -> b.CumulativeCount)
                    Expect.equal counts [ 1; 2 ] "Histogram counts mismatch"
                    Expect.equal dataSet.Sum 2.5 "Histogram sum mismatch"
                    Expect.equal dataSet.Count 2 "Histogram count mismatch"

            testCase "should store histogram under metric name from histogram definition" <| fun _ ->
                let reg = Registry.create ()
                let histogramMetric =
                    HistogramMetric.createOrFail "state_hist_name" (HistogramBuckets.create [ 1.0; 2.0 ])

                State.observeHistogramSetValueIn reg histogramMetric 1.5 DataSetKey.empty

                let histogram = State.getHistogramIn reg histogramMetric
                Expect.isSome histogram "Histogram should be retrievable by baked metric name"

            testCase "should include newly observed histogram in bulk histogram read" <| fun _ ->
                let reg = Registry.create ()
                let histogramMetric =
                    HistogramMetric.createOrFail "bulk_hist" (HistogramBuckets.create [ 1.0 ])

                State.observeHistogramSetValueIn reg histogramMetric 0.5 DataSetKey.empty

                let histograms = State.getHistogramsIn reg ()
                let expectedName =
                    histogramMetric
                    |> HistogramMetric.name
                    |> MetricName.value

                let found =
                    histograms
                    |> List.tryFind (fun h -> MetricName.value h.Name = expectedName)

                match found with
                | None -> failtest "getHistograms should include newly observed histogram"
                | Some histogram ->
                    match histogram.DataSets with
                    | [] -> failtest "Histogram should contain at least one dataset"
                    | firstDataSet :: _ ->
                        Expect.equal firstDataSet.Count 1 "Histogram should contain one observation"
                        Expect.equal firstDataSet.Sum 0.5 "Histogram sum should match the observed value"
        ]

        testList "ServiceStatus" [
            testCase "should format enabled value when marked as enabled" <| fun _ ->
                let reg = Registry.create ()

                let markEnabled =
                    ServiceStatus.markAsEnabledIn reg instance Audience.Sys
                    |> okOrFail

                let (ServiceStatus.MarkAsEnabled enableFn) = markEnabled

                enableFn ()
                let enabledValue = ServiceStatus.getFormattedValueIn reg ()

                match getMetricLine "service_status" enabledValue with
                | None -> failtest "Missing service_status metric line"
                | Some line ->
                    Expect.stringEnds line " 1" "Service should be enabled"

            testCase "should format disabled value when marked as disabled" <| fun _ ->
                let reg = Registry.create ()

                let markEnabled =
                    ServiceStatus.markAsEnabledIn reg instance Audience.Sys
                    |> okOrFail

                let markDisabled =
                    ServiceStatus.markAsDisabledIn reg instance Audience.Sys
                    |> okOrFail

                let (ServiceStatus.MarkAsEnabled enableFn) = markEnabled
                let (ServiceStatus.MarkAsDisabled disableFn) = markDisabled

                enableFn ()

                disableFn ()
                let disabledValue = ServiceStatus.getFormattedValueIn reg ()

                match getMetricLine "service_status" disabledValue with
                | None -> failtest "Missing service_status metric line"
                | Some line ->
                    Expect.stringEnds line " 0" "Service should be disabled"
        ]

        testList "ResourceAvailability" [
            testCase "should include multi-tenant service labels when enabled" <| fun _ ->
                let reg = Registry.create ()
                let resource =
                    ResourceAvailability.createForMultiTenantServiceFromStrings
                        "postgres"
                        "db"
                        "dc1"
                        testBox
                        Audience.Sys

                match ResourceAvailability.enableIn reg instance resource with
                | Error e -> failtestf "Enable should succeed, got %A" e
                | Ok _ ->
                    let formatted = ResourceAvailability.getFormattedValueIn reg ()
                    Expect.stringContains formatted "resource_availability" "Missing metric"
                    Expect.stringContains formatted "res_svc_zone=\"data\"" "Missing zone label"
                    Expect.stringContains formatted "res_svc_bucket=\"lmc\"" "Missing bucket label"

                    match getMetricLine "resource_availability" formatted with
                    | None -> failtest "Missing resource_availability metric line"
                    | Some line ->
                        Expect.stringEnds line " 1" "Resource should be enabled"

            testCase "should omit service zone label for common resource variant" <| fun _ ->
                let reg = Registry.create ()
                let resource =
                    ResourceAvailability.createFromStrings
                        "redis"
                        "cache-01"
                        "eu-west"
                        Audience.Sys

                match ResourceAvailability.enableIn reg instance resource with
                | Error e -> failtestf "Enable should succeed, got %A" e
                | Ok _ ->
                    let formatted = ResourceAvailability.getFormattedValueIn reg ()
                    Expect.stringContains formatted "res_type=\"redis\"" "Missing res_type label"
                    Expect.stringContains formatted "res_identification=\"cache-01\"" "Missing res_identification"
                    Expect.stringContains formatted "res_location=\"eu-west\"" "Missing res_location"
                    Expect.isFalse (formatted.Contains "res_svc_zone") "Common resource must not have zone label"

            testCase "should include service domain label for service resource variant" <| fun _ ->
                let reg = Registry.create ()
                let resource =
                    ResourceAvailability.createForServiceFromStrings
                        "mysql"
                        "db-primary"
                        "us-east"
                        instance
                        Audience.Sys

                match ResourceAvailability.enableIn reg instance resource with
                | Error e -> failtestf "Enable should succeed, got %A" e
                | Ok _ ->
                    let formatted = ResourceAvailability.getFormattedValueIn reg ()
                    Expect.stringContains formatted "res_type=\"mysql\"" "Missing res_type label"
                    Expect.stringContains formatted "res_svc_domain=\"consents\"" "Missing res_svc_domain"
                    Expect.isFalse (formatted.Contains "res_svc_zone") "Service resource must not have zone label"

            testCase "should keep resource identification visible after disable" <| fun _ ->
                let reg = Registry.create ()
                let resource =
                    ResourceAvailability.createForMultiTenantServiceFromStrings
                        "kafka"
                        "broker-1"
                        "dc2"
                        testBox
                        Audience.Sys

                ResourceAvailability.enableIn reg instance resource |> ignore

                match ResourceAvailability.disableIn reg instance resource with
                | Error e -> failtestf "Disable should succeed, got %A" e
                | Ok _ ->
                    let formatted = ResourceAvailability.getFormattedValueIn reg ()
                    Expect.stringContains formatted "res_identification=\"broker-1\"" "Missing identification"
        ]
    ]

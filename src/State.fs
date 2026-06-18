namespace Alma.Metrics

open System.Collections.Concurrent
open System.Collections.Generic
open System

type private HistogramObservation = {
    Bounds: float list
    CumulativeBucketCounts: int list
    Sum: float
    Count: int
}

type Registry = private {
    MetricsWithDataSets: ConcurrentDictionary<MetricName, ConcurrentDictionary<DataSetKey, MetricValue>>
    MetricsWithValues: ConcurrentDictionary<MetricName, MetricValue>
    MetricsWithHistogramDataSets: ConcurrentDictionary<MetricName, ConcurrentDictionary<DataSetKey, HistogramObservation>>
}

[<RequireQualifiedAccess>]
module Registry =
    let create () = {
        MetricsWithDataSets = ConcurrentDictionary()
        MetricsWithValues = ConcurrentDictionary()
        MetricsWithHistogramDataSets = ConcurrentDictionary()
    }

    let defaultRegistry = create ()

    /// Given a function whose first argument is a Registry, returns a tuple of
    /// (the function itself, the function partially applied to defaultRegistry).
    /// Use to declare the *In variant and the default-registry variant together:
    ///
    ///     let enableIn, enable = Registry.withDefault (fun reg ... -> ...)
    ///
    let withDefault (f: Registry -> 'a) : (Registry -> 'a) * 'a =
        f, f defaultRegistry

module State =
    type private MetricDataSet = ConcurrentDictionary<DataSetKey, MetricValue>
    type private HistogramDataSet = ConcurrentDictionary<DataSetKey, HistogramObservation>

    let private kvPairToTuple (kvPair: KeyValuePair<_, _>) =
        (kvPair.Key, kvPair.Value)

    let private addSetValue value key (metricDataSet: MetricDataSet) =
        metricDataSet.AddOrUpdate(
            key,
            value,
            fun _ old -> old + value
        )

    let private setSetValue value key (metricDataSet: MetricDataSet) =
        metricDataSet.AddOrUpdate(
            key,
            value,
            fun _ _ -> value
        )

    let private createHistogramObservation buckets =
        let normalizedBounds =
            buckets
            |> HistogramBuckets.value

        {
            Bounds = normalizedBounds
            CumulativeBucketCounts = normalizedBounds |> List.map (fun _ -> 0)
            Sum = 0.0
            Count = 0
        }

    let private addHistogramObservationValue value observation =
        let newBucketCounts =
            List.map2 
                (fun currentCount bound ->
                    if value <= bound then currentCount + 1
                    else currentCount
                ) 
                observation.CumulativeBucketCounts
                observation.Bounds

        {
            observation with
                CumulativeBucketCounts = newBucketCounts
                Sum = observation.Sum + value
                Count = observation.Count + 1
        }

    let private observeHistogramValue buckets value key (histogramDataSet: HistogramDataSet) =
        let initialObservation =
            buckets
            |> createHistogramObservation
            |> addHistogramObservationValue value

        histogramDataSet.AddOrUpdate(
            key,
            initialObservation,
            fun _ observation ->
                observation
                |> addHistogramObservationValue value
        )
        |> ignore

    let private toHistogramDataSet (key, observation) =
        {
            Key = key
            Buckets =
                List.map2 
                    (fun count bound ->
                        {
                            UpperBound = HistogramBound.toMetricValue bound
                            CumulativeCount = count
                        }
                    )
                    observation.CumulativeBucketCounts
                    observation.Bounds
            Sum = observation.Sum
            Count = observation.Count
            Timestamp = None
        }

    let private getOrCreateMetricDataSetIn (registry: Registry) metric =
        registry.MetricsWithDataSets.GetOrAdd(metric, fun _ -> MetricDataSet())

    let private getOrCreateHistogramDataSetIn (registry: Registry) metric =
        registry.MetricsWithHistogramDataSets.GetOrAdd(metric, fun _ -> HistogramDataSet())

    let private (|HasDataSetIn|_|) (registry: Registry) metric =
        match registry.MetricsWithDataSets.TryGetValue metric with
        | true, dataSet -> Some dataSet
        | _ -> None

    let private (|HasHistogramDataSetIn|_|) (registry: Registry) metric =
        match registry.MetricsWithHistogramDataSets.TryGetValue metric with
        | true, dataSet -> Some dataSet
        | _ -> None

    let private (|HasValueIn|_|) (registry: Registry) metric =
        match registry.MetricsWithValues.TryGetValue metric with
        | true, value -> Some value
        | _ -> None

    //
    // Write
    //

    let incrementMetricSetValueIn, incrementMetricSetValue =
        Registry.withDefault (fun (registry: Registry) value metric setKey ->
            metric
            |> getOrCreateMetricDataSetIn registry
            |> addSetValue value setKey
        )

    let incrementMetricValueIn, incrementMetricValue =
        Registry.withDefault (fun (registry: Registry) value metric ->
            registry.MetricsWithValues.AddOrUpdate(
                metric,
                value,
                fun _ old -> old + value
            )
        )

    let observeHistogramSetValueIn, observeHistogramSetValue =
        Registry.withDefault (fun (registry: Registry) histogramMetric value setKey ->
            let metricName =
                histogramMetric
                |> HistogramMetric.name

            let buckets =
                histogramMetric
                |> HistogramMetric.buckets

            metricName
            |> getOrCreateHistogramDataSetIn registry
            |> observeHistogramValue buckets value setKey
        )

    let enableStatusMetricIn, enableStatusMetric =
        Registry.withDefault (fun (registry: Registry) metric setKey ->
            metric
            |> getOrCreateMetricDataSetIn registry
            |> setSetValue (Int 1) setKey
            |> ignore
        )

    let disableStatusMetricIn, disableStatusMetric =
        Registry.withDefault (fun (registry: Registry) metric setKey ->
            metric
            |> getOrCreateMetricDataSetIn registry
            |> setSetValue (Int 0) setKey
            |> ignore
        )

    let setMetricSetValueIn, setMetricSetValue =
        Registry.withDefault (fun (registry: Registry) value metric setKey ->
            metric
            |> getOrCreateMetricDataSetIn registry
            |> setSetValue value setKey
            |> ignore
        )

    let setMetricValueIn, setMetricValue =
        Registry.withDefault (fun (registry: Registry) value metric ->
            registry.MetricsWithValues.AddOrUpdate(
                metric,
                value,
                fun _ _ -> value
            )
            |> ignore
        )

    //
    // Read
    //

    let getMetricIn, getMetric =
        Registry.withDefault (fun (registry: Registry) metric ->
            match metric with
            | HasDataSetIn registry dataSet ->
                dataSet
                |> Seq.map (kvPairToTuple >> DataSet.createFromTuple)
                |> List.ofSeq
                |> Metric.createMetric metric None None
                |> Some
            | _ ->
                match metric with
                | HasValueIn registry value ->
                    value
                    |> Metric.createSimpleMetric metric
                    |> Some
                | _ ->
                    None
        )

    let getHistogramIn, getHistogram =
        Registry.withDefault (fun (registry: Registry) histogramMetric ->
            let metricName = histogramMetric |> HistogramMetric.name

            match metricName with
            | HasHistogramDataSetIn registry dataSet ->
                dataSet
                |> Seq.map (kvPairToTuple >> toHistogramDataSet)
                |> List.ofSeq
                |> Histogram.createHistogram metricName None
                |> Some
            | _ ->
                None
        )

    let getHistogramsIn, getHistograms =
        Registry.withDefault (fun (registry: Registry) () ->
            registry.MetricsWithHistogramDataSets
            |> Seq.map (
                kvPairToTuple
                >> fun (name, dataSets) ->
                    dataSets
                    |> Seq.map (kvPairToTuple >> toHistogramDataSet)
                    |> List.ofSeq
                    |> Histogram.createHistogram name None
            )
            |> List.ofSeq
        )

    let getMetricsIn, getMetrics =
        Registry.withDefault (fun (registry: Registry) () -> [
            yield!
                registry.MetricsWithValues
                |> Seq.map (
                    kvPairToTuple
                    >> fun nameValue ->
                        nameValue
                        ||> Metric.createSimpleMetric
                )
            yield!
                registry.MetricsWithDataSets
                |> Seq.map (
                    kvPairToTuple
                    >> fun (name, dataSets) ->
                        dataSets
                        |> Seq.map (kvPairToTuple >> DataSet.createFromTuple)
                        |> List.ofSeq
                        |> Metric.createSimpleMetricWithDataSets name
                )
        ])

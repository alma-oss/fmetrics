namespace Metrics

module State =
    open System.Collections.Generic
    open System.Collections.Concurrent

    type private MetricDataSet = ConcurrentDictionary<DataSetKey, MetricValue>

    let private metricsWithDataSets = new ConcurrentDictionary<MetricName, MetricDataSet>()
    let private metricsWithValues = new ConcurrentDictionary<MetricName, MetricValue>()

    let private kvPairToTuple (kvPair: KeyValuePair<_, _>) =
        (kvPair.Key, kvPair.Value)

    let private incrementSetValue value key (metricDataSet: MetricDataSet) =
        metricDataSet.AddOrUpdate(
            key,
            value,
            fun _ (old) -> old + value
        )

    let incrementMetricSetValue value metric setKey =
        match metricsWithDataSets.TryGetValue metric with
        | true, dataSet ->
            dataSet
            |> incrementSetValue value setKey
        | _ ->
            let dataSet = new MetricDataSet()

            if metricsWithDataSets.TryAdd(metric, dataSet)
            then dataSet |> incrementSetValue value setKey
            else failwithf "DataSet \"%A\" for Metric %A was not stored." setKey metric

    let incrementMetricValue value metric =
        metricsWithValues.AddOrUpdate(
            metric,
            value,
            fun _ (old) -> old + value
        )

    let getMetric metric =
        match metricsWithDataSets.TryGetValue metric with
        | true, dataSets ->
            dataSets
            |> Seq.map (kvPairToTuple >> DataSet.createFromTuple)
            |> List.ofSeq
            |> Metric.createMetric metric None None
            |> Some
        | _ ->
            match metricsWithValues.TryGetValue metric with
            | true, value ->
                value
                |> Metric.createSimpleMetric metric
                |> Some
            | _ ->
                None

    let getMetrics () =
        [
            yield!
                metricsWithValues
                |> Seq.map (
                    kvPairToTuple
                    >> fun nameValue ->
                        nameValue
                        ||> Metric.createSimpleMetric
                )
            yield!
                metricsWithDataSets
                |> Seq.map (
                    kvPairToTuple
                    >> fun (name, dataSets) ->
                        dataSets
                        |> Seq.map (kvPairToTuple >> DataSet.createFromTuple)
                        |> List.ofSeq
                        |> Metric.createSimpleMetricWithDataSets name
                )
        ]

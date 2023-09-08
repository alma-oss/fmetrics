namespace Alma.Metrics

module State =
    open System.Collections.Generic
    open System.Collections.Concurrent

    type private MetricDataSet = ConcurrentDictionary<DataSetKey, MetricValue>

    let private metricsWithDataSets = new ConcurrentDictionary<MetricName, MetricDataSet>()
    let private metricsWithValues = new ConcurrentDictionary<MetricName, MetricValue>()

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

    let private createSetValue metric setKey value =
        let dataSet = new MetricDataSet()

        if metricsWithDataSets.TryAdd(metric, dataSet)
        then dataSet |> setSetValue value setKey
        else failwithf "DataSet \"%A\" for Metric %A was not stored." setKey metric

    let private (|HasDataSet|_|) metric =
        match metricsWithDataSets.TryGetValue metric with
        | true, dataSet -> Some dataSet
        | _ -> None

    let private (|HasSetValue|_|) (dataSet: MetricDataSet) setKey =
        match dataSet.TryGetValue setKey with
        | true, value -> Some value
        | _ -> None

    let private (|HasValue|_|) metric =
        match metricsWithValues.TryGetValue metric with
        | true, dataSet -> Some dataSet
        | _ -> None

    //
    // Write
    //

    let incrementMetricSetValue value metric setKey =
        match metric with
        | HasDataSet dataSet -> addSetValue value setKey dataSet
        | _ -> createSetValue metric setKey value

    let incrementMetricValue value metric =
        metricsWithValues.AddOrUpdate(
            metric,
            value,
            fun _ old -> old + value
        )

    let enableStatusMetric metric setKey =
        match metric with
        | HasDataSet dataSet ->
            match setKey with
            | HasSetValue dataSet value when value = Int 1 -> ()
            | _ -> setSetValue (Int 1) setKey dataSet |> ignore
        | _ -> createSetValue metric setKey (Int 1) |> ignore

    let disableStatusMetric metric setKey =
        match metric with
        | HasDataSet dataSet ->
            match setKey with
            | HasSetValue dataSet value when value = Int 0 -> ()
            | _ -> setSetValue (Int 0) setKey dataSet |> ignore
        | _ -> createSetValue metric setKey (Int 0) |> ignore

    let setMetricSetValue value metric setKey =
        match metric with
        | HasDataSet dataSet -> setSetValue value setKey dataSet
        | _ -> createSetValue metric setKey value
        |> ignore

    let setMetricValue value metric =
        metricsWithValues.AddOrUpdate(
            metric,
            value,
            fun _ _ -> value
        )
        |> ignore

    //
    // Read
    //

    let getMetric metric =
        match metric with
        | HasDataSet dataSet ->
            dataSet
            |> Seq.map (kvPairToTuple >> DataSet.createFromTuple)
            |> List.ofSeq
            |> Metric.createMetric metric None None
            |> Some
        | _ ->
            match metric with
            | HasValue value ->
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

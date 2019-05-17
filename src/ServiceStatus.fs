namespace Metrics

module ServiceStatus =
    type MarkAsEnabled = MarkAsEnabled of (unit -> unit)
    type MarkAsDisabled = MarkAsDisabled of (unit -> unit)

    module MarkAsEnabled =
        let execute (MarkAsEnabled f) = f()

    module MarkAsDisabled =
        let execute (MarkAsDisabled f) = f()

    type ServiceStatus = {
        MarkAsEnabled: MarkAsEnabled
        MarkAsDisabled: MarkAsDisabled
    }

    let private createDataSetKey instance audience =
        [
            ("audience", audience |> Audience.value)
        ]
        |> DataSetKey.createFromInstance instance

    let private serviceStatusMetric = "service_status" |> MetricName.createOrFail

    let markAsEnabled instance audience =
        audience
        |> createDataSetKey instance
        |> Result.map (fun dataSetKey -> fun () -> State.enableStatusMetric serviceStatusMetric dataSetKey)
        |> Result.map MarkAsEnabled
        |> Result.mapError DataSetError

    let markAsDisabled instance audience =
        audience
        |> createDataSetKey instance
        |> Result.map (fun dataSetKey -> fun () -> State.disableStatusMetric serviceStatusMetric dataSetKey)
        |> Result.map MarkAsDisabled
        |> Result.mapError DataSetError

    let getFormattedValue () =
        serviceStatusMetric
        |> State.getMetric
        |> function
            | Some metric ->
                { metric with
                    Description = Some "Current service status."
                    Type = Some MetricType.Gauge
                }
                |> Metric.format
            | _ -> ""

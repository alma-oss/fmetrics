# Examples

All example code for this skill lives here. Examples are ordered by increasing complexity and each is self-contained. Neutral placeholder names are used throughout.

## Basic single metric

```fs
open Alma.Metrics

result {
    let! metric =
        MetricValue.Int 42
        |> Metric.createSimple "requests_total"

    return metric |> Metric.format
}
|> function
    | Ok formatted -> printfn "%s" formatted
    | Error error -> failwithf "Error: %A" error
```

Output:

```
requests_total 42
```

## With description and type

```fs
open Alma.Metrics

result {
    let! metric =
        MetricValue.Int 42
        |> Metric.createSimple "requests_total"

    return
        { metric with
            Description = Some "Total number of handled requests."
            Type = Some MetricType.Counter
        }
        |> Metric.format
}
|> function
    | Ok formatted -> printfn "%s" formatted
    | Error error -> failwithf "Error: %A" error
```

Output:

```
# HELP requests_total Total number of handled requests.
# TYPE requests_total counter
requests_total 42
```

## Labeled data sets

```fs
open Alma.Metrics

result {
    let! metric =
        [
            SimpleDataSet.create [ ("region", "a"); ("tier", "1") ] MetricValue.Infinite
            SimpleDataSet.create [ ("region", "b"); ("tier", "1") ] MetricValue.NegativeInfinite
            SimpleDataSet.create [ ("region", "c"); ("tier", "1") ] (MetricValue.Float 4.2)
            SimpleDataSet.create [ ("region", "d"); ("tier", "1") ] (MetricValue.Int 42)
            SimpleDataSet.create [ ("region", "e"); ("tier", "1") ] MetricValue.NotANumber
        ]
        |> Metric.createWithSimpleDataSets "queue_depth"
            (Some "Queue depth across regions.")
            (Some MetricType.Gauge)

    return metric |> Metric.format
}
|> function
    | Ok formatted -> printfn "%s" formatted
    | Error error -> failwithf "Error: %A" error
```

Output:

```
# HELP queue_depth Queue depth across regions.
# TYPE queue_depth gauge
queue_depth {region="a", tier="1"} +Inf
queue_depth {region="b", tier="1"} -Inf
queue_depth {region="c", tier="1"} 4.2
queue_depth {region="d", tier="1"} 42
queue_depth {region="e", tier="1"} Nan
```

## Histogram from observations

`HistogramBuckets.create` appends the `+Inf` bucket; the library computes cumulative bucket counts, `_sum` and `_count` from the raw observations.

```fs
open Alma.Metrics

result {
    let buckets = HistogramBuckets.create [ 0.05; 0.1; 0.5; 1.0 ]

    let! histogram =
        [
            SimpleHistogramDataSet.create [ ("endpoint", "/a") ] buckets [ 0.03; 0.07; 0.4; 2.0 ]
            SimpleHistogramDataSet.create [ ("endpoint", "/b") ] buckets [ 0.2 ]
        ]
        |> Histogram.createWithSimpleDataSets "request_duration_seconds"
            (Some "Request duration in seconds.")

    return histogram |> Histogram.format
}
|> function
    | Ok formatted -> printfn "%s" formatted
    | Error error -> failwithf "Error: %A" error
```

Output:

```
# HELP request_duration_seconds Request duration in seconds.
# TYPE request_duration_seconds histogram
request_duration_seconds_bucket{le="0.05", endpoint="/a"} 1
request_duration_seconds_bucket{le="0.1", endpoint="/a"} 2
request_duration_seconds_bucket{le="0.5", endpoint="/a"} 3
request_duration_seconds_bucket{le="1", endpoint="/a"} 3
request_duration_seconds_bucket{le="+Inf", endpoint="/a"} 4
request_duration_seconds_sum{endpoint="/a"} 2.5
request_duration_seconds_count{endpoint="/a"} 4
request_duration_seconds_bucket{le="0.05", endpoint="/b"} 0
request_duration_seconds_bucket{le="0.1", endpoint="/b"} 0
request_duration_seconds_bucket{le="0.5", endpoint="/b"} 1
request_duration_seconds_bucket{le="1", endpoint="/b"} 1
request_duration_seconds_bucket{le="+Inf", endpoint="/b"} 1
request_duration_seconds_sum{endpoint="/b"} 0.2
request_duration_seconds_count{endpoint="/b"} 1
```

## State accumulation workflow

Parts typically live in different places in an application: a reusable key builder, a constant metric name, write sites that accumulate, and a read/format site.

```fs
open Alma.Metrics

// reusable key builder
let createRegionTierKey region tier =
    [
        ("region", region |> string)
        ("tier", tier |> string)
    ]
    |> List.map Label.create
    |> Result.sequence
    |> Result.map DataSetKey
    |> Result.mapError LabelError

// constant, known-valid metric name
let metricName =
    match "items_processed" |> MetricName.create with
    | Ok validName -> validName
    | Error error -> failwithf "%A" error

// write sites: increments accumulate via MetricValue (+)
result {
    let! regionA = createRegionTierKey "a" 1
    State.incrementMetricSetValue (MetricValue.Int 1) metricName regionA |> ignore
    State.incrementMetricSetValue (MetricValue.Int 2) metricName regionA |> ignore
    State.incrementMetricSetValue (MetricValue.Float 1.2) metricName regionA |> ignore

    let! regionB = createRegionTierKey "b" 1
    State.incrementMetricSetValue (MetricValue.Int 3) metricName regionB |> ignore
    State.incrementMetricSetValue (MetricValue.Infinite) metricName regionB |> ignore
}
|> function
    | Error error -> failwithf "Error: %A" error
    | _ -> ()

// read/format site: state has no description/type, so add them
match State.getMetric metricName with
| Some metric ->
    { metric with
        Description = Some "Items processed per region."
        Type = Some MetricType.Counter
    }
    |> Metric.format
    |> printfn "%s"
| None -> ()
```

Output:

```
# HELP items_processed Items processed per region.
# TYPE items_processed counter
items_processed {region="a", tier="1"} 4.2
items_processed {region="b", tier="1"} +Inf
```

## Histogram state accumulation

Same split as above: a constant `HistogramMetric` (name + buckets), a reusable key builder, write sites that observe raw values, and a read/format site.

```fs
open Alma.Metrics

// constant histogram definition, shared by every write site
let requestDuration =
    match HistogramMetric.create "request_duration_seconds" (HistogramBuckets.create [ 0.05; 0.1; 0.5; 1.0 ]) with
    | Ok validMetric -> validMetric
    | Error error -> failwithf "%A" error

// reusable key builder
let createEndpointKey endpoint =
    [ ("endpoint", endpoint) ]
    |> List.map Label.create
    |> Result.sequence
    |> Result.map DataSetKey
    |> Result.mapError LabelError

// write sites: each observation updates buckets, sum and count
result {
    let! endpointA = createEndpointKey "/a"
    State.observeHistogramSetValue requestDuration 0.03 endpointA
    State.observeHistogramSetValue requestDuration 0.07 endpointA
    State.observeHistogramSetValue requestDuration 0.4 endpointA
    State.observeHistogramSetValue requestDuration 2.0 endpointA
}
|> function
    | Error error -> failwithf "Error: %A" error
    | _ -> ()

// read/format site: state has no description, so add it
match State.getHistogram requestDuration with
| Some histogram ->
    { histogram with
        Description = Some "Request duration in seconds."
    }
    |> Histogram.format
    |> printfn "%s"
| None -> ()
```

Output:

```
# HELP request_duration_seconds Request duration in seconds.
# TYPE request_duration_seconds histogram
request_duration_seconds_bucket{le="0.05", endpoint="/a"} 1
request_duration_seconds_bucket{le="0.1", endpoint="/a"} 2
request_duration_seconds_bucket{le="0.5", endpoint="/a"} 3
request_duration_seconds_bucket{le="1", endpoint="/a"} 3
request_duration_seconds_bucket{le="+Inf", endpoint="/a"} 4
request_duration_seconds_sum{endpoint="/a"} 2.5
request_duration_seconds_count{endpoint="/a"} 4
```

## Per-service data set key

`DataSetKey.createFromInstance` prepends the `svc_*` identity labels from an `Instance` before your custom labels.

```fs
open Alma.Metrics
open Alma.ServiceIdentification

let instance = {
    Domain = Domain "demo"
    Context = Context "example"
    Purpose = Purpose "common"
    Version = Version "stable"
}

let key =
    [
        ("operation", "read")
        ("stream", "primary")
    ]
    |> DataSetKey.createFromInstance instance
```

## Service status metric

```fs
open Alma.Metrics
open Alma.Metrics.ServiceStatus
open Alma.ServiceIdentification

let instance = {
    Domain = Domain "demo"
    Context = Context "example"
    Purpose = Purpose "common"
    Version = Version "stable"
}

ServiceStatus.markAsEnabled instance (Audience "arch")
|> function
    | Ok mark -> mark |> MarkAsEnabled.execute
    | Error error -> failwithf "Error: %A" error

ServiceStatus.getFormattedValue ()
|> printfn "%s"
```

Output:

```
# HELP service_status Current service status.
# TYPE service_status gauge
service_status {svc_domain="demo", svc_context="example", svc_purpose="common", svc_version="stable", audience="arch"} 1
```

## Resource availability metric

```fs
open Alma.Metrics
open Alma.ServiceIdentification

let instance = {
    Domain = Domain "demo"
    Context = Context "example"
    Purpose = Purpose "common"
    Version = Version "stable"
}

let clusterResource =
    ResourceAvailability.createFromStrings "demo-cluster" "demo-cluster-1" "example.host" (Audience "sys")
let nodeResource =
    ResourceAvailability.createFromStrings "demo-node" "demo-node-1" "example.host" (Audience "sys")

[ clusterResource; nodeResource ]
|> List.iter (fun resource ->
    resource
    |> ResourceAvailability.enable instance
    |> function
        | Ok () -> ()
        | Error error -> failwithf "Error: %A" error
)

ResourceAvailability.getFormattedValue ()
|> printfn "%s"
```

Output:

```
# HELP resource_availability Current instance resources.
# TYPE resource_availability gauge
resource_availability {svc_domain="demo", svc_context="example", svc_purpose="common", svc_version="stable", res_location="example.host", res_type="demo-cluster", res_identification="demo-cluster-1", audience="sys"} 1
resource_availability {svc_domain="demo", svc_context="example", svc_purpose="common", svc_version="stable", res_location="example.host", res_type="demo-node", res_identification="demo-node-1", audience="sys"} 1
```

## Isolated registry

Every `State`, `ServiceStatus` and `ResourceAvailability` function has an `*In` variant that takes a `Registry` first. Values written to an explicit registry are invisible to the plain functions, which use `Registry.defaultRegistry`.

```fs
open Alma.Metrics
open Alma.Metrics.ServiceStatus
open Alma.ServiceIdentification

let instance = {
    Domain = Domain "demo"
    Context = Context "example"
    Purpose = Purpose "common"
    Version = Version "stable"
}

let registry = Registry.create ()
let jobsProcessed = "jobs_processed" |> MetricName.createOrFail

State.incrementMetricValueIn registry (MetricValue.Int 3) jobsProcessed |> ignore

ServiceStatus.markAsEnabledIn registry instance (Audience "arch")
|> function
    | Ok mark -> mark |> MarkAsEnabled.execute
    | Error error -> failwithf "Error: %A" error

// Some metric with value 3
State.getMetricIn registry jobsProcessed |> ignore
// None: the default registry was not written
State.getMetric jobsProcessed |> ignore

ServiceStatus.getFormattedValueIn registry ()
|> printfn "%s"
```

Output:

```
# HELP service_status Current service status.
# TYPE service_status gauge
service_status {svc_domain="demo", svc_context="example", svc_purpose="common", svc_version="stable", audience="arch"} 1
```

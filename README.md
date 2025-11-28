F-Metrics
=========

[![NuGet](https://img.shields.io/nuget/v/Alma.Metrics.svg)](https://www.nuget.org/packages/Alma.Metrics)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Alma.Metrics.svg)](https://www.nuget.org/packages/Alma.Metrics)
[![Tests](https://github.com/alma-oss/fmetrics/actions/workflows/tests.yaml/badge.svg)](https://github.com/alma-oss/fmetrics/actions/workflows/tests.yaml)

Library for showing app metrics for prometheus.
See https://prometheus.io/docs/instrumenting/exposition_formats/ for more information about format itself

## Install

Add following into `paket.references`
```
Alma.Metrics
```

## Use

### Simple metric
_With error handling_

```fs
open Alma.Metrics

result {
    let! metric =
        MetricValue.Int 42
        |> Metric.createSimple "simple_metric"

    return metric |> Metric.format
}
|> function
    | Ok formatted -> printfn "%s" formatted
    | Error error -> failwithf "Error: %A" error
```

Formatted Metric:
```
simple_metric 42
```

### Simple metric with description and type
_With error handling_

```fs
open Alma.Metrics

result {
    let! metric =
        MetricValue.Int 42
        |> Metric.createSimple "simple_metric"

    return
        { metric with
            Description = Some "This is just a single simple metric."
            Type = Some MetricType.Counter
        } |> Metric.format
}
|> function
    | Ok formatted -> printfn "%s" formatted
    | Error error -> failwithf "Error: %A" error
```

Formatted Metric:
```
# HELP simple_metric This is just a single simple metric.
# TYPE simple_metric counter
simple_metric 42
```

### Metric with description, type and labeled data sets
_With error handling_

```fs
open Alma.Metrics

result {
    let! metric =
        [
            SimpleDataSet.create [ ("month", "1"); ("year", "2018") ] MetricValue.Infinite
            SimpleDataSet.create [ ("month", "2"); ("year", "2018") ] MetricValue.NegativeInfinite
            SimpleDataSet.create [ ("month", "3"); ("year", "2018") ] (MetricValue.Float 4.2)
            SimpleDataSet.create [ ("month", "4"); ("year", "2018") ] (MetricValue.Int 42)
            SimpleDataSet.create [ ("month", "5"); ("year", "2018") ] MetricValue.NotANumber
        ]
        |> Metric.createWithSimpleDataSets "complex_metric"
            (Some "This is metric with data sets with different lables and values.")
            (Some MetricType.Counter)

    return metric |> Metric.format
}
|> function
    | Ok formatted -> printfn "%s" formatted
    | Error error -> failwithf "Error: %A" error
```

Formatted Metric:
```
# HELP complex_metric This is metric with data sets with different lables and values.
# TYPE complex_metric counter
complex_metric {month="1", year="2018"} +Inf
complex_metric {month="2", year="2018"} -Inf
complex_metric {month="3", year="2018"} 4.2
complex_metric {month="4", year="2018"} 42
complex_metric {month="5", year="2018"} Nan
```

### Errors (result)
Metric name (and Label name) has a specific format, so if you create a metric with invalid name, you will get `Result.Error` instead of `Result.Ok`, so you need to handle it.

### Real-life
Above examples are just the simpliest ones. But in the real case scenario, you will probably use more complex ones, since metric values and keys will be stored somewhere and you will probably map them or compose by custom functions. They probably wont be use directly as is in the example.

There are many constructors (`create` functions) for every metric part (`Metric`, `MetricValue`, `DataSet`, ...).

### Metric with state
_With error handling_

```fs
open Alma.Metrics

// PART 1: helper function for creating your specific data set key
let createMonthYearKey month year =
    [
        ("month", month |> string)
        ("year", year |> string)
    ]
    |> List.map Label.create
    |> Result.sequence
    |> Result.map DataSetKey
    |> Result.mapError LabelError

// PART 2: create metric name for your metric (or fail with an exception)
let metricName =
    match "metric_with_state" |> MetricName.create with
    | Ok validName -> validName
    | Error error -> failwithf "%A" error

// PART 3: increment inner state of metrics for your metric with specific keys
result {
    let! january2018 = createMonthYearKey 1 2018
    State.incrementMetricSetValue (MetricValue.Int 1) metricName january2018 |> ignore      // incrementMetricSetValue returns a current value, we can ignore it now
    State.incrementMetricSetValue (MetricValue.Int 2) metricName january2018 |> ignore
    State.incrementMetricSetValue (MetricValue.Float 1.2) metricName january2018 |> ignore  // as you can see, since the value is "MetricValue", you can even mix the value types

    let! february2018 = createMonthYearKey 2 2018
    State.incrementMetricSetValue (MetricValue.Int 3) metricName february2018 |> ignore
    State.incrementMetricSetValue (MetricValue.Infinite) metricName february2018 |> ignore
}
|> function
    | Error error -> failwithf "Error: %A" error    // one of incrementing failed, so we handle error somehow (for now, just fail with an exception)
    | _ -> ()

// PART 4: print current state for your metric
match State.getMetric metricName with
| Some metric ->
    { metric with
        Description = Some "This is metric with state."
        Type = Some MetricType.Counter
    }
    |> Metric.format
    |> printfn "%s"
| None -> ()
```
_NOTE: All parts from the above example would probably be in the different parts of the application, so it is separated also in the example._

Formatted Metric:
```
# HELP metric_with_state This is metric with state.
# TYPE metric_with_state counter
metric_with_state {month="1", year="2018"} 4.2
metric_with_state {month="2", year="2018"} +Inf
```

### Metric for service
If you need metric labels based by `Instance`, you can use shortcut function to do that. _Otherwise it is same as above._

```fs
open Alma.Metrics

let createKeyForInputEvent instance (InputStreamName (StreamName inputStream)) (event: RawEvent) =
    [
        ("event", event.Event |> EventName.value)
        ("input_stream", inputStream)
    ]
    |> DataSetKey.createFromInstance instance
```

## Service metrics
Service metrics (see [confluence](https://confluence.int.lmc.cz/display/ARCH/Services+Metrics)) are special status metrics which has its own audience and format.

### Metric `service_status`
```fs
open Alma.Metrics
open Alma.Metrics

let instance = {
    Domain = Domain "consents"
    Context = Context "example"
    Purpose = Purpose "common"
    Version = Version "stable"
}

let exampleServiceStatus = {
    Audience = Audience.Arch
}

exampleServiceStatus
|> ServiceStatus.enable instance
|> ignore    // ignore is there because `enable` function returns Result, which might have error, but we don't care now

ServiceStatus.getFormattedValue()
|> printfn "%s"
```

Formatted Metric:
```
# HELP service_status Current service status.
# TYPE service_status gauge
service_status {svc_domain="consents", svc_context="example", svc_purpose="common", svc_version="stable", audience="arch"} 1
```

### Metric `resource_availability`
```fs
open Alma.Metrics
open Alma.Metrics

let instance = {
    Domain = Domain "consents"
    Context = Context "example"
    Purpose = Purpose "common"
    Version = Version "stable"
}

let kafkaClusterResource = ResourceAvailability.createFromStrings "kafka-cluster" "kfall-1.dev1.services.lmc" "kfall-1.dev1.services.lmc" Audience.Sys
let kafkaTopicResource = ResourceAvailability.createFromStrings "kafka-topic" "consents-consentorStream-common-all" "kfall-1.dev1.services.lmc" Audience.Sys

[
    kafkaClusterResource
    kafkaTopicResource
]
|> List.iter ((ResourceAvailability.enable instance) >> ignore) // ignore is there because `enable` function returns Result, which might have error, but we don't care now

ResourceAvailability.getFormattedValue()
|> printfn "%s"
```

Formatted Metric:
```
# HELP resource_availability Current instance resources.
# TYPE resource_availability gauge
resource_availability {svc_domain="consents", svc_context="example", svc_purpose="common", svc_version="stable", res_location="kfall-1.dev1.services.lmc", res_type="kafka-cluster", res_identification="kfall-1.dev1.services.lmc", audience="sys"} 1
resource_availability {svc_domain="consents", svc_context="example", svc_purpose="common", svc_version="stable", res_location="kfall-1.dev1.services.lmc", res_type="kafka-topic", res_identification="consents-consentorStream-common-all", audience="sys"} 1
```

## Release
1. Increment version in `Metrics.fsproj`
2. Update `CHANGELOG.md`
3. Commit new version and tag it

## Development
### Requirements
- [dotnet core](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial)

### Build
```bash
./build.sh build
```

### Tests
```bash
./build.sh -t tests
```

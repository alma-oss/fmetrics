---
name: fmetrics
description: Use whenever generating or reviewing F# code that produces Prometheus exposition-format metrics with the Alma.Metrics library — building a `Metric` via `Metric.createSimple`, `Metric.create`, `Metric.createWithSimpleDataSets`, rendering with `Metric.format`, building histograms via `Histogram.createWithSimpleDataSets` / `Histogram.format`, accumulating values through the `State` registry (`State.incrementMetricSetValue`, `State.getMetric`, `State.observeHistogramSetValue`, `State.getHistogram`) or an explicit `Registry.create ()` with the `*In` variants, or emitting status metrics via `ServiceStatus.markAsEnabled` / `ResourceAvailability.enable`. Trigger also on mentions of `MetricValue`, `MetricType`, `SimpleDataSet`, `SimpleHistogramDataSet`, `HistogramBuckets`, `HistogramMetric`, `DataSetKey.createFromInstance`, `Audience`, `service_status`, `resource_availability`, Prometheus labels or histogram buckets, or `result {}` validation of metric/label names.
---

# F-Metrics

Library: [alma-oss/fmetrics](https://github.com/alma-oss/fmetrics)
NuGet: `Alma.Metrics`

## Purpose

Alma.Metrics is an F# library for constructing application metrics and rendering them as text in the [Prometheus exposition format](https://prometheus.io/docs/instrumenting/exposition_formats/). It models metric names, typed values, labels and data sets as validated domain types, keeps an optional in-process registry for accumulating values over time, and provides higher-level helpers for `service_status` and `resource_availability` status metrics.

## When to Use

- Generating Prometheus-format output for a scrape endpoint or text export.
- Building counters, gauges, histograms or summaries with labeled data sets.
- Accumulating metric values and histogram observations across a process lifetime via the `State` registry.
- Isolating metric state per test or per scope with an explicit `Registry`.
- Publishing service/resource availability status metrics tied to a service `Instance`.

## When NOT to Use

- You need a full Prometheus client with HTTP scraping, pushgateway, or registry-per-collector semantics — this library only formats text.
- You need metric storage that outlives the process — `State` is in-memory only.
- The consumer is not Prometheus-compatible.

## Main Concepts

- **MetricName** — validated metric identifier; created with `MetricName.create` (returns `Result`) or `MetricName.createOrFail`.
- **MetricValue** — value union: `Int`, `Float`, `Infinite`, `NegativeInfinite`, `NotANumber`; supports `+`.
- **MetricType** — `Counter`, `Gauge`, `Histogram`, `Summary`, `Untyped`.
- **Label** — validated `Name`/`Value` pair; `Label.create` returns `Result`.
- **SimpleDataSet** — untyped-key data point (`(string * string) list` key + `MetricValue` + optional `DateTimeOffset` timestamp).
- **DataSetKey** — validated label set; `DataSetKey.createFromInstance` prefixes service-identity labels.
- **DataSet** — validated key + value + optional `DateTimeOffset` timestamp.
- **Metric** — name + optional description/type + list of data sets; rendered with `Metric.format`.
- **HistogramBuckets** — normalized bucket upper bounds; `HistogramBuckets.create` (infallible) sorts, deduplicates, drops `NaN`/infinite bounds and appends `+Inf`.
- **SimpleHistogramDataSet** — untyped-key histogram data point (`(string * string) list` key + `HistogramBuckets` + raw `float` observations + optional `DateTimeOffset` timestamp).
- **HistogramDataSet** — validated key + cumulative `HistogramBucket` list + `Sum` + `Count` + optional timestamp.
- **Histogram** — name + optional description + list of histogram data sets; rendered with `Histogram.format` as `_bucket` / `_sum` / `_count` lines.
- **HistogramMetric** — validated `MetricName` + `HistogramBuckets`; the definition `State` observations are recorded against.
- **Registry** — concurrent store of accumulated metrics and histogram observations; `Registry.create ()` makes an isolated one, `Registry.defaultRegistry` is the process-global one.
- **State** — increment/set/observe/read functions over a `Registry`; plain functions target `Registry.defaultRegistry`, `*In` variants take an explicit `Registry`.
- **ServiceStatus** — helper for the `service_status` gauge keyed by `Instance` + `Audience`.
- **ResourceAvailability** — helper for the `resource_availability` gauge describing external resources.
- **Audience** — single-case `Audience of string` wrapper; its string is rendered verbatim into the `audience` label.

## Related Libraries

- **Alma.ServiceIdentification** — supplies `Instance`, `Box`, `Spot` and their accessors used by `DataSetKey.createFromInstance`, `ServiceStatus`, and `ResourceAvailability`.
- **Feather.ErrorHandling** — supplies the `result {}` computation expression and `Result.sequence` / `Result.orFail` used throughout the API.

## Keywords for Search

Alma.Metrics, fmetrics, Prometheus, exposition format, MetricName, MetricValue, MetricType, Counter, Gauge, Histogram, Summary, Label, SimpleDataSet, DataSet, DataSetKey, createFromInstance, Metric.format, Metric.createSimple, Metric.createWithSimpleDataSets, HistogramBuckets, HistogramMetric, SimpleHistogramDataSet, HistogramDataSet, Histogram.format, Histogram.createWithSimpleDataSets, _bucket, _sum, _count, le, State, incrementMetricSetValue, getMetric, observeHistogramSetValue, getHistogram, Registry, getMetricIn, markAsEnabledIn, DateTimeOffset, ServiceStatus, markAsEnabled, ResourceAvailability, service_status, resource_availability, Audience, result CE, F#

## Reference Files

- For composition principles, name validation rules, error handling, state usage, and testing guidance, read `references/preferred-patterns.md`.
- For known pitfalls, the outdated README API, and incorrect assumptions, read `references/anti-patterns.md`.
- For worked, self-contained code examples ordered by increasing complexity, read `references/examples.md`.

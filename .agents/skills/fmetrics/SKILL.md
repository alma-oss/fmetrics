---
name: fmetrics
description: Use whenever generating or reviewing F# code that produces Prometheus exposition-format metrics with the Alma.Metrics library — building a `Metric` via `Metric.createSimple`, `Metric.create`, `Metric.createWithSimpleDataSets`, rendering with `Metric.format`, accumulating values through the global `State` registry (`State.incrementMetricSetValue`, `State.getMetric`), or emitting status metrics via `ServiceStatus.markAsEnabled` / `ResourceAvailability.enable`. Trigger also on mentions of `MetricValue`, `MetricType`, `SimpleDataSet`, `DataSetKey.createFromInstance`, `service_status`, `resource_availability`, Prometheus labels, or `result {}` validation of metric/label names.
---

# F-Metrics

Library: [alma-oss/fmetrics](https://github.com/alma-oss/fmetrics)
NuGet: `Alma.Metrics`

## Purpose

Alma.Metrics is an F# library for constructing application metrics and rendering them as text in the [Prometheus exposition format](https://prometheus.io/docs/instrumenting/exposition_formats/). It models metric names, typed values, labels and data sets as validated domain types, keeps an optional in-process registry for accumulating values over time, and provides higher-level helpers for `service_status` and `resource_availability` status metrics.

## When to Use

- Generating Prometheus-format output for a scrape endpoint or text export.
- Building counters, gauges, histograms or summaries with labeled data sets.
- Accumulating metric values across a process lifetime via the `State` registry.
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
- **SimpleDataSet** — untyped-key data point (`(string * string) list` key + `MetricValue` + optional timestamp).
- **DataSetKey** — validated label set; `DataSetKey.createFromInstance` prefixes service-identity labels.
- **DataSet** — validated key + value + optional timestamp.
- **Metric** — name + optional description/type + list of data sets; rendered with `Metric.format`.
- **State** — process-global concurrent registry of accumulated metrics (increment/set/read).
- **ServiceStatus** — helper for the `service_status` gauge keyed by `Instance` + `Audience`.
- **ResourceAvailability** — helper for the `resource_availability` gauge describing external resources.
- **Audience** — fixed union of consumer audiences rendered into the `audience` label.

## Related Libraries

- **Alma.ServiceIdentification** — supplies `Instance`, `Box`, `Spot` and their accessors used by `DataSetKey.createFromInstance`, `ServiceStatus`, and `ResourceAvailability`.
- **Feather.ErrorHandling** — supplies the `result {}` computation expression and `Result.sequence` / `Result.orFail` used throughout the API.

## Keywords for Search

Alma.Metrics, fmetrics, Prometheus, exposition format, MetricName, MetricValue, MetricType, Counter, Gauge, Histogram, Summary, Label, SimpleDataSet, DataSet, DataSetKey, createFromInstance, Metric.format, Metric.createSimple, Metric.createWithSimpleDataSets, State, incrementMetricSetValue, getMetric, ServiceStatus, markAsEnabled, ResourceAvailability, service_status, resource_availability, Audience, result CE, F#

## Reference Files

- For composition principles, name validation rules, error handling, state usage, and testing guidance, read `references/preferred-patterns.md`.
- For known pitfalls, the outdated README API, and incorrect assumptions, read `references/anti-patterns.md`.
- For worked, self-contained code examples ordered by increasing complexity, read `references/examples.md`.

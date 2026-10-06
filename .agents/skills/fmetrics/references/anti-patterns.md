# Anti-Patterns

Format: **mistake → why it is wrong → fix**.

## Outdated / Non-existent API

- **Calling `ServiceStatus.enable instance` with a hand-built status record** → the README shows this older shape, but the current API has no `enable` on `ServiceStatus` and no such record; it will not compile → use `ServiceStatus.markAsEnabled instance audience`, run the returned value with `MarkAsEnabled.execute`, then `ServiceStatus.getFormattedValue ()`. See `examples.md` → "Service status metric".

- **Writing `ServiceStatus.MarkAsEnabled.execute mark`** → `ServiceStatus.MarkAsEnabled` resolves to the union case, so `.execute` does not compile → `open Alma.Metrics.ServiceStatus` and call `MarkAsEnabled.execute` / `MarkAsDisabled.execute`.

- **Using `Audience.Arch`, `Audience.Sys` or other named `Audience` cases** → `Audience` is a single-case `Audience of string` wrapper with no named cases; it will not compile → pass `Audience "arch"`, `Audience "sys"`, etc. The string is the exact `audience` label value.

- **Passing `DateTime` timestamps to `SimpleDataSet.createWithTimestamp`, `SimpleHistogramDataSet.createWithTimestamp`, or the `Timestamp` field of `DataSet` / `HistogramDataSet`** → these fields are `DateTimeOffset option`; it will not compile → pass a `DateTimeOffset` (e.g. `DateTimeOffset.UtcNow`).

- **Matching `HistogramBuckets.create` on `Ok` / `Error`** → the README "Histogram with state" example does this, but `HistogramBuckets.create` is infallible and returns `HistogramBuckets` directly; it will not compile → bind the result directly. Only `HistogramMetric.create` returns `Result`.

- **Expecting `ServiceStatus.markAsEnabled` to mutate state immediately** → it returns a `Result` wrapping a *deferred* `MarkAsEnabled` action; nothing is recorded until you execute it → unwrap the `Result` and call `MarkAsEnabled.execute` (likewise `MarkAsDisabled.execute`).

## Ignoring Results

- **Discarding the `Result` from `MetricName.create`, `Label.create`, `Metric.create*`, `Histogram.create*`, `HistogramMetric.create`, `enable`/`disable`, or `markAs*`** → name/label validation failures are silently dropped and the metric is never produced → bind with `let!` inside `result {}` and handle the `Error` branch; convert error types with `Result.mapError`.

- **Using `MetricName.createOrFail` or `HistogramMetric.createOrFail` on dynamic or user-supplied names** → it throws on invalid input instead of returning an error → reserve `createOrFail` for hardcoded constant names; use `MetricName.create` / `HistogramMetric.create` for anything dynamic.

## Value Pitfalls

- **Summing opposite infinities through the registry (e.g. incrementing a data set holding `Infinite` by `NegativeInfinite`)** → `MetricValue.(+)` raises an exception for `Inf + (-Inf)` → avoid accumulating both infinities into the same data set, or guard the value before incrementing.

- **Observing `nan` into a histogram** → `NaN` falls into no bucket (not even `+Inf`) yet increments `_count` and turns `_sum` into `NaN`, so the series becomes inconsistent → filter out `NaN` before `State.observeHistogramSetValue` or `SimpleHistogramDataSet.create`.

- **Adding `infinity` (or `nan`) to histogram bounds** → `HistogramBuckets.create` drops non-finite bounds and appends `+Inf` itself → pass only finite upper bounds.

- **Encoding special values as strings (`"+Inf"`, `"NaN"`)** → bypasses the value model and produces malformed or inconsistent output → use `MetricValue.Infinite`, `MetricValue.NegativeInfinite`, `MetricValue.NotANumber`.

## Misusing State

- **Treating `State` as pure or per-request** → the plain `State` / `ServiceStatus` / `ResourceAvailability` functions all write to the process-global `Registry.defaultRegistry`; values persist across calls and tests and are shared across the whole process → use unique metric names per concern, and in tests create a `Registry.create ()` per test and use the `*In` variants.

- **Formatting a metric read from `State` and expecting description/type** → `State.getMetric` / `getMetrics` return metrics with `Description = None` and `Type = None` (and `State.getHistogram` / `getHistograms` return histograms with `Description = None`) → add them via a record copy (`{ metric with Description = ...; Type = ... }`) before `Metric.format` / `Histogram.format`.

- **Expecting `State.getMetrics ()` to include histograms** → histogram observations are stored separately → read them with `State.getHistogram` / `State.getHistograms ()` and render with `Histogram.format`.

- **Building a `HistogramMetric` with different buckets at each call site** → the bucket bounds for a data set key are fixed by its first observation, so later observations with other bounds are counted against the original ones and keys can end up with different layouts → define one `HistogramMetric` constant per histogram and reuse it.

- **Writing to one `Registry` and reading from another** → `*In` writes are invisible to the plain read functions (and vice versa) → pass the same registry to every `*In` call for that scope.

## Output Construction

- **Hand-assembling Prometheus text (`# HELP`, `# TYPE`, label braces) with string concatenation** → easy to violate the exposition format (escaping, label ordering, value rendering) → render exclusively through `Metric.format`, `Histogram.format`, `ServiceStatus.getFormattedValue`, or `ResourceAvailability.getFormattedValue`.

- **Emulating a histogram with `MetricType.Histogram` and hand-built `_bucket` / `le` data sets in `Metric.create*`, or with a counter labeled per range** → buckets must be cumulative and paired with `+Inf`, `_sum` and `_count` → use `Histogram.createWithSimpleDataSets` or `State.observeHistogramSetValue` with raw observations.

- **Copying histogram output from the README** → its sample output shows a space before `{` and `le` as the last label; `Histogram.format` emits no space and puts `le` first (`name_bucket{le="0.5", endpoint="/a"} 3`) → assert against the output in `examples.md`.

- **Building per-service label sets manually and prepending `svc_*` by hand** → duplicates identity-label logic and drifts from the canonical key shape → use `DataSetKey.createFromInstance` so identity labels are generated consistently.

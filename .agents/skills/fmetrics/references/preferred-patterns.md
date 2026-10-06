# Preferred Patterns

## Core Principles

- Treat metric construction as validation: most constructors return `Result<_, _>`. Compose them inside a `result {}` computation expression and handle the `Error` branch explicitly.
- Keep value typing explicit. Wrap raw numbers in `MetricValue.Int` / `MetricValue.Float`; use `Infinite`, `NegativeInfinite`, `NotANumber` for special values rather than encoding them as strings.
- Render only through the library. Never hand-write Prometheus text — `Metric.format`, `Histogram.format`, `ServiceStatus.getFormattedValue`, and `ResourceAvailability.getFormattedValue` guarantee a compliant layout.

## Name Validation Rules

Both metric names and label names share the same validation:

- Must be non-empty (`EmptyName` otherwise) and at least 2 characters (`TooShortName` otherwise).
- `-` and spaces are automatically replaced with `_`, so a hyphenated input becomes an underscore name.

Use `MetricName.create` (returns `Result`) when the name is dynamic/untrusted, and `MetricName.createOrFail` only for hardcoded constant names that are known-valid. The same split applies to `HistogramMetric.create` / `HistogramMetric.createOrFail`. `MetricError.value` turns a `MetricError` into a human-readable string.

## Recommended API Usage

- Single value, no metadata: `Metric.createSimple`. Add description/type afterwards with an F# record copy (`{ metric with Description = ...; Type = ... }`). See `examples.md` → "With description and type".
- Single value plus metadata in one call: `Metric.createSingle`.
- Many labeled points: build a `SimpleDataSet list` and call `Metric.createWithSimpleDataSets`; this validates every label in one pass. See `examples.md` → "Labeled data sets".
- Already-validated data sets: `Metric.create` (takes `DataSet list`).
- Extracting a lone unlabeled value back out: `Metric.singleValue` returns `Some value` only when the metric has exactly one data set with no labels.
- Timestamped points: `SimpleDataSet.createWithTimestamp labels value (Some timestamp)` with a `DateTimeOffset`; it renders as Unix seconds after the value.
- Histogram from raw observations: `HistogramBuckets.create bounds` (infallible, `+Inf` appended), then `SimpleHistogramDataSet.create labels buckets observations` per label set, then `Histogram.createWithSimpleDataSets name description`; the library computes cumulative bucket counts, `_sum` and `_count`. See `examples.md` → "Histogram from observations".
- Already-validated histogram data sets: `Histogram.create` (takes `HistogramDataSet list`).

## Error Handling

- Aggregate independent `Result` values with `Result.sequence` (used internally by `createWithSimpleDataSets` and `DataSetKey.createFromInstance`) so a single invalid label fails the whole batch.
- Propagate with `let!` / `return!` inside `result {}`; convert error types at the boundary with `Result.mapError`.
- `HistogramMetric.create` fails with `MetricNameError`, while `Metric.create*` / `Histogram.create*` fail with `MetricError`; map with `Result.mapError MetricError.MetricNameError` when binding both in one `result {}`.
- Surface failures at the edge — log or fail on the `Error` branch rather than ignoring the returned `Result`.

## Composition

- Build reusable key constructors that produce a `DataSetKey`, then share them across data points. See `examples.md` → "State accumulation workflow".
- `DataSetKey.createFromInstance instance labels` prepends `svc_domain`, `svc_context`, `svc_purpose`, `svc_version` from the `Instance`, then your extra labels — use it for any per-service metric so identity labels stay consistent.
- `DataSetKey.empty` is the canonical key for unlabeled metrics; `DataSetKey.labels` reads labels back out.

## State Registry

- `State` functions operate on a concurrent `Registry`. The plain functions use the process-global `Registry.defaultRegistry`; each has an `*In` variant taking the registry first (`State.incrementMetricSetValueIn registry ...`, `State.getMetricIn registry ...`). Create isolated registries with `Registry.create ()`. See `examples.md` → "Isolated registry".
- `incrementMetricSetValue` / `incrementMetricValue` add to existing values (using `MetricValue.(+)`); `setMetricSetValue` / `setMetricValue` overwrite.
- `enableStatusMetric` / `disableStatusMetric` set a data set to `Int 1` / `Int 0`.
- Read accumulated state with `State.getMetric name` (single metric, `Option`) or `State.getMetrics ()` (all). Metrics read back have no description/type — add them via record copy before formatting.
- Record histogram observations with `State.observeHistogramSetValue histogramMetric value key` against one shared `HistogramMetric` constant; read them with `State.getHistogram histogramMetric` (`Option`) or `State.getHistograms ()`. Histograms read back have no description — add it via record copy before `Histogram.format`. See `examples.md` → "Histogram state accumulation".

## Integration with Other Libraries

- Pass an `Alma.ServiceIdentification` `Instance` to `DataSetKey.createFromInstance`, `ServiceStatus.markAsEnabled` / `markAsDisabled`, and `ResourceAvailability.enable` / `disable`.
- Pass the audience as `Audience "<name>"`; the string becomes the `audience` label value unchanged.
- For status metrics, prefer the dedicated helpers over assembling the metric by hand:
  - `ServiceStatus.markAsEnabled instance audience` / `markAsDisabled` return a `Result` wrapping a deferred `MarkAsEnabled` / `MarkAsDisabled`; run it with `MarkAsEnabled.execute` / `MarkAsDisabled.execute` (requires `open Alma.Metrics.ServiceStatus`; `ServiceStatus.MarkAsEnabled.execute` does not resolve), then render with `ServiceStatus.getFormattedValue ()`. See `examples.md` → "Service status metric".
  - `ResourceAvailability.createFromStrings` (and the `createForServiceFromStrings` / `createForMultiTenantServiceFromStrings` variants) build a resource; `enable` / `disable instance resource` toggle it; `getFormattedValue ()` renders. See `examples.md` → "Resource availability metric".

## Naming Conventions

- Metric names are snake_case (e.g. `requests_total`), matching Prometheus convention; remember `-`/space inputs are normalized to `_`.
- Service-identity labels use the `svc_` prefix; resource labels use `res_` / `res_svc_` prefixes (produced automatically by the helpers).
- Name a histogram by its base name (e.g. `request_duration_seconds`); `Histogram.format` appends `_bucket` / `_sum` / `_count` and adds the `le` label itself.

## Testing Recommendations

- The default registry is process-global and persists across calls, so tests that use the plain `State` / `ServiceStatus` / `ResourceAvailability` functions share data. Create a `Registry.create ()` per test and use the `*In` variants to isolate them.
- For pure construction/formatting tests, prefer `Metric.create*` + `Metric.format` and assert on the exact string — formatting is deterministic (data sets are sorted by key).

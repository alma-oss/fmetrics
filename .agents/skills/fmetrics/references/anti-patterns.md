# Anti-Patterns

Format: **mistake → why it is wrong → fix**.

## Outdated / Non-existent API

- **Calling `ServiceStatus.enable instance` with a hand-built status record** → the README shows this older shape, but the current API has no `enable` on `ServiceStatus` and no such record; it will not compile → use `ServiceStatus.markAsEnabled instance audience`, run the returned value with `MarkAsEnabled.execute`, then `ServiceStatus.getFormattedValue ()`. See `examples.md` → "Service status metric".

- **Expecting `ServiceStatus.markAsEnabled` to mutate state immediately** → it returns a `Result` wrapping a *deferred* `MarkAsEnabled` action; nothing is recorded until you execute it → unwrap the `Result` and call `MarkAsEnabled.execute` (likewise `MarkAsDisabled.execute`).

## Ignoring Results

- **Discarding the `Result` from `MetricName.create`, `Label.create`, `Metric.create*`, `enable`/`disable`, or `markAs*`** → name/label validation failures are silently dropped and the metric is never produced → bind with `let!` inside `result {}` and handle the `Error` branch; convert error types with `Result.mapError`.

- **Using `MetricName.createOrFail` on dynamic or user-supplied names** → it throws on invalid input instead of returning an error → reserve `createOrFail` for hardcoded constant names; use `MetricName.create` for anything dynamic.

## Value Pitfalls

- **Summing opposite infinities through the registry (e.g. incrementing a data set holding `Infinite` by `NegativeInfinite`)** → `MetricValue.(+)` raises an exception for `Inf + (-Inf)` → avoid accumulating both infinities into the same data set, or guard the value before incrementing.

- **Encoding special values as strings (`"+Inf"`, `"NaN"`)** → bypasses the value model and produces malformed or inconsistent output → use `MetricValue.Infinite`, `MetricValue.NegativeInfinite`, `MetricValue.NotANumber`.

## Misusing State

- **Treating `State` as pure or per-request** → it is one process-global concurrent registry; values persist across calls and tests and are shared across the whole process → use unique metric names per concern, and in tests assert on a specific named metric rather than global reads.

- **Formatting a metric read from `State` and expecting description/type** → `State.getMetric` / `getMetrics` return metrics with `Description = None` and `Type = None` → add them via a record copy (`{ metric with Description = ...; Type = ... }`) before `Metric.format`.

## Output Construction

- **Hand-assembling Prometheus text (`# HELP`, `# TYPE`, label braces) with string concatenation** → easy to violate the exposition format (escaping, label ordering, value rendering) → render exclusively through `Metric.format`, `ServiceStatus.getFormattedValue`, or `ResourceAvailability.getFormattedValue`.

- **Building per-service label sets manually and prepending `svc_*` by hand** → duplicates identity-label logic and drifts from the canonical key shape → use `DataSetKey.createFromInstance` so identity labels are generated consistently.

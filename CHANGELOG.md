# Changelog

<!-- There is always Unreleased section on the top. Subsections (Add, Changed, Fix, Removed) should be Add as needed. -->
## Unreleased

## 2.0.0 - 2019-05-17
- Add `createServiceDataSetKey` function to easy create a `DataSetKey` with `Instance`
- Add `ResourceAvailability` and `Audience` metric module
- Add `ServiceStatus` metric module
- Refactor `State` module
    - [**BC**] Change parameters of `incrementMetricSetValue` function
    - Add `enableStatusMetric` function to enable status metric (set value to `1`)
    - Add `disableStatusMetric` function to disable status metric (set value to `0`)

## 1.0.0 - 2019-01-31
- Initial implementation

# Changelog

<!-- There is always Unreleased section on the top. Subsections (Add, Changed, Fix, Removed) should be Add as needed. -->
## Unreleased

## 3.3.0 - 2019-10-25
- Add `[<RequireQualifiedAccess>]` to modules
- Allow to run `WebServer` with `runStateAsync` with additional web server settings.

## 3.2.0 - 2019-08-07
- Add `State` functions:
    - `setMetricSetValue`
    - `setMetricValue`

## 3.1.0 - 2019-06-26
- Use lint

## 3.0.0 - 2019-06-07
- [**BC**] Extend `ResourceAvailability`
    - Add `Common` - common resource availability
    - Add `Service` - resource availability for service (_identified by `Instance`_)
    - Add `MultiTenantService` - resource availability for multi-tenant service (_identified by `Box`_)

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

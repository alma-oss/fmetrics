namespace Metrics

type ResourceType = ResourceType of string

module ResourceType =
    let value (ResourceType resourceType) = resourceType

type ResourceIdentification = ResourceIdentification of string

module ResourceIdentification =
    let value (ResourceIdentification identification) = identification

type ResourceLocation = ResourceLocation of string

module ResourceLocation =
    let value (ResourceLocation location) = location

type ResourceAvailability = {
    Type: ResourceType
    Identification: ResourceIdentification
    Location: ResourceLocation
    Audience: Audience
}

type ResourceAvailabilityState =
    | Available of ResourceAvailability
    | NotAvailable of ResourceAvailability

type ResourceStatus =
    | Up
    | Down

module ResourceAvailability =
    let createFromStrings resourceType resourceIdentification resourceLocation audience =
        {
            Type = ResourceType resourceType
            Identification = ResourceIdentification resourceIdentification
            Location = ResourceLocation resourceLocation
            Audience = audience
        }

    let private createDataSetKey instance resourceAvailability =
        [
            ("res_type", resourceAvailability.Type |> ResourceType.value)
            ("res_identification", resourceAvailability.Identification |> ResourceIdentification.value)
            ("res_location", resourceAvailability.Location |> ResourceLocation.value)
            ("audience", resourceAvailability.Audience |> Audience.value)
        ]
        |> DataSetKey.createFromInstance instance

    let private resourceAvailabilityMetric = "resource_availability" |> MetricName.createOrFail

    let enable instance resource =
        resource
        |> createDataSetKey instance
        |> Result.map (State.enableStatusMetric resourceAvailabilityMetric)
        |> Result.mapError DataSetError

    let disable instance resource =
        resource
        |> createDataSetKey instance
        |> Result.map (State.disableStatusMetric resourceAvailabilityMetric)
        |> Result.mapError DataSetError

    let getFormattedValue () =
        resourceAvailabilityMetric
        |> State.getMetric
        |> function
            | Some metric ->
                { metric with
                    Description = Some "Current instance resources."
                    Type = Some MetricType.Gauge
                }
                |> Metric.format
            | _ -> ""

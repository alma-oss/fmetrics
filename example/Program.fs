// Learn more about F# at http://fsharp.org

open System
open MF.ConsoleStyle

module Example =
    open Metrics
    open ServiceIdentification

    let run () =
        let instance = {
            Domain = Domain "consents"
            Context = Context "example"
            Purpose = Purpose "common"
            Version = Version "stable"
        }

        let formatBox box =
            sprintf "%s.%s@%s"
                (box.Zone |> Zone.value)
                (box.Bucket |> Bucket.value)
                (box |> Box.instance |> Instance.concat ".")

        let failOnError = function
            | Ok a -> a
            | Error e -> failwithf "Error %A" e

        //
        // Resources
        //

        let kafkaClusterResource = ResourceAvailability.createFromStrings "kafka-cluster" "kfall-1.dev1.services.lmc" "kfall-1.dev1.services.lmc" Audience.Sys
        let kafkaTopicResource = ResourceAvailability.createFromStrings "kafka-topic" "consents-consentorStream-common-all" "kfall-1.dev1.services.lmc" Audience.Sys
        let kafkaTopicResource2 = ResourceAvailability.createFromStrings "kafka-topic" "consents-consentorStream-unavailable-all" "kfall-1.dev1.services.lmc" Audience.Sys
        let kafkaTopicResource3 = ResourceAvailability.createFromStrings "kafka-topic" "consents-consentorStream-disabled-all" "kfall-1.dev1.services.lmc" Audience.Sys

        let kafkaTopicInstance = {
            Domain = Domain "consents"
            Context = Context "exampleStream"
            Purpose = Purpose "common"
            Version = Version "stable"
        }
        let kafkaTopicResourceAsService =
            ResourceAvailability.createForServiceFromStrings
                "kafka-topic"
                (kafkaTopicInstance |> Instance.concat "-")
                "kfall-1.dev1.services.lmc"
                kafkaTopicInstance
                Audience.Sys

        let consentorMultiTenantDatabase = {
            Domain = Domain "consents"
            Context = Context "database"
            Purpose = Purpose "common"
            Version = Version "stable"
            Zone = Zone "data_processing"
            Bucket = Bucket "lmc_cz"
        }
        let consentorMultiTenantDatabaseResource =
            ResourceAvailability.createForMultiTenantServiceFromStrings
                "postgres"
                (consentorMultiTenantDatabase |> formatBox)
                "kfall-1.dev1.services.lmc"
                consentorMultiTenantDatabase
                Audience.Sys

        //
        // Examples
        //

        Console.section "Kafka resources - enabled/disabled"
        [
            kafkaClusterResource
            kafkaTopicResource
            kafkaTopicResource2
        ]
        |> List.iter ((ResourceAvailability.enable instance) >> failOnError)

        [
            kafkaTopicResource2
            kafkaTopicResource3
        ]
        |> List.iter ((ResourceAvailability.disable instance) >> failOnError)

        ResourceAvailability.getFormattedValue() |> Console.message

        Console.section "Kafka resources - all enabled"
        [
            kafkaClusterResource
            kafkaTopicResource
            kafkaTopicResource2
            kafkaTopicResource3
        ]
        |> List.iter ((ResourceAvailability.enable instance) >> failOnError)

        ResourceAvailability.getFormattedValue() |> Console.message

        Console.section "Kafka resources - all disabled"
        [
            kafkaClusterResource
            kafkaTopicResource
            kafkaTopicResource2
            kafkaTopicResource3
        ]
        |> List.iter ((ResourceAvailability.disable instance) >> failOnError)

        ResourceAvailability.getFormattedValue() |> Console.message

        Console.section "Service resources - all enabled"
        [
            kafkaTopicResourceAsService
            consentorMultiTenantDatabaseResource
        ]
        |> List.iter ((ResourceAvailability.disable instance) >> failOnError)

        ResourceAvailability.getFormattedValue() |> Console.message

[<EntryPoint>]
let main argv =
    Console.title "Admin - kafka"

    Example.run()

    Console.success "Done"
    0 // return an integer exit code

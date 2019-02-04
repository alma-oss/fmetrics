namespace Metrics

open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

module WebServer =
    let showStateAsync metricsPath getMetrics =
        let dockerBinding = HttpBinding.createSimple HTTP "0.0.0.0" 8080
        let config = { defaultConfig with bindings = [ dockerBinding ] }

        choose [
            GET >=> choose [
                path metricsPath
                    >=> request (getMetrics >> OK)
                    >=> Writers.setMimeType "text/plain; version=0.0.4"
            ]
        ]
        |> startWebServerAsync config
        |> snd

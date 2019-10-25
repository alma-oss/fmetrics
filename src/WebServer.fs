namespace Metrics

open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

module WebServer =
    let private runAsync settings =
        let dockerBinding = HttpBinding.createSimple HTTP "0.0.0.0" 8080
        let config = { defaultConfig with bindings = [ dockerBinding ] }

        choose settings
        |> startWebServerAsync config
        |> snd

    let private statePart metricsPath getMetrics =
        GET >=> choose [
            path metricsPath
                >=> request (getMetrics >> OK)
                >=> Writers.setMimeType "text/plain; version=0.0.4"
        ]

    let showStateAsync metricsPath getMetrics =
        [ statePart metricsPath getMetrics ]
        |> runAsync

    let runStateAsync metricsPath getMetrics settings =
        statePart metricsPath getMetrics
        :: settings
        |> runAsync

namespace Alma.Metrics

type Audience =
    | Audience of string

[<RequireQualifiedAccess>]
module Audience =
    let value (Audience audience) = audience

namespace Lmc.Metrics

[<RequireQualifiedAccess>]
type Audience =
    | PrivacyComponents
    | Rad
    | Ict
    | SharedCompresCd
    | Sys
    | Compres
    | Arch
    | Shared
    | Adminint
    | SharedJobsPrace
    | Jobote
    | NoGroup
    | Prace
    | Tech
    | Recruit
    | Jobs
    | Ontology
    | Bi
    | ZaRohem
    | Edu
    | Xslt
    | Atmoskop

[<RequireQualifiedAccess>]
module Audience =
    let value = function
        | Audience.PrivacyComponents -> "privacy-components"
        | Audience.Rad -> "rad"
        | Audience.Ict -> "ict"
        | Audience.SharedCompresCd -> "shared-compres_cd"
        | Audience.Sys -> "sys"
        | Audience.Compres -> "compres"
        | Audience.Arch -> "arch"
        | Audience.Shared -> "_shared_"
        | Audience.Adminint -> "adminint"
        | Audience.SharedJobsPrace -> "shared-jobs_prace"
        | Audience.Jobote -> "jobote"
        | Audience.NoGroup -> "no-group"
        | Audience.Prace -> "prace"
        | Audience.Tech -> "tech"
        | Audience.Recruit -> "recruit"
        | Audience.Jobs -> "jobs"
        | Audience.Ontology -> "ontology"
        | Audience.Bi -> "bi"
        | Audience.ZaRohem -> "za_rohem"
        | Audience.Edu -> "edu"
        | Audience.Xslt -> "xslt"
        | Audience.Atmoskop -> "atmoskop"

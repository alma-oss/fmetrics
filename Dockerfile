FROM dcreg.service.consul/prod/development-dotnet-core-sdk-common:latest

# build scripts
COPY ./fake.sh /fmetrics/
COPY ./build.fsx /fmetrics/
COPY ./paket.dependencies /fmetrics/
COPY ./paket.references /fmetrics/
COPY ./paket.lock /fmetrics/

# sources
COPY ./Metrics.fsproj /fmetrics/
COPY ./src /fmetrics/src

WORKDIR /fmetrics

RUN \
    ./fake.sh build target Build no-clean

CMD ["./fake.sh", "build", "target", "Tests", "no-clean"]

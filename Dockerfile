FROM dcreg.service.consul/dev/development-dotnet-core-sdk-common:3.1

# build scripts
COPY ./build.sh /fmetrics/
COPY ./build.fsx /fmetrics/
COPY ./paket.dependencies /fmetrics/
COPY ./paket.references /fmetrics/
COPY ./paket.lock /fmetrics/

# sources
COPY ./Metrics.fsproj /fmetrics/
COPY ./src /fmetrics/src

# others
COPY ./.config /fmetrics/.config
COPY ./.git /fmetrics/.git
COPY ./CHANGELOG.md /fmetrics/

WORKDIR /fmetrics

RUN \
    ./build.sh -t build no-clean

CMD ["./build.sh", "-t", "Tests", "no-clean"]

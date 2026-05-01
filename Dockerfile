FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build

COPY ["FLS/FLS.csproj", "FLS/"]
COPY . .

RUN dotnet publish FLS -c Release -o /build/publish/ --self-contained -p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/runtime:8.0.0-bookworm-slim AS runtime

RUN apt-get update && apt-get install -y curl

COPY --from=build /build/publish /FLS
COPY --from=build /build/docker-entrypoint.sh /FLS

RUN mkdir /workdir
RUN chmod +x /FLS/docker-entrypoint.sh

ENTRYPOINT ["/FLS/docker-entrypoint.sh"]

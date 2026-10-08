# ------------------------------------------------------------------------------
# BacateTagAssist - Dockerfile Multi-arch (.NET 10 + MediaInfo)
# Compatível com Docker, Podman, OpenMediaVault, Unraid e TrueNAS
# ------------------------------------------------------------------------------

# Estágio de Compilação
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copia arquivos de projeto para cache de restauração
COPY Directory.Build.props ./
COPY src/BacateTagAssist.Core/BacateTagAssist.Core.csproj src/BacateTagAssist.Core/
COPY src/BacateTagAssist.Web/BacateTagAssist.Web.csproj src/BacateTagAssist.Web/
COPY src/BacateTagAssist.Server/BacateTagAssist.Server.csproj src/BacateTagAssist.Server/

RUN dotnet restore src/BacateTagAssist.Server/BacateTagAssist.Server.csproj -a $TARGETARCH

# Copia o restante do código e compila
COPY src/BacateTagAssist.Core/ src/BacateTagAssist.Core/
COPY src/BacateTagAssist.Web/ src/BacateTagAssist.Web/
COPY src/BacateTagAssist.Server/ src/BacateTagAssist.Server/

RUN dotnet publish src/BacateTagAssist.Server/BacateTagAssist.Server.csproj \
    -c Release \
    -a $TARGETARCH \
    -o /app \
    --no-restore

# Estágio Final de Execução
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Instala MediaInfo CLI nativo do Linux
RUN apt-get update && \
    apt-get install -y --no-install-recommends mediainfo tzdata ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Cria diretórios de dados e mídia com permissões adequadas
RUN mkdir -p /config /media && \
    groupadd -g 1000 bta && \
    useradd -u 1000 -g bta -d /config -s /bin/bash bta && \
    chown -R bta:bta /config /media

COPY --from=build /app ./

# Variáveis de Ambiente Padrão
ENV ASPNETCORE_URLS=http://0.0.0.0:5000 \
    BTA_CONFIG_DIR=/config \
    BTA_ROOTS=/media \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5000

VOLUME ["/config", "/media"]

USER bta

ENTRYPOINT ["dotnet", "BacateTagAssist.Server.dll", "--no-browser"]

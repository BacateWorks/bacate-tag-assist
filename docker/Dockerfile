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

# Cria diretórios de dados e mídia e garante permissões para o usuário não-root (UID 1000)
# Compatível com imagens onde UID/GID 1000 já existe (usuário 'app' do .NET 8+) ou imagens genéricas
RUN mkdir -p /config /media && \
    (getent group 1000 >/dev/null 2>&1 || groupadd -g 1000 bta 2>/dev/null || true) && \
    (getent passwd 1000 >/dev/null 2>&1 || useradd -u 1000 -g 1000 -d /config -s /bin/bash bta 2>/dev/null || true) && \
    chown -R 1000:1000 /config /media && \
    chmod -R 775 /config /media

COPY --from=build /app ./

# Variáveis de Ambiente Padrão
ENV ASPNETCORE_URLS=http://0.0.0.0:5000 \
    BTA_CONFIG_DIR=/config \
    BTA_ROOTS=/media \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5000

VOLUME ["/config", "/media"]

USER 1000

ENTRYPOINT ["dotnet", "BacateTagAssist.Server.dll", "--no-browser"]

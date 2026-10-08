#!/bin/bash
# ------------------------------------------------------------------------------
# Script de execução para Podman (Rootless com suporte a SELinux)
# ------------------------------------------------------------------------------

set -e

CONFIG_DIR="${HOME}/.config/bacate-tag-assist"
MEDIA_DIR="${1:-${HOME}/Videos}"

mkdir -p "${CONFIG_DIR}"

echo "Iniciando BacateTagAssist no Podman..."
echo "Configurações: ${CONFIG_DIR}"
echo "Mídia: ${MEDIA_DIR}"

podman run -d \
  --name bacate-tag-assist \
  --replace \
  --restart unless-stopped \
  -p 5000:5000 \
  -v "${CONFIG_DIR}:/config:Z" \
  -v "${MEDIA_DIR}:/media:Z" \
  -e TZ=America/Sao_Paulo \
  -e BTA_CONFIG_DIR=/config \
  -e BTA_ROOTS=/media \
  ghcr.io/bacate/bacate-tag-assist:latest

echo "Pronto! Acesse http://localhost:5000 no seu navegador."

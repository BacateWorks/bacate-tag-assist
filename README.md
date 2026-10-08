# 🥑 BacateTagAssist

<p align="center">
  <img src="src/BacateTagAssist.Web/wwwroot/assets/mascot.png" width="160" alt="Bacate Mascot" />
  <br>
  <b>O renomeador inteligente e transcendental para releases de filmes, séries e animes.</b>
  <br>
  <i>Formatado rigorosamente de acordo com os padrões dos trackers brasileiros (CapybaraBR, Samaritano e correlatos).</i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/Docker-Multi--Arch-2496ED?logo=docker&logoColor=white" alt="Docker">
  <img src="https://img.shields.io/badge/Podman-Rootless%20%26%20Quadlet-892CA0?logo=podman&logoColor=white" alt="Podman">
  <img src="https://img.shields.io/badge/License-MIT-green" alt="MIT License">
</p>

---

## 🌟 Recursos Principais

- 🏷️ **Nomenclatura Fiel aos Trackers:** Suporte completo e granular a todas as tags técnicas exigidas por trackers privados como **CapybaraBR** e **Samaritano** (Filmes, Season Packs, Episódios Avulsos, Edições Especiais, REMUX, DUAL/MULTi, dois grupos de lançamento).
- 🧬 **Anatomia Visual da Tag:** Dissecação gráfica do nome em tempo real (Título, Ano, Resolução, Origem, Streaming, Áudio, Codec, Grupos) com botão de 1 clique para copiar o título da postagem com espaços.
- 🔍 **MediaInfo Integrado & Automático:**
  - Analisa arquivos de vídeo locais ou no servidor.
  - Detecta e sugere automaticamente: Resolução (`1080p`, `2160p`...), HDR/DV (`HDR10+`, `DV HDR`, `HLG`...), Codecs de vídeo (`AVC`, `HEVC`, `x264`...), Codecs de áudio (`DDP`, `TrueHD`, `DTS-HD MA`...), Canais (`2.0`, `5.1`, `7.1`), Dolby Atmos e presença de dublagem PT-BR para marcar `DUAL` ou `MULTi`.
- 🎬 **Assistente TMDB Oficial:**
  - Pesquise por Nome, Link do TMDB, ID ou IMDb ID (`tt1234567`).
  - Preenche automaticamente o título oficial (em Inglês, Português ou Original), ano de lançamento e seleciona temporadas com um clique.
- 🛡️ **Renomeação Transacional & Desfazer (Undo):**
  - Execução em duas fases (origem → temporário → destino) que previne conflitos de maiúsculas/minúsculas e colisões.
  - Histórico persistido para desfazer a última operação com segurança total.
- 🖥️ **Windows Desktop Nativo:**
  - Executável único e independente (*Self-Contained*), não necessita instalar .NET.
  - Diálogo nativo de seleção de pastas e integração com o Explorador de Arquivos.
- 🐳 **Docker, Podman & OpenMediaVault:**
  - Imagem pronta com .NET 10 e MediaInfo CLI embutido.
  - Navegador de arquivos integrado na interface web para navegar pelas pastas do seu NAS remotamente.

---

## 🚀 Como Usar

### 🪟 No Windows (Desktop)

1. Baixe o arquivo `BacateTagAssist-v1.0.0-Windows-x64.zip` na aba [Releases](https://github.com/BacateWorks/bacate-tag-assist/releases).
2. Extraia o conteúdo para uma pasta de sua preferência.
3. Execute `BacateTagAssist.Desktop.exe`.
4. Selecione a pasta, preencha os dados (ou use o MediaInfo e o TMDB) e clique em **Renomear Selecionados**!

> **Requisito do Windows:** Microsoft Edge WebView2 Runtime (já presente por padrão no Windows 10 e 11).

---

### 🐳 No Docker / Docker Compose

Crie um arquivo `docker-compose.yml`:

```yaml
services:
  bacate-tag-assist:
    image: ghcr.io/bacateworks/bacate-tag-assist:latest
    container_name: bacate-tag-assist
    restart: unless-stopped
    ports:
      # Permite alterar a porta externa definindo a variável BTA_PORT (padrão: 5000)
      - "${BTA_PORT:-5000}:5000"
    environment:
      - TZ=America/Sao_Paulo
      - BTA_CONFIG_DIR=/config
      - BTA_ROOTS=/media
      # - TMDB_API_KEY=sua_chave_opcional
    volumes:
      # Configurações, histórico de desfazer e preferências (persistente)
      - ./config:/config
      # Pastas de mídia onde ficam os seus downloads/filmes/séries
      # IMPORTANTE: A pasta montada em /media precisa de permissão de escrita para que o aplicativo renomeie arquivos!
      - /caminho/para/seus/videos:/media
```

Inicie o container:
```bash
docker compose up -d
```
Acesse no seu navegador: `http://localhost:5000` (ou o IP do seu servidor).

---

### 🗄️ No OpenMediaVault (OMV) / Unraid

1. No OpenMediaVault, acesse **Services** > **Compose** > **Files**.
2. Adicione um novo arquivo Compose colando a estrutura acima.
3. Certifique-se de apontar `/media` para a pasta compartilhada dos seus vídeos (ex: `/srv/dev-disk-by-uuid-.../videos:/media`).
4. **Permissões:** O container executa sob o usuário não-root `1000:1000`. No OMV, assegure-se de que a pasta de mídia conceda permissão de leitura e escrita (Read/Write) para o usuário ou utilize `chmod -R 775` na pasta compartilhada.
5. Inicie o container e acesse pela porta `5000`.

> ⚠️ **Configuração de Visibilidade do Pacote no GitHub Packages (GHCR):**
> Caso você seja o mantenedor e a imagem do GHCR seja recém-criada, certifique-se de torná-la **Pública** para permitir o download sem necessidade de login:
> 1. No GitHub, acesse a página da organização ou usuário: **BacateWorks** > aba **Packages**.
> 2. Clique no pacote `bacate-tag-assist`.
> 3. No menu lateral, acesse **Package settings** > **Danger Zone** > **Change visibility**.
> 4. Alterne para **Public** e confirme.

---

### 🦭 No Podman (Rootless / Quadlet)

Execute diretamente:
```bash
podman run -d \
  --name bacate-tag-assist \
  --restart unless-stopped \
  -p 5000:5000 \
  -v ~/.config/bacate-tag-assist:/config:Z \
  -v /seus/videos:/media:Z \
  -e TZ=America/Sao_Paulo \
  ghcr.io/bacateworks/bacate-tag-assist:latest
```

Ou use o arquivo de serviço systemd Quadlet incluído em `deploy/podman/bacate-tag-assist.container`.

---

## 🛠️ Compilação a partir do Código Fonte

Para compilar localmente, é necessário o **.NET 10 SDK**:

```bash
# Clonar o repositório
git clone https://github.com/BacateWorks/bacate-tag-assist.git
cd bacate-tag-assist

# Rodar os testes unitários
dotnet test

# Executar em modo servidor web
dotnet run --project src/BacateTagAssist.Server

# Executar em modo desktop Windows
dotnet run --project src/BacateTagAssist.Desktop
```

Para gerar os pacotes de distribuição para release:
```powershell
pwsh ./build/build-all.ps1 -Version 1.0.0
```

---

## 📋 Nomenclaturas Suportadas

O **BacateTagAssist** segue as convenções estabelecidas pela comunidade P2P brasileira:

| Modo | Exemplo de Nome Gerado |
| :--- | :--- |
| **Filme WEB** | `Dune.Part.Two.2024.1080p.AMZN.WEB-DL.DDP5.1.Atmos.H.264.DUAL-BiOMA.mkv` |
| **Filme 4K REMUX** | `Oppenheimer.2023.2160p.UHD.Blu-Ray.REMUX.DV.HDR.HEVC.TrueHD7.1.Atmos.DUAL-BiOMA.mkv` |
| **Season Pack** | `Breaking.Bad.S01.1080p.NF.WEB-DL.DDP5.1.H.264-FLUX.DUAL-Kitsune` |
| **Episódio Avulso** | `Breaking.Bad.S01E01.1080p.NF.WEB-DL.DDP5.1.H.264-Kitsune.mkv` |
| **Dois Grupos** | `Anime.S01E05.1080p.CR.WEB-DL.AAC2.0.H.264-GrupoOrig.DUAL-GrupoAdapt.mkv` |

---

## 📄 Licença

Distribuído sob a licença **MIT**. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

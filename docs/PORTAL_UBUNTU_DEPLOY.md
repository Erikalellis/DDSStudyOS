# DDS StudyOS Portal - Deploy Ubuntu

Este portal deve ser publicado como site separado do portal ja existente no servidor.

## Regra operacional

- Nao reutilizar a pasta do outro site.
- Nao sobrescrever configuracao nginx existente.
- Publicar em pasta propria: `~/dds-projetos/ddsstudyos-portal/`
- Expor por porta interna propria: `127.0.0.1:5081`
- Fazer proxy reverso por `server_name` dedicado ou bloco nginx separado.

## Estrutura sugerida no servidor

```text
~/dds-projetos/ddsstudyos-portal/
  current/
  releases/
  shared/
```

## Modo recomendado hoje

Use container separado para o portal DDS StudyOS.

- portal atual do servidor: permanece intocado
- portal DDS: container proprio
- tunel reverso AWS: aponta para o container DDS, nao para o outro site
- rede: interna entre containers sempre que possivel

## Publish local

No Windows:

```powershell
.\scripts\publish-portal-linux.ps1
```

Saida padrao:

```text
artifacts/portal/linux-x64/publish
```

## Alternativa com Docker

Arquivos adicionados:

- `src/DDSStudyOS.Portal/Dockerfile`
- `src/DDSStudyOS.Portal/.dockerignore`
- `deploy/portal/docker-compose.portal.example.yml`
- `scripts/export-portal-docker-bundle.ps1`
- `deploy/portal/ddsstudyos-portal-autossh.example.sh`

Fluxo sugerido:

1. copiar o exemplo de compose para o host
2. ajustar URLs publicas e rede Docker
3. subir o container do portal
4. apontar o tunel reverso AWS para `http://ddsstudyos-portal:8080`

Exemplo:

```bash
docker compose -f deploy/portal/docker-compose.portal.example.yml up -d --build
```

## Bundle pronto para servidor

No Windows, gere um pacote minimo do portal para copiar ao Ubuntu:

```powershell
.\scripts\export-portal-docker-bundle.ps1
```

Saida:

```text
artifacts/portal/docker-bundle/
  docker-compose.yml
  data-protection/
  portal/
```

Esse bundle ja fica pronto para ser copiado como uma stack isolada em:

```text
~/dds-projetos/ddsstudyos-portal/
```

## Arquivos de referencia

- `deploy/portal/ddsstudyos-portal.service.example`
- `deploy/portal/ddsstudyos-portal.nginx.example.conf`

## Passos de deploy

1. Publicar localmente para `linux-x64`.
2. Copiar os arquivos para `~/dds-projetos/ddsstudyos-portal/current`.
3. Ajustar o arquivo de service do `systemd`.
4. Criar bloco nginx separado para o portal DDS.
5. Reiniciar `systemd` e nginx.

## Passos de deploy com Docker

1. Garantir Docker e Docker Compose no Ubuntu.
2. Copiar o repositorio ou ao menos a pasta do portal para `~/dds-projetos/ddsstudyos-portal/`.
3. Ajustar `deploy/portal/docker-compose.portal.example.yml`.
4. Subir o portal em container.
5. Conectar o tunel reverso AWS ao hostname interno do container.

Observacao operacional:

- se o seu tunel AWS ja roda em Docker com os outros servicos, coloque o portal na mesma rede Docker
- se o tunel roda fora do Docker, mantenha o bind em `127.0.0.1:5081` e aponte o tunel para essa porta

## Tunel reverso AWS dedicado

Nao reutilize o `dds-portal-manager.sh` legado para o portal DDS StudyOS.

Motivo:

- o manager legado aponta `8080` para outro portal
- o portal DDS roda isolado em `127.0.0.1:5081`
- misturar os dois fluxos aumenta risco operacional

Use um autossh dedicado para o DDS:

```bash
AUTOSSH_GATETIME=0 autossh \
  -M 0 \
  -N \
  -f \
  -T \
  -o ServerAliveInterval=30 \
  -o ServerAliveCountMax=3 \
  -o ExitOnForwardFailure=yes \
  -o StrictHostKeyChecking=accept-new \
  -o ConnectTimeout=30 \
  -o TCPKeepAlive=yes \
  -i /home/kika/dds-key.pem \
  -R 5081:localhost:5081 \
  ubuntu@177.71.165.60
```

Validacao minima:

```bash
ssh -i /home/kika/dds-key.pem ubuntu@177.71.165.60 "curl -fsS http://127.0.0.1:5081/healthz"
```

## Endpoints base

- Home: `/`
- Health: `/healthz`
- Catalogo: `/api/catalog`
- Metadata: `/api/meta`

## Exposicao publica recomendada

No host AWS, publique o portal sob path dedicado no `nginx` existente:

- portal: `https://deepdarkness.com.br/studyos/`
- catalogo: `https://deepdarkness.com.br/studyos/api/catalog`
- health: `https://deepdarkness.com.br/studyos/healthz`

Motivo:

- evita depender da porta `5081` aberta ao publico
- preserva `5081` como backend interno do portal
- mantem o outro portal respondendo na raiz (`/`)

## Observacao

O portal DDS StudyOS foi desenhado para virar a camada publica do projeto, mas sem depender do site ja existente no Ubuntu. O isolamento faz parte da arquitetura.

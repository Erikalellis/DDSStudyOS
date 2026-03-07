# DDS StudyOS - Evidencia de deploy Docker do portal (2026-03-07)

## Objetivo

Colocar o portal publico do DDS StudyOS em stack Docker isolada no Ubuntu, sem tocar no portal/site ja existente do servidor.

## Artefatos locais preparados

- `src/DDSStudyOS.Portal/Dockerfile`
- `src/DDSStudyOS.Portal/.dockerignore`
- `deploy/portal/docker-compose.portal.example.yml`
- `scripts/export-portal-docker-bundle.ps1`

## Ajustes tecnicos aplicados

- `ForwardedHeaders` habilitado no portal para operacao atras de proxy/tunel reverso.
- `UseHttpsRedirection()` removido do portal para evitar redirecionamento interno indevido em ambiente containerizado com bind HTTP local.
- bundle Docker minimo gerado em `artifacts/portal/docker-bundle/`.
- persistencia de chaves em `data-protection/` adicionada ao compose.

## Ambiente remoto validado

Servidor Ubuntu:

- host: `192.168.1.10`
- pasta alvo: `~/dds-projetos/ddsstudyos-portal/stack`
- Docker: presente
- Docker Compose: presente

## Validacao remota concluida

Deploy inicial concluido com:

```bash
docker compose up -d --build
```

Validacoes executadas com sucesso antes da queda de conectividade SSH:

- `curl http://127.0.0.1:5081/healthz`
- `curl http://127.0.0.1:5081/api/meta`
- `docker ps --filter name=ddsstudyos-portal`

Resultados observados:

- container `ddsstudyos-portal` em execucao
- bind local `127.0.0.1:5081->8080/tcp`
- endpoint `/healthz` retornando `status=ok`
- endpoint `/api/meta` retornando metadata do portal

## Estado operacional

- stack isolada do portal existente
- bind apenas em localhost no Ubuntu
- pronta para consumo por tunel reverso AWS apontando para `127.0.0.1:5081`

## Revalidacao final

Depois da recuperacao da conectividade SSH, o bundle foi reenviado e o rebuild final foi reaplicado com:

```bash
docker compose up -d --build
```

Validacoes finais:

- `curl http://127.0.0.1:5081/healthz` => OK
- `curl http://127.0.0.1:5081/api/meta` => OK
- `docker logs ddsstudyos-portal` => sem erro critico

## Tunel reverso AWS

Foi validado um `autossh` dedicado, separado do manager legado:

```bash
autossh -R 5081:localhost:5081 ubuntu@177.71.165.60
```

Verificacao executada do lado AWS:

```bash
curl http://127.0.0.1:5081/healthz
```

Resultado:

- retorno `status=ok`
- porta `5081` em escuta no host AWS

## Estado final

- portal DDS em Docker isolado
- bind local `127.0.0.1:5081`
- tunel reverso AWS dedicado validado na porta `5081`
- sem dependencia operacional do portal legado

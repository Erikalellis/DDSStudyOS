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

## Pendencia aberta

Durante a reaplicacao final do bundle atualizado, a conexao SSH com `192.168.1.10` caiu por timeout de rede. O portal permaneceu no ar no deploy inicial validado, mas a reaplicacao do ultimo ajuste fino de log ficou pendente de nova conexao SSH.

## Proximo passo

Quando a conectividade SSH voltar:

1. reenviar o bundle Docker atualizado
2. rodar `docker compose up -d --build` em `~/dds-projetos/ddsstudyos-portal/stack`
3. validar novamente `/healthz`
4. apontar o tunel reverso AWS para `127.0.0.1:5081`

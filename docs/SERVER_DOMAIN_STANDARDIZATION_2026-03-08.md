# Padronizacao de Dominios e TLS - Inventario de Servidor (2026-03-08)

## Objetivo

- inventariar os projetos ativos no ambiente atual
- separar o que e publico do que deve continuar interno
- definir subdominios padronizados em `deepdarkness.com.br`
- definir a estrategia correta de HTTPS/TLS

## Estado confirmado

### DNS

- `deepdarkness.com.br` -> `177.71.165.60`
- `www.deepdarkness.com.br` -> `177.71.165.60`
- `studyos.deepdarkness.com.br` ainda nao existe

### Host publico AWS (`177.71.165.60`)

- `nginx` ativo na porta `80`
- `certbot` instalado
- `443` ainda nao esta publicado
- o host publico hoje responde:
  - raiz `http://deepdarkness.com.br/`
  - rota `http://deepdarkness.com.br/studyos/`

### Risco estrutural atual

O host AWS esta com varias portas publicamente expostas por `reverse SSH`:

- `3000`
- `3001`
- `3002`
- `3003`
- `5000`
- `5081`
- `8000`
- `8080`
- `8585`
- `1984`

Essas portas estao em `0.0.0.0` porque os tunels atuais usam `-R porta:localhost:porta`. O padrao correto para producao e:

- `-R 127.0.0.1:porta:localhost:porta`

Assim o servico fica acessivel apenas via `nginx` local do host AWS.

## Inventario de projetos ativos

### Publico principal

| Projeto | Tipo | Origem atual | Publicacao atual | Status |
|---|---|---|---|---|
| Site principal DDS | nao-Docker | Ubuntu local `python3 -m http.server 8080` | AWS raiz `/` -> `localhost:8080` | ativo |
| DDS StudyOS Portal | Docker | Ubuntu local `127.0.0.1:5081` + tunel AWS `5081` | AWS `/studyos/` -> `localhost:5081` | ativo |

### Stack Academia Digital

| Projeto | Tipo | Porta local | Publicacao atual | Status |
|---|---|---:|---|---|
| `ad-app-backend` | Docker | `8000` | porta publica direta | ativo |
| `ad-app-admin-panel` | Docker | `3001` | porta publica direta | ativo |
| `ad-app-professor-dashboard` | Docker | `3002` | porta publica direta | ativo |
| `ad-app-student-portal` | Docker | `3003` | porta publica direta | ativo |

### Ferramentas e operacao

| Projeto | Tipo | Porta local | Publicacao atual | Status |
|---|---|---:|---|---|
| Open WebUI | Docker | `3000` | porta publica direta | ativo |
| FileBrowser | Docker | `8585` | porta publica direta | ativo |
| LEDM | nao-Docker (`gunicorn`) | `5000` | porta publica direta | ativo |
| DDS Monitor API | nao-Docker (`node`) | `1984` | porta publica direta | ativo |
| Server Monitor API | nao-Docker (`node`) | `3008` | local apenas | ativo |
| Hestia panel stack | painel do servidor | `8083` | encaminhado por tunel, nao deve ser publico | ativo |

### Legado Hestia / DuckDNS

Dominios presentes no Ubuntu local:

- `deepdarkness.duckdns.org`
- `ddscreate.duckdns.org`
- `ddsfit.duckdns.org`
- `ddsmidiawiki.duckdns.org`
- `forumdds.duckdns.org`

Esses dominios ainda existem no Hestia e devem ser tratados como legado. Nao devem ser migrados automaticamente sem confirmar se ainda sao usados.

## Padrao recomendado de subdominios

### Publicos imediatos

| Dominio | Destino AWS | Destino final | Observacao |
|---|---|---|---|
| `deepdarkness.com.br` | `localhost:8080` | site principal DDS | manter |
| `www.deepdarkness.com.br` | redirecionar para raiz | site principal DDS | manter |
| `studyos.deepdarkness.com.br` | `localhost:5081` | DDS StudyOS Portal | criar agora |

### Publicos por produto

| Dominio | Destino AWS | Observacao |
|---|---|---|
| `academia-api.deepdarkness.com.br` | `localhost:8000` | backend |
| `academia-admin.deepdarkness.com.br` | `localhost:3001` | painel admin |
| `academia-professor.deepdarkness.com.br` | `localhost:3002` | dashboard professor |
| `academia-aluno.deepdarkness.com.br` | `localhost:3003` | portal aluno |

### Restritos por autenticacao/IP

| Dominio | Destino AWS | Politica recomendada |
|---|---|---|
| `ai.deepdarkness.com.br` | `localhost:3000` | basic auth + allowlist |
| `files.deepdarkness.com.br` | `localhost:8585` | basic auth obrigatoria |
| `ledm.deepdarkness.com.br` | `localhost:5000` | publicar so se houver necessidade real |
| `monitor.deepdarkness.com.br` | `localhost:1984` | internal/admin only |

### Nao publicar no dominio publico principal

- `8083` / Hestia
- `3008` / monitor interno cru
- bancos (`5432`, `3306`)
- painel Webmin
- portas de e-mail/FTP

## Estrategia de TLS

### Fase 1

- emitir certificado para:
  - `deepdarkness.com.br`
  - `www.deepdarkness.com.br`
  - `studyos.deepdarkness.com.br`

### Fase 2

- emitir certificados por subdominio para a stack Academia Digital

### Fase 3

- avaliar migracao para wildcard `*.deepdarkness.com.br` se o numero de projetos crescer

## Hardening recomendado antes de abrir novos subdominios

### 1. Fechar exposicao direta dos tunels

Trocar:

- `-R 8080:localhost:8080`

Por:

- `-R 127.0.0.1:8080:localhost:8080`

Aplicar a mesma regra para:

- `8000`
- `1984`
- `3000`
- `3001`
- `3002`
- `3003`
- `3004`
- `3005`
- `5000`
- `5081`
- `8083`
- `8585`

### 2. Deixar publico apenas

- `80`
- `443`
- `22`

Todo o resto deve ficar acessivel somente via `localhost` no host AWS e ser publicado por `nginx`.

## Ordem correta de execucao

1. criar DNS `studyos.deepdarkness.com.br`
2. criar `server block` do StudyOS no `nginx` do AWS
3. emitir TLS do StudyOS
4. ajustar os tunels para `127.0.0.1`
5. publicar a stack Academia Digital por subdominio
6. proteger `ai`, `files` e `monitor` com autenticacao/allowlist

## Resultado alvo

- um dominio por projeto
- HTTPS em todos os endpoints publicos
- nenhuma porta de app exposta diretamente no AWS
- `nginx` como unico ponto publico de entrada

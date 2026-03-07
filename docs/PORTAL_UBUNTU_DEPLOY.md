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

## Publish local

No Windows:

```powershell
.\scripts\publish-portal-linux.ps1
```

Saida padrao:

```text
artifacts/portal/linux-x64/publish
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

## Endpoints base

- Home: `/`
- Health: `/healthz`
- Catalogo: `/api/catalog`
- Metadata: `/api/meta`

## Observacao

O portal DDS StudyOS foi desenhado para virar a camada publica do projeto, mas sem depender do site ja existente no Ubuntu. O isolamento faz parte da arquitetura.

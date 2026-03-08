# AWS Nginx - Padrao de publicacao `deepdarkness.com.br`

Este diretorio contem exemplos de `server blocks` para o host publico AWS.

## Regras operacionais

- publicar apps por `server_name`, nao por porta publica crua
- manter somente `80`, `443` e `22` expostos no host AWS
- todos os tunels reversos devem usar `127.0.0.1`
- emitir certificados com `certbot --nginx`

## Ordem sugerida

1. `deepdarkness.com.br` + `www.deepdarkness.com.br`
2. `studyos.deepdarkness.com.br`
3. `academia-api.deepdarkness.com.br`
4. `academia-admin.deepdarkness.com.br`
5. `academia-professor.deepdarkness.com.br`
6. `academia-aluno.deepdarkness.com.br`

## Comandos de certificado

### Site principal

```bash
sudo certbot --nginx -d deepdarkness.com.br -d www.deepdarkness.com.br
```

### DDS StudyOS

```bash
sudo certbot --nginx -d studyos.deepdarkness.com.br
```

### Academia Digital

```bash
sudo certbot --nginx \
  -d academia-api.deepdarkness.com.br \
  -d academia-admin.deepdarkness.com.br \
  -d academia-professor.deepdarkness.com.br \
  -d academia-aluno.deepdarkness.com.br
```

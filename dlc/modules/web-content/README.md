# DLC web-content

Modulo incremental usado para atualizar o navegador interno sem mexer no core.

Conteudo atual:
- `content/home.html`: nova home interna do navegador
- `content/error.html`: pagina refinada para erro de navegacao
- `content/404.html`: pagina dedicada para alias `dds://` inexistente

Uso:
- o app tenta carregar primeiro `%LocalAppData%\\DDSStudyOS\\modules\\web-content\\content`
- em desenvolvimento local, tambem aceita a pasta `dlc/modules/web-content/content`
- se nada existir, o navegador cai no HTML embutido do app

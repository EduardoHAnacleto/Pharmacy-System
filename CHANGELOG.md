# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [Não lançado]

Transformação de uma vitrine de farmácia com dados fixos no código numa vitrine
configurável para qualquer loja. Implementado em fases; ver
[`docs/PLANO_DE_MELHORIAS.md`](docs/PLANO_DE_MELHORIAS.md).

### Segurança

- **A API de escrita passou a exigir autenticação.** `POST` e `DELETE` de promoções
  eram públicos: qualquer pessoa criava, apagava e fazia upload de arquivo.
- **Senha de admin saiu do bundle JavaScript.** O login comparava com a constante
  `'1234'` e gravava `localStorage.admin_authenticated = 'true'`; o lockout de três
  tentativas também morava em `localStorage`, onde o visitante o apagava.
- JWT em memória + refresh token rotativo em cookie `HttpOnly`/`Secure`/`SameSite=Strict`,
  com `Path` restrito a `/api/v1/auth`. Só o digest SHA-256 do refresh vai ao banco.
- Senhas com PBKDF2-HMAC-SHA256, 600 000 iterações, em formato auto-descritivo.
- Rate limit de login por IP, no servidor.
- **CORS deixou de aceitar qualquer origem.** Havia `SetIsOriginAllowed(_ => true)`
  depois da allowlist, junto com `AllowCredentials()`.
- **Path traversal no delete de imagem eliminado** — junto com o próprio delete.
  Upload passou a ser validado por magic bytes, não pelo `Content-Type` do cliente,
  e a extensão vem do tipo detectado, não do `FileName`.
- `CreatedByUserId`/`CreatedByUserName` deixaram de vir do cliente. O admin enviava
  `0`/`'Admin'` fixos: a trilha de auditoria era o que o chamador quisesse escrever.
- **Produção deixou de rodar como Development** (Swagger exposto, stack trace em
  página de erro, `EnableDetailedErrors` no SignalR).
- MySQL deixou de publicar `3306` no host; Redis passou a exigir senha.
- Guardas de configuração: a API não sobe sem connection string ou com
  `Jwt:SigningKey` menor que 32 caracteres.
- `.env` e `.env.development` **com senhas** deixaram de ser rastreados — o
  `.gitignore` existia com o nome errado (`.git.ignore`) e o git nunca o leu.
  **As credenciais que estiveram versionadas precisam ser rotacionadas.**

### Corrigido

- **Imagens não somem mais a cada deploy.** Uploads iam para dentro do container sem
  volume; toda linha do banco apontava para URL morta depois de cada `docker compose up`.
- **O schema passou a ser aplicado em Linux.** O compose montava `./Database/schema.sql`
  e o diretório em disco é `database/`: o Docker criava um dir vazio e a API subia
  contra um banco sem tabelas.
- **`docker compose up --build` deixou de ser no-op.** Nenhum serviço tinha `build:`.
- **Cache deixou de servir o resultado do timezone errado.** `GetActivePaged`
  filtrava por timezone, mas a chave o omitia: o primeiro visitante definia o
  resultado de todos por cinco minutos.
- Invalidação de cache deixou de varrer o keyspace (`KEYS`) em toda escrita
  ([ADR 0006](docs/adr/0006-cache-versionado-em-vez-de-varredura.md)).
- URLs de API unificadas num caminho relativo. Havia três diferentes hardcoded
  (`localhost:80/api`, `localhost:8080/api`, `localhost:5000/promotionsHub`), então
  admin e SignalR quebravam fora do host Docker.
- CPF tinha **duas implementações com algoritmos diferentes**; sobrou a do store.
- `/contact` versus `/Contact`: o link do menu não resolvia.
- `PUT /item-promotions/{id}` passou a existir — o front já o chamava.
- `try/catch` no `JSON.parse` do carrinho: um `localStorage` truncado derrubava a
  aplicação na montagem.
- Feedback visível de erro nas ações do admin, que usavam `try/finally` sem `catch`.
- `<title>` deixou de ser "Vite App".

### Adicionado

- **`store_settings`**: identidade, marca, localização, contato, moeda, locale,
  regras de entrega, campos do checkout e horários — com tela de administração.
  Saíram do código: mapa apontando para a **Torre de Pisa**, `EMAIL@MAIL.com`,
  `ADDRESS ADDRESS ADDRESS`, dois números de WhatsApp diferentes, `deliveryFee: 8`,
  `minDeliveryTotal: 30`, `allowedCity: 'Santa Terezinha de Itaipu'`, feriados
  fixos em `/BR`.
- **i18n** (`vue-i18n`) com pt-BR e en-NZ; moeda e datas por `Intl`, seguindo o
  locale da loja. CPF e CEP passaram a ser opcionais controlados por configuração.
- **Ciclo de vida de promoção**: `status` no lugar de `is_active`, arquivar,
  reativar, duplicar, editar, com histórico de transições e linhagem
  ([ADR 0007](docs/adr/0007-arquivar-em-vez-de-apagar.md)).
- **`media_assets`**: deduplicação por SHA-256; reativar reaproveita a imagem.
  `MediaBackfillService` marca as imagens perdidas antes do volume existir.
- **Analytics sem PII** e dashboard de insights: funil, desempenho por promoção,
  resumo de vendas, alertas operacionais, séries temporais.
- **Pedidos anônimos** (`orders`, `order_items`) — itens, valores, tipo de entrega e
  cidade, nunca a pessoa ([ADR 0002](docs/adr/0002-nenhum-dado-pessoal-no-servidor.md)).
- **Exportação CSV** com guarda contra injeção de fórmula.
- **`audit_log`** de toda ação administrativa.
- Busca por nome, filtro por categoria e faixa de preço, cinco ordenações.
- CRUD de categorias.
- **EF Core Migrations** como dono do schema, aplicadas por um serviço `migrator`
  separado ([ADR 0004](docs/adr/0004-migrations-como-dono-do-schema.md)).
- Health checks `/health` e `/health/ready`, usados pelo compose.
- **GitHub Actions**: backend (build, format, pending-model-changes, test), frontend
  (type-check, lint, prettier, vitest, build), E2E e validação do compose.
  `docker.yml` publica no GHCR com scan Trivy.
- **Suíte de testes** onde havia zero: 74 unitários e 52 de integração com
  Testcontainers ([ADR 0008](docs/adr/0008-testcontainers-com-skip.md)).
- Página `/privacy` explicando ao visitante o que acontece com o que ele digita.
- SEO e PWA: dados estruturados `Product`/`Offer`, Open Graph, `robots.txt`,
  `sitemap.xml`, manifest.
- Acessibilidade: `alt` descritivo, `label` associado a cada campo, ARIA nos modais.
- Serilog estruturado, `ProblemDetails`, e OpenTelemetry (traces e métricas, export
  OTLP quando configurado).
- Documentação: README honesto, `docs/OPERACOES.md`, oito ADRs,
  `docs/MODELO_DE_DADOS.md`, `CONTRIBUTING.md`, `LICENSE`, e XML docs alimentando o
  Swagger.

### Alterado

- Camada de serviço: o controller ficou só com HTTP. As **seis projeções duplicadas**
  do DTO viraram um mapeamento único — a do `POST` era a única que não prefixava a
  URL da imagem, então o create devolvia formato diferente de todos os gets.
- API versionada em `/api/v1`.
- `ImageUrl` passou a ser relativo; `PublicBaseUrl` saiu.
- Índice composto `ix_item_promotions_window` na query quente da vitrine.
- `.editorconfig`, `TreatWarningsAsErrors` e analyzers no backend; husky e
  lint-staged no frontend.
- `SiteFooter.vue` passou a ser renderizado — existia e nunca era usado.
- Componentes renomeados para nomes multi-palavra (`Contact.vue` → `ContactInfo.vue`,
  `Footer.vue` → `SiteFooter.vue`, `Hero.vue` → `HeroBanner.vue`).

### Removido

- `DELETE /item-promotions/{id}`. Apagava a linha **e** o arquivo de imagem, então
  uma campanha encerrada nunca podia ser repetida e corrigir um preço exigia
  recriar, perdendo a arte.
- Código morto: `LoginView.vue` (segunda tela de login sem rota), `PromotionList.vue`
  (segunda grade infinita completa), `stores/products.ts`, `productService.ts`,
  `promotionService.ts`, `models/ItemPromotion.ts`, `Models/ProductType.cs`, a
  propriedade `ProductType` solta no `AppDbContext`, ícones de scaffold.
- Dependências não usadas: `AWSSDK.Core`, `AWSSDK.Extensions.NETCore.Setup`,
  `Newtonsoft.Json`; `multer` e `cors` (pacotes de servidor Express num SPA); e
  `"boostrap": "^2.0.0"` — **typo-squat** do `bootstrap`.
- 57 artefatos de build rastreados (`backend/obj/`, `backend/.vs/`, `.csproj.user`) e
  um arquivo de 0 byte chamado
  `frontend/console.log(r.headers.get(access-control-allow-origin)))`.

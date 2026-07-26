# Plano de Evolução — Pharmacy System → Plataforma de Vitrine On-line

> Branch de trabalho: `claude/project-improvement-plan-d9iacl`
>
> **Estado:** Fases 0 a 8 implementadas. O plano está completo.
>
> Procedimentos operacionais em [`OPERACOES.md`](OPERACOES.md); decisões e seus
> motivos em [`adr/`](adr/); schema em [`MODELO_DE_DADOS.md`](MODELO_DE_DADOS.md);
> histórico do que mudou em [`../CHANGELOG.md`](../CHANGELOG.md).
>
> **Pendências que dependem do dono do projeto**, não de código, estão na
> [§7](#7-decisões-pendentes) e na [§9](#9-o-que-ficou-fora).

---

## 1. Contexto

O README apresenta o projeto como um "Full-Stack Pharmacy Management System" com autenticação, gestão de produtos, estoque, clientes, vendas e dashboard. **O código implementa uma coisa só:** uma vitrine de promoções (`ItemPromotion`) com handoff de pedido via WhatsApp.

Números reais do repositório:

| Métrica | Valor |
|---|---|
| Controllers | 1 (`ItemPromotionController`) |
| Model classes | 3 (`Category`, `ItemPromotion`, `ProductType` — esta órfã) |
| Tabelas | 2 (`categories`, `item_promotions`) |
| Rotas frontend | 7 |
| Linhas C# / TS+Vue | ~700 / ~2.310 |
| Testes | 0 (só o scaffold do Vite, que falharia) |
| Pipelines CI | 0 |

### Objetivos

1. Corrigir bugs reais e aumentar robustez.
2. Valor de portfólio para vagas na Nova Zelândia.
3. Valor operacional para a farmácia que já usa o sistema em produção.
4. **Generalizar o produto**: plataforma de vitrine on-line para qualquer tipo de loja, vendável como serviço a empresas que querem demonstrar produtos à venda.
5. **Persistir dados mensuráveis** no banco para análise estratégica e operacional.
6. **Arquivar anúncios com a imagem**, permitindo reativar promoções passadas.
7. **Exportar os dados** para análise externa.

### Restrições

| Fora de escopo | Detalhe |
|---|---|
| Pagamento on-line | O fechamento continua sendo handoff via WhatsApp |
| Dados sensíveis de cliente no servidor | Nome, telefone, CPF, CEP e endereço continuam **apenas** no `localStorage` do navegador do cliente e na conversa do WhatsApp. Nunca no banco |

Os objetivos 5–7 foram desenhados para conviver com essa restrição: o que vai para o banco são **fatos de negócio** (o que foi anunciado, visto, adicionado ao carrinho, pedido, por quanto, quando), não **pessoas**. Detalhe na [Fase 5](#fase-5--analytics-pedidos-anônimos-e-exportação).

---

## 2. Diagnóstico

### 2.1 Bugs de produção confirmados

| # | Problema | Onde | Impacto |
|---|---|---|---|
| B1 | **Imagens somem a cada deploy.** Uploads gravam em `wwwroot/images/promotions` dentro do container, sem volume | `docker-compose.yml` (backend sem `volumes`) | Linhas no banco apontando para URLs mortas após todo `docker compose up` |
| B2 | **Schema nunca é aplicado em Linux.** Compose monta `./Database/schema.sql`; o diretório em disco é `database/` | `docker-compose.yml:22-23` | Docker cria um dir vazio e a API sobe contra banco sem tabelas — no exato SO que o `README_DEPLOY.md` recomenda |
| B3 | **Backend sem nenhuma autenticação.** `app.UseAuthorization()` sem esquema registrado, zero `[Authorize]` | `Program.cs:95`; `ItemPromotionController.cs:48,177` | `POST` e `DELETE` de promoções são públicos: qualquer pessoa cria, apaga e faz upload de arquivo direto na API |
| B4 | **Senha de admin no bundle JS.** `ADMIN_PASSWORD = '1234'`, "auth" é `localStorage.setItem('admin_authenticated','true')` | `AdminLoginView.vue:76-79`; `router/index.ts:42` | Qualquer visitante entra no `/admin` pelo devtools. O lockout de 3 tentativas também é localStorage |
| B5 | **`docker compose up --build` é no-op.** Nenhum serviço tem `build:`; todos puxam imagens do GHCR que nenhum pipeline no repo constrói | `docker-compose.yml:37,70` | Alterações locais nunca chegam aos containers |
| B6 | **Produção roda como Development** (no Dockerfile *e* no compose) | `backend/Dockerfile`; `docker-compose.yml:44` | Swagger exposto, páginas de exceção com stack trace, `EnableDetailedErrors` no SignalR |
| B7 | **CORS totalmente aberto**, anulando a própria allowlist: `.SetIsOriginAllowed(_ => true)` após `WithOrigins(...)`, junto com `AllowCredentials()` | `Program.cs:41-59` | Qualquer origem chama os endpoints de escrita sem auth |
| B8 | **Três URLs hardcoded diferentes:** `localhost:80/api` (axios), `localhost:8080/api` (duplicata), `localhost:5000/promotionsHub`. Só `itemPromotionService.ts` usa caminho relativo e funciona pelo nginx | `services/api.ts:5`; `api/api.ts:4`; `services/signalr.ts:4` | Admin e realtime quebram fora do host Docker. `PublicBaseUrl: "http://localhost:80"` faz o mesmo com as URLs de imagem |
| B9 | **`.gitignore` está com o nome errado: `.git.ignore`.** O arquivo é completo e correto — git nunca o lê | raiz | 57 artefatos rastreados: `backend/obj/` (DLL, PDB, apphost.exe), `backend/.vs/` (bancos de índice do Copilot), `.csproj.user`, `.env` e `.env.development` **com senhas**, e um arquivo de 0 byte chamado `frontend/console.log(r.headers.get(access-control-allow-origin)))` |
| B10 | **Cache serve resultado do timezone errado.** `GetActivePaged` filtra por `nowLocal` derivado do parâmetro `timeZone`, mas a chave de cache o omite | `ItemPromotionController.cs:346-358` | O primeiro visitante define o resultado de todos por 5 minutos |
| B11 | `togglePromotion()` chama `PUT /item-promotions/{id}` — **não existe endpoint PUT** | `stores/promotions.ts:127` | 405 garantido; é a feature "editar promoção" que falta |
| B12 | `productService.ts` chama `GET /products` — **endpoint inexistente** | `services/productService.ts:16` | A store `products.ts` nunca funcionou |
| B13 | Link do menu aponta `/contact`; a rota registrada é `/Contact` | `NavBar.vue` vs `router/index.ts:22` | Link do menu não resolve |
| B14 | MySQL publicado em `3306:3306` no host, contrariando o próprio guia de deploy | `docker-compose.yml:18-19` | Banco exposto |
| B15 | **Path traversal no delete:** `Path.Combine(WebRootPath, promotion.ImagePath...)` + `File.Delete` sem verificação de contenção. Upload validado só pelo `ContentType` que o cliente envia, com extensão tirada do `FileName` não confiável | `ItemPromotionController.cs:75,188-195` | Escrita e deleção fora do diretório pretendido |
| **B16** | **`DELETE` destrói o histórico.** Remove a linha do banco **e** apaga o arquivo de imagem do disco | `ItemPromotionController.cs:177-204` | Toda promoção encerrada é perda total: sem histórico de preço, sem imagem, sem possibilidade de reativar. É o bug que mais atrapalha os objetivos 5–7 |

### 2.2 Dados placeholder num app "em produção"

`components/Contact.vue` tem `mailto:EMAIL@MAIL.com`, endereço `ADDRESS ADDRESS ADDRESS` e um Google Maps embarcado apontando para a **Torre de Pisa**. `WhatsappFloating.vue` e `Contact.vue` usam `641111111111`; `CheckoutView.vue:312` usa outro número hardcoded, `5545999975299`. O `.env` define `VITE_WHATSAPP_NUMBER`, que nenhum código lê.

### 2.3 Duplicação e código morto

- A projeção de 12 campos do `ItemPromotionResponseDto` está **copiada 6 vezes** no controller (linhas 110, 151, 221, 267, 318, 379). A da linha 110 (`POST`) é a única que **não** prefixa `publicBaseUrl` — a URL devolvida pelo create tem formato diferente de todos os gets.
- A cerimônia de cache (get → miss → query → set) repetida nas 6 actions de leitura.
- **CPF validado duas vezes com algoritmos diferentes:** `stores/checkout.ts:36-64` usa `(sum*10)%11`; `views/CheckoutView.vue:181-205` usa `11-(sum%11)`. A view ignora o getter da store. Os guards de repunit também divergem.
- **Seis definições concorrentes** do mesmo shape: `types/itemPromotion.ts`, `types/promotionForm.ts` (byte-idêntico), `stores/promotions.ts`, `models/ItemPromotion.ts` (obsoleto, com `nameProduct`/`newPrice`), `ProductGrid.vue`, `productService.ts`.
- Morto: `views/LoginView.vue` (2ª tela de login, sem rota, que não valida nada e só faz `router.push('/admin')`), `stores/auth.ts` (store de auth que ninguém importa), `components/PromotionList.vue` (2ª grid infinita completa), `components/Footer.vue` (nunca renderizado), `stores/products.ts`, `services/productService.ts`, `services/promotionService.ts`, `Models/ProductType.cs`, os 5 ícones de scaffold.
- `AppDbContext.cs:14` expõe `public ProductType ProductType { get; set; }` — propriedade de entidade solta, não um `DbSet<>`.
- Deps não usadas: `AWSSDK.Core`, `AWSSDK.Extensions.NETCore.Setup`, `Newtonsoft.Json` (0 referências no C#); `multer`, `cors` (pacotes de servidor Express num SPA) e `"boostrap": "^2.0.0"` — **typo-squat** do `bootstrap`.

### 2.4 Qualidade

- **Testes: zero.** Vitest e Playwright configurados, nenhum teste escrito. O único arquivo é `e2e/vue.spec.ts`, que espera `<h1>You did it!</h1>` e falharia. O eslint config referencia `src/**/__tests__/*`, diretório inexistente.
- **CI/CD: zero.** Nenhum `.github/`.
- **Tratamento de erro fino.** Sem `UseExceptionHandler`/ProblemDetails. `Utilities.cs:14-17` tem `catch { return TimeZoneInfo.Utc; }` silencioso engolindo parâmetro controlado pelo cliente. `Program.cs:99-107` usa `Console.WriteLine` em vez de `ILogger`. No frontend existe **um único `catch`** em todo o `src` — as actions do admin usam `try/finally` sem `catch`, então falha de save/delete não mostra nada ao usuário.
- **Zero validação declarativa.** `ItemPromotionCreateRequestDto` não tem uma DataAnnotation. Colunas são `VARCHAR(100)/(50)/(30)`, então input grande vira 500 do banco em vez de 400. Preço negativo é aceito. `CreatedByUserId`/`CreatedByUserName` vêm **do cliente** (`AdminView.vue` manda `0`/`'Admin'` fixos) — a trilha de auditoria é o que o chamador quiser escrever.
- **Sem EF Migrations.** Schema é `schema.sql` via entrypoint do MySQL, que só roda em volume vazio. Falta índice em `(is_active, date_start, date_end)`, exatamente a query quente.
- `RedisService.InvalidateByPrefixAsync` usa `server.Keys(pattern:)` — varredura `KEYS`/`SCAN` do keyspace inteiro em todo create/delete.
- README contradiz o código: diz **SQL Server** (é MySQL 8), **.NET 8** (é `net9.0`), lista Inventory/Customer/Sales Management e Dashboard que não existem, e mostra diretórios `Docker/` e `Documentation/` inexistentes. `frontend/README.md` é o scaffold intocado. `grep '///'` no backend → 0. `index.html` tem `<title>Vite App</title>`.
- Locale travado em pt-BR/Brasil em pontos que impedem vender fora: `toLocaleDateString('pt-BR')` em 4 arquivos; moeda como literal `R$ {{ price.toFixed(2) }}`; CPF/CEP como campos de primeira classe; `holidayService.ts:8` fixo em `/PublicHolidays/{year}/BR`; `allowedCity: 'Santa Terezinha de Itaipu'` em `stores/checkout.ts:31-32`; `deliveryFee: 8` e `minDeliveryTotal: 30` em `stores/cart.ts:20-21`.

### 2.5 Convenção a preservar

Identificadores em **inglês** (`ItemPromotionController`, `PriceBefore`, `useInfinitePromotions`), colunas em `snake_case` inglês, **todo texto de usuário e mensagem de erro da API em pt-BR**, comentários em blocos ALL-CAPS (`// ===== CREATE PROMOTION =====`). Duas funções escapam da regra e devem ser renomeadas: `abrirConfirmacao()` e `confirmarPedido()` em `CheckoutView.vue`.

---

## 3. Roadmap

Nove fases. **Fases 0–2 são pré-requisito de tudo**: não faz sentido construir persistência analítica sobre um deploy que perde imagens, um schema que não aplica e uma API de escrita sem autenticação.

| Fase | Tema | Esforço | Entrega principal |
|---|---|---|---|
| [0](#fase-0--higiene-do-repositório-e-bugs-críticos) ✅ | Higiene do repo + bugs críticos | P | Repo limpo, deploy funcional em Linux, imagens que sobrevivem |
| [1](#fase-1--autenticação-real) ✅ | Autenticação real | M | JWT, `[Authorize]` nas escritas, senha fora do bundle |
| [2](#fase-2--configuração-migrations-e-cicd) ✅ | Config, migrations e CI/CD | M | EF Migrations, `.env.example`, GitHub Actions, health checks |
| [3](#fase-3--arquitetura-e-testes) ✅ | Arquitetura e testes | M–G | Camada de serviço, validação, Serilog, suíte de testes |
| [4](#fase-4--modelo-de-dados-mídia-histórico-e-reativação) ✅ | **Mídia, histórico e reativação** | G | Biblioteca de anúncios, arquivar em vez de deletar, reativar promoção |
| [5](#fase-5--analytics-pedidos-anônimos-e-exportação) ✅ | **Analytics, pedidos e exportação** | G | Funil, dashboard, pedidos anônimos, export CSV |
| [6](#fase-6--white-label-qualquer-loja-on-line) ✅ | White-label (qualquer loja) | G | `StoreSettings` + catálogo genérico + i18n |
| [7](#fase-7--operacional-do-admin-seo-e-acessibilidade) ✅ | Operacional do admin, SEO, A11y | M | Editar promoção, categorias, busca, Open Graph |
| [8](#fase-8--documentação-e-observabilidade) ✅ | Docs e observabilidade | P–M | README honesto, ADRs, OpenTelemetry |

As Fases 4 e 5 são o núcleo dos objetivos 5–7 e vêm **antes** do white-label (Fase 6) de propósito: ambas mexem no mesmo schema, e migrar o modelo de dados duas vezes seria desperdício.

---

## Fase 0 — Higiene do repositório e bugs críticos

Baixo risco, alto retorno. Um PR único, sem mudança de comportamento visível ao usuário final.

### Git e limpeza

- `git mv .git.ignore .gitignore` — o conteúdo já está correto e ignora exatamente o que precisa.
- `git rm -r --cached backend/obj backend/.vs backend/Storefront.Api.csproj.user .env .env.development frontend/.env` e apagar o arquivo lixo `frontend/console.log(r.headers.get(access-control-allow-origin)))`.
- Criar `.env.example` com os nomes das variáveis e valores vazios (o `.gitignore` já traz a exceção `!.env.example`). **Rotacionar as senhas em produção**, já que estiveram versionadas.
- Remover deps não usadas: `AWSSDK.*` e `Newtonsoft.Json` do `.csproj`; `boostrap`, `cors`, `multer` do `package.json`; `using static System.Net.WebRequestMethods;` (`Program.cs:8`).
- Apagar código morto: `views/LoginView.vue`, `components/PromotionList.vue`, `stores/products.ts`, `services/productService.ts`, `services/promotionService.ts`, `models/ItemPromotion.ts`, `Models/ProductType.cs`, a propriedade `ProductType` de `AppDbContext.cs:14`, os 5 ícones de scaffold, e os `console.log` de debug em `useInfinitePromotions.ts:25` e `router/index.ts:47`. **`stores/auth.ts` não apagar** — vira a fonte de verdade na Fase 1.

### Deploy (`docker-compose.yml`)

- `./Database/` → `./database/` (**B2**).
- Volume nomeado `promotion_images:/app/wwwroot/images` no backend (**B1**). Pré-requisito absoluto da Fase 4: sem isso, arquivar imagens não faz sentido.
- `build: { context: ./backend }` e `build: { context: ./frontend }`, mantendo `image:` para o push no GHCR (**B5**).
- `depends_on: db: { condition: service_healthy }` — o healthcheck já existe e é ignorado.
- Healthcheck com `-p${MYSQL_ROOT_PASSWORD}` em vez de `-proot` hardcoded.
- Remover o mapeamento `3306:3306` (**B14**); adicionar `requirepass` no Redis e volume para persistência.
- `ASPNETCORE_ENVIRONMENT: Production` no compose e no `backend/Dockerfile` (**B6**), com um `docker-compose.override.yml` para dev.
- `npm ci` em vez de `npm install` no `frontend/Dockerfile`; remover o `ARG/ENV VITE_API_URL`, que roda no stage do nginx **depois** do build e portanto nunca chega ao bundle.

### Backend

- CORS: remover `SetIsOriginAllowed(_ => true)`; origens vindas de `Cors:AllowedOrigins` (**B7**).
- Remover a connection string com `pharmacy123` de `appsettings.json` e `appsettings.Development.json` (hoje byte-idênticos) — valores só por env.
- **Eliminar `PublicBaseUrl`**: devolver `ImageUrl` relativo (`/images/promotions/x.webp`) nas 6 projeções, deixando o nginx resolver. Corrige **B8** e a inconsistência do `POST` de uma vez.
- Incluir o timezone na chave de cache de `GetActivePaged`, e ler o cache **antes** do trabalho de timezone (**B10**).
- Endurecer upload e delete (**B15**): validar **magic bytes** em vez do `ContentType`; extensão derivada do tipo detectado, não do `FileName`; limite explícito por arquivo; `Path.GetFullPath(...).StartsWith(uploadsRoot)` antes de qualquer `File.Delete`. Remover o `Directory.CreateDirectory` do construtor, que roda a cada request.
- `ILogger` no lugar de `Console.WriteLine` e do `try/catch (ReflectionTypeLoadException)` em volta de `MapControllers()`; logar o catch de `Utilities.GetTimeZone`.

### Frontend

- Uma única base de API relativa (`/api`) via nginx; apagar `src/api/api.ts`; `signalr.ts` passa a usar `/promotionsHub` relativo. O `nginx.conf` já tem os três proxies (`/api/`, `/images/`, `/promotionsHub`) prontos, com `Upgrade` configurado.
- Corrigir `NavBar` → `/contact` e padronizar a rota em minúsculo (**B13**).
- Consolidar os 6 tipos duplicados em `types/itemPromotion.ts` como fonte única.
- CPF: manter só `isCpfValid` de `stores/checkout.ts`; `CheckoutView.vue` consome o getter.
- Renomear `abrirConfirmacao`/`confirmarPedido` → `openConfirmation`/`confirmOrder`.
- `catch` com feedback visível nas actions de `stores/promotions.ts`; `try/catch` no `JSON.parse(localStorage)` de `cart.ts:48`.
- `<title>` real no `index.html`.

---

## Fase 1 — Autenticação real

O item mais urgente depois da Fase 0, e pré-requisito de audit log confiável (Fase 4) e de qualquer atribuição de ação a usuário.

### Backend

- Tabela `users` (id, username, email, password_hash, role, is_active, created_at, last_login_at) via migration. Hash com **BCrypt** (`BCrypt.Net-Next`) ou **Argon2id**.
- `AuthController`: `POST /api/v1/auth/login` → JWT de vida curta + refresh token em cookie `HttpOnly`/`Secure`/`SameSite=Strict`; `POST /refresh`; `POST /logout`.
- `AddAuthentication().AddJwtBearer(...)`; `app.UseAuthentication()` **antes** do `UseAuthorization()` que já está lá.
- `[Authorize(Roles = "Admin")]` em `POST`/`PUT`/`PATCH`/`DELETE`. Leituras da vitrine seguem públicas — é uma vitrine.
- `CreatedByUserId`/`CreatedByUserName` passam a vir de `User.FindFirst(...)` e são **removidos** do `ItemPromotionCreateRequestDto`. Fim do audit trail falsificável.
- Rate limiting no login com o `AddRateLimiter` nativo do .NET 9 (substitui o lockout em localStorage, trivialmente burlável).
- Autenticação também no hub SignalR para eventos de admin.

### Frontend

- `AdminLoginView.vue`: apagar `ADMIN_USERNAME`/`ADMIN_PASSWORD`, o bloco de lockout em localStorage e o botão "🔓 Desbloquear (DEV)". O formulário chama a API.
- `stores/auth.ts` vira a fonte de verdade: token **em memória**, refresh via cookie — não guardar JWT em localStorage. O guard de `router/index.ts` consulta a store, não `localStorage.getItem('admin_authenticated')`.
- Interceptor axios: injeta `Authorization` e, em 401, tenta refresh uma vez antes de redirecionar para `/login`.

> Conta de admin do lojista **não** é dado sensível de cliente — é credencial operacional. Guardar `username` + hash é compatível com a restrição.

---

## Fase 2 — Configuração, migrations e CI/CD

- **EF Core Migrations** substituindo `database/schema.sql`. `InitialCreate` gerado a partir do `AppDbContext` atual; segundo migration com índice composto em `(is_active, date_start, date_end)`. `database/seed.sql` (as 5 categorias) vira `HasData` ou seeder idempotente. Rodar via serviço `migrator` separado no compose, não no boot da API.
- Substituir `ServerVersion.Parse("8.0.45-mysql")` por `AutoDetect` ou valor de config.
- Todas as constantes de negócio saem do código para configuração — preparação da Fase 6.
- **Health checks**: `AddHealthChecks().AddDbContextCheck().AddRedis()` em `/health` e `/health/ready`; usados pelo `depends_on`.
- **GitHub Actions** (`.github/workflows/`):
  - `ci.yml` — `dotnet build` + `dotnet test` + `dotnet format --verify-no-changes`; `npm ci` + `type-check` + `lint` + `test:unit`; Playwright em container.
  - `docker.yml` — build e push de `ghcr.io/eduardohanacleto/pharmacy-{backend,frontend}` com tag de versão + `latest`, e scan Trivy. Hoje o compose puxa essas imagens e **nenhum pipeline as constrói**.
  - Branch protection exigindo CI verde.
- `.editorconfig` no backend (não existe), `TreatWarningsAsErrors`, analyzers. `husky` + `lint-staged` no frontend — hoje nada força os scripts de lint/format que já existem.
- **Backup**: script de `mysqldump` + `tar` do volume de imagens, com retenção. O `README_DEPLOY.md` documenta o dump manual, mas não há automação — e a partir da Fase 4 o banco passa a ser o ativo estratégico do cliente.

---

## Fase 3 — Arquitetura e testes

Refatorar **antes** de crescer o domínio. O controller já tem 6 projeções duplicadas; as Fases 4–5 acrescentam ~12 endpoints.

### Backend

- `Services/IOfferService`, `IMediaService`, `IAnalyticsService`: o controller fica só com HTTP concerns. Hoje ele faz upload, validação, projeção, cache e broadcast SignalR.
- **Um único mapeamento**: extension method `ToResponseDto(this Offer)`, eliminando as 6 projeções copiadas e a divergência de `ImageUrl` entre `POST` e `GET`.
- `RedisService.GetOrSetAsync<T>(key, ttl, factory)` genérico, eliminando a cerimônia get/miss/set repetida 6×.
- Substituir `InvalidateByPrefixAsync` (varredura `KEYS`) por **versionamento de chave**: chaves viram `offers:v{n}:...`, invalidar é `INCR` num contador. O(1) em vez de O(keyspace).
- **Validação**: FluentValidation ou DataAnnotations nos DTOs (`[Required]`, `[MaxLength(100)]` casando com o `VARCHAR(100)`, `[Range]` para preços) → 400 em vez de 500 do banco.
- `app.UseExceptionHandler` + `ProblemDetails` padronizado. **Serilog** com structured logging e correlation id.
- Versionar a API: `/api/v1/...` (`Asp.Versioning.Mvc`).

### Testes

- **Backend** — xUnit + `WebApplicationFactory` + **Testcontainers** (MySQL e Redis reais). Cobrir: create com/sem auth (401 vs 201), validações (promocional ≥ original, datas invertidas, imagem ausente, tipo inválido), paginação e clamp de `pageSize`, invalidação de cache, tentativa de path traversal, upload com extensão falsa.
- **Frontend unit (Vitest)** — `stores/cart.ts` (`productsTotal`, `canDeliver` com `minDeliveryTotal`, `finalTotal` com `deliveryFee`, expiração de 24 h, localStorage corrompido), `stores/checkout.ts` (`isCpfValid` válido/inválido/repunit, cidade atendida), `hooks/useInfinitePromotions.ts` (paginação, `hasMore`, erro), `services/holidayService.ts`.
- **E2E (Playwright)** — reescrever `e2e/vue.spec.ts`. Fluxos: home → scroll infinito → carrinho → checkout → link do WhatsApp correto; login → criar promoção → aparece na home via SignalR → arquivar.
- Meta pragmática: ~70% de linha no backend, stores cobertas, 2 fluxos E2E, badge de cobertura no README.

---

## Fase 4 — Modelo de dados: mídia, histórico e reativação

**Esta fase implementa o objetivo 6.** Hoje o `DELETE` apaga a linha **e** o arquivo de imagem (**B16**): toda promoção encerrada é perda total. Não há histórico de preço, não há imagem, não há como reativar. E como o upload não tem volume (**B1**), até as promoções ativas perdem a imagem a cada deploy.

Princípio: **nada é apagado, tudo é arquivado.**

### 4.1 `media_assets` — biblioteca de imagens reutilizável

```sql
CREATE TABLE media_assets (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    file_path         VARCHAR(255) NOT NULL,
    content_hash      CHAR(64)     NOT NULL,   -- SHA-256 do conteúdo
    mime_type         VARCHAR(50)  NOT NULL,
    byte_size         INT          NOT NULL,
    width             INT          NULL,
    height            INT          NULL,
    original_filename VARCHAR(255) NULL,
    created_by_user_id INT         NOT NULL,
    created_at        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_media_assets_hash UNIQUE (content_hash),
    CONSTRAINT fk_media_assets_user FOREIGN KEY (created_by_user_id) REFERENCES users(id)
);
```

- `content_hash` único faz **deduplicação**: subir a mesma imagem duas vezes reaproveita o registro, em vez de duplicar arquivo.
- Uma imagem passa a poder ser referenciada por **várias** ofertas — é o que torna a reativação viável sem novo upload.
- Arquivo físico **nunca** é apagado enquanto houver `offer` referenciando. Purga só via job explícito para assets órfãos, com retenção configurável.
- Endpoint `GET /api/v1/media-assets` alimenta um seletor "escolher da biblioteca" no admin, ao lado do upload.

### 4.2 `products` e `offers` — separar produto de promoção

Hoje `ItemPromotion` é produto **e** promoção ao mesmo tempo. Consequências: não existe catálogo permanente (só aparece o que está em promoção), o item precisa ser recadastrado em cada promoção, e não há como comparar o preço promocional com um preço base.

```sql
CREATE TABLE products (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    name          VARCHAR(150)   NOT NULL,
    description   TEXT           NULL,
    sku           VARCHAR(50)    NULL,
    category_id   INT            NOT NULL,
    base_price    DECIMAL(10,2)  NULL,
    is_active     BOOLEAN        NOT NULL DEFAULT TRUE,
    display_order INT            NOT NULL DEFAULT 0,
    attributes    JSON           NULL,   -- atributos livres por vertical
    created_at    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at    DATETIME       NULL,
    CONSTRAINT fk_products_category FOREIGN KEY (category_id) REFERENCES categories(id)
);

CREATE TABLE offers (                    -- evolução de item_promotions
    id                 INT AUTO_INCREMENT PRIMARY KEY,
    product_id         INT            NULL,   -- NULL durante a transição
    name               VARCHAR(100)   NOT NULL,
    price              DECIMAL(10,2)  NOT NULL,
    price_before       DECIMAL(10,2)  NOT NULL,
    media_asset_id     INT            NOT NULL,
    date_start         DATETIME       NOT NULL,
    date_end           DATETIME       NOT NULL,
    status             VARCHAR(20)    NOT NULL,  -- draft|scheduled|active|expired|archived
    category_id        INT            NOT NULL,
    source_offer_id    INT            NULL,      -- de qual oferta esta foi reativada
    created_by_user_id INT            NOT NULL,
    created_at         DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at         DATETIME       NULL,
    archived_at        DATETIME       NULL,
    archived_by_user_id INT           NULL,
    CONSTRAINT fk_offers_product  FOREIGN KEY (product_id)      REFERENCES products(id),
    CONSTRAINT fk_offers_media    FOREIGN KEY (media_asset_id)  REFERENCES media_assets(id),
    CONSTRAINT fk_offers_source   FOREIGN KEY (source_offer_id) REFERENCES offers(id),
    CONSTRAINT fk_offers_category FOREIGN KEY (category_id)     REFERENCES categories(id),
    INDEX ix_offers_window (status, date_start, date_end)
);
```

- `status` substitui o `is_active` booleano, que hoje não distingue "rascunho", "agendada", "expirada" e "arquivada" — todas viram `false`.
- `source_offer_id` cria uma **linhagem de reativação**: "esta promoção já rodou 4 vezes; nas anteriores rendeu X visualizações e Y cliques". É exatamente o insight estratégico que justifica guardar o histórico.
- `media_asset_id` obrigatório: uma oferta sempre tem imagem, e reativar não exige novo upload.

### 4.3 `offer_status_history` — trilha operacional

```sql
CREATE TABLE offer_status_history (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    offer_id        INT         NOT NULL,
    from_status     VARCHAR(20) NULL,
    to_status       VARCHAR(20) NOT NULL,
    changed_by_user_id INT      NULL,     -- NULL = transição automática pelo job
    reason          VARCHAR(200) NULL,
    changed_at      DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_offer_status_offer FOREIGN KEY (offer_id) REFERENCES offers(id),
    INDEX ix_offer_status_offer (offer_id, changed_at)
);
```

Responde: quando entrou no ar, quanto tempo ficou, quem arquivou, se expirou sozinha ou foi tirada na mão. Sem isso, "duração média de promoção" e "promoções encerradas antes do previsto" são inconsultáveis.

### 4.4 `audit_log`

```sql
CREATE TABLE audit_log (
    id          BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id     INT          NULL,
    action      VARCHAR(50)  NOT NULL,   -- create|update|archive|reactivate|delete|login
    entity_type VARCHAR(50)  NOT NULL,
    entity_id   INT          NULL,
    changes     JSON         NULL,       -- diff resumido
    occurred_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_audit_occurred (occurred_at),
    INDEX ix_audit_entity (entity_type, entity_id)
);
```

Alimentado por interceptor do EF ou action filter. Só é confiável **depois da Fase 1** — hoje "quem fez" é o que o cliente HTTP resolver mandar. Resolve o "Audit Logging" prometido no README.

### 4.5 Endpoints

| Verbo | Rota | Efeito |
|---|---|---|
| `PATCH` | `/api/v1/offers/{id}/archive` | `status = archived`, grava `archived_at`/`archived_by`, registra em `offer_status_history`. **Não toca no arquivo de imagem.** Substitui o `DELETE` destrutivo |
| `POST` | `/api/v1/offers/{id}/reactivate` | Clona a oferta arquivada com novas `date_start`/`date_end`, reusando `media_asset_id`, e grava `source_offer_id` apontando para a original. Aceita override de preço |
| `PUT` | `/api/v1/offers/{id}` | Editar — o endpoint que `stores/promotions.ts:127` já chama e que não existe (**B11**) |
| `GET` | `/api/v1/offers?status=archived&page=` | Biblioteca de anúncios passados, com métricas de performance ao lado (após a Fase 5) |
| `GET` | `/api/v1/offers/{id}/history` | Linha do tempo de status + linhagem de reativações |
| `GET` | `/api/v1/media-assets?page=` | Biblioteca de imagens reutilizáveis |
| `DELETE` | `/api/v1/offers/{id}` | Mantido só para `role = Owner`, com `?purge=true` explícito. Hard delete deixa de ser o caminho padrão |

### 4.6 Migração de dados

Sequência, testada contra um dump de produção antes de rodar:

1. Criar `media_assets`, `products`, `offers`, `offer_status_history`, `audit_log`.
2. Para cada `item_promotions`: calcular SHA-256 do arquivo em `wwwroot`, inserir/reaproveitar `media_assets`, inserir `offers` com `status` derivado de `is_active` + janela de datas, e um registro inicial em `offer_status_history`.
3. Imagens referenciadas no banco mas **ausentes em disco** (consequência de **B1**) recebem um `media_assets` placeholder marcado, e o admin ganha um alerta "N anúncios sem imagem" para recuperação manual.
4. Manter `item_promotions` como view ou tabela legada por um ciclo de release, para rollback.
5. `products` fica opcional nesta fase (`product_id NULL`): o split completo pode vir junto da Fase 6, sem bloquear o arquivamento.

### 4.7 Job de manutenção

Serviço em background (`BackgroundService` ou `Hangfire` se houver mais jobs) rodando de hora em hora:

- Promove `scheduled` → `active` e `active` → `expired` conforme a janela de datas, registrando em `offer_status_history`. **Hoje a expiração é só um filtro de query** — o estado real nunca é gravado, então não existe o fato "expirou às 23h59 do dia 12".
- Recalcula agregados da Fase 5.
- Detecta `media_assets` órfãos.

---

## Fase 5 — Analytics, pedidos anônimos e exportação

**Esta fase implementa os objetivos 5 e 7.** Hoje o lojista tem **zero** visibilidade: o pedido só existe como mensagem no celular dele, e não há registro de nada que aconteceu na vitrine.

### 5.1 Limite de privacidade

O que vai para o banco são **fatos de negócio**, não pessoas.

| Persistido | Nunca persistido |
|---|---|
| Qual oferta foi vista/clicada, quando, quantas vezes | Nome, telefone, CPF, CEP, endereço, e-mail |
| Itens, quantidades, preços, total, tipo de entrega | IP do visitante |
| Cidade de entrega (opcional, agregada) | Identificador persistente de pessoa entre visitas |
| Contadores diários por oferta/produto/categoria | Qualquer cookie de rastreamento |

Nome, telefone, CPF e endereço continuam **exatamente como hoje**: em `stores/checkout.ts`, no `localStorage` do navegador do cliente, e na mensagem do WhatsApp. O `POST /orders` proposto abaixo **não recebe esses campos** — o payload é montado a partir do carrinho, não do formulário de dados pessoais.

**Decisão de projeto a confirmar:** para calcular *funil* (quantas das visitas que viram um produto o colocaram no carrinho) é preciso correlacionar eventos da mesma visita. Recomendo um id aleatório em **`sessionStorage`**, efêmero, descartado ao fechar a aba, nunca ligado a pessoa, e **eliminado no rollup diário** — analytics de primeira parte, sem cookie, sem IP. A alternativa, se preferir o mínimo absoluto, é abrir mão do funil por sessão e ficar só com contadores por evento (ainda dá ranking de produtos e volume, mas não taxa de conversão).

### 5.2 `analytics_events` — bruto, retenção curta

```sql
CREATE TABLE analytics_events (
    id           BIGINT AUTO_INCREMENT PRIMARY KEY,
    event_type   VARCHAR(30) NOT NULL,   -- offer_view|product_view|add_to_cart|
                                         -- cart_view|checkout_started|whatsapp_click
    offer_id     INT         NULL,
    product_id   INT         NULL,
    session_key  CHAR(32)    NULL,       -- efêmero, descartado no rollup
    occurred_at  DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_events_occurred (occurred_at, event_type),
    INDEX ix_events_offer (offer_id, occurred_at)
);
```

`POST /api/v1/events` público, com **rate limit** e aceitando lote (o frontend acumula e envia em batch, para não fazer uma request por scroll). Purga de bruto após N dias (config, default 90).

### 5.3 `analytics_daily` — rollup agregado, retenção longa

```sql
CREATE TABLE analytics_daily (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    stat_date       DATE        NOT NULL,
    event_type      VARCHAR(30) NOT NULL,
    offer_id        INT         NULL,
    product_id      INT         NULL,
    category_id     INT         NULL,
    event_count     INT         NOT NULL DEFAULT 0,
    unique_sessions INT         NOT NULL DEFAULT 0,
    CONSTRAINT uq_daily UNIQUE (stat_date, event_type, offer_id, product_id),
    INDEX ix_daily_date (stat_date)
);
```

O rollup é a fonte dos relatórios e dos exports: mantém o banco pequeno, as consultas rápidas e o histórico **para sempre** — inclusive de ofertas já arquivadas, que é o que faz a reativação da Fase 4 ser uma decisão informada.

### 5.4 `orders` e `order_items` — pedido sem PII

```sql
CREATE TABLE orders (
    id               INT AUTO_INCREMENT PRIMARY KEY,
    order_number     VARCHAR(20)   NOT NULL,   -- referência para o lojista casar com o WhatsApp
    fulfillment_type VARCHAR(20)   NOT NULL,   -- pickup|delivery
    payment_method   VARCHAR(20)   NULL,       -- pix|credit|debit|cash (declarado, não processado)
    delivery_city    VARCHAR(100)  NULL,       -- só cidade, nunca endereço
    currency         CHAR(3)       NOT NULL DEFAULT 'BRL',
    items_subtotal   DECIMAL(10,2) NOT NULL,
    delivery_fee     DECIMAL(10,2) NOT NULL DEFAULT 0,
    total            DECIMAL(10,2) NOT NULL,
    item_count       INT           NOT NULL,
    created_at       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_orders_number UNIQUE (order_number),
    INDEX ix_orders_created (created_at)
);

CREATE TABLE order_items (
    id               INT AUTO_INCREMENT PRIMARY KEY,
    order_id         INT           NOT NULL,
    offer_id         INT           NULL,
    product_id       INT           NULL,
    name_snapshot    VARCHAR(150)  NOT NULL,   -- nome no momento do pedido
    unit_price_snapshot DECIMAL(10,2) NOT NULL,-- preço no momento do pedido
    quantity         INT           NOT NULL,
    line_total       DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders(id),
    CONSTRAINT fk_order_items_offer FOREIGN KEY (offer_id) REFERENCES offers(id)
);
```

- Os **snapshots** de nome e preço são essenciais: o relatório histórico não pode mudar quando o produto for renomeado ou o preço reajustado. É o erro mais comum nesse tipo de modelagem.
- `order_number` curto (ex. `#A7K3`) vai também na mensagem do WhatsApp, permitindo ao lojista casar a conversa com o registro sem que o sistema saiba quem é a pessoa.
- Gravado no clique de "confirmar pedido" em `CheckoutView.vue`, **antes** do `window.open('https://wa.me/...')`. O `fulfillment_type`, `delivery_fee` e `total` vêm de `stores/cart.ts` (`finalTotal`, `deliveryFee`, `deliveryType`), que já calculam tudo isso.
- `status` **não** é modelado: o sistema não tem como saber se o pedido foi concluído — isso acontece no WhatsApp. Registrar "pedido iniciado" é honesto; inventar um ciclo de vida que ninguém alimenta produziria relatório falso.

### 5.5 Dashboard de insights no admin

- **Funil**: vitrine → carrinho → checkout → clique no WhatsApp, com taxa de conversão em cada passo.
- **Produtos mais vistos vs. mais adicionados ao carrinho** — revela o item de boa procura e conversão ruim, que é problema de preço ou de foto. É o insight de maior valor prático para o lojista.
- **Performance por oferta**: views, adds, cliques, pedidos, receita, e comparação com reativações anteriores da mesma oferta (via `source_offer_id`).
- **Ranking de reativação**: quais ofertas arquivadas tiveram melhor conversão — resposta direta para "qual promoção vale repetir".
- Horários e dias de pico; ticket médio; retirada vs. entrega; receita por categoria.
- Séries temporais com comparação período a período.

### 5.6 Alertas operacionais

Widget no admin, e opcionalmente resumo semanal por e-mail: ofertas expirando em 7 dias; expiradas ainda marcadas ativas; promocional ≥ preço base; produto sem imagem, sem preço ou sem categoria; produto sem nenhuma view em 30 dias; queda abrupta de conversão semana a semana.

### 5.7 Exportação

`GET /api/v1/exports/{dataset}?from=&to=&format=csv` (autenticado, streaming, sem carregar tudo em memória):

| Dataset | Conteúdo |
|---|---|
| `offers` | Catálogo completo com status, janela, preços, linhagem de reativação |
| `offer-performance` | Uma linha por oferta × dia: views, adds, cliques, pedidos, receita |
| `funnel-daily` | Funil agregado por dia |
| `orders` | Pedidos anônimos com totais e tipo de entrega |
| `order-items` | Itens com snapshots de nome e preço |
| `top-products` | Ranking por período |
| `audit-log` | Ações administrativas |

Formatos: **CSV** (padrão, UTF-8 com BOM para abrir corretamente no Excel pt-BR) e **JSON**. Botão de export em cada tela do dashboard, respeitando os filtros ativos. Resolve o "Reporting Module" prometido no README.

---

## Fase 6 — White-label: qualquer loja on-line

**Objetivo 4.** Hoje o app tem Brasil, pt-BR, R$, CPF, Santa Terezinha de Itaipu e Torre de Pisa embutidos no código. Para vender a "empresas que querem demonstrar produtos à venda", nada disso pode estar hardcoded.

**Recomendação: white-label single-tenant primeiro, multi-tenant depois.**

- **Etapa A (agora)** — uma stack Docker por cliente, 100% configurável **sem deploy**. Entrega valor comercial imediato sem reescrever o modelo de dados.
- **Etapa B (depois)** — multi-tenant real: coluna `tenant_id` em todas as tabelas, resolução por host/subdomínio (`loja1.dominio.com`), global query filter no EF. Só vale o custo quando o custo por stack incomodar.

### 6.1 `store_settings`

Tabela de linha única (por tenant na Etapa B) + `GET /api/v1/store-settings` (público, cacheado) + `PUT` (autenticado) + tela "Configurações da loja" no admin:

| Grupo | Campos |
|---|---|
| Identidade | nome, slogan, logo, favicon, cor primária/secundária, fonte |
| Localização | endereço, coordenadas do mapa, cidade, país, timezone |
| Contato | telefone, WhatsApp, e-mail, redes sociais |
| Comercial | moeda, locale, taxa de entrega, mínimo para entrega, cidades atendidas, retirada habilitada |
| Operação | horários de funcionamento por dia, país para feriados |
| Checkout | quais campos coletar (CPF on/off, endereço on/off), texto de confirmação |

O frontend carrega no bootstrap e aplica branding via **CSS custom properties** (`--brand-primary`). Com isso morrem, de uma vez: Torre de Pisa e `EMAIL@MAIL.com` (`Contact.vue`), os dois números de WhatsApp hardcoded diferentes, `deliveryFee: 8`/`minDeliveryTotal: 30` (`cart.ts:20-21`), `allowedCity: 'Santa Terezinha de Itaipu'` (`checkout.ts:31-32`) e o `/BR` fixo do `holidayService.ts:8`.

### 6.2 Catálogo genérico

Completar o split iniciado na Fase 4: popular `products`, ligar `offers.product_id`, e o `attributes JSON` permitir campos por vertical (farmácia, padaria, autopeças) sem migration. `ProductType` (classe órfã hoje) volta como `Category` hierárquica ou enum real.

Renomear `Storefront.Api` → nome neutro (`Storefront.Api`) num PR isolado, puramente mecânico.

### 6.3 i18n e locale

- `vue-i18n` com `pt-BR` e `en-NZ`; extrair as strings inline dos templates (`"Usuário"`, `"Senha"`, `"Administração de Promoções"`, `"Nenhuma promoção disponível"`, …). Mensagens de erro da API via resource files.
- `Intl.NumberFormat` no lugar de `R$ {{ price.toFixed(2) }}`; `Intl.DateTimeFormat` no lugar dos 4 `toLocaleDateString('pt-BR')`.
- Campos brasileiros (CPF, CEP) viram opcionais controlados por `store_settings`. **A farmácia atual continua idêntica** — mesmos textos, mesmo checkout com CPF, mesma taxa de R$ 8 e mínimo de R$ 30; um cliente de outro país simplesmente não vê esses campos.

---

## Fase 7 — Operacional do admin, SEO e acessibilidade

- **Categorias de verdade**: `CategoryDto` hoje devolve só `Name`, sem `Id`, e por isso `AdminView.vue` manda `categoryId: 1` fixo. Adicionar `Id`, usar o dropdown, criar CRUD de categorias.
- Duplicar oferta, ativar/desativar em lote, agendar publicação (o `status = scheduled` da Fase 4 já suporta).
- **Busca e filtros** na vitrine (o "Product Search" do README): nome, categoria, faixa de preço, com índice full-text no MySQL.
- **Imagens**: upload múltiplo, conversão para WebP + thumbnails no servidor (`ImageSharp`), `srcset` e lazy loading. Ganho direto de Lighthouse e de dados móveis do cliente final. Integra com `media_assets` da Fase 4.
- **SEO/PWA**: meta description, **Open Graph** — hoje, quando o lojista compartilha o link no WhatsApp, não aparece preview nenhum, o que numa vitrine é perda direta de conversão. Sitemap, `robots.txt`, dados estruturados `Product`/`Offer` (schema.org), manifest PWA + service worker.
- **Acessibilidade**: `alt` nas imagens de produto, contraste, navegação por teclado, ARIA nos modais do checkout. Requisito real de mercado na NZ.
- Renderizar `components/Footer.vue`, que existe e nunca é usado.

---

## Fase 8 — Documentação e observabilidade

- **README honesto** — hoje é o maior risco de credibilidade num processo seletivo: diz SQL Server (é MySQL 8), .NET 8 (é `net9.0`), lista features inexistentes e mostra diretórios que não existem. Reescrever com o que o sistema faz de verdade, diagrama correto, screenshots, e roadmap apontando para este documento. **Faria isso junto da Fase 0** — é o que um recrutador lê primeiro.
- Substituir `frontend/README.md` (scaffold intocado do Vite) por instruções reais; documentar o fluxo local sem Docker (`dotnet run` + `npm run dev`), hoje não escrito em lugar nenhum.
- `docs/` com diagrama C4 nível 2, **modelo de dados** (as tabelas das Fases 4–5 merecem um ER diagram), e **ADRs curtas**: por que JWT, por que white-label antes de multi-tenant, por que WhatsApp em vez de pagamento on-line, por que analytics sem PII, por que arquivar em vez de deletar. ADRs são sinal forte de senioridade em revisão de portfólio.
- `LICENSE`, `CONTRIBUTING.md`, `CHANGELOG.md`.
- XML docs no backend (hoje `grep '///'` → 0) alimentando o Swagger com descrições e exemplos. Substituir `Storefront.Api.http`, que ainda tem só o `GET /weatherforecast` do scaffold.
- **Observabilidade**: Serilog estruturado (Fase 3), OpenTelemetry (traces + métricas), `/health` (Fase 2), e opcionalmente Prometheus + Grafana no compose — o `README_DEPLOY.md` já os menciona como opcionais.
- **Documento de privacidade**: página curta declarando o que é e o que não é coletado. Com a Fase 5 no ar, isso deixa de ser opcional e passa a ser argumento de venda.

---

## 4. Arquivos críticos

- `backend/Controllers/ItemPromotionController.cs` — o arquivo com mais dívida do repo: 6 projeções duplicadas, 6 blocos de cache repetidos, zero auth, validação inline, upload inseguro, delete destrutivo.
- `backend/Program.cs` — auth, CORS, migrations, health checks, Serilog, rate limiting, ambiente.
- `backend/Data/AppDbContext.cs` — todas as entidades novas; remover a propriedade `ProductType` solta; global filters se for para multi-tenant.
- `backend/Services/RedisService.cs` — `GetOrSetAsync<T>` e invalidação por versão em vez de varredura de keyspace.
- `docker-compose.yml` — casing de `database/`, volume de imagens, contextos de build, healthy dependency, ambiente, portas.
- `.git.ignore` → `.gitignore`.
- `frontend/src/services/api.ts` + `signalr.ts` — base relativa única, interceptor de auth (e apagar `src/api/api.ts`).
- `frontend/src/views/AdminLoginView.vue` + `stores/auth.ts` + `router/index.ts` — auth real de ponta a ponta.
- `frontend/src/views/CheckoutView.vue` — `POST /orders` antes do `window.open`, CPF unificado, funções renomeadas.
- `frontend/src/stores/cart.ts` + `checkout.ts` — constantes de negócio saem para `store_settings`; fonte dos totais do pedido.
- `frontend/src/views/AdminView.vue` — biblioteca de anúncios, reativação, dashboard, export.

### Reaproveitar em vez de reescrever

Já existe e é bom: `Utilities.GetTimeZone` (só precisa logar o catch); `DTOs/PagedResultDto.cs`, que já serve para paginar catálogo, biblioteca de anúncios e relatórios; `hooks/useInfinitePromotions.ts`, que já resolve scroll infinito com `IntersectionObserver` e serve para o catálogo genérico; `Hubs/PromotionsHub.cs` + `services/signalr.ts` (realtime já funciona, e serve para atualizar o dashboard ao vivo); `frontend/nginx.conf`, com os três proxies corretos; `services/holidayService.ts`, que só precisa parametrizar o país; a lógica "Aberto agora / Fechado agora" de `Contact.vue`, que só precisa ler config; os getters de `stores/cart.ts` (`productsTotal`, `canDeliver`, `finalTotal`), que já produzem exatamente os totais do `orders`; e `eslint.config.ts` + `.prettierrc.json` + `vitest.config.ts` + `playwright.config.ts`, configurados e prontos — só faltam testes.

---

## 5. Verificação

### Fase 0
```bash
git status --porcelain                     # nada de obj/, .vs/, .env
docker compose down -v && docker compose up --build -d
docker compose exec db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
  -e "SHOW TABLES;" pharmacy_db            # tabelas presentes → B2 corrigido
curl -I  http://localhost:5000/swagger     # 404 em Production → B6
curl -H "Origin: https://evil.example" -i \
     http://localhost:5000/api/item-promotions/all   # sem CORS header → B7
```
Criar promoção com imagem, `docker compose restart backend`, conferir que a imagem continua carregando (**B1**). Abrir a vitrine de outra máquina da rede: imagens e SignalR funcionam (**B8**).

### Fase 1
```bash
curl -X DELETE http://localhost:5000/api/v1/offers/1               # 401
TOKEN=$(curl -s -X POST .../auth/login -d '{...}' | jq -r .token)
curl -X DELETE .../offers/1 -H "Authorization: Bearer $TOKEN"      # 204
grep -ri "1234\|ADMIN_PASSWORD" frontend/dist/                     # vazio
```
Forçar `admin_authenticated=true` no devtools: `/admin` **não** deve carregar dados. Criar oferta autenticado e conferir que `created_by_user_id` é o id real do token, não `0`.

### Fase 2
`dotnet ef migrations list`; `dotnet ef database update`; `curl /health`; `EXPLAIN` mostrando uso do índice composto. PR de teste com CI verde e imagem publicada no GHCR com tag de versão.

### Fase 3
```bash
dotnet test /p:CollectCoverage=true     # Testcontainers sobe MySQL + Redis
cd frontend && npm run test:unit && npm run test:e2e && npm run type-check
```
Path traversal e upload com extensão falsa (`.png` com bytes de executável) devem dar 400.

### Fase 4
- Arquivar uma oferta → linha continua no banco com `status='archived'`, arquivo de imagem **ainda existe em disco**, e há registro em `offer_status_history`.
- Reativar → nova oferta criada, `source_offer_id` apontando para a original, **mesmo `media_asset_id`**, sem novo upload.
- Subir a mesma imagem duas vezes → um único registro em `media_assets` (dedup por `content_hash`).
- Rodar a migração contra um dump de produção num container descartável e conferir contagem: `SELECT COUNT(*) FROM item_promotions` = `SELECT COUNT(*) FROM offers`.
- `docker compose restart backend` depois de arquivar → imagem da oferta arquivada continua servindo.
- Deixar uma oferta expirar e conferir que o job gravou a transição `active → expired`.

### Fase 5
- Navegar como visitante: ver produto, adicionar ao carrinho, ir ao checkout, clicar no WhatsApp. Conferir os 5 eventos e o funil batendo no dashboard.
- `SELECT * FROM orders JOIN order_items ...` → totais idênticos ao que a UI mostrou; **zero** colunas de nome, telefone, CPF, CEP ou endereço.
- `SHOW COLUMNS FROM analytics_events` → nenhuma coluna de PII, nenhum IP.
- Renomear um produto depois do pedido → `order_items.name_snapshot` **não muda**.
- Rodar o rollup e conferir `analytics_daily` contra agregação direta de `analytics_events`; depois purgar o bruto e confirmar que os relatórios continuam iguais.
- Exportar CSV de cada dataset, abrir no Excel pt-BR (acentos corretos) e conferir totais contra SQL direto.

### Fase 6
- Subir uma segunda stack com `.env` diferente (outro nome, logo, cor, moeda, país, WhatsApp) e confirmar que **nenhuma linha de código** foi alterada — é o teste real do white-label.
- Trocar o locale para `en-NZ`: moeda, datas e ausência dos campos CPF/CEP.
- Confirmar que a farmácia atual segue idêntica.

### Fases 7–8
Lighthouse ≥ 90 em Performance, SEO e Accessibility. Colar a URL no WhatsApp e ver o preview Open Graph renderizar. Ler o README linha por linha contra o código: nenhuma afirmação sem implementação correspondente.

---

## 6. Riscos

| Risco | Mitigação |
|---|---|
| Sistema **em produção numa farmácia real** | Fases 0–2 primeiro (só correções, sem mudança visível). Backup do volume MySQL **e** do diretório de imagens antes de tudo. A Fase 6 preserva a experiência atual por configuração |
| Migração `item_promotions` → `offers` + `media_assets` | Script testado contra dump de produção em container descartável; tabela legada mantida por um ciclo de release para rollback |
| Imagens já perdidas por **B1** antes da correção | A migração marca os assets ausentes e o admin lista "N anúncios sem imagem" para recuperação manual. Quanto mais cedo a Fase 0, menos perda |
| `analytics_events` crescer sem controle | Rollup diário + purga do bruto com retenção configurável; `analytics_daily` é a fonte de relatório |
| Persistir pedido pode virar PII por acidente | O DTO de `POST /orders` **não tem** campos de pessoa; teste automatizado assertando que as tabelas não contêm colunas de PII |
| Renomear `Storefront.Api` toca todos os namespaces | PR mecânico isolado, revisado por diff de rename |
| Multi-tenant no modelo desde já vira over-engineering | Etapa A (1 stack por cliente) entrega o valor comercial; Etapa B só sob demanda |
| `.env` versionado com senhas | Rotacionar em produção; decidir sobre reescrita de histórico |

---

## 7. Decisões pendentes

Todas as cinco foram decididas. Nenhuma continua aberta.

| # | Decisão | Resolução |
|---|---|---|
| 1 | **Correlação de sessão para o funil** (§5.1) | **Chave efêmera em `sessionStorage`**, como recomendado: morre com a aba e é descartada no rollup. Sem ela existem contadores, mas não taxa de conversão |
| 2 | **`orders.delivery_city`** | **Mantida.** Útil para análise de cobertura e não identifica ninguém |
| 3 | **Expurgar `.env` do histórico** | **Não expurgar.** Removido do HEAD e as credenciais rotacionadas. O histórico continua com as senhas antigas, que a rotação torna inúteis — reescrever o histórico invalidaria todo clone e fork por um ganho que a rotação já entrega |
| 4 | **Renomear o projeto** | **Feito:** `PharmacyWorkerAPI` → **`Storefront.Api`** |
| 5 | **Retenção do bruto de `analytics_events`** | **90 dias**, configurável em `ANALYTICS_RAW_RETENTION_DAYS`. Longo o bastante para comparar uma promoção com o mesmo mês do trimestre anterior; curto o bastante para a tabela bruta nunca ser a maior coisa no banco. Os relatórios leem `analytics_daily`, que não é purgado |

### O que o rename tocou, e o que não tocou

Renomeado: namespaces `Storefront.Api.*`, `Storefront.Api.csproj`, `.sln`, `.http`,
`tests/Storefront.Api.Tests/`, os containers (`storefront_api`, `storefront_db`, …),
a rede (`storefront_network`) e as imagens do GHCR
(`storefront-backend`, `storefront-migrator`, `storefront-frontend`).

**Não** renomeado, de propósito:

- **`name: pharmacy-system` no `docker-compose.yml`.** Esse nome prefixa os volumes:
  `db_data` é `pharmacy-system_db_data` no disco. Trocá-lo deixaria o banco, o
  appendonly do Redis e as imagens enviadas órfãos atrás do prefixo antigo, e a
  stack subiria vazia — perda de dados disfarçada de rename. Quem quiser trocar
  precisa migrar os volumes primeiro; o procedimento está em
  [`OPERACOES.md`](OPERACOES.md).
- **`MYSQL_DATABASE` (`pharmacy_db`).** É o nome do banco de uma instalação
  existente. Trocar não tem ganho nenhum e tem o mesmo risco.

> ⚠️ **Consequência de deploy:** as imagens no GHCR mudaram de nome. O primeiro
> deploy depois do merge precisa de `docker compose up --build`, ou de esperar o
> `docker.yml` publicar as tags `storefront-*` — `docker compose pull` não vai
> encontrar as antigas.

### Número do WhatsApp: agora um override de runtime

`STORE_WHATSAPP_NUMBER` deixou de ser apenas semente. Quando definida, ela **vence
sobre a linha do banco em toda leitura** de `GET /api/v1/store-settings`; vazia, o
número é gerenciado em `/admin/settings`.

Antes, a variável era write-once e silenciosamente inerte: mudá-la no `.env` e
reiniciar não fazia nada, porque a linha já existia. Também não é build-time — nada
sobre o número entra no bundle, então trocar exige reiniciar o backend e nada mais.
Um valor que não tenha de 8 a 20 dígitos é ignorado com aviso no log, em vez de
gerar um link `wa.me` quebrado que custaria todos os pedidos em silêncio. A tela de
admin mostra o campo como somente-leitura e diz que o ambiente é o dono.

O `VITE_WHATSAPP_NUMBER` e todo o encanamento de build arg que ninguém lia foram
removidos.

### Ações do seu lado, antes de subir em produção

1. **Rotacionar** `MYSQL_ROOT_PASSWORD`, `MYSQL_PASSWORD` e `REDIS_PASSWORD` — estiveram versionados. É a mitigação escolhida em vez de reescrever o histórico, então é obrigatória, não opcional.
2. Definir `JWT_SIGNING_KEY` (32+ caracteres; `openssl rand -base64 48`) e `ADMIN_SEED_USERNAME`/`ADMIN_SEED_PASSWORD`.
3. Aplicar o baseline de `database/upgrades/` **numa base criada antes das migrations**, uma vez.
4. Definir `STORE_WHATSAPP_NUMBER` (só dígitos), ou deixar vazio e gerenciar em `/admin/settings`.
5. Abrir `/admin/settings` e preencher o resto — inclusive `logoUrl` (`/logoFarma.png` recupera a arte atual, que agora vive em `frontend/public/`).
6. Primeiro deploy com `docker compose up --build`, por causa do rename das imagens.
7. Configurar branch protection exigindo CI verde (precisa de permissão de admin no repositório).

---

## 9. O que ficou fora

Deliberadamente, com o motivo:

| Item | Por que não |
|---|---|
| **Preview Open Graph por promoção** | O crawler do WhatsApp não executa JavaScript, então título e imagem vindos de `store_settings` depois do mount nunca são lidos. As tags estáticas estão no `index.html`; preview dinâmico exige renderizar a página no servidor — mudança de arquitetura, não ajuste |
| **Separar `Product` de `Offer`** (§6.2) | Migração de dados sobre uma base em produção. Merece um PR próprio, com script testado contra um dump, sem misturar com mudança de comportamento. Enquanto isso, reativar e duplicar mitigam o recadastro |
| **Multi-tenancy** | [ADR 0005](adr/0005-white-label-antes-de-multi-tenant.md) |
| **Índice full-text na busca** | `LIKE '%termo%'` não usa índice, e no tamanho de um catálogo de loja isso não custa nada. Um índice full-text no MySQL adiciona dependência de schema por um ganho ainda inexistente |
| **Conversão de imagem para WebP e thumbnails** | `ImageSharp` resolveria, e é ganho de Lighthouse, não de correção. O `loading="lazy"` já está no lugar |
| **Prometheus e Grafana no compose** | OpenTelemetry instrumenta e exporta por OTLP quando `OpenTelemetry:OtlpEndpoint` está configurado. Subir o coletor é decisão de operação, não de código |
| **Resumo semanal por e-mail dos alertas** | Exige credencial de SMTP e uma decisão de para quem enviar. Os alertas já aparecem no dashboard |

---

## 8. Ordem de execução

```
Fase 0  ─ Higiene + bugs críticos          ← começar aqui, 1 PR
Fase 1  ─ Autenticação real                 ← fecha o buraco de segurança
Fase 2  ─ Migrations + CI/CD                ← destrava todo o resto com segurança
Fase 3  ─ Refatoração + testes              ← antes de crescer o domínio, não depois
Fase 4  ─ Mídia, histórico, reativação      ← objetivo 6
Fase 5  ─ Analytics, pedidos, exportação    ← objetivos 5 e 7
Fase 6  ─ White-label                       ← objetivo 4
Fase 7  ─ Admin, SEO, acessibilidade
Fase 8  ─ Docs + observabilidade             ← README honesto junto da Fase 0
```

O que muda a vida do lojista mais rápido: **Fase 0** (para de perder imagem) e **Fase 4** (para de perder histórico). O que vende o produto: **Fases 5 e 6**. O que impressiona em entrevista: **Fases 1, 2, 3** e o README da Fase 8.

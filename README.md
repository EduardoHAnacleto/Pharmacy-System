# Storefront — vitrine de promoções com pedido por WhatsApp

<div align="center">

![.NET](https://img.shields.io/badge/.NET-9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Vue.js](https://img.shields.io/badge/Vue.js-3-42B883?style=for-the-badge&logo=vue.js&logoColor=white)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=for-the-badge&logo=typescript&logoColor=white)
![MySQL](https://img.shields.io/badge/MySQL-8-4479A1?style=for-the-badge&logo=mysql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)

</div>

Vitrine on-line configurável para lojas que querem mostrar produtos em promoção e
receber o pedido pelo WhatsApp — sem pagamento on-line e **sem guardar nenhum dado
pessoal do cliente no servidor**.

Roda em produção numa farmácia no Brasil. Nada no código é específico dessa
farmácia: nome, cores, logotipo, endereço, contato, moeda, idioma, taxa de entrega,
cidades atendidas, horário de funcionamento e quais campos pedir no checkout são
dados, editáveis numa tela de administração.

> **Escopo, por decisão do dono do projeto:** não há pagamento on-line e não há
> armazenamento de dados sensíveis de clientes. Essas duas restrições moldaram a
> arquitetura e não são omissões.

---

## O que o sistema faz

**Vitrine (público, sem login)**

- Grade de promoções com scroll infinito, atualizada em tempo real via SignalR
- Busca por nome, filtro por categoria e faixa de preço, cinco ordenações
- Carrinho no navegador, com retirada ou entrega conforme as regras da loja
- Checkout que monta a mensagem de WhatsApp com o pedido
- Página de contato com mapa, horário e o estado "aberto/fechado agora" calculado
  no fuso da loja, considerando feriados do país da loja
- Dados estruturados schema.org `Product`/`Offer`, `robots.txt`, `sitemap.xml`,
  manifest PWA
- Português (pt-BR) e inglês (en-NZ), com moeda e datas formatadas pelo locale da loja

**Administração (JWT, papel Admin)**

- Criar, editar, arquivar, reativar e duplicar promoções
- Biblioteca de arquivadas: reativar reaproveita a imagem, sem novo upload
- CRUD de categorias
- Configurações da loja: identidade, marca, localização, contato, comercial,
  checkout e horários
- Insights: funil de conversão, desempenho por promoção, resumo de vendas,
  alertas operacionais
- Exportação CSV para análise fora do sistema
- Trilha de auditoria de toda ação administrativa

**O que o sistema deliberadamente não faz**

Pagamento on-line; cadastro de clientes; controle de estoque; PDV; nota fiscal.

---

## Privacidade por construção

O ponto que mais influenciou o desenho:

| Dado | Onde fica |
|---|---|
| Nome, telefone, CPF, CEP, endereço | **Somente no navegador do visitante** (`localStorage`) e na mensagem de WhatsApp |
| Itens, quantidades, valores, tipo de entrega, cidade | Banco de dados (`orders`, `order_items`) |
| Eventos de uso | Banco (`analytics_events`), com chave de sessão aleatória de `sessionStorage`, sem IP, sem user-agent, sem cookie |

Não é uma promessa em prosa: `CreateOrderDto` não tem campo para dado pessoal, as
tabelas não têm coluna para isso, e `NoPersonalDataTests` falha o build se alguém
adicionar uma — a asserção roda contra o modelo EF mapeado, não contra o texto do
código. Os eventos brutos são agregados em `analytics_daily` e apagados após a
retenção.

A página `/privacy` explica isso ao visitante da loja.

---

## Arquitetura

```
                    Navegador
                        │
                        ▼
              nginx  (frontend)
        /  ·  /api/  ·  /images/  ·  /promotionsHub
                        │
                        ▼
              ASP.NET Core 9  (backend)
              Controllers → Services → EF Core
                    │              │
                    ▼              ▼
              MySQL 8          Redis 7
            (migrations)   (cache versionado)
```

O nginx serve o SPA e é o único host público: o frontend chama `/api/v1` relativo,
então não há URL de backend embutida no bundle. Migrations são aplicadas por um
serviço `migrator` separado, que roda até o fim antes de o backend subir — DDL não
fica acoplado ao boot da API.

Decisões e seus motivos estão em [`docs/adr/`](docs/adr/).

---

## Stack

| Camada | Tecnologia |
|---|---|
| Frontend | Vue 3, TypeScript, Vite 7, Pinia, vue-router, vue-i18n, Bootstrap 5 |
| Backend | ASP.NET Core 9, EF Core 9 (Pomelo MySQL), SignalR, Serilog, OpenTelemetry |
| Dados | MySQL 8, Redis 7 |
| Auth | JWT + refresh token rotativo em cookie HttpOnly, PBKDF2-HMAC-SHA256 (600 000 iterações) |
| Testes | xUnit + Testcontainers (MySQL e Redis reais), Vitest, Playwright |
| Infra | Docker Compose, nginx, GitHub Actions, GHCR, Trivy |

---

## Rodando localmente

Precisa de Docker e Docker Compose.

```bash
git clone https://github.com/EduardoHAnacleto/Pharmacy-System.git
cd Pharmacy-System

cp .env.example .env
# Preencha .env. Obrigatórios: MYSQL_*, REDIS_PASSWORD, JWT_SIGNING_KEY
# (mínimo 32 caracteres — a API se recusa a subir com menos) e ADMIN_SEED_*.
#   openssl rand -base64 48
#
# STORE_WHATSAPP_NUMBER é opcional: definida, o ambiente manda no número do
# WhatsApp; vazia, quem manda é a tela /admin/settings.

docker compose up --build -d
```

- Vitrine: <http://localhost>
- Administração: <http://localhost/login>
- Health: <http://localhost:5000/health> e `/health/ready`

Sem Docker, para desenvolvimento:

```bash
# Backend — precisa de MySQL e Redis acessíveis
cd backend
dotnet ef database update
dotnet run

# Frontend, noutro terminal
cd frontend
npm ci
npm run dev            # http://localhost:5173, proxy para o backend
```

Detalhes de deploy, variáveis, ordem de subida, backup e rotação de credenciais
estão em [`docs/OPERACOES.md`](docs/OPERACOES.md).

---

## Testes

```bash
# Backend — Testcontainers sobe MySQL e Redis de verdade
cd backend && dotnet test

# Frontend
cd frontend
npm run type-check
npm run lint
npm run test:unit
npm run test:e2e
```

Os testes de integração **pulam** (não passam em falso) quando não há Docker
disponível. A verificação real é o CI, que tem daemon.

---

## Estrutura

```
Pharmacy-System
├── backend/                 ASP.NET Core 9 — projeto Storefront.Api
│   ├── Controllers/         HTTP
│   ├── Services/            regras de negócio
│   ├── Data/                AppDbContext
│   ├── Migrations/          dono do schema
│   ├── DTOs/  Models/  Mapping/  Hubs/  Options/  Infrastructure/
│   └── Dockerfile           build, migrations-build, migrator, api
├── frontend/                Vue 3 + Vite
│   └── src/                 views, components, stores, services, i18n, utils
├── tests/                   Storefront.Api.Tests (fora de backend/: o SDK web engloba **/*.cs)
├── database/upgrades/       baseline para bases criadas antes das migrations
├── docs/                    plano, runbook, ADRs, modelo de dados
├── scripts/backup.sh
└── docker-compose.yml       db, redis, migrator, backend, frontend
```

---

## CI/CD

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) em todo push e PR:

- **Backend** — build Release com `TreatWarningsAsErrors`, `dotnet format --verify-no-changes`,
  `dotnet ef migrations has-pending-model-changes` (falha se o modelo divergir das
  migrations) e `dotnet test`
- **Frontend** — `type-check`, `eslint`, `prettier --check`, `vitest`, `build`
- **E2E** — Playwright em chromium
- **Compose** — valida as duas stacks contra `.env.example`

[`docker.yml`](.github/workflows/docker.yml) publica `storefront-backend`,
`storefront-migrator` e `storefront-frontend` no GHCR e roda Trivy.

---

## Estado e roadmap

O plano completo, com os bugs encontrados e o que vem depois, está em
[`docs/PLANO_DE_MELHORIAS.md`](docs/PLANO_DE_MELHORIAS.md).

Limitações conhecidas, explicitamente:

- **Preview de link (Open Graph) é estático.** O crawler do WhatsApp não executa
  JavaScript, então um título vindo de `store_settings` depois do mount nunca é
  lido. Preview por promoção exige renderizar a página no servidor.
- **Uma stack por loja.** Multi-tenant real (`tenant_id` + resolução por
  subdomínio) só se paga com clientes suficientes; ver
  [ADR 0005](docs/adr/0005-white-label-antes-de-multi-tenant.md).
- **`name: pharmacy-system` no compose continua com o nome antigo.** Ele prefixa os
  volumes, então trocá-lo sem migrar os dados sobe a stack vazia. Procedimento em
  [`docs/OPERACOES.md`](docs/OPERACOES.md) §9; o ganho é só cosmético.
- **`ItemPromotion` é produto e promoção ao mesmo tempo.** Separar em `Product` +
  `Offer` é migração de dados, planejada e ainda não feita.
- **Busca usa `LIKE '%termo%'`**, que não usa índice. Correto neste tamanho;
  revisitar com dezenas de milhares de linhas.

---

## Sobre

Desenvolvido por **Eduardo Hipolito Anacleto** — Full-Stack Software Developer,
Auckland, Nova Zelândia.

- 📧 eduardohanacleto@gmail.com
- 💼 <https://linkedin.com/in/eduardohipolitoanacleto>
- 🐙 <https://github.com/EduardoHAnacleto>

Licença: [MIT](LICENSE).

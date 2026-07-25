# Operações

Procedimentos de deploy, migração e backup. Complementa o `README_DEPLOY.md`,
que cobre a instalação inicial do servidor.

---

## 1. Variáveis de ambiente

O `.env` na raiz é obrigatório e **não é versionado**. Copie o `.env.example` e
preencha. As variáveis abaixo não têm valor padrão — sem elas o backend recusa
subir, por desenho:

| Variável | Como gerar |
|---|---|
| `JWT_SIGNING_KEY` | `openssl rand -base64 48`. Mínimo 32 caracteres. Trocar invalida todos os tokens e desloga todos os admins |
| `MYSQL_ROOT_PASSWORD`, `MYSQL_PASSWORD` | senhas fortes distintas |
| `REDIS_PASSWORD` | `openssl rand -base64 24` |
| `ADMIN_SEED_USERNAME`, `ADMIN_SEED_PASSWORD` | credenciais do primeiro admin |
| `CORS_ALLOWED_ORIGINS` | URL pública do frontend, ex. `https://loja.exemplo.com` |

O `ADMIN_SEED_*` é usado **uma única vez**, e só enquanto a tabela `users`
estiver vazia. Depois disso o valor é ignorado — mudar a senha aqui não muda a
senha da conta. Se as duas variáveis ficarem em branco, nenhuma conta é criada:
o sistema prefere ficar sem login utilizável a ter um login padrão conhecido.

### Opcionais

Todas têm default e nenhuma impede a aplicação de subir.

| Variável | Para quê |
|---|---|
| `Store__Name`, `Store__Currency`, `Store__Locale`, `Store__CountryCode`, `Store__TimeZone`, `Store__WhatsAppNumber`, `Store__LogoUrl` | Valores iniciais da linha de `store_settings`, gravados **uma única vez**, na primeira subida com a tabela vazia. Serve para um deploy novo já chegar apresentável; depois disso a fonte de verdade é a tela `/admin/settings` |
| `OpenTelemetry__OtlpEndpoint` | Endereço do coletor OTLP, ex. `http://otel-collector:4317`. Sem ele a instrumentação roda e nada é exportado — não há caminho de código separado para um deploy sem coletor |
| `OpenTelemetry__ServiceName` | Nome do serviço nos traces. Default `storefront-api`; vale distinguir quando houver mais de uma loja no mesmo coletor |
| `RateLimit__LoginPermitLimit`, `RateLimit__LoginWindowMinutes` | Ajuste do limite de tentativas de login por IP |

> O separador `__` (dois sublinhados) é como o .NET mapeia variável de ambiente
> para configuração aninhada: `Store__Name` é `Store:Name`.

### Configuração que **não** é variável de ambiente

Nome da loja, cores, logotipo, endereço, contato, moeda, idioma, taxa de entrega,
cidades atendidas, horário de funcionamento e quais campos pedir no checkout ficam
em `store_settings` e são editados em `/admin/settings`. É proposital: a lojista
precisa poder mudar o próprio telefone sem redeploy.

---

## 2. Subir a aplicação

```bash
# Produção
docker compose up -d --build

# Desenvolvimento (Swagger, páginas de exceção, portas 5000 e 3306 no host)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

A ordem de inicialização é imposta pelo compose e não precisa de intervenção:

```
db (healthy) ──> migrator (roda e termina) ──> backend (healthy) ──> frontend
redis (healthy) ─────────────────────────────┘
```

O `migrator` é um container efêmero que aplica as migrations e sai. O `backend`
só inicia depois que ele termina com sucesso, então a API nunca atende tráfego
contra um schema desatualizado.

### Health checks

| Endpoint | Responde |
|---|---|
| `GET /health` | o processo está de pé. Sem checagem de dependência, para um soluço no banco não virar loop de restart |
| `GET /health/ready` | banco alcançável e Redis alcançável. É o que o healthcheck do compose usa |

Redis fora do ar retorna `Degraded`, não `Unhealthy`: o cache é otimização e a
API responde sem ele. Marcar como `Unhealthy` tiraria o container de rotação por
uma falha não fatal.

---

## 3. Migrations

O schema é propriedade das migrations EF Core em `backend/Migrations/`. Os
antigos `database/schema.sql` e `database/seed.sql` foram removidos: eram
executados pelo entrypoint do MySQL, que **só roda em volume vazio**, e portanto
nunca aplicavam mudança nenhuma depois do primeiro boot.

### Aplicar

```bash
docker compose run --rm migrator
```

Roda automaticamente em todo `docker compose up`. Migrations já aplicadas são
ignoradas.

### Criar uma nova migration

```bash
cd backend
dotnet ef migrations add NomeDaMudanca
```

O CI roda `dotnet ef migrations has-pending-model-changes` e falha se o model
mudou sem a migration correspondente — assim o esquecimento aparece no PR, não
no deploy.

Dados de referência (categorias, primeiro admin) **não** são seed de migration:
ficam no `DatabaseSeeder`, que roda no startup e é idempotente. Schema é
migration; dado é seeder.

### Banco que existia antes das migrations

Um banco criado pelo `schema.sql` antigo já tem `categories` e
`item_promotions`, então o `InitialCreate` falharia ao tentar criá-las. Sequência
para adotar migrations nesse banco, **uma vez só**:

```bash
# 1. tabelas de autenticação (idempotente)
docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
  "$MYSQL_DATABASE" < database/upgrades/001_add_auth_tables.sql

# 2. registrar o InitialCreate como já aplicado
docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
  "$MYSQL_DATABASE" < database/upgrades/002_baseline_migrations.sql

# 3. aplicar o que vem depois dele
docker compose run --rm migrator
```

O script `002` só insere a linha de baseline se as tabelas antigas realmente
existirem — em banco vazio ele não faz nada, e o migrator cria o schema normal.

**Faça backup antes** (seção 5).

---

## 4. Imagens enviadas

Ficam no volume `pharmacy-system_promotion_images`, montado em
`/app/wwwroot/images`. Não estão no git e **não são reconstruíveis**: cada linha
de promoção aponta para um arquivo. Um banco restaurado sem elas volta com o
catálogo de fotos quebrado.

---

## 5. Backup

```bash
scripts/backup.sh                    # grava em ./backups/<timestamp>/
scripts/backup.sh /mnt/backups       # destino alternativo
BACKUP_RETENTION_DAYS=30 scripts/backup.sh
```

Salva dois artefatos: `database.sql.gz` (`mysqldump --single-transaction`, sem
travar as tabelas, então a loja continua no ar) e `promotion_images.tar.gz`.

A rotação só acontece **depois** de verificar que os dois artefatos existem, não
estão vazios e descomprimem — um backup que falhou nunca deve ser o motivo de
apagar backups bons.

Cron diário:

```
15 3 * * * cd /srv/pharmacy-system && scripts/backup.sh >> /var/log/pharmacy-backup.log 2>&1
```

### Restaurar

```bash
gunzip -c backups/<stamp>/database.sql.gz \
  | docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" "$MYSQL_DATABASE"

docker run --rm \
  -v pharmacy-system_promotion_images:/target \
  -v "$PWD/backups/<stamp>":/backup \
  alpine sh -c 'rm -rf /target/* && tar xzf /backup/promotion_images.tar.gz -C /target'
```

---

## 6. CI/CD

| Workflow | Dispara em | Faz |
|---|---|---|
| `ci.yml` | PR e push na `main` | build do backend em Release com warnings-as-errors, `dotnet format`, checagem de migration pendente; type-check, lint, prettier e build do frontend; validação dos dois stacks de compose |
| `docker.yml` | push na `main` e tags `v*` | build e push de `pharmacy-backend`, `pharmacy-migrator` e `pharmacy-frontend` no GHCR, com scan Trivy |

O Trivy roda em modo relatório (`continue-on-error`) e publica no Security tab.
Falhar o publish por CVE de imagem base bloquearia deploy por algo que este
repositório não tem como corrigir.

### Pendente de configuração manual

**Branch protection** não pode ser criada por código — precisa de admin do
repositório. Em *Settings → Branches → Add rule* para `main`:

- Require a pull request before merging
- Require status checks to pass: `Backend`, `Frontend`, `Compose`
- Require branches to be up to date before merging

### `VITE_WHATSAPP_NUMBER` não é mais usado

O Vite embute variáveis `VITE_*` no bundle em tempo de build, então o número do
WhatsApp ficava assado na imagem. **Isso foi resolvido:** o número vem de
`store_settings` em runtime, e a variável não é lida por código nenhum. Ela
continua no `.env.example` apenas para não quebrar deploys existentes que a
definam; pode ser removida.

O mesmo vale para o `logoUrl`: a arte da farmácia está em `frontend/public/logoFarma.png`
e é referenciada pela configuração, não importada pelo bundle.

---

## 7. Rotação de credenciais

As senhas do banco estiveram versionadas no `.env` antes da Fase 0. **Rotacione
em produção:**

```bash
docker compose exec db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
  -e "ALTER USER '${MYSQL_USER}'@'%' IDENTIFIED BY 'nova-senha';"
# atualize MYSQL_PASSWORD no .env, então:
docker compose up -d
```

Trocar `JWT_SIGNING_KEY` invalida todos os access e refresh tokens emitidos —
efeito prático: todos os admins são deslogados. É o procedimento correto se
houver suspeita de vazamento da chave.

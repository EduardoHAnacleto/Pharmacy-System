# Operações

Procedimentos de deploy, migração e backup. Complementa o `README_DEPLOY.md`,
que cobre a instalação inicial do servidor.

---

## 1. Variáveis de ambiente

O `.env` na raiz é obrigatório e **não é versionado**. **Copiar o `.env.example`
não basta** — ele vem com as senhas em branco de propósito, para que nenhum valor
placeholder chegue a um servidor por descuido. Um `.env` copiado e não preenchido
não sobe a stack.

### Procedimento

1. **Copie o exemplo.**

   ```bash
   cp .env.example .env      # Windows: copy .env.example .env
   ```

2. **Preencha as sete obrigatórias.** Nenhuma tem valor padrão:

   | Variável | Como gerar |
   |---|---|
   | `MYSQL_ROOT_PASSWORD` | senha forte, usada só pelo container do MySQL e pelo seu healthcheck |
   | `MYSQL_USER` | nome do usuário da aplicação, ex. `storefront` — não use `root` |
   | `MYSQL_PASSWORD` | senha forte, **distinta** da de root |
   | `REDIS_PASSWORD` | `openssl rand -base64 24` |
   | `JWT_SIGNING_KEY` | `openssl rand -base64 48`. Mínimo 32 caracteres — a API recusa subir com menos. Trocar invalida todos os tokens e desloga todos os admins |
   | `ADMIN_SEED_USERNAME` | usuário do primeiro admin |
   | `ADMIN_SEED_PASSWORD` | senha do primeiro admin |

   `MYSQL_DATABASE` e `CORS_ALLOWED_ORIGINS` também são obrigatórias, mas já
   chegam preenchidas com um valor que funciona localmente. Em produção,
   `CORS_ALLOWED_ORIGINS` precisa da URL pública do frontend, ex.
   `https://loja.exemplo.com` — nenhuma origem fora dessa lista consegue chamar
   a API.

   Sem `$`, `#` ou aspas nos valores: o compose expande `$` como variável e
   trata `#` como início de comentário.

3. **Confira antes de subir.** Isto valida a interpolação sem iniciar nada:

   ```bash
   docker compose config >/dev/null && echo ok
   ```

Faltando qualquer obrigatória, o compose **para na hora** e diz qual é:

```
error while interpolating services.db.environment.MYSQL_ROOT_PASSWORD:
required variable MYSQL_ROOT_PASSWORD is missing a value: set it in .env, see .env.example
```

Isso vem das guardas `:?` no `docker-compose.yml`. Antes delas um valor vazio era
substituído em silêncio e a falha aparecia meio minuto depois, no log do MySQL,
como `Database is uninitialized and password option is not specified` seguido de
`container storefront_db is unhealthy` — mensagem que não nomeia o `.env` nem
sugere o que fazer.

### O par de seed do admin

`ADMIN_SEED_USERNAME` e `ADMIN_SEED_PASSWORD` são usados **uma única vez**, e só
enquanto a tabela `users` estiver vazia. Depois disso são ignorados — mudar a
senha aqui não muda a senha da conta. Se ficarem em branco, nenhuma conta é
criada: o sistema prefere ficar sem login utilizável a ter um login padrão
conhecido.

Por isso os dois **não** têm guarda `:?`, ao contrário das outras obrigatórias:
um redeploy sobre um banco que já tem o admin não precisa deles, e uma guarda
quebraria justamente o deploy que não tem mais nada a semear. Numa instalação
nova, preencha.

### Opcionais

Todas têm default e nenhuma impede a aplicação de subir.

| Variável | Para quê |
|---|---|
| `STORE_NAME`, `STORE_CURRENCY`, `STORE_LOCALE`, `STORE_COUNTRY_CODE`, `STORE_TIME_ZONE`, `STORE_LOGO_URL` | Valores iniciais da linha de `store_settings`, gravados **uma única vez**, na primeira subida com a tabela vazia. Serve para um deploy novo já chegar apresentável; depois disso a fonte de verdade é a tela `/admin/settings` |
| `STORE_WHATSAPP_NUMBER` | Exceção à regra acima: é override **vivo**, reaplicado a cada leitura. Ver §8 |
| `ANALYTICS_RAW_RETENTION_DAYS` | Janela de retenção do `analytics_events` bruto. Default 90 |
| `OTLP_ENDPOINT` | Endereço do coletor OTLP, ex. `http://otel-collector:4317`. Sem ele a instrumentação roda e nada é exportado — não há caminho de código separado para um deploy sem coletor |
| `OTEL_SERVICE_NAME` | Nome do serviço nos traces. Default `storefront-api`; vale distinguir quando houver mais de uma loja no mesmo coletor |

> **Os nomes acima são os do `.env`.** Dentro do container eles chegam com o
> nome que o .NET espera, mapeado pelo `docker-compose.yml`: `STORE_NAME` vira
> `Store__Name`, `OTLP_ENDPOINT` vira `OpenTelemetry__OtlpEndpoint`. O separador
> `__` é como o .NET representa configuração aninhada (`Store__Name` é
> `Store:Name`). Escrever `Store__Name=` no `.env` **não tem efeito** — nada no
> compose lê esse nome.

O limite de tentativas de login (`RateLimit:LoginPermitLimit`,
`RateLimit:LoginWindowMinutes`) fica no `backend/appsettings.json` e **não** é
exposto como variável de ambiente. Ajustar exige editar o arquivo e reconstruir a
imagem do backend.

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

**A rotação é a mitigação escolhida para o `.env` versionado**, em vez de reescrever
o histórico do git. Foi uma decisão consciente
([`PLANO_DE_MELHORIAS.md` §7](PLANO_DE_MELHORIAS.md#7-decisões-pendentes)): as senhas
antigas continuam no histórico, e a rotação as torna inúteis, sem invalidar todo
clone e fork do repositório. Isso torna os passos acima **obrigatórios**, não
recomendados.

---

## 8. Número do WhatsApp

Há dois lugares de onde o número pode vir, e a precedência é fixa:

| `STORE_WHATSAPP_NUMBER` | Quem manda |
|---|---|
| definida | **O ambiente.** Vence sobre a linha do banco em toda leitura. A tela de admin mostra o campo somente-leitura e diz que o ambiente é o dono |
| vazia | **`/admin/settings`.** A lojista muda sozinha, sem tocar em servidor |

Trocar pelo ambiente:

```bash
# .env — só dígitos, com DDI. Sem +, espaços, parênteses ou hífens.
STORE_WHATSAPP_NUMBER=5545999975299

docker compose up -d backend      # reinicia a API; nada é reconstruído
```

É runtime, não build time: nada sobre o número entra no bundle do frontend, então
não há `--build` nem republicação de imagem. O override é aplicado **depois** do
cache, então remover a variável passa a valer na leitura seguinte, sem esperar o TTL.

Um valor que não tenha de 8 a 20 dígitos é **ignorado** com um aviso no log e o
número do banco é usado. É deliberado: um `wa.me` malformado não dá erro visível —
apenas perde todos os pedidos em silêncio.

`VITE_WHATSAPP_NUMBER` não existe mais. Era build-time, ficava assado na imagem, e
nenhum código o lia.

---

## 9. Renomear o projeto do compose (opcional, e perigoso)

`docker-compose.yml` ainda declara `name: pharmacy-system` embora o resto do projeto
seja `Storefront.Api`. Não é descuido: esse nome **prefixa todos os volumes**.

```bash
docker volume ls | grep pharmacy-system
# pharmacy-system_db_data
# pharmacy-system_redis_data
# pharmacy-system_promotion_images
```

Mudar `name:` faz o compose procurar `storefront_db_data`, não encontrar, e criar um
volume vazio — banco sem tabelas, imagens sumidas, e os dados antigos órfãos no
disco. **Só faça isso com os volumes migrados antes:**

```bash
docker compose down                       # sem -v, que apagaria os volumes

for v in db_data redis_data promotion_images; do
  docker volume create "storefront_$v"
  docker run --rm \
    -v "pharmacy-system_$v":/from \
    -v "storefront_$v":/to \
    alpine sh -c 'cd /from && cp -a . /to'
done

# só então edite name: no docker-compose.yml
docker compose up -d
```

Confira que a vitrine lista promoções e que as imagens carregam **antes** de remover
os volumes antigos. Não há ganho funcional nenhum nessa mudança — é cosmética, e o
modo de falha é perda de dados. Deixar como está é uma resposta perfeitamente boa.

# Modelo de dados

Onze tabelas. As migrations em `backend/Migrations/` são o dono do schema; este
documento explica **por que** cada tabela existe e o que a distingue.

```mermaid
erDiagram
    store_settings {
        int id PK "sempre 1"
        varchar store_name
        char country_code
        varchar time_zone
        char currency
        decimal delivery_fee
        text opening_hours "JSON"
    }

    categories {
        int id PK
        varchar name
    }

    media_assets {
        int id PK
        varchar file_path
        char content_hash "SHA-256, único"
        bool is_missing
    }

    item_promotions {
        int id PK
        varchar name
        decimal price
        decimal price_before
        datetime date_start
        datetime date_end
        varchar status
        int category_id FK
        int media_asset_id FK
        int source_promotion_id FK "linhagem"
        int created_by_user_id
    }

    promotion_status_history {
        int id PK
        int promotion_id FK
        varchar from_status
        varchar to_status
        datetime changed_at
    }

    users {
        int id PK
        varchar username "único"
        varchar password_hash "PBKDF2"
        varchar role
    }

    refresh_tokens {
        int id PK
        int user_id FK
        char token_hash "SHA-256, único"
        datetime revoked_at
    }

    orders {
        int id PK
        varchar order_number "único"
        varchar fulfillment_type
        varchar delivery_city
        decimal total
        datetime created_at
    }

    order_items {
        int id PK
        int order_id FK
        int promotion_id FK "SetNull"
        varchar name_snapshot
        decimal unit_price_snapshot
        int quantity
    }

    analytics_events {
        int id PK
        varchar event_type
        int promotion_id
        char session_key "efêmero"
        datetime occurred_at
    }

    analytics_daily {
        int id PK
        date stat_date
        varchar event_type
        int promotion_id
        int event_count
    }

    audit_log {
        int id PK
        int user_id
        varchar action
        varchar entity_type
        int entity_id
        datetime occurred_at
    }

    categories ||--o{ item_promotions : "Restrict"
    media_assets ||--o{ item_promotions : "Restrict"
    item_promotions ||--o{ item_promotions : "source, Restrict"
    item_promotions ||--o{ promotion_status_history : "Cascade"
    users ||--o{ refresh_tokens : "Cascade"
    orders ||--o{ order_items : "Cascade"
    item_promotions ||--o{ order_items : "SetNull"
```

## O que cada tabela resolve

| Tabela | Existe porque |
|---|---|
| `store_settings` | Linha única. Tudo que distinguia uma loja da outra era código; ver [ADR 0005](adr/0005-white-label-antes-de-multi-tenant.md) |
| `categories` | As cinco iniciais são de farmácia; um shop de outro tipo cria as suas pelo admin |
| `media_assets` | Deduplica arquivos por SHA-256 e permite reativar uma promoção sem novo upload |
| `item_promotions` | O item em promoção. `status` substituiu `is_active`; `source_promotion_id` registra a linhagem de reativação |
| `promotion_status_history` | Sem ela, "quando isso saiu do ar e por quê" não tem resposta |
| `users` | Credenciais operacionais do lojista — não são dados de cliente |
| `refresh_tokens` | Guarda só o digest; rotação a cada uso ([ADR 0003](adr/0003-jwt-com-refresh-token-em-cookie.md)) |
| `orders` / `order_items` | Relatório de vendas sem nenhum dado pessoal ([ADR 0002](adr/0002-nenhum-dado-pessoal-no-servidor.md)) |
| `analytics_events` | Funil de conversão. Bruto, retenção curta |
| `analytics_daily` | Agregado diário; os eventos brutos são apagados depois |
| `audit_log` | Quem fez o quê. Só confiável depois da autenticação real |

## Decisões que não são óbvias no schema

**Nenhuma coluna de dado pessoal, em nenhuma tabela.** Não é convenção: é o que
`NoPersonalDataTests` verifica contra o modelo EF mapeado. Adicionar `phone` a
`orders` quebra o build.

**`order_items` guarda snapshots.** `name_snapshot` e `unit_price_snapshot`
duplicam o que estava na promoção no momento da venda. Deliberado: um relatório
histórico tem de mostrar o preço cobrado, não o preço atual — e a FK é `SetNull`,
então a linha continua legível se a promoção desaparecer.

**Comportamento de FK escolhido caso a caso.** `Restrict` onde apagar destruiria
histórico (categoria, media asset, promoção de origem). `Cascade` onde o filho não
existe sem o pai (histórico de status, itens de pedido, refresh tokens). `SetNull`
onde o filho se sustenta sozinho (item de pedido → promoção).

**Defaults de banco não são declarados.** O EF sempre escreve todas as colunas, e
`HasDefaultValue(true)` num bool faria salvar `false` inserir `TRUE`, porque `false`
também é o default do CLR.

**`opening_hours` é `text` com JSON.** É lido inteiro e nunca filtrado; o tipo `json`
do MySQL só adicionaria validação que a API já faz.

**`ix_item_promotions_window`** em `(status, date_start, date_end)` cobre a query
quente da vitrine, que roda em toda página da grade.

**`uq_analytics_daily`** em `(stat_date, event_type, promotion_id)` permite o rollup
fazer upsert idempotente e ser reexecutado sem duplicar.

## Limitação conhecida

`item_promotions` é produto **e** promoção ao mesmo tempo. Consequências: não há
catálogo permanente (só aparece o que está em promoção) e o item é recadastrado a
cada campanha — mitigado por reativar/duplicar, não resolvido. Separar em `Product`
+ `Offer` é migração de dados, planejada em `PLANO_DE_MELHORIAS.md` §6.2.

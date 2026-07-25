# 0004 — EF Migrations como dono do schema, aplicadas por um serviço separado

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

O schema vinha de `database/schema.sql`, montado pelo entrypoint do container MySQL.
Dois problemas sérios:

1. **O entrypoint só roda em volume vazio.** Depois do primeiro `up`, nenhuma
   alteração de schema é aplicada. Não havia caminho de evolução: mudar uma coluna
   significava editar o banco à mão em produção.
2. **O compose montava `./Database/schema.sql`, mas o diretório em disco é
   `database/`.** No Linux — o alvo recomendado no próprio guia de deploy — o Docker
   criava um diretório vazio e a API subia contra um banco sem tabelas.

Além disso, `AppDbContext` não declarava tipos de coluna, então um banco criado pelo
modelo divergia do criado pelo `.sql`.

## Decisão

**EF Core Migrations é o dono do schema.** `database/schema.sql` sai do caminho de
execução; `database/upgrades/` guarda o baseline para bases criadas antes disso.

O `AppDbContext` declara explicitamente comprimentos de `varchar`, `decimal(10,2)`
para dinheiro, `char(n)` de tamanho fixo para digests, e o comportamento de cada FK
(`Restrict` onde apagar destruiria histórico, `SetNull` onde a linha filha se
sustenta sozinha).

**As migrations são aplicadas por um serviço `migrator` separado no compose**, um
bundle self-contained gerado por `dotnet ef migrations bundle`. Ele roda até o fim
antes de o backend subir (`condition: service_completed_successfully`).

**O CI falha se o modelo divergir das migrations**, via
`dotnet ef migrations has-pending-model-changes`.

Defaults de banco (`is_active DEFAULT TRUE`) foram deliberadamente **não**
declarados: o EF sempre escreve as duas colunas, e `HasDefaultValue(true)` num bool
faria salvar `false` inserir `TRUE`, porque `false` também é o default do CLR.

## Consequências

**A favor**

- Existe caminho de evolução, versionado, revisável em PR.
- Um banco criado pelas migrations é idêntico ao criado pelo modelo — garantido pelo
  CI, não pela lembrança de quem revisa.
- DDL não fica acoplado ao boot da API: uma migration lenta não deixa a API num
  estado meio-de-pé, e várias réplicas não correm para migrar ao mesmo tempo.
- O bundle não precisa do SDK .NET na máquina de produção.

**Contra, e assumido**

- Uma etapa a mais no deploy. O compose expressa a dependência, mas quem sobe à mão
  precisa saber da ordem — está no runbook.
- Bases pré-existentes precisam do baseline de `database/upgrades/` uma vez.
- Migration com transformação de dados exige revisão à mão. Aconteceu:
  `AddMediaAssetsAndPromotionLifecycle` foi gerada com `DropColumn is_active` antes
  do `AddColumn status`, o que apagaria a informação antes de derivar o novo valor.
  Foi reordenada para adicionar, popular e só então remover, com o `Down()`
  simétrico.

## Alternativas descartadas

- **`Database.Migrate()` no startup da API.** Um comando menos no deploy, mas
  acopla DDL ao boot e dá corrida entre réplicas.
- **Ferramenta externa (Flyway, DbUp).** Funcionaria, e adiciona uma tecnologia e
  um dialeto SQL a manter em paralelo ao modelo que já descreve o schema.

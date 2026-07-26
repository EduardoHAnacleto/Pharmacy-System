# 0008 — Testcontainers com skip explícito em vez de provedor em memória

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

O projeto tinha zero testes. Vitest e Playwright estavam configurados e sem um único
teste escrito; o único arquivo existente era o scaffold `e2e/vue.spec.ts`, que
esperava `<h1>You did it!</h1>` e falharia se rodasse.

Ao escrever os testes de integração do backend, a escolha do banco importa. As
alternativas usuais:

1. **`Microsoft.EntityFrameworkCore.InMemory`** — rápido, sem dependência externa.
2. **SQLite in-memory** — relacional de verdade, ainda leve.
3. **Testcontainers** — MySQL e Redis reais em containers descartáveis.

O que estes testes precisam pegar é justamente o que depende do provedor: tipos de
coluna, o índice composto da query quente, o índice único de `content_hash`,
`ON DELETE RESTRICT`, o comportamento do `INCR` de versão de cache. O provedor em
memória não tem opinião sobre nenhuma dessas coisas — ele passaria com um schema que
o MySQL rejeitaria.

Complicação: o ambiente de desenvolvimento deste trabalho não tem daemon Docker.

## Decisão

**Testcontainers**, com MySQL 8 e Redis 7 reais, dirigidos por
`WebApplicationFactory<Program>` — a API de verdade, com sua configuração de verdade.

Sem Docker, os testes **pulam explicitamente**, via `Xunit.SkippableFact`: o
`ApiFixture` tenta subir os containers, marca `DockerAvailable = false` se falhar, e
cada teste começa com `Skip.IfNot(_fixture.DockerAvailable, ...)`.

Os containers são criados dentro de `InitializeAsync`, não em inicializadores de
campo: o Testcontainers valida o endpoint do Docker ao montar a configuração, e fazer
isso cedo lança durante a construção do fixture, que o xUnit reporta como falha em
todo teste da coleção em vez de skip.

O CI tem daemon, então lá eles rodam. **O CI é o caminho de verificação real**, não a
máquina local.

## Consequências

**A favor**

- Os testes exercem o comportamento que vai para produção, incluindo tudo que é
  específico do MySQL e do Redis.
- Sem Docker o resultado é honesto: "52 skipped" e não "52 passed". Um teste que passa
  sem ter rodado é pior que teste nenhum, porque produz confiança falsa.
- O CI já pegou quatro defeitos meus que não apareciam localmente — entre eles três
  testes que ainda chamavam o endpoint DELETE removido, e um seeder que rodava antes
  das migrations.

**Contra, e assumido**

- A suíte leva ~35 s no CI, contra ~2 s de um provedor em memória.
- Requer daemon Docker para a verificação valer. Localmente, sem ele, o feedback vem
  só dos testes unitários — que são 74 e cobrem hashing, validação de imagem,
  timezone, mapeamento, visibilidade de promoção, normalização de filtro e a asserção
  de ausência de PII.
- Os testes de integração compartilham uma instância de banco por coleção, então
  precisam ser tolerantes ao estado deixado por outros. Onde isso importa, usam
  marcadores únicos (`Guid`) em vez de assumir uma tabela vazia.

## Alternativas descartadas

- **Provedor em memória.** Passaria com schemas que o MySQL rejeita. Para uma suíte
  cujo propósito é justamente validar o schema, é o antipadrão exato.
- **SQLite.** Melhor que em memória, e ainda com sintaxe, tipos e semântica de índice
  diferentes do MySQL. Não pegaria os `char(64)`, o `decimal(10,2)`, nem o Redis.
- **Passar em vez de pular quando não há Docker.** Descartada de imediato: seria uma
  suíte que mente.

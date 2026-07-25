# 0006 — Cache com chave versionada em vez de varredura de keyspace

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

A invalidação de cache era `RedisService.InvalidateByPrefixAsync`, que chamava
`server.Keys(pattern: "prefixo*")`. No Redis isso é `KEYS` (ou `SCAN` iterado sobre
todo o keyspace) e custa O(número de chaves no servidor) — bloqueando o servidor
enquanto roda. Era executado em toda criação e remoção de promoção.

Havia também um bug de correção independente: `GetActivePaged` filtrava a janela da
promoção pelo timezone recebido como parâmetro, mas a chave de cache **não incluía o
timezone**. O primeiro visitante definia o resultado de todos por cinco minutos.

E a cerimônia get → miss → query → set estava repetida nas seis actions de leitura
do controller.

## Decisão

**Chave versionada.** Cada escopo de cache tem um contador em
`<escopo>:version`. Toda chave inclui o valor atual: `item-promotions:v7:active:...`.
Invalidar é `INCR` nesse contador — O(1) — e as entradas antigas ficam inalcançáveis
e expiram pelo próprio TTL.

**`GetOrSetAsync<T>(scope, key, ttl, factory)`** concentra o padrão de leitura num
lugar; as actions passaram a descrever apenas o que consultam.

**A chave carrega tudo que muda o resultado:** timezone, página, tamanho de página
e, agora, o filtro inteiro (busca, categoria, faixa de preço, ordenação), via
`PromotionFilterDto.CacheKey()`. Decimais formatados com `InvariantCulture`, para o
mesmo filtro não gerar duas chaves em hosts de cultura diferente.

**Falha de cache não derruba requisição.** Erro de leitura é logado e a factory
responde direto; erro de escrita é logado e ignorado. Um `INCR` que falha significa
leitura velha até o TTL — melhor que falhar a escrita que o usuário já confirmou.

## Consequências

**A favor**

- Invalidação em tempo constante, independente do tamanho do keyspace.
- Nada de `KEYS` em produção.
- O bug de timezone deixa de ser possível por construção: a chave é montada a partir
  de tudo que a query usa.
- Uma queda do Redis degrada latência, não disponibilidade.
- Um teste de integração confirma que criar uma promoção aparece imediatamente na
  listagem, e outro que cada filtro tem entrada própria.

**Contra, e assumido**

- Entradas obsoletas ocupam memória até expirarem. Com TTL de cinco minutos, é
  desprezível.
- Toda leitura faz um `GET` extra do contador de versão. Uma ida a mais ao Redis,
  contra uma varredura de keyspace.
- Uma chave incompleta volta a ser possível se alguém acrescentar um parâmetro de
  query e esquecer de incluí-lo. `CacheKey()` centraliza isso, e há um teste que
  afirma que filtros diferentes produzem chaves diferentes.

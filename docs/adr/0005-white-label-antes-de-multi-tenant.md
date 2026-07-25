# 0005 — White-label single-tenant antes de multi-tenant

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

O objetivo de produto é vender a vitrine para qualquer tipo de loja, não só para
farmácias. Havia dois caminhos.

**Multi-tenant:** coluna `tenant_id` em todas as tabelas, resolução de tenant por
subdomínio (`loja1.dominio.com`), global query filter no EF, uma instalação servindo
todos os clientes.

**White-label single-tenant:** uma stack Docker por cliente, com tudo o que
distingue uma loja da outra vindo de configuração.

O que impedia vender para um segundo cliente não era a arquitetura de dados — era
que o código continha a farmácia. Nome e telefone no navbar e no rodapé,
`mailto:EMAIL@MAIL.com`, um mapa embutido apontando para a **Torre de Pisa**, dois
números de WhatsApp diferentes hardcoded em dois componentes, `deliveryFee: 8`,
`minDeliveryTotal: 30`, `allowedCity: 'Santa Terezinha de Itaipu'`, feriados fixos
em `/BR`, preços como `R$ {{ price.toFixed(2) }}`, datas em `toLocaleDateString('pt-BR')`
em quatro arquivos, CPF e CEP como campos obrigatórios.

Nenhum desses problemas é resolvido por `tenant_id`.

## Decisão

White-label primeiro. Multi-tenant quando houver clientes suficientes para o custo
por stack incomodar.

Concretamente: uma tabela `store_settings` de linha única, `GET /api/v1/store-settings`
público e cacheado, `PUT` restrito a Admin e auditado, e uma tela de configurações no
admin. As cores saem como CSS custom properties no `:root`; `vue-i18n` cobre pt-BR e
en-NZ; `Intl.NumberFormat` e `Intl.DateTimeFormat` seguem o locale da loja; CPF e CEP
passam a ser opcionais controlados por configuração; feriados usam o país da loja; o
mapa é derivado do endereço.

## Consequências

**A favor**

- Um segundo cliente entra sem uma linha de código: outra linha em `store_settings`.
- O teste do white-label é objetivo — subir uma segunda stack com outro nome, logo,
  cor, moeda, país e WhatsApp e confirmar que nenhum código mudou.
- Isolamento total entre clientes: banco, imagens e logs separados. O modo de falha
  mais grave de um multi-tenant — vazar dado de um cliente para outro por um filtro
  esquecido — é impossível aqui.
- O modelo de dados não carrega complexidade por tenants que ainda não existem.

**Contra, e assumido**

- Custo de infraestrutura cresce linearmente. Um MySQL e um Redis por loja.
- Atualizar N clientes é N deploys. Aceitável em N pequeno, doloroso em N grande.
- Migrar para multi-tenant depois é mais trabalho do que ter começado assim: exige
  `tenant_id` em todas as tabelas e uma migração de dados por cliente.

O último ponto é o trade-off real. É aceito conscientemente: a alternativa é pagar
hoje a complexidade de multi-tenancy por clientes hipotéticos, e o histórico desse
tipo de aposta é ruim.

## Alternativas descartadas

- **Multi-tenant desde já.** Global query filter esquecido num único lugar vaza
  dado entre clientes. Não é complexidade que se adota antes da demanda.
- **Configuração por variável de ambiente em vez de banco.** Mais simples, e obriga
  redeploy para trocar um telefone. A lojista precisa poder mudar isso sozinha.

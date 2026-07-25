# Frontend — Vue 3 + TypeScript + Vite

SPA da vitrine e da administração. O backend fica em [`../backend`](../backend); a
visão geral do projeto está no [README da raiz](../README.md).

## Rodando

```bash
npm ci
npm run dev            # http://localhost:5173
```

O dev server faz proxy de `/api`, `/images` e `/promotionsHub` para o backend em
`localhost:5000`, então **é preciso ter o backend rodando** — inclusive para a tela
inicial, que pede `/api/v1/store-settings` antes de qualquer coisa.

Com a stack Docker de pé, a vitrine servida pelo nginx está em `http://localhost`.

## Scripts

| Comando              | O que faz                                   |
| -------------------- | ------------------------------------------- |
| `npm run dev`        | Dev server com HMR                          |
| `npm run build`      | `type-check` + build de produção em `dist/` |
| `npm run type-check` | `vue-tsc`, sem emitir                       |
| `npm run lint`       | ESLint com `--fix`                          |
| `npm run format`     | Prettier em `src/`                          |
| `npm run test:unit`  | Vitest                                      |
| `npm run test:e2e`   | Playwright                                  |

O CI roda `type-check`, `lint`, `prettier --check`, `test:unit` e `build`. Há
`lint-staged` no commit, mas ele não substitui rodar isso antes do PR.

## Como o código está organizado

```
src/
├── views/          uma por rota
├── components/     reutilizáveis
├── layouts/        MainLayout: navbar, router-view, footer, botões flutuantes
├── stores/         Pinia: settings, cart, checkout, promotions, auth
├── services/       clientes HTTP e SignalR
├── composables/    useJsonLd
├── hooks/          useInfinitePromotions
├── i18n/           locales/pt-BR.ts e locales/en-NZ.ts
├── types/          contratos espelhando os DTOs da API
└── utils/          format.ts — moeda, data, percentual
```

## Três coisas que você precisa saber antes de editar

### 1. Nada de constante de negócio no código

Nome da loja, cores, contato, moeda, taxa de entrega, cidades atendidas, horário,
país dos feriados e quais campos pedir no checkout vêm de `useSettingsStore()`, que
carrega `/api/v1/store-settings` no bootstrap.

Foi exatamente o oposto disso que impedia vender o produto para uma segunda loja: o
código carregava um mapa da Torre de Pisa, `EMAIL@MAIL.com`, dois números de WhatsApp
diferentes e uma taxa de R$8. Se você está a ponto de escrever um valor de negócio
literal, ele pertence a `store_settings`.

As cores são publicadas como `--brand-primary` e `--brand-secondary` no `:root`, e
`assets/main.css` mapeia as classes do Bootstrap para elas. Nenhum componente precisa
saber que uma cor é configurável.

### 2. Formatação passa por `@/utils/format`

```ts
import { formatDate, formatMoney } from '@/utils/format'

formatMoney(19.9) // R$ 19,90  ou  $19.90 — conforme a loja
formatDate(iso)
```

Nunca `` `R$ ${price.toFixed(2)}` `` nem `toLocaleDateString('pt-BR')`. Os
formatadores `Intl` são memoizados porque a grade formata um preço por cartão a cada
render.

### 3. Toda string visível é uma chave de i18n

```vue
<button>{{ t('product.add') }}</button>
```

Adicionar uma string significa adicionar a chave em **`pt-BR.ts` e `en-NZ.ts`**. Só
num arquivo cai em fallback silencioso, que é pior que faltar.

O locale ativo vem de `store_settings` depois do bootstrap; o primeiro paint usa
pt-BR.

## O que nunca é enviado à API

Nome, telefone, CPF, CEP e endereço do checkout ficam neste navegador e na mensagem
de WhatsApp. `POST /orders` envia itens, valores, tipo de entrega e cidade — nada
mais, e não há campo para nada mais.

Não é convenção que se possa relaxar: é o que
[ADR 0002](../docs/adr/0002-nenhum-dado-pessoal-no-servidor.md) decide, o que a
página `/privacy` promete ao visitante, e o que um teste do backend verifica contra o
schema.

## Analytics

`services/analytics.ts` enfileira eventos anônimos e os envia em lote (10 eventos ou
5 s), com `navigator.sendBeacon` no `pagehide` — o evento mais interessante, o clique
no WhatsApp, acontece justamente quando a página está saindo.

A chave de correlação é aleatória e vive em `sessionStorage`: morre com a aba, não é
cookie e não deriva de nada do visitante.

## Editor

VS Code com a extensão [Vue (Official)](https://marketplace.visualstudio.com/items?itemName=Vue.volar);
desative o Vetur. `vue-tsc` faz a checagem de tipos — `tsc` sozinho não entende
`.vue`.

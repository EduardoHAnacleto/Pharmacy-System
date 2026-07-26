# Contribuindo

## Antes de qualquer coisa: duas restrições que não se negociam

Este projeto roda numa loja real e faz uma promessa aos clientes dela. Duas regras
valem para todo PR:

1. **Nada de pagamento on-line.** Nem gateway, nem token de cartão, nem redirect de
   checkout.
2. **Nenhum dado pessoal de cliente chega ao servidor.** Nome, telefone, CPF, CEP e
   endereço ficam no navegador do visitante e na mensagem de WhatsApp. Não existe
   campo para eles em DTO nenhum e não existe coluna para eles em tabela nenhuma.

A segunda é verificada por `NoPersonalDataTests`, que roda contra o modelo EF
mapeado. Adicionar uma coluna dessas quebra o build — de propósito. Se você acha que
precisa de uma, abra uma issue antes de escrever código: o raciocínio está na
[ADR 0002](docs/adr/0002-nenhum-dado-pessoal-no-servidor.md).

## Ambiente

```bash
cp .env.example .env      # preencha; JWT_SIGNING_KEY precisa de 32+ caracteres
docker compose up --build -d
```

Ou, sem Docker:

```bash
cd backend  && dotnet ef database update && dotnet run
cd frontend && npm ci && npm run dev
```

## Antes de abrir o PR

O CI roda exatamente isto. Rodar antes economiza um ciclo:

```bash
# Backend
cd backend
dotnet build --configuration Release                  # TreatWarningsAsErrors: warning = erro
dotnet format Storefront.Api.csproj --verify-no-changes
dotnet ef migrations has-pending-model-changes        # modelo tem de casar com as migrations
dotnet test

# Frontend
cd frontend
npm run type-check
npm run lint
npx prettier --check src/
npm run test:unit
npm run build
```

Sem daemon Docker os testes de integração **pulam** em vez de passar. É intencional
([ADR 0008](docs/adr/0008-testcontainers-com-skip.md)) — e significa que, nesse caso,
o CI é a única verificação real.

## Convenções

**Idioma.** Identificadores e comentários em **inglês**; colunas em `snake_case`
inglês. **Todo texto visível ao usuário e toda mensagem de erro da API em
português** — no frontend, via `src/i18n/locales/`, nunca literal no template.

Adicionar uma string de interface significa adicionar a chave em **`pt-BR.ts` e
`en-NZ.ts`**. Uma chave só num arquivo cai em fallback silencioso.

**Comentários** explicam *por quê*, não *o quê*. O código já diz o que faz. Um
comentário que descreve a linha seguinte é ruído; um que explica por que a
alternativa óbvia não serve é o que vale ser lido depois.

Blocos de seção em ALL-CAPS seguem o padrão do repositório:

```csharp
// ===============================
// CREATE
// ===============================
```

**Backend.** Controller cuida de HTTP; regra de negócio vai em `Services/`. Todo DTO
de escrita tem DataAnnotations com limites que **casam com as colunas**, para input
grande virar 400 e não 500. Mudança de schema é migration; o CI verifica.

**Frontend.** Nada de constante de negócio no código — vem de `store_settings`.
Preço via `formatMoney`, data via `formatDate` de `@/utils/format`; nunca
`toFixed(2)` com símbolo de moeda literal nem `toLocaleDateString('pt-BR')`.

**Testes.** Todo bug corrigido ganha um teste que falharia antes. Todo endpoint novo
ganha um teste de autorização.

## Commits e PRs

Commits no imperativo, com o escopo à frente quando ajudar:
`fix(cache): include the timezone in the active listing key`.

O PR descreve **o que mudou e por quê**, e diz explicitamente o que foi verificado e
o que não foi.

## Segurança

Não abra issue pública para vulnerabilidade. Escreva para
eduardohanacleto@gmail.com.

Nunca comite `.env`, credencial ou chave. `.gitignore` cobre isso, com exceção para
`.env.example` — que só tem nomes de variável, nunca valores.

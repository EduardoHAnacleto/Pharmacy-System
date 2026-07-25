# 0002 — Nenhum dado pessoal do cliente no servidor

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

O checkout coleta nome, telefone, CPF, CEP e endereço — é o que a mensagem de
WhatsApp precisa conter para a entrega acontecer. O reflexo natural seria persistir
isso: histórico por cliente, endereço salvo para a próxima compra, campanha por
telefone.

O dono do produto foi explícito: não guardar dados sensíveis de clientes. E há um
argumento técnico independente na mesma direção. O que se guarda, se vaza. Uma tabela
de clientes com CPF e endereço num sistema operado por uma loja pequena, sem equipe
de segurança, é um passivo LGPD desproporcional ao ganho.

Ao mesmo tempo, sem *nenhum* dado não existe relatório de vendas, e um dos objetivos
do produto é justamente dar inteligência operacional à loja.

## Decisão

Dois grupos, separados na origem.

**Nunca chega ao servidor** — nome, telefone, CPF, CEP, endereço. Fica no
`localStorage` do navegador do visitante e na mensagem de WhatsApp que ele envia.

**Persistido, anônimo** — itens, quantidades, preços, total, tipo de entrega,
cidade de entrega, timestamp. Mais eventos de uso (`promotion_view`,
`add_to_cart`, `checkout_started`, `whatsapp_click`) correlacionados por uma chave
aleatória guardada em `sessionStorage`, que morre com a aba.

Sem IP, sem user-agent, sem cookie de rastreamento, sem identificador persistente.
Os eventos brutos são agregados em `analytics_daily` e apagados após a retenção.

A cidade é a única granularidade geográfica guardada: um relatório por cidade é
útil e uma cidade não identifica ninguém.

## Como isso é garantido, e não apenas prometido

- `CreateOrderDto` não tem campo para dado pessoal. O que não existe no contrato
  não pode ser enviado por engano.
- As tabelas não têm coluna para isso.
- `NoPersonalDataTests` percorre o **modelo EF mapeado** procurando fragmentos
  proibidos (`name`, `phone`, `cpf`, `email`, `address`, `zip`, `ip`, `user_agent`)
  nas entidades de pedido e analytics, verifica o schema exato de
  `analytics_events`, e afirma que `CreateOrderDto` não tem propriedade de PII.
  Adicionar uma coluna dessas quebra o build.

Testar contra o modelo mapeado, e não contra o texto do código, é o ponto: uma
coluna adicionada por atributo, por Fluent API ou por convenção é pega igual.

## Consequências

**A favor**

- Não há o que vazar. A resposta a "o que acontece se o banco for comprometido" é
  "os pedidos ficam expostos; as pessoas, não".
- Requisição de titular sob LGPD é trivial: não há dado dele.
- A promessa da página `/privacy` é verificável por um teste.

**Contra, e assumido**

- Sem histórico por cliente, sem "comprar de novo", sem endereço salvo entre
  dispositivos, sem campanha por telefone.
- O visitante que limpa o navegador redigita tudo.
- Reconciliar um pedido com a conversa depende do número curto na mensagem.

## Alternativas descartadas

- **Guardar com criptografia em repouso.** A chave vive ao lado do dado na mesma
  stack; contra o cenário realista (acesso ao servidor) protege pouco, e o passivo
  legal continua existindo porque o dado continua sendo tratado.
- **Guardar apenas telefone.** Telefone é identificador direto. Uma exceção
  "pequena" é o começo do fim do invariante — e o teste deixaria de poder afirmar
  qualquer coisa.

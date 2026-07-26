# 0001 — Handoff por WhatsApp em vez de pagamento on-line

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

A loja precisa receber pedidos. O caminho convencional é integrar um gateway de
pagamento: checkout no site, cobrança no cartão ou Pix, confirmação automática.

Três coisas pesaram contra isso neste projeto:

1. **Restrição explícita do dono do produto:** não incluir pagamento on-line.
2. **O canal que a loja já usa é o WhatsApp.** A dona da farmácia atende pedidos
   por lá todo dia. Um fluxo que termina numa conversa que ela já sabe conduzir
   tem adoção garantida; um que exige aprender a conciliar transações não tem.
3. **Custo de conformidade.** Guardar ou trafegar dado de cartão traz PCI-DSS.
   Mesmo delegando ao gateway, o fluxo passa a exigir estorno, contestação,
   antifraude e reconciliação — cada um deles um subsistema.

## Decisão

O checkout monta a mensagem do pedido e abre `wa.me/<número>` com ela pronta. O
pagamento acontece fora do sistema: Pix ou na entrega, combinado na conversa.

Antes de abrir o WhatsApp, o pedido é registrado **anonimamente** (itens,
quantidades, valores, tipo de entrega, cidade) e recebe um número curto que
aparece na mensagem, para a lojista casar a conversa com o registro.

## Consequências

**A favor**

- Zero superfície de PCI-DSS e nenhum dado de cartão em lugar nenhum.
- Fluxo que a lojista já domina; a mudança para ela é quase nula.
- Existe relatório de vendas de verdade, apesar de o pagamento ser externo.

**Contra, e assumido**

- Não há confirmação automática de pagamento. Um pedido registrado pode não se
  concretizar, então o relatório mede intenção de compra, não caixa. Está dito na
  tela de Insights.
- A lojista digita a conversa. Não escala para centenas de pedidos por dia — e
  neste volume não precisa escalar.
- O visitante sai do site. É onde o funil mais perde, e é exatamente por isso que
  `whatsapp_click` é um evento medido.

## Alternativas descartadas

- **Gateway (Stripe, Mercado Pago).** Descartada pela restrição de escopo e pelo
  custo de conformidade e operação desproporcional ao volume.
- **Pedido por e-mail.** Preservaria o visitante no site, mas nenhuma das duas
  pontas usa e-mail para isso; a taxa de resposta seria pior.

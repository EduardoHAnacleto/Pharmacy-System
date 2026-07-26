# 0007 — Arquivar promoções em vez de apagar

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

`DELETE /item-promotions/{id}` removia a linha do banco **e** apagava o arquivo de
imagem do disco. Consequências:

- Uma promoção encerrada não podia mais ser repetida. Repetir significava recadastrar
  o item e subir a arte de novo.
- Corrigir um preço errado exigia apagar e recriar, perdendo a imagem.
- Não havia histórico: nenhum relatório podia comparar duas edições da mesma
  campanha, porque a primeira deixava de existir.
- O `File.Delete` usava `Path.Combine(WebRootPath, promotion.ImagePath)` sem verificar
  contenção — um `ImagePath` manipulado apagava arquivo fora do diretório pretendido.

O contexto que agrava tudo isso: até a Fase 0 os uploads iam para dentro do container
sem volume, então a imagem morria a cada deploy de qualquer forma.

## Decisão

**Não existe delete.** A rota foi removida, e um teste afirma que ela responde 405.

No lugar:

- **`status`** substituiu `is_active`: `Draft`, `Scheduled`, `Active`, `Expired`,
  `Archived`. A vitrine mostra o que não é `Draft` nem `Archived` e está dentro da
  janela de datas.
- **`PATCH {id}/archive`** aposenta a promoção mantendo linha e imagem.
- **`POST {id}/reactivate`** cria uma promoção nova com a mesma arte e uma janela
  nova, gravando `SourcePromotionId` — a linhagem fica registrada. `POST {id}/duplicate`
  é a mesma operação, sob o nome que o operador procura ao copiar algo que ainda está
  no ar.
- **`PUT {id}`** edita a promoção sem destruí-la.
- **`media_assets`** guarda cada arquivo uma vez, deduplicado por SHA-256, e a FK é
  `Restrict`: um arquivo referenciado por qualquer promoção não pode ser removido
  debaixo dela.
- **`promotion_status_history`** registra cada transição, com motivo.
- Volume nomeado para as imagens, e `PromotionImageStorage.ResolveContained` verifica
  contenção de caminho antes de qualquer operação em arquivo.

## Consequências

**A favor**

- Repetir uma campanha é um clique com uma janela nova, sem upload.
- O histórico permite comparar edições da mesma promoção — que é o que dá sentido ao
  relatório de desempenho.
- Nenhuma ação da interface destrói dado.
- A classe de vulnerabilidade de path traversal no delete deixou de existir junto com
  o delete.

**Contra, e assumido**

- A tabela só cresce. Com o volume de uma loja, irrelevante por muitos anos.
- Imagens órfãs acumulam se uma promoção arquivada nunca for reativada. Uma limpeza
  consciente pode ser adicionada; apagar automático seria repetir o erro original.
- Um operador que realmente queira remover algo precisa de acesso ao banco. É
  proposital: o caminho fácil não deve ser o destrutivo.
- Promoções apontando para imagens já perdidas antes do volume existem no banco.
  `MediaBackfillService` as marca com `IsMissing`, o admin mostra a contagem, e a
  correção é reativar com uma arte nova.

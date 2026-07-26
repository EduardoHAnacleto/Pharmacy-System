# 0003 — JWT em memória + refresh token em cookie HttpOnly

**Estado:** Aceita
**Data:** 2026-07-25

## Contexto

Antes da Fase 1 não havia autenticação nenhuma. O "login" comparava a senha com uma
constante `'1234'` no bundle JavaScript e gravava
`localStorage.setItem('admin_authenticated', 'true')`. Os endpoints de escrita da API
não tinham `[Authorize]`: qualquer pessoa que descobrisse a URL criava e apagava
promoções e fazia upload de arquivo.

Ao substituir isso, a pergunta que sobra é onde o token do navegador fica.

## Decisão

- **Access token JWT de vida curta**, guardado **só em memória** (num store Pinia).
  Nunca em `localStorage` nem em `sessionStorage`.
- **Refresh token** entregue num cookie `HttpOnly`, `Secure`, `SameSite=Strict`,
  com `Path` restrito a `/api/v1/auth`.
- **Rotação a cada uso:** o refresh emite um novo par e invalida o anterior. Só o
  digest SHA-256 do refresh token fica no banco, nunca o valor.
- Numa recarga o token em memória se perde, então o cliente chama `/auth/refresh`
  uma vez para se restabelecer.
- Senhas com **PBKDF2-HMAC-SHA256, 600 000 iterações**, em formato
  auto-descritivo (`pbkdf2-sha256$iterações$salt$hash`) para poder aumentar o custo
  depois sem invalidar hashes existentes.
- **Rate limit no login** por IP, no servidor.

## Consequências

**A favor**

- XSS não consegue ler o access token (não está em storage acessível) nem o refresh
  token (`HttpOnly`).
- `SameSite=Strict` mais `Path` restrito reduz a superfície de CSRF: o cookie só
  acompanha requisições de mesma origem e só para as rotas de auth.
- Roubo de refresh token tem janela curta, porque a rotação invalida o anterior no
  primeiro uso legítimo.
- O limite de tentativas passou a ser real. O lockout anterior morava em
  `localStorage`, onde qualquer visitante o apagava.
- Vazar o banco não vaza tokens utilizáveis: só digests.

**Contra, e assumido**

- Toda recarga custa uma chamada de refresh antes de a tela de admin renderizar.
- Fluxo mais complexo: interceptor no axios que tenta o refresh uma vez em 401,
  com uma promessa compartilhada para não disparar N refreshes concorrentes.
- Precisa de HTTPS para valer (`Secure`), o que já era requisito de produção.

## Alternativas descartadas

- **JWT em `localStorage`.** Simples e comum, e legível por qualquer XSS. O ganho
  de simplicidade não paga o risco.
- **Sessão em servidor com cookie de sessão.** Defensável, e mais simples em vários
  aspectos. Descartada porque o SignalR e o cliente já falavam bearer, e porque a
  API não guarda estado de sessão em mais nada.
- **BCrypt em vez de PBKDF2.** Boa opção. PBKDF2 venceu por vir na biblioteca
  padrão — uma dependência menos numa aplicação que precisa se manter fácil de
  atualizar.

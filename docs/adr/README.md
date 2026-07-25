# Architecture Decision Records

Uma ADR registra **uma** decisão: o que estava em jogo, o que foi escolhido, o que
se perdeu com a escolha. Serve para que a próxima pessoa — ou eu mesmo em seis
meses — não refaça o raciocínio nem desfaça a decisão sem saber o que ela custava.

Só entram decisões difíceis de reverter ou que surpreendem quem lê o código pela
primeira vez. Escolha de biblioteca trivial não é ADR.

| # | Decisão | Estado |
|---|---|---|
| [0001](0001-whatsapp-em-vez-de-pagamento-on-line.md) | Handoff por WhatsApp em vez de pagamento on-line | Aceita |
| [0002](0002-nenhum-dado-pessoal-no-servidor.md) | Nenhum dado pessoal do cliente no servidor | Aceita |
| [0003](0003-jwt-com-refresh-token-em-cookie.md) | JWT em memória + refresh token em cookie HttpOnly | Aceita |
| [0004](0004-migrations-como-dono-do-schema.md) | EF Migrations como dono do schema, aplicadas por um serviço separado | Aceita |
| [0005](0005-white-label-antes-de-multi-tenant.md) | White-label single-tenant antes de multi-tenant | Aceita |
| [0006](0006-cache-versionado-em-vez-de-varredura.md) | Cache com chave versionada em vez de varredura de keyspace | Aceita |
| [0007](0007-arquivar-em-vez-de-apagar.md) | Arquivar promoções em vez de apagar | Aceita |
| [0008](0008-testcontainers-com-skip.md) | Testcontainers com skip explícito em vez de provedor em memória | Aceita |

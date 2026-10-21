# Migrations

Mudanças de banco depois da carga inicial. Tudo aqui precisa rodar em produção: Oracle 19c, `NFSE` com centenas de milhões de linhas e sistema no ar.

`VNNN__descricao.sql` é a migration e `VNNN__descricao_rollback.sql` desfaz. O `db/init.sh` aplica as migrations em ordem depois dos scripts base, e os rollbacks não são aplicados.

O motivo, o lock esperado e como reverter de cada migration ficam no documento do chamado em `docs/chamados/`.

Regras:

- `SET DEFINE OFF` e `WHENEVER SQLERROR EXIT FAILURE` no início, `EXIT` no fim.
- `ALTER SESSION SET DDL_LOCK_TIMEOUT = 30` antes de DDL.
- Pode rodar duas vezes sem erro.
- Nada exclusivo do 23ai.
- Índice `ONLINE`, constraint em tabela grande com `ENABLE NOVALIDATE`, coluna nova nullable e sem default.
- Correção de dados em lotes, com auditoria dos valores anteriores.
- Nenhuma migration altera valores de `NFSE`.

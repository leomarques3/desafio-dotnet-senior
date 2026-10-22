# Runbook de plantão

O que olhar primeiro quando algo parecido com os chamados CH-01 a CH-05 acontecer. Os scripts citados estão em `tools/diagnostico/` e `tools/medicoes/`. Todos os SQL são só de leitura, a não ser os marcados como correção.

No ambiente local, os scripts rodam assim:

```bash
docker compose exec -T oracle sqlplus -s NFSE_OWNER/nfse_dev_123@//localhost:1521/FREEPDB1 < tools/diagnostico/ch02_invariantes.sql
```

Os que recebem parâmetro usam `&1` e `&2`. Em produção, rode pelo sqlplus passando os valores. No local, troque com `sed` antes de mandar para o container, como em `docs/chamados/CH-01.md`.

## Guia não bate com as notas

Inclui a reclamação de nota cancelada que continua na guia.

1. Compare a guia com a soma das notas: `tools/diagnostico/ch01_caso.sql <inscrição> <AAAA-MM>`. A guia tem de ser igual à soma do `VALOR_ISS` das notas normais e não retidas da competência.
2. Confira se a trigger da guia está ativa e se é a versão nova:

   ```sql
   SELECT TRIGGER_NAME, TRIGGER_TYPE, STATUS FROM USER_TRIGGERS WHERE TRIGGER_NAME = 'TRG_NFSE_SITUACAO_GUIA';
   ```

   O esperado é `COMPOUND` e `ENABLED`. Se aparecer `AFTER EACH ROW`, a V004 não está aplicada e o cancelamento volta a não atualizar a guia (CH-05).
3. Se a guia está aberta (`SITUACAO = 'A'`), recalcule (correção):

   ```sql
   EXEC PKG_GUIA.recalcular_guia(<prestador_id>, DATE 'AAAA-MM-01');
   COMMIT;
   ```

4. Se a guia está paga (`'P'`), não mexa. Registre o caso para a fiscalização, que decide entre restituição e compensação.
5. Para ver se é um caso isolado, `tools/diagnostico/ch01_impacto.sql` lista as guias divergentes por situação e as notas com ISS fora do arredondamento comercial. Depois da V006 e da V007 não deve haver guia aberta divergente.

## Lote com retorno estranho, `ORA-00001` ou nota em duplicidade

1. `tools/diagnostico/ch02_invariantes.sql` mostra RPS repetidos, buracos ou números repetidos na numeração e os lotes recentes com notas e erros.
2. No log do serviço, procure o protocolo. Cada lote tem uma linha `processando lote` e uma `concluido em ...: N resultados, M com erro`. Duas linhas `processando` para o mesmo protocolo indicam o lote processado duas vezes. `falha no lote` indica que o lote voltou para a fila.
3. Confira se as restrições do CH-02 estão no banco:

   ```sql
   SELECT CONSTRAINT_NAME, STATUS, VALIDATED FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = 'UK_RPS_PREST_SERIE_NUM';
   ```

   O esperado é `ENABLED` e `NOT VALIDATED`.
4. Lotes cujo retorno ficou com erro em RPS que viraram nota aparecem na consulta de levantamento de `docs/chamados/CH-02.md`. As notas estão certas. Só o retorno está errado, e o caso vai para o atendimento.

## Lote parado em "em processamento"

```sql
SELECT PROTOCOLO, PRESTADOR_ID, QTD_RPS, SITUACAO, DT_RECEBIMENTO,
       (SELECT COUNT(*) FROM RPS r WHERE r.PROTOCOLO = l.PROTOCOLO) AS NOTAS
  FROM LOTES_RPS l
 WHERE SITUACAO IN (1, 2)
 ORDER BY DT_RECEBIMENTO;
```

Situação 1 há mais de alguns segundos indica que o agendador não está pegando lotes: veja se o serviço está no ar e se o log mostra os ticks. Situação 2 antiga, com o serviço reiniciado depois do recebimento, indica um lote interrompido. Nada retoma esse lote sozinho (`docs/outros-achados.md`, item 6).

Antes de devolver o lote para a fila, olhe a coluna `NOTAS`. Se ela for maior que zero, o reprocessamento vai devolver E060 para esses RPS no retorno. Avise o atendimento antes. Para devolver (correção):

```sql
UPDATE LOTES_RPS SET SITUACAO = 1 WHERE PROTOCOLO = '<protocolo>' AND SITUACAO = 2;
COMMIT;
```

## Memória alta ou pool reciclando

1. Veja os maiores lotes pendentes ou em processamento:

   ```sql
   SELECT PROTOCOLO, QTD_RPS, SITUACAO, ROUND(DBMS_LOB.GETLENGTH(XML_LOTE) / 1000000, 1) AS MILHOES_CARACTERES
     FROM LOTES_RPS
    WHERE SITUACAO IN (1, 2)
    ORDER BY DBMS_LOB.GETLENGTH(XML_LOTE) DESC;
   ```

2. O processamento roda no máximo `NFSE_MAX_LOTES_PARALELOS` lotes ao mesmo tempo (padrão 4). Se vários lotes grandes chegarem juntos, baixar para 2 reduz a memória sem recusar lote.
3. A recepção de um lote de 50 mil RPS ainda chega perto de 1 GB por alguns segundos (`docs/chamados/CH-03.md`, "O que continua"). Vários lotes grandes enviados ao mesmo tempo podem estourar o limite do pool na recepção, e o cliente recebe erro SOAP sem o lote ser gravado.
4. Para reproduzir fora de produção: `tools/medicoes/medir-lote.sh 50000 2`, com o limite do container reduzido por `docker update --memory`.

## Consulta lenta ou timeout

Não comece aumentando o timeout: isso só deixa a consulta ocupando o banco por mais tempo.

1. Veja as consultas que mais leem blocos:

   ```sql
   SELECT sql_id, executions, ROUND(buffer_gets / GREATEST(executions, 1)) AS gets_por_exec,
          ROUND(elapsed_time / 1000 / GREATEST(executions, 1)) AS ms_por_exec, SUBSTR(sql_text, 1, 80) AS sql
     FROM v$sqlstats
    ORDER BY buffer_gets DESC
    FETCH FIRST 10 ROWS ONLY;
   ```

2. Veja o plano: `SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR('<sql_id>', NULL, 'TYPICAL +PREDICATE'));`. Procure `TABLE ACCESS FULL` em `NFSE` e funções em volta de colunas em "Predicate Information" (`TO_NUMBER`, `TRUNC`, `INTERNAL_FUNCTION`). Uma conversão implícita na coluna impede o índice (CH-04).
3. Para a consulta de serviços tomados, `tools/diagnostico/ch04_plano.sql` mostra o último cursor da aplicação. O esperado é `INDEX RANGE SCAN` em `IX_NFSE_TOMADOR_DT`. `tools/medicoes/medir-tomado.sh` mede o tempo pelo serviço.

As consultas em `v$` precisam de `SELECT_CATALOG_ROLE` ou equivalente.

## Migrations

Aplicar em ordem. Todas podem rodar mais de uma vez. Para reverter, rodar os `_rollback.sql` em ordem inversa. Detalhes de lock e tempo em cada documento.

| Migration | O que faz | Rollback | Documento |
|---|---|---|---|
| V001 | numeração da NFS-e com trava por prestador | volta o corpo original do `PKG_NFSE` | CH-02 |
| V002 | restrição única em `RPS` (`NOVALIDATE`) | remove a restrição e mantém o índice | CH-02 |
| V003 | `MERGE` da guia tenta de novo em caso de duplicidade | volta o corpo original do `PKG_GUIA` | CH-02 |
| V004 | trigger da guia na mesma transação do cancelamento | recria a trigger original | CH-05 |
| V005 | recalcula guias abertas com nota cancelada | devolve os valores anteriores | CH-05 |
| V006 | guia soma o ISS gravado nas notas | volta o `PKG_GUIA` da V003 | CH-01 |
| V007 | recalcula guias abertas diferentes da soma das notas | devolve os valores anteriores | CH-01 |
| V008 | coluna `RPS.TOMADOR_DOCUMENTO` | marca a coluna como não usada | CNPJ alfanumérico |

A V008 tem de estar aplicada antes do deploy da aplicação com o CNPJ alfanumérico, porque o `INSERT` do RPS usa a coluna nova. As migrations de package (V001, V003 e V006) devem ser aplicadas fora do pico, porque esperam as sessões que estão usando o package.

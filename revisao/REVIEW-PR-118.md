# Revisão do PR #118 (fix/lotes-ora-00001 → main)

**Veredito: Request changes.** Não aprovaria o merge.

@rafael.sustentacao, obrigado por pegar esses chamados. Dois diagnósticos do PR estão certos: o `MAX(NUMERO)+1` sem trava e a lista `_resultados` compartilhada causam problema de verdade. Mas apliquei o PR localmente e encontrei pontos que não podem ir para produção, e o mais grave mexe na numeração das notas, que depois não tem como corrigir. Deixo abaixo cada ponto com o que medi e, no final, uma sugestão de como dividir o trabalho para ter algo seguro antes do fechamento. Se quiser, a gente faz a parte de concorrência junto.

Como testei: apliquei o diff numa branch local, rodei os testes existentes em `net8.0` e mais alguns que escrevi, apliquei a migration e o rollback no Oracle do compose (carga completa, cerca de 2 milhões de notas) e comparei os planos de execução com `DBMS_XPLAN.DISPLAY_CURSOR(..., 'ALLSTATS LAST +PREDICATE +HINT_REPORT')`.

---

## Bloqueia o merge

### 1. A `SEQ_NFSE_NUMERO` acaba com a numeração por prestador

Arquivos: `db/03_pkg_nfse.sql`, `db/migrations/V001__seq_numero_nfse.sql`

A sequence é uma só para todos os prestadores e começa no `MAX(NUMERO)` da tabela inteira. Com a migration aplicada no ambiente local:

```
SEQ_NFSE_NUMERO: próximo = 401, CACHE 100
MAX(NUMERO) do prestador 25 = 10
nota emitida p/ prestador 25 -> NUMERO = 401
após ROLLBACK, nova nota     -> NUMERO = 402
nota p/ prestador 1          -> NUMERO = 403
```

O prestador 25 saltou da nota 10 para a 401. O número 401 se perdeu no rollback, e o prestador 1 pegou o 403 da mesma sequence. A partir do deploy, todo prestador passa a ter buracos na numeração. Somam-se a isso os números que o `CACHE 100` descarta em restart da instância ou flush do shared pool, e em RAC a ordem cronológica também deixa de ser garantida.

O comentário logo acima da linha alterada diz "numeração sequencial por prestador, sem buracos (exigência fiscal)". O que mais me preocupa é que isso não volta atrás: nota emitida não pode ser renumerada, então mesmo um rollback do PR depois de um dia em produção deixa essas notas com a numeração quebrada para sempre.

Minha sugestão é manter o `MAX(NUMERO)+1`, que é barato (o plano é `INDEX RANGE SCAN (MIN/MAX)` em `UK_NFSE_NUMERO`), e serializar a emissão por prestador dentro da própria transação:

```sql
SELECT ID INTO v_id FROM CONTRIBUINTES WHERE ID = p_prestador_id FOR UPDATE;
SELECT NVL(MAX(NUMERO), 0) + 1 INTO v_numero FROM NFSE WHERE PRESTADOR_ID = p_prestador_id;
```

A trava dura até o commit, que é por RPS, e um rollback devolve o número. Lotes do mesmo prestador passam a emitir um de cada vez, o que para numeração sequencial é o comportamento esperado.

### 2. O retry em ORA-00001 não resolve o "mesmo RPS gerou duas NFS-e"

Arquivo: `legado/src/NotaCarioca.Servicos/ProcessadorLoteRps.cs` (`ProcessarRps` / `GravarRps`)

A duplicidade de RPS nunca gera ORA-00001, porque a tabela `RPS` não tem restrição única em `(PRESTADOR_ID, SERIE, NUMERO)`:

```
INDEX_NAME             UNIQUENESS      CONSTRAINTS (P/U)
PK_RPS                 UNIQUE          PK_RPS (P)
IX_RPS_PRESTADOR_NUM   NONUNIQUE       nenhuma
IX_RPS_PROTOCOLO       NONUNIQUE       nenhuma
```

O código consulta com `_rps.Existe()` (`SELECT COUNT(*)`) e só depois faz o `INSERT`. Se duas transações processam o mesmo RPS ao mesmo tempo (ERP que reenviou o lote por timeout, ou o mesmo lote pego por dois ticks do agendador), as duas enxergam que o RPS não existe e as duas geram nota. O retry não passa por esse caminho.

Sobre o retry em si: com a sequence ele não dispara mais por causa da numeração, então só esconde o sintoma. Além disso, o `new Random()` criado a cada tentativa, por várias threads ao mesmo tempo, pode repetir a semente, e o `Thread.Sleep` segura a thread e a conexão do lote enquanto espera.

A garantia precisa estar no banco. Dá para criar a constraint aproveitando o índice que já existe, sem rebuild e sem validar o histórico, o que evita lock longo em produção:

```sql
ALTER SESSION SET DDL_LOCK_TIMEOUT = 30;
ALTER TABLE RPS ADD CONSTRAINT UK_RPS_PREST_SERIE_NUM
  UNIQUE (PRESTADOR_ID, SERIE, NUMERO) USING INDEX IX_RPS_PRESTADOR_NUM ENABLE NOVALIDATE;
```

Na aplicação, o ORA-00001 nessa constraint vira E060 para o RPS. Antes de aplicar, convém levantar quantos RPS já têm duas notas em produção. Esses casos precisam ir para a fiscalização, porque as notas não podem ser simplesmente apagadas.

### 3. O lock no `Add` não resolve a disputa em `_resultados`

Arquivo: `ProcessadorLoteRps.cs`, campo `private static List<ResultadoRps> _resultados`

A lista é `static`, compartilhada por todos os lotes, e cada lote começa com `_resultados.Clear()`, que continua sem lock. Com dois lotes rodando em paralelo, o lote B pode limpar resultados que o lote A ainda não leu, e erros de B acabam gravados no campo `ERROS` do protocolo de A. Isso bate com o relato de "retorno do lote veio faltando RPS" no CH-02. O lock no `Add` deixa o `Clear()` e o laço que monta os erros desprotegidos.

Se a lista virar variável local de `ProcessarLote`, o problema some e o lock deixa de ser necessário.

### 4. CH-04: o otimizador ignora o hint INDEX e o plano não muda

Arquivo: `legado/src/NotaCarioca.Dados/NfseRepositorio.cs` (`ConsultarServicoTomado`)

Comparei as três versões usando o tomador com mais notas na carga (`11444777000161`), período de 09/2026, página 1:

| Versão | Acesso a `NFSE` | Blocos lidos por execução | Tempo |
|---|---|---|---|
| atual, `FULL(n)` | TABLE ACCESS FULL | ~161.000 | 180 a 480 ms |
| PR-118, `INDEX(n IX_NFSE_TOMADOR_DT)` | TABLE ACCESS FULL | ~161.000 | ~178 ms |
| sem hint, bind VARCHAR2, sem TRUNC | INDEX RANGE SCAN em IX_NFSE_TOMADOR_DT | ~830 | 5 a 20 ms |

O hint report do Oracle para a versão do PR:

```
Total hints for statement: 1 (U - Unused (1))
   8 -  SEL$9834E3F4 / "N"@"SEL$4"
         U -  INDEX(n IX_NFSE_TOMADOR_DT)
```

E o motivo aparece no predicate information:

```
8 - filter((TO_NUMBER("N"."TOMADOR_DOCUMENTO")=:DOCN AND TRUNC(INTERNAL_FUNCTION("N"."DT_EMISSAO"))>=...
```

O documento vai como `OracleDbType.Int64` (via `ValidadorDocumento.ParaNumero`) contra uma coluna `VARCHAR2(14)`, então o Oracle aplica `TO_NUMBER` na coluna e o índice deixa de servir. O `TRUNC(n.DT_EMISSAO)` também impede usar a data do índice. Trocar o hint não mexe em nenhuma das duas coisas. Esse `TO_NUMBER` implícito também vai dar ORA-01722 assim que entrar o primeiro CNPJ alfanumérico em `TOMADOR_DOCUMENTO`, e aí a consulta quebra para todos os tomadores.

Quanto ao timeout de 120 s, em produção ele faz a consulta que o DBA já apontou como uma das mais pesadas do pico ficar mais tempo ocupando sessão e I/O, e o ERP do outro lado provavelmente desiste antes disso. Eu não usaria o timeout como correção do CH-04.

O que proponho: tirar o hint, fazer o bind como `Varchar2` com o documento normalizado e trocar o `TRUNC` por faixa de data.

```sql
WHERE n.TOMADOR_DOCUMENTO = :doc
  AND n.DT_EMISSAO >= :ini AND n.DT_EMISSAO < :fim
```

Como `:ini` e `:fim` já chegam com `.Date`, o resultado continua o mesmo. Antes de trocar o bind, precisamos conferir se todos os `TOMADOR_DOCUMENTO` gravados têm 11 ou 14 caracteres com os zeros à esquerda, porque a comparação deixa de ser numérica. O índice `IX_NFSE_TOMADOR_DT` já existe, então não precisa de DDL. Seria bom anexar o plano antes e depois no PR.

### 5. CH-01: AwayFromZero sobre double ainda erra o ISS, e o teste foi ajustado para o valor errado

Arquivos: `legado/src/NotaCarioca.Dominio/CalculoIss.vb`, `legado/tests/NotaCarioca.Testes/CalculoIssTests.cs`

O cálculo ainda passa por `double` (`CDbl(base) * CDbl(aliquota)`). Em ponto flutuante, 1234,50 × 0,03 dá 37,0349999..., e o arredondamento nem chega a encontrar o meio. Testei com os valores das notas do caso reportado (prestador 10000001, 09/2026), comparando com o `ROUND` do Oracle:

```
Failed Iss_IgualAoRoundDoOracle ("1234.50","0.03","37.04")   Expected:<37,04>. Actual:<37,03>.
Failed Iss_IgualAoRoundDoOracle ("375.50","0.03","11.27")    Expected:<11,27>. Actual:<11,26>.
Passaram: 10,50×0,05=0,53; 250,50×0,05=12,53; 100,10×0,05=5,01; 150,30×0,02=3,01
```

O `Iss_DuasCasas` mudou de 150,30 × 0,02 = 3,01 para 1234,50 × 0,03 = 37,03, que é justamente um dos valores errados. O teste antigo estava correto, e o novo passa a garantir o bug. Peço para não alterar a expectativa de um teste existente só para ele passar.

Também ficaram de fora:

- A guia. O `PKG_GUIA.recalcular_guia` recalcula o ISS com `ROUND` em vez de somar o `VALOR_ISS` das notas. Como a nota emitida não muda, a guia precisa somar o que está nas notas, senão as notas antigas continuam divergindo.
- As perguntas da fiscalização (desde quando, quantos contribuintes, quanto). Elas dependem de levantamento nos dados já gravados, incluindo guias que já foram pagas.
- A diferença entre runtimes. `Math.Round(double)` tem implementação diferente no .NET Framework 4.8 e no .NET Core, então o teste passando em `net8.0` não garante o mesmo resultado em produção, que roda `net48`.

Eu tiraria o CH-01 deste PR e trataria separado: cálculo todo em `Decimal` (`Math.Round(base * aliquota, 2, MidpointRounding.AwayFromZero)`), guia somando `VALOR_ISS` e um script de levantamento e correção das guias abertas, com relatório das pagas para a fiscalização.

### 6. O rollback invalida o PKG_NFSE e para a emissão

Arquivo: `db/migrations/V001__seq_numero_nfse_rollback.sql`

O rollback só faz `DROP SEQUENCE`, e o corpo do package continua usando a sequence:

```
PKG_NFSE   PACKAGE        VALID
PKG_NFSE   PACKAGE BODY   INVALID

ORA-04063: package body "NFSE_OWNER.PKG_NFSE" has errors
ORA-06508: PL/SQL: could not find program unit being called: "NFSE_OWNER.PKG_NFSE"
```

Rodar esse rollback em produção derruba toda a emissão de NFS-e. Ele precisa restaurar o corpo anterior do `PKG_NFSE` antes de remover a sequence.

### 7. A migration falha se rodar duas vezes e foi colocada no init.sh

Arquivos: `db/migrations/V001__seq_numero_nfse.sql`, `db/init.sh`

Na segunda execução a migration dá `ORA-00955: name is already used by an existing object`, e uma reaplicação depois de falha parcial em produção vira incidente. Além disso, o `03_pkg_nfse.sql` passou a depender de um objeto criado só pela migration, então num ambiente montado apenas pelos scripts base o package nasce inválido. O `init.sh` só roda na primeira subida do container local e não serve como caminho de deploy.

### 8. Faltam testes do cenário do chamado

Um lote de 1000 RPS processado sozinho é o caso que já funcionava antes ("com lote pequeno ninguém conseguiu reproduzir"). O CH-02 depende de concorrência: dois lotes do mesmo prestador ao mesmo tempo, ou o mesmo RPS em dois lotes. Os dois casos dão para reproduzir contra o Oracle real sem depender de sorte:

- Numeração: a conexão A chama `gerar_nfse` sem commit, a conexão B chama para o mesmo prestador e A faz commit. Hoje B recebe ORA-00001.
- RPS: A insere o RPS sem commit, B chama `Existe()`, recebe false e insere, e as duas fazem commit. Hoje ficam duas notas.

Esses testes deveriam falhar antes do PR e passar depois. Com a sequence, o primeiro passa, mas à custa das lacunas, e o segundo continua falhando.

---

## O que pode ficar

- O log com a exceção completa (`ex` em vez de `ex.Message`) e com o prestador na mensagem de "processando lote" vai ajudar bastante o plantão.
- A extração do `GravarRps` deixou o `ProcessarRps` mais fácil de ler.
- O overload com timeout no `OracleHelper` pode ser útil em outro momento, só não como solução do CH-04.

## Pontos menores (não bloqueiam)

- O E999 devolve o `ex.Message` do Oracle para o cliente, expondo texto interno do banco no retorno do lote. Melhor uma mensagem genérica, com o detalhe só no log.
- O `ProcessarRps` só captura `OracleException`. Qualquer outra exceção derruba o lote inteiro, que volta para `SITUACAO=1` e é reprocessado em loop.
- A descrição diz que não houve mudança de schema, mas a sequence é DDL e o rollback afeta o package. Vale deixar isso explícito no PR.

---

## Sugestão para o fechamento

Para não perder a release de quinta, eu dividiria em PRs menores:

1. Pode entrar já, com risco baixo: `_resultados` como variável local de `ProcessarLote`, mais as melhorias de log. Isso resolve o retorno de lote faltando RPS causado pela lista compartilhada.
2. CH-02: trava por prestador no `gerar_nfse` (sem sequence), constraint única em `RPS` com `ENABLE NOVALIDATE`, ORA-00001 dessa constraint convertido em E060 e os dois testes de concorrência acima contra o Oracle. A migration deve poder rodar mais de uma vez, e o rollback precisa restaurar o package.
3. CH-04: bind `VARCHAR2`, faixa de data, sem hint, com o plano antes e depois e um teste mostrando que o resultado da consulta não mudou.
4. CH-01 separado: cálculo em `Decimal`, guia somando `VALOR_ISS` e levantamento de impacto (desde quando, quantos contribuintes, quanto, guias pagas e abertas), alinhado com a fiscalização antes de corrigir os dados.

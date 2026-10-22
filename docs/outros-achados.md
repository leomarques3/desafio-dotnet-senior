# Outros problemas encontrados

Problemas que apareceram durante o trabalho e que não estão na fila de chamados. Nenhum foi corrigido aqui. Cada um precisa de decisão de negócio ou de um chamado próprio. Evidências em `docs/outros-achados/`.

| # | Problema | Gravidade |
|---|---|---|
| 1 | RPS com data antiga vira nota numa competência com guia paga, e o ISS não é cobrado | alta |
| 2 | Exceção fora do RPS devolve o lote para a fila e ele é processado de novo do zero | alta |
| 3 | `DataEmissao` inválida vira a data de agora | média |
| 4 | E999 devolve ao cliente o erro interno do Oracle | média |
| 5 | CNPJ do prestador não é conferido no lote nem no cancelamento | média |
| 6 | Lote parado na situação 2 depois de uma queda nunca é retomado | média |
| 7 | Guia recalculada fora da transação dos RPS | média |
| 8 | Consulta de notas do prestador lê todo o histórico dele | média |
| 9 | `QuantidadeRps` informada não é conferida | baixa |
| 10 | Cancelamento e pagamento da guia sem trava entre si | baixa |
| 11 | `XML_LOTE` guardado para sempre | baixa |
| 12 | Código de verificação gerado com `DBMS_RANDOM` | baixa |
| 13 | Teste de `Competencia.Parse` ignorado desde 2021 | baixa |

## 1. RPS com data antiga cai em competência com guia paga

A competência da nota vem da `DataEmissao` do RPS (`ProcessadorLoteRps.cs:153`), sem limite de quanto pode ser antiga ou futura. Se a guia daquela competência já foi paga, o recálculo não mexe nela, porque o `MERGE` só atualiza guia aberta (`V006__guia_soma_iss_das_notas.sql:25`).

No lote de `lote-com-problemas.txt`, um RPS de 15/07/2026 virou a nota 13 do prestador 10000004 na competência 07/2026, com ISS de 50,00. A guia de 07/2026 continuou paga com 290,29, e as notas da competência passaram a somar 340,29. Esses 50,00 não aparecem em nenhuma guia.

Proposta: definir com a fiscalização uma janela para a data do RPS, recusando com erro próprio o que estiver fora dela, ou gerar guia complementar quando a competência já tiver guia paga.

## 2. Exceção fora do RPS devolve o lote para a fila

`ProcessarRps` só trata `OracleException` (`ProcessadorLoteRps.cs:169`). Qualquer exceção fora dele, no recálculo da guia, na leitura do XML ou por falta de memória, cai no `catch` da thread, que devolve o lote para a situação 1 (`ProcessadorLoteRps.cs:50-53`). No tick seguinte o lote é processado de novo do começo, e os RPS que já tinham virado nota voltam com E060 no retorno. Se o erro se repetir, o lote entra nesse ciclo para sempre.

Isso aconteceu nas medições do CH-03 com o limite de memória de 1 GB: o log mostrou `falha no lote NC20261000001304: Exception of type 'System.OutOfMemoryException' was thrown.`

Proposta: contar as tentativas do lote e, depois de um limite, marcar uma situação de falha que o plantão consiga ver, e continuar o lote de onde parou em vez de recomeçar.

## 3. `DataEmissao` inválida vira a data de agora

Quando a data não é reconhecida, `LeitorLoteRps` usa `DateTime.Now` (`LeitorLoteRps.cs:158`). No lote de teste, o RPS com `31/13/2026` virou a nota 14 na competência 10/2026, sem aviso ao cliente. O cliente pode ter mandado um RPS de outro mês, que vai para a guia errada.

Proposta: recusar o RPS com erro de data inválida.

## 4. E999 devolve o erro interno do Oracle

O texto da `OracleException` vai direto para a mensagem do RPS (`ProcessadorLoteRps.cs:175`). No lote de teste, a discriminação com 2100 caracteres voltou com o esquema, a tabela, a coluna, a linha do package e um link da Oracle. A correção sugerida ao cliente é "Tente novamente mais tarde", mas reenviar o mesmo RPS dá o mesmo erro.

Proposta: validar o tamanho dos campos antes de gravar, com o código de erro do campo, e devolver ao cliente uma mensagem genérica com um identificador que leve ao log.

## 5. CNPJ do prestador não é conferido

O prestador é identificado só pela inscrição municipal. No lote de teste, o CNPJ informado era de outra empresa e o lote foi aceito. O cancelamento da nota 14 com o mesmo CNPJ trocado também foi aceito (`cancelamento-cnpj-trocado.txt`), e a resposta repete o CNPJ errado.

Se a autenticação de produção for por certificado digital, o risco depende de o certificado ser conferido contra a inscrição. Vale confirmar.

Proposta: recusar com E010 quando o CNPJ informado não for o do prestador da inscrição.

## 6. Lote parado na situação 2

O lote vai para a situação 2 quando começa a ser processado. Se o processo cair no meio, por reciclagem do pool ou deploy, nada devolve o lote para a fila, e o cliente vê "em processamento" para sempre. O CH-03 traz a consulta para encontrar esses lotes, e o runbook descreve o que fazer com eles.

Proposta: retomar o lote pulando os RPS que já viraram nota, junto com o item 2.

## 7. Guia recalculada fora da transação dos RPS

Cada RPS é gravado na sua própria transação, e a guia só é recalculada no fim do lote (`ProcessadorLoteRps.cs:110`). Se o processo cair entre o último RPS e o recálculo, a guia fica sem as notas novas até o próximo lote ou cancelamento do prestador naquela competência. É o mesmo efeito do CH-05, por outro caminho.

Proposta: recalcular a guia também na retomada do lote (item 6), ou na emissão de cada nota, se o custo for aceitável.

## 8. Consulta de notas do prestador lê todo o histórico

`ConsultarPorPrestador` filtra `n.COMPETENCIA <= :fim` (`NfseRepositorio.cs:113`) e usa o índice `(PRESTADOR_ID, COMPETENCIA)` só com esse limite superior. Para um prestador da carga de volume com 400 notas em 24 competências, a consulta de setembro leu as 400 notas para devolver 15 (`consulta-por-prestador.txt`). Em produção, com anos de histórico por prestador, cresce sem limite.

Acrescentar `COMPETENCIA >= mês de :ini` mudaria o resultado, porque a competência vem da data do RPS e a data de emissão da nota é a do processamento. A nota 13 do item 1 tem competência 07/2026 e emissão em 10/2026, e sumiria da consulta de outubro.

Proposta: índice `(PRESTADOR_ID, DT_EMISSAO)` criado com `ONLINE`, que precisa de janela e de medição em produção antes.

## 9. `QuantidadeRps` não é conferida

O valor informado é lido (`LeitorLoteRps.cs:58`) e não é usado. No lote de teste, `QuantidadeRps` era 9 para 3 RPS, e o lote foi aceito com `QTD_RPS = 3`. Um arquivo truncado pelo ERP do cliente passa sem aviso.

Proposta: recusar o lote quando a quantidade informada não bater com a lida.

## 10. Cancelamento e pagamento da guia sem trava

`PKG_NFSE.cancelar_nfse` confere se a guia está paga e depois cancela a nota, sem travar a guia. Se o pagamento for baixado entre as duas coisas, a nota fica cancelada e a guia paga com o valor dela, o mesmo caso das guias pagas do CH-05, embora a janela seja de milissegundos.

Proposta: `SELECT ... FOR UPDATE` na guia antes da conferência.

## 11. `XML_LOTE` guardado para sempre

Todo lote recebido fica com o XML inteiro em `LOTES_RPS.XML_LOTE`, sem rotina de limpeza. No banco local, 13,8 milhões de caracteres ocupam 42 MB de segmento LOB (`xml-lote.txt`). Um lote de 50 mil RPS tem 67 milhões de caracteres.

Proposta: definir com a área fiscal por quanto tempo o XML precisa ficar no banco e mover ou apagar os antigos.

## 12. Código de verificação com `DBMS_RANDOM`

O código de verificação da nota é gerado com `DBMS_RANDOM.STRING('X', 9)` (`PKG_NFSE.gerar_nfse`), que não é um gerador criptográfico. Se o código servir para conferir a autenticidade da nota em consulta pública, ele não deveria ser previsível.

Proposta: gerar com `DBMS_CRYPTO.RANDOMBYTES`.

## 13. Teste de `Competencia.Parse` ignorado

`CompetenciaTests.Parse_MesPorExtenso` está com `[Ignore("quebrado desde 2021, ver com o Marcos")]` (`CompetenciaTests.cs:31`). `Competencia.Parse("jan/2020")` não funciona. Ou a função deveria aceitar esse formato, ou o teste deveria sair.

Proposta: descobrir se algum cliente manda a competência por extenso e corrigir a função ou apagar o teste.

# CNPJ alfanumérico

Situação: implementado.

## O que pede

Desde julho de 2026 a Receita pode emitir CNPJ alfanumérico (IN RFB nº 2.229/2024). Prestadores e tomadores com o novo formato precisam funcionar na recepção e no processamento de lotes, nas consultas e no cancelamento. Os CNPJs numéricos continuam existindo, e quem já integra não pode perceber diferença.

## Regra

Fonte: manual "Cálculo dos dígitos verificadores de CNPJ alfanumérico" da Receita Federal e do Serpro, em https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/documentos-tecnicos/cnpj.

- 14 posições: as 12 primeiras com `0-9` ou `A-Z`, e os 2 dígitos verificadores numéricos.
- O valor de cada caractere é o código ASCII menos 48. Os algarismos continuam valendo 0 a 9, `A` vale 17 e `Z` vale 42.
- Pesos de 2 a 9 da direita para a esquerda, recomeçando depois do oitavo caractere, os mesmos do CNPJ numérico. Resto 0 ou 1 dá DV 0, e nos outros casos o DV é 11 menos o resto.
- O segundo DV usa os 12 caracteres mais o primeiro DV.

O exemplo do manual, `12.ABC.345/01DE-35`, tem somas 459 e 424 e DVs 3 e 5, e é o caso usado nos testes unitários.

## O que não funcionava

| Onde | Problema |
|---|---|
| `ValidadorDocumento.ValidarCnpj` e `ValidarCpfOuCnpj` (VB) | `IsNumeric`, `CLng` e `SomenteDigitos` recusavam ou descartavam as letras. RPS com tomador alfanumérico voltava E020, e as consultas por tomador também. |
| `NfseRepositorio` | Gravava e consultava o documento com `SomenteDigitos`, que tira as letras. |
| `XmlNfseBuilder.CompNfse` | `Convert.ToInt64(TOMADOR_DOCUMENTO)` lançaria exceção e derrubaria qualquer consulta que trouxesse uma nota com tomador alfanumérico. |
| `RPS.TOMADOR_CNPJ` | É `NUMBER(14)` e não comporta letras. |
| `ConsultarServicoTomado` | O `TO_NUMBER` implícito quebrava a consulta com `ORA-01722`, corrigido no CH-04. |

O prestador já funcionava. Ele é identificado pela inscrição municipal, e o CNPJ dele, guardado em `CONTRIBUINTES.DOCUMENTO VARCHAR2(14)`, só é repetido nas respostas.

## O que mudou

`ValidadorDocumento` (VB):

- `ValidarCnpj` continua com o cálculo antigo para os numéricos e, só quando o valor não é numérico, tenta a regra alfanumérica.
- `ValidarCpfOuCnpj` aceita um CNPJ alfanumérico válido e, nos outros casos, segue exatamente como antes.
- `Normalizar` devolve o documento como ele é gravado e consultado: o CNPJ alfanumérico sem máscara e em maiúsculas, e para qualquer outra entrada o mesmo resultado de `SomenteDigitos`.
- `EhAlfanumerico` diz se o documento tem letra.

Letras minúsculas são aceitas e gravadas em maiúsculas, então um cliente que mande `12abc34501de35` encontra as notas de `12ABC34501DE35`.

`NfseRepositorio` usa `Normalizar` na emissão, na consulta por tomador e na consulta de serviços tomados.

`XmlNfseBuilder.CompNfse` devolve o CNPJ alfanumérico em `<Cnpj>`, como está gravado. Para documento numérico, o código é o mesmo de antes: sem zeros à esquerda e com a tag escolhida pelo tamanho.

`RPS` ganhou a coluna `TOMADOR_DOCUMENTO VARCHAR2(14)` (`db/migrations/V008__rps_tomador_documento.sql`), preenchida em todo RPS novo. `TOMADOR_CNPJ` e `TOMADOR_CPF` continuam preenchidos para os numéricos, porque outros sistemas podem ler essas colunas, e ficam nulos para o alfanumérico.

## Compatibilidade com os numéricos

Para uma entrada sem letra, todo o caminho é o código antigo: `ValidarCpfOuCnpj` só testa a regra nova quando há letra, `ValidarCnpj` só quando o valor não é numérico, e `Normalizar` devolve `SomenteDigitos`. Uma entrada com letra só muda de comportamento quando é um CNPJ alfanumérico válido. O código antigo também tirava as letras de entradas como `CNPJ 11222333000181`, e isso continua igual.

Os testes de caracterização e os exemplos 01 a 06 comparam as respostas com as do código original, inclusive documentos com zero à esquerda, e continuam passando sem mudança.

## Testes

Unitários em `ValidadorDocumentoTests`: o exemplo do manual com e sem máscara e em minúsculas, DV errado, letra na posição do DV e máscara incompleta.

Integração em `CnpjAlfanumerico/CnpjAlfanumericoTests.cs`:

- `Lote_TomadorAlfanumerico`: o lote gera a nota, o retorno traz `<Cnpj>` com o documento e o RPS grava a coluna nova.
- `ConsultarNfse_TomadorAlfanumerico`: a consulta do prestador filtrada pelo tomador, pedida em minúsculas.
- `ServicoTomado_ConsulenteAlfanumerico`: notas de dois prestadores para o mesmo tomador.
- `Prestador_Alfanumerico`: emissão, consulta e cancelamento de um prestador com CNPJ alfanumérico. Esse já passava no código original e entra como proteção.

`Consulta_ComTomadorAlfanumerico`, do CH-04, cobre a consulta de serviços tomados de um tomador numérico com uma nota alfanumérica no mesmo período.

A saída dos testes no código original está em `docs/cnpj-alfanumerico/testes-antes.txt`.

| Commit | O que entrou | Falhando |
|---|---|---|
| `0dbc8f0` | testes | `CnpjAlfanumerico_Valido`, `Lote_TomadorAlfanumerico`, `ConsultarNfse_TomadorAlfanumerico`, `ServicoTomado_ConsulenteAlfanumerico` |
| `b27d06b` | validador, emissão e consultas | `Lote_TomadorAlfanumerico` (a coluna nova do RPS ainda não existe) |
| `9c99c70` | V008 e gravação no RPS | nenhum |

## Migration

| Migration | Lock | Tempo esperado |
|---|---|---|
| V008 (coluna em `RPS`) | lock exclusivo curto em `RPS`, só no dicionário (coluna anulável e sem default, sem varrer a tabela) | segundos; `DDL_LOCK_TIMEOUT = 30` |

O rollback marca a coluna como não usada (`SET UNUSED`), o que também só mexe no dicionário. A remoção física (`ALTER TABLE RPS DROP UNUSED COLUMNS`) reescreve a tabela e fica para uma janela de manutenção. A migration e o rollback podem rodar mais de uma vez, e a migration volta a criar a coluna depois do rollback (`docs/cnpj-alfanumerico/migration.txt`).

A V008 precisa estar aplicada antes do deploy da aplicação, porque o `INSERT` do RPS passa a usar a coluna nova.

## Risco

RPS gravados antes da V008 ficam com `TOMADOR_DOCUMENTO` nulo. Não fiz carga retroativa, porque eles têm o documento em `TOMADOR_CNPJ` ou `TOMADOR_CPF` e na nota gerada.

Outros sistemas que leem `RPS.TOMADOR_CNPJ` não vão ver o tomador alfanumérico e precisam passar a ler `TOMADOR_DOCUMENTO`.

Documentos enviados com letras por engano, que hoje são aceitos porque o código descarta as letras, continuam aceitos. A exceção é quando o valor com as letras forma um CNPJ alfanumérico válido.

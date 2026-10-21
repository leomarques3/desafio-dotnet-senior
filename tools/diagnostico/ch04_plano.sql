SET LINES 250 PAGES 200 FEEDBACK OFF TRIMSPOOL ON VERIFY OFF
COLUMN sql_id NEW_VALUE v_sql_id
COLUMN child_number NEW_VALUE v_child

PROMPT == consulta de servicos tomados como a aplicacao envia (ultimo cursor executado)
SELECT sql_id, child_number, executions, ROUND(buffer_gets / GREATEST(executions, 1)) AS buffer_gets_por_exec,
       ROUND(elapsed_time / 1000 / GREATEST(executions, 1), 1) AS ms_por_exec
  FROM v$sql
 WHERE sql_text LIKE '%n.TOMADOR_DOCUMENTO = :doc%'
   AND sql_text NOT LIKE '%v$sql%'
 ORDER BY last_active_time DESC
 FETCH FIRST 1 ROW ONLY;

SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR('&v_sql_id', &v_child, 'TYPICAL +PREDICATE -NOTE'));

EXIT

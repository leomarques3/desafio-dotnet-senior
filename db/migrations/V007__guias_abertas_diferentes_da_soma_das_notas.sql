SET DEFINE OFF
WHENEVER SQLERROR EXIT FAILURE
SET SERVEROUTPUT ON

DECLARE
    v_existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM USER_TABLES WHERE TABLE_NAME = 'AJUSTE_GUIA_CH01';
    IF v_existe = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TABLE AJUSTE_GUIA_CH01 (' ||
                          'GUIA_ID NUMBER NOT NULL, PRESTADOR_ID NUMBER NOT NULL, COMPETENCIA DATE NOT NULL, ' ||
                          'VALOR_ANTES NUMBER(15,2), VALOR_DEPOIS NUMBER(15,2), DT_AJUSTE DATE NOT NULL)';
    END IF;
END;
/

DECLARE
    TYPE t_guias IS TABLE OF GUIAS%ROWTYPE;
    v_guias t_guias;
    v_total NUMBER := 0;
    v_inicio DATE := SYSDATE;
BEGIN
    LOOP
        SELECT g.*
          BULK COLLECT INTO v_guias
          FROM GUIAS g
         WHERE g.SITUACAO = 'A'
           AND g.VALOR_ISS <> (SELECT NVL(SUM(n.VALOR_ISS), 0)
                                 FROM NFSE n
                                WHERE n.PRESTADOR_ID = g.PRESTADOR_ID AND n.COMPETENCIA = g.COMPETENCIA
                                  AND n.SITUACAO = 'N' AND n.ISS_RETIDO = 'N')
           AND NOT EXISTS (SELECT 1 FROM AJUSTE_GUIA_CH01 a WHERE a.GUIA_ID = g.ID AND a.DT_AJUSTE >= v_inicio)
         FETCH FIRST 500 ROWS ONLY;

        EXIT WHEN v_guias.COUNT = 0;

        FOR i IN 1 .. v_guias.COUNT LOOP
            PKG_GUIA.recalcular_guia(v_guias(i).PRESTADOR_ID, v_guias(i).COMPETENCIA);
            INSERT INTO AJUSTE_GUIA_CH01 (GUIA_ID, PRESTADOR_ID, COMPETENCIA, VALOR_ANTES, VALOR_DEPOIS, DT_AJUSTE)
            SELECT v_guias(i).ID, v_guias(i).PRESTADOR_ID, v_guias(i).COMPETENCIA, v_guias(i).VALOR_ISS, g.VALOR_ISS, SYSDATE
              FROM GUIAS g
             WHERE g.ID = v_guias(i).ID;
        END LOOP;
        COMMIT;
        v_total := v_total + v_guias.COUNT;
    END LOOP;
    DBMS_OUTPUT.PUT_LINE('guias corrigidas: ' || v_total);
END;
/

EXIT

SET DEFINE OFF
WHENEVER SQLERROR EXIT FAILURE

ALTER SESSION SET DDL_LOCK_TIMEOUT = 30;

CREATE OR REPLACE TRIGGER TRG_NFSE_SITUACAO_GUIA
FOR UPDATE OF SITUACAO ON NFSE
COMPOUND TRIGGER

    TYPE t_guia IS RECORD (prestador_id NUMBER, competencia DATE);
    TYPE t_guias IS TABLE OF t_guia INDEX BY VARCHAR2(60);
    v_guias t_guias;

    AFTER EACH ROW IS
        v_chave VARCHAR2(60) := :NEW.PRESTADOR_ID || '|' || TO_CHAR(:NEW.COMPETENCIA, 'YYYYMMDD');
    BEGIN
        v_guias(v_chave).prestador_id := :NEW.PRESTADOR_ID;
        v_guias(v_chave).competencia := :NEW.COMPETENCIA;
    END AFTER EACH ROW;

    AFTER STATEMENT IS
        v_chave VARCHAR2(60) := v_guias.FIRST;
    BEGIN
        WHILE v_chave IS NOT NULL LOOP
            PKG_GUIA.recalcular_guia(v_guias(v_chave).prestador_id, v_guias(v_chave).competencia);
            v_chave := v_guias.NEXT(v_chave);
        END LOOP;
    END AFTER STATEMENT;

END TRG_NFSE_SITUACAO_GUIA;
/

SHOW ERRORS
EXIT

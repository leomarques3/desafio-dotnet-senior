using System;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Infra
{
    public static class Invariantes
    {
        public static int RpsDuplicados(long prestadorId)
        {
            return Convert.ToInt32(Sql.Escalar(
                "SELECT COUNT(*) FROM (SELECT SERIE, NUMERO FROM RPS WHERE PRESTADOR_ID = :p " +
                "GROUP BY SERIE, NUMERO HAVING COUNT(*) > 1)", new OracleParameter("p", prestadorId)));
        }

        public static bool NumeracaoSemLacunas(long prestadorId)
        {
            return Convert.ToInt32(Sql.Escalar(
                "SELECT CASE WHEN COUNT(*) = NVL(MAX(NUMERO), 0) AND COUNT(*) = COUNT(DISTINCT NUMERO) THEN 1 ELSE 0 END " +
                "FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", prestadorId))) == 1;
        }
    }
}

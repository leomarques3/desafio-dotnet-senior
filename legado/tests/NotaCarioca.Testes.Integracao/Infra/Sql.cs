using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Infra
{
    public static class Sql
    {
        public static object Escalar(string sql, params OracleParameter[] parametros)
        {
            using (OracleConnection conn = BancoOracle.Conectar())
            using (OracleCommand cmd = Comando(conn, sql, parametros))
            {
                return cmd.ExecuteScalar();
            }
        }

        public static System.DateTime AgoraBanco()
        {
            return System.Convert.ToDateTime(Escalar("SELECT SYSDATE FROM DUAL"));
        }

        public static System.DateTime HojeBanco()
        {
            return AgoraBanco().Date;
        }

        public static System.DateTime CompetenciaAtual()
        {
            System.DateTime h = HojeBanco();
            return new System.DateTime(h.Year, h.Month, 1);
        }

        public static decimal ValorGuia(long prestadorId, System.DateTime competencia)
        {
            return Decimal("SELECT VALOR_ISS FROM GUIAS WHERE PRESTADOR_ID = :p AND COMPETENCIA = :c",
                new OracleParameter("p", prestadorId), new OracleParameter("c", OracleDbType.Date) { Value = competencia });
        }

        public static decimal Decimal(string sql, params OracleParameter[] parametros)
        {
            object o = Escalar(sql, parametros);
            return o == null || o == System.DBNull.Value ? 0m : System.Convert.ToDecimal(o);
        }

        public static int Executar(string sql, params OracleParameter[] parametros)
        {
            using (OracleConnection conn = BancoOracle.Conectar())
            using (OracleCommand cmd = Comando(conn, sql, parametros))
            {
                return cmd.ExecuteNonQuery();
            }
        }

        public static DataTable Tabela(string sql, params OracleParameter[] parametros)
        {
            using (OracleConnection conn = BancoOracle.Conectar())
            using (OracleCommand cmd = Comando(conn, sql, parametros))
            using (OracleDataAdapter da = new OracleDataAdapter(cmd))
            {
                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        private static OracleCommand Comando(OracleConnection conn, string sql, OracleParameter[] parametros)
        {
            OracleCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.BindByName = true;
            foreach (OracleParameter p in parametros) cmd.Parameters.Add(p);
            return cmd;
        }
    }
}

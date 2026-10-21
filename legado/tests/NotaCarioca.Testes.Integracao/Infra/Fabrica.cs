using System;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Infra
{
    public class PrestadorTeste
    {
        public long Id;
        public string InscricaoMunicipal;
        public string Cnpj;
        public string RazaoSocial;
    }

    public static class Fabrica
    {
        private static int _contadorDocumentos = new Random().Next(1, 900000);

        public static PrestadorTeste CriarPrestador(string regime = "N", bool ativo = true)
        {
            PrestadorTeste p = new PrestadorTeste();
            p.Id = Convert.ToInt64(Sql.Escalar("SELECT SEQ_CONTRIBUINTES.NEXTVAL FROM DUAL"));
            p.InscricaoMunicipal = "9" + (p.Id % 10000000).ToString("D7", CultureInfo.InvariantCulture);
            p.Cnpj = NovoCnpj();
            p.RazaoSocial = "PRESTADOR TESTE " + p.Id;
            Sql.Executar(
                "INSERT INTO CONTRIBUINTES (ID, INSCRICAO_MUNICIPAL, DOCUMENTO, RAZAO_SOCIAL, REGIME, ATIVO) " +
                "VALUES (:id, :im, :doc, :razao, :regime, :ativo)",
                new OracleParameter("id", p.Id),
                new OracleParameter("im", p.InscricaoMunicipal),
                new OracleParameter("doc", p.Cnpj),
                new OracleParameter("razao", p.RazaoSocial),
                new OracleParameter("regime", regime),
                new OracleParameter("ativo", ativo ? "S" : "N"));
            return p;
        }

        public static string NovoCnpj(bool zeroAEsquerda = false)
        {
            int n = System.Threading.Interlocked.Increment(ref _contadorDocumentos);
            string raiz = (zeroAEsquerda ? "0" : "8") + (n % 10000000).ToString("D7", CultureInfo.InvariantCulture);
            return CnpjComDv(raiz + "0001");
        }

        public static string NovoCpf(bool zeroAEsquerda = false)
        {
            int n = System.Threading.Interlocked.Increment(ref _contadorDocumentos);
            string raiz = (zeroAEsquerda ? "0" : "7") + (n % 100000000).ToString("D8", CultureInfo.InvariantCulture);
            return CpfComDv(raiz);
        }

        private static string CnpjComDv(string base12)
        {
            int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int s = 0;
            for (int i = 0; i < 12; i++) s += (base12[i] - '0') * p1[i];
            int d1 = s % 11 < 2 ? 0 : 11 - s % 11;
            s = 0;
            for (int i = 0; i < 12; i++) s += (base12[i] - '0') * p2[i];
            s += d1 * p2[12];
            int d2 = s % 11 < 2 ? 0 : 11 - s % 11;
            return base12 + d1 + d2;
        }

        private static string CpfComDv(string base9)
        {
            int s = 0;
            for (int i = 0; i < 9; i++) s += (base9[i] - '0') * (10 - i);
            int d1 = (s * 10) % 11;
            if (d1 == 10) d1 = 0;
            s = 0;
            for (int i = 0; i < 9; i++) s += (base9[i] - '0') * (11 - i);
            s += d1 * 2;
            int d2 = (s * 10) % 11;
            if (d2 == 10) d2 = 0;
            return base9 + d1 + d2;
        }
    }
}

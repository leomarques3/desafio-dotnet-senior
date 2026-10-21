using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Chamados
{
    [TestClass]
    public class CH04ServicoTomadoTests
    {
        [TestMethod]
        public void Consulta_ComTomadorAlfanumerico()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            string tomador = Fabrica.NovoCnpj();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, tomadorCnpj: tomador).Rps(2));
            string original = Convert.ToString(Sql.Escalar("SELECT TOMADOR_DOCUMENTO FROM NFSE WHERE PRESTADOR_ID = :p AND NUMERO = 2",
                new OracleParameter("p", p.Id)));
            DateTime hoje = Sql.HojeBanco();

            Sql.Executar("UPDATE NFSE SET TOMADOR_DOCUMENTO = '12ABC34501DE35' WHERE PRESTADOR_ID = :p AND NUMERO = 2",
                new OracleParameter("p", p.Id));
            try
            {
                string resposta = Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado(tomador, null, hoje, hoje.AddDays(1)));

                Assert.AreEqual(1, Ws.Contar(resposta, "//CompNfse"), resposta);
            }
            finally
            {
                Sql.Executar("UPDATE NFSE SET TOMADOR_DOCUMENTO = :d WHERE PRESTADOR_ID = :p AND NUMERO = 2",
                    new OracleParameter("d", original), new OracleParameter("p", p.Id));
            }
        }
    }
}

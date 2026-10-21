using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Chamados
{
    [TestClass]
    public class CH05CancelamentoGuiaTests
    {
        [TestMethod]
        public void Cancelar_AtualizaGuia()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, valor: 1000m).Rps(2, valor: 2000m));
            Assert.AreEqual(150m, Sql.ValorGuia(p.Id, Sql.CompetenciaAtual()));

            Ws.CancelarNfse(Ws.PedidoCancelar(p, 1));

            Assert.AreEqual(100m, Sql.ValorGuia(p.Id, Sql.CompetenciaAtual()));
        }

        [TestMethod]
        public void CancelamentoEmMassa_AtualizaGuia()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, valor: 1000m).Rps(2, valor: 2000m).Rps(3, valor: 4000m));

            Sql.Executar("UPDATE NFSE SET SITUACAO = 'C', DT_CANCELAMENTO = SYSDATE WHERE PRESTADOR_ID = :p AND NUMERO IN (1, 2)",
                new OracleParameter("p", p.Id));

            Assert.AreEqual(200m, Sql.ValorGuia(p.Id, Sql.CompetenciaAtual()));
        }
    }
}

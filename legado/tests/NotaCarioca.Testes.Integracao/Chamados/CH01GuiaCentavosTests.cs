using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Chamados
{
    [TestClass]
    public class CH01GuiaCentavosTests
    {
        [TestMethod]
        public void Lote_IssMeioCentavo()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();

            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, valor: 10.50m));

            Assert.AreEqual(0.53m, Sql.Decimal("SELECT VALOR_ISS FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
            Assert.AreEqual(0.53m, Sql.ValorGuia(p.Id, Sql.CompetenciaAtual()));
        }

        [TestMethod]
        public void Guia_SomaIssDasNotas()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            DateTime competencia = Sql.CompetenciaAtual();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, valor: 1000m).Rps(2, valor: 2000m));

            Sql.Executar("UPDATE NFSE SET VALOR_ISS = 49.99 WHERE PRESTADOR_ID = :p AND NUMERO = 1", new OracleParameter("p", p.Id));
            Sql.Executar("BEGIN PKG_GUIA.recalcular_guia(:p, :c); END;",
                new OracleParameter("p", p.Id), new OracleParameter("c", OracleDbType.Date) { Value = competencia });

            Assert.AreEqual(149.99m, Sql.ValorGuia(p.Id, competencia));
        }
    }
}

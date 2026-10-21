using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Caracterizacao
{
    [TestClass]
    public class CancelamentoTests
    {
        [TestMethod]
        public void Cancelar_DentroDoPrazo()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));

            string resposta = Ws.CancelarNfse(Ws.PedidoCancelar(p, 1, "Servico nao prestado"));

            DataTable nota = Sql.Tabela("SELECT SITUACAO, DT_CANCELAMENTO, MOTIVO_CANCELAMENTO FROM NFSE WHERE PRESTADOR_ID = :p",
                new OracleParameter("p", p.Id));
            Assert.AreEqual("C", nota.Rows[0]["SITUACAO"]);
            Assert.AreEqual("Servico nao prestado", nota.Rows[0]["MOTIVO_CANCELAMENTO"]);
            string dataHora = Convert.ToDateTime(nota.Rows[0]["DT_CANCELAMENTO"]).ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(
                "<CancelarNfseResposta><Cancelamento><Confirmacao><Pedido><InfPedidoCancelamento><IdentificacaoNfse>" +
                "<Numero>1</Numero><Cnpj>" + p.Cnpj + "</Cnpj><InscricaoMunicipal>" + p.InscricaoMunicipal + "</InscricaoMunicipal>" +
                "</IdentificacaoNfse><CodigoCancelamento>1</CodigoCancelamento></InfPedidoCancelamento></Pedido>" +
                "<DataHora>" + dataHora + "</DataHora></Confirmacao></Cancelamento></CancelarNfseResposta>", resposta);
        }

        [TestMethod]
        public void Cancelar_JaCancelada()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));
            string primeira = Ws.CancelarNfse(Ws.PedidoCancelar(p, 1));

            string segunda = Ws.CancelarNfse(Ws.PedidoCancelar(p, 1, "outro motivo"));

            Assert.AreEqual(primeira, segunda);
            Assert.AreEqual("Servico nao prestado", Sql.Escalar(
                "SELECT MOTIVO_CANCELAMENTO FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
        }

        [TestMethod]
        public void Cancelar_SemMotivo()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));

            Ws.CancelarNfse(Ws.PedidoCancelar(p, 1, ""));

            Assert.AreEqual("Cancelamento solicitado pelo contribuinte", Sql.Escalar(
                "SELECT MOTIVO_CANCELAMENTO FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
        }

        [TestMethod]
        public void Cancelar_GuiaPaga_E053()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));
            Sql.Executar("UPDATE GUIAS SET SITUACAO = 'P' WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id));

            Ws.AssertErro("E053", Ws.CancelarNfse(Ws.PedidoCancelar(p, 1)));
            Assert.AreEqual("N", Sql.Escalar("SELECT SITUACAO FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
        }

        [TestMethod]
        public void Cancelar_PedidoInvalido()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));

            Ws.AssertErro("E050", Ws.CancelarNfse(Ws.PedidoCancelar(p, 2)));
            Ws.AssertErro("E010", Ws.CancelarNfse(Ws.PedidoCancelar(p.Cnpj, "00000000", "1")));
            Ws.AssertErro("E001", Ws.CancelarNfse(Ws.PedidoCancelar(p.Cnpj, p.InscricaoMunicipal, "um")));
            Ws.AssertErro("E001", Ws.CancelarNfse("<CancelarNfseEnvio><Pedido/></CancelarNfseEnvio>"));
            Ws.AssertErro("E001", Ws.CancelarNfse("<CancelarNfseEnvio>"));
            Assert.AreEqual("N", Sql.Escalar("SELECT SITUACAO FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
        }
    }
}

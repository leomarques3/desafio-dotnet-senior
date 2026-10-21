using System;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Caracterizacao
{
    [TestClass]
    public class ConsultaNfseTests
    {
        [TestMethod]
        public void DataFinal_EhExclusiva()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));
            DateTime hoje = Sql.HojeBanco();

            Ws.AssertErro("E040", Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje)));
            Assert.AreEqual(1, Ws.Contar(Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1))), "//CompNfse"));
        }

        [TestMethod]
        public void Periodo_Maximo31Dias()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));
            DateTime hoje = Sql.HojeBanco();

            Assert.AreEqual(1, Ws.Contar(Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(31))), "//CompNfse"));
            Ws.AssertErro("E030", Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(32))));
            Ws.AssertErro("E030", Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(-1))));
            string semData = Ws.PedidoConsultarNfse(p, hoje, hoje).Replace("<DataInicial>", "<DataInicial>x");
            Ws.AssertErro("E030", Ws.ConsultarNfse(semData));
        }

        [TestMethod]
        public void Paginacao()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            LoteXml lote = new LoteXml(p);
            for (int i = 1; i <= 51; i++) lote.Rps(i);
            Ws.EnviarEProcessar(lote);
            DateTime hoje = Sql.HojeBanco();

            string pagina1 = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1), 1));
            string pagina2 = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1), 2));

            XmlNodeList numeros = Ws.Doc(pagina1).SelectNodes("//CompNfse/Nfse/InfNfse/Numero");
            Assert.AreEqual(50, numeros.Count);
            for (int i = 0; i < 50; i++) Assert.AreEqual((i + 1).ToString(), numeros[i].InnerText);
            Assert.AreEqual("true", Ws.Texto(pagina1, "/ConsultarNfseResposta/ProximaPagina"));
            Assert.AreEqual("51", Ws.Texto(pagina2, "//CompNfse/Nfse/InfNfse/Numero"));
            Assert.AreEqual("false", Ws.Texto(pagina2, "/ConsultarNfseResposta/ProximaPagina"));
            Ws.AssertErro("E040", Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1), 3)));
        }

        [TestMethod]
        public void FiltroPorTomador()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            string cnpj = Fabrica.NovoCnpj(zeroAEsquerda: true);
            string cpf = Fabrica.NovoCpf(zeroAEsquerda: true);
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, tomadorCnpj: cnpj).Rps(2, tomadorCpf: cpf).Rps(3));
            DateTime hoje = Sql.HojeBanco();

            string porCnpj = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1), tomadorCnpj: cnpj));
            Assert.AreEqual(1, Ws.Contar(porCnpj, "//CompNfse"));
            Assert.AreEqual(cnpj.TrimStart('0'), Ws.Texto(porCnpj, "//TomadorServico//Cnpj"));

            string porCpf = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1), tomadorCpf: cpf));
            Assert.AreEqual(1, Ws.Contar(porCpf, "//CompNfse"));
            Assert.AreEqual(cpf.TrimStart('0'), Ws.Texto(porCpf, "//TomadorServico//Cpf"));

            Ws.AssertErro("E020", Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1), tomadorCnpj: "11222333000182")));
        }

        [TestMethod]
        public void PrestadorInvalido_E010()
        {
            DateTime hoje = Sql.HojeBanco();
            PrestadorTeste fantasma = new PrestadorTeste { Cnpj = "11222333000181", InscricaoMunicipal = "00000000" };
            PrestadorTeste inativo = Fabrica.CriarPrestador(ativo: false);

            Ws.AssertErro("E010", Ws.ConsultarNfse(Ws.PedidoConsultarNfse(fantasma, hoje, hoje)));
            Ws.AssertErro("E010", Ws.ConsultarNfse(Ws.PedidoConsultarNfse(inativo, hoje, hoje)));
            Ws.AssertErro("E001", Ws.ConsultarNfse("<ConsultarNfseEnvio>"));
        }

        [TestMethod]
        public void NotaSubstituida()
        {
            DateTime emissao = Convert.ToDateTime(Sql.Escalar(
                "SELECT DT_EMISSAO FROM NFSE WHERE PRESTADOR_ID = 3 AND SITUACAO = 'S'"));
            decimal numero = Sql.Decimal("SELECT NUMERO FROM NFSE WHERE PRESTADOR_ID = 3 AND SITUACAO = 'S'");
            decimal substituidora = Sql.Decimal(
                "SELECT s.NUMERO FROM NFSE n JOIN NFSE s ON s.ID = n.NFSE_SUBSTITUIDORA_ID WHERE n.PRESTADOR_ID = 3 AND n.SITUACAO = 'S'");
            PrestadorTeste p3 = new PrestadorTeste { Cnpj = "11300009000134", InscricaoMunicipal = "10000003" };

            string resposta = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p3, emissao.Date, emissao.Date.AddDays(1)));

            XmlNode nota = Ws.Doc(resposta).SelectSingleNode("//CompNfse[Nfse/InfNfse/Numero='" + numero + "']/Nfse");
            Assert.IsNotNull(nota, resposta);
            Assert.AreEqual("<NfseSubstituicao><NfseSubstituidora>" + substituidora + "</NfseSubstituidora></NfseSubstituicao>",
                nota.SelectSingleNode("NfseSubstituicao").OuterXml);
        }

        [TestMethod]
        public void NotaCancelada()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1));
            Ws.CancelarNfse(Ws.PedidoCancelar(p, 1));
            DateTime cancelamento = Convert.ToDateTime(Sql.Escalar(
                "SELECT DT_CANCELAMENTO FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
            DateTime hoje = Sql.HojeBanco();

            string resposta = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1)));

            Assert.AreEqual(
                "<NfseCancelamento><Confirmacao><DataHora>" + cancelamento.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) +
                "</DataHora></Confirmacao></NfseCancelamento>",
                Ws.Doc(resposta).SelectSingleNode("//CompNfse/Nfse/NfseCancelamento").OuterXml);
        }
    }
}

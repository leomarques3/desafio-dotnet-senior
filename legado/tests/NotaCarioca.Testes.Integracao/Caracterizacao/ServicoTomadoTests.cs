using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;

namespace NotaCarioca.Testes.Integracao.Caracterizacao
{
    [TestClass]
    public class ServicoTomadoTests
    {
        [TestMethod]
        public void Tomador_VariosPrestadores()
        {
            string tomador = Fabrica.NovoCnpj(zeroAEsquerda: true);
            PrestadorTeste a = Fabrica.CriarPrestador();
            PrestadorTeste b = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(a).Rps(1, tomadorCnpj: tomador).Rps(2));
            Ws.EnviarEProcessar(new LoteXml(b).Rps(1, tomadorCnpj: tomador));
            DateTime hoje = Sql.HojeBanco();

            string resposta = Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado(tomador, null, hoje, hoje.AddDays(1)));

            Assert.AreEqual(2, Ws.Contar(resposta, "/ConsultarNfseServicoTomadoResposta/ListaNfse/CompNfse"));
            Assert.AreEqual(a.InscricaoMunicipal, Ws.Texto(resposta, "(//CompNfse)[1]//IdentificacaoPrestador/InscricaoMunicipal"));
            Assert.AreEqual(b.InscricaoMunicipal, Ws.Texto(resposta, "(//CompNfse)[2]//IdentificacaoPrestador/InscricaoMunicipal"));
            Assert.AreEqual(tomador.TrimStart('0'), Ws.Texto(resposta, "(//CompNfse)[1]//TomadorServico//Cnpj"));
            Assert.AreEqual("false", Ws.Texto(resposta, "/ConsultarNfseServicoTomadoResposta/ProximaPagina"));
        }

        [TestMethod]
        public void Tomador_SemConsulente_Cpf()
        {
            string cpf = Fabrica.NovoCpf(zeroAEsquerda: true);
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, tomadorCpf: cpf));
            DateTime hoje = Sql.HojeBanco();

            string resposta = Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado(null, cpf, hoje, hoje.AddDays(1), elemento: "Tomador"));

            Assert.AreEqual(1, Ws.Contar(resposta, "//CompNfse"));
            Assert.AreEqual(cpf.TrimStart('0'), Ws.Texto(resposta, "//TomadorServico//Cpf"));
        }

        [TestMethod]
        public void DataFinalExclusiva()
        {
            string tomador = Fabrica.NovoCnpj();
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, tomadorCnpj: tomador));
            DateTime hoje = Sql.HojeBanco();

            Ws.AssertErro("E040", Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado(tomador, null, hoje, hoje)));
            Ws.AssertErro("E040", Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado(Fabrica.NovoCnpj(), null, hoje, hoje.AddDays(1))));
        }

        [TestMethod]
        public void PedidoInvalido()
        {
            DateTime hoje = Sql.HojeBanco();

            Ws.AssertErro("E020", Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado("11222333000182", null, hoje, hoje)));
            Ws.AssertErro("E020", Ws.ConsultarNfseServicoTomado(
                "<ConsultarNfseServicoTomadoEnvio><PeriodoEmissao><DataInicial>2026-10-01</DataInicial>" +
                "<DataFinal>2026-10-02</DataFinal></PeriodoEmissao></ConsultarNfseServicoTomadoEnvio>"));
            Ws.AssertErro("E030", Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado("11222333000181", null, hoje, hoje.AddDays(32))));
            Ws.AssertErro("E001", Ws.ConsultarNfseServicoTomado("<ConsultarNfseEnvio/>"));
        }
    }
}

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.CnpjAlfanumerico
{
    [TestClass]
    public class CnpjAlfanumericoTests
    {
        [TestMethod]
        public void Lote_TomadorAlfanumerico()
        {
            string tomador = Fabrica.NovoCnpjAlfanumerico();
            PrestadorTeste p = Fabrica.CriarPrestador();
            string protocolo = Ws.EnviarEProcessar(new LoteXml(p).Rps(1, tomadorCnpj: tomador));

            string resposta = Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo));

            Assert.AreEqual(1, Ws.Contar(resposta, "//CompNfse"), resposta);
            Assert.AreEqual(tomador, Ws.Texto(resposta, "//TomadorServico//Cnpj"));
            Assert.AreEqual(tomador, Convert.ToString(Sql.Escalar("SELECT TOMADOR_DOCUMENTO FROM RPS WHERE PRESTADOR_ID = :p",
                new OracleParameter("p", p.Id))));
        }

        [TestMethod]
        public void ConsultarNfse_TomadorAlfanumerico()
        {
            string tomador = Fabrica.NovoCnpjAlfanumerico();
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1, tomadorCnpj: tomador).Rps(2));
            DateTime hoje = Sql.HojeBanco();

            string resposta = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1), tomadorCnpj: tomador.ToLowerInvariant()));

            Assert.AreEqual(1, Ws.Contar(resposta, "//CompNfse"), resposta);
            Assert.AreEqual(tomador, Ws.Texto(resposta, "//TomadorServico//Cnpj"));
        }

        [TestMethod]
        public void ServicoTomado_ConsulenteAlfanumerico()
        {
            string tomador = Fabrica.NovoCnpjAlfanumerico();
            PrestadorTeste a = Fabrica.CriarPrestador();
            PrestadorTeste b = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(a).Rps(1, tomadorCnpj: tomador));
            Ws.EnviarEProcessar(new LoteXml(b).Rps(1, tomadorCnpj: tomador).Rps(2));
            DateTime hoje = Sql.HojeBanco();

            string resposta = Ws.ConsultarNfseServicoTomado(Ws.PedidoServicoTomado(tomador, null, hoje, hoje.AddDays(1)));

            Assert.AreEqual(2, Ws.Contar(resposta, "//CompNfse"), resposta);
        }

        [TestMethod]
        public void Prestador_Alfanumerico()
        {
            PrestadorTeste p = Fabrica.CriarPrestador(cnpj: Fabrica.NovoCnpjAlfanumerico());
            Ws.EnviarEProcessar(new LoteXml(p).Rps(1).Rps(2));
            DateTime hoje = Sql.HojeBanco();

            string consulta = Ws.ConsultarNfse(Ws.PedidoConsultarNfse(p, hoje, hoje.AddDays(1)));
            string cancelamento = Ws.CancelarNfse(Ws.PedidoCancelar(p, 1));

            Assert.AreEqual(2, Ws.Contar(consulta, "//CompNfse"), consulta);
            Assert.AreEqual(p.Cnpj, Ws.Texto(consulta, "(//PrestadorServico//Cnpj)[1]"));
            Assert.AreEqual(p.Cnpj, Ws.Texto(cancelamento, "//IdentificacaoNfse/Cnpj"), cancelamento);
        }
    }
}

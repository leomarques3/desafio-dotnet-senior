using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;

namespace NotaCarioca.Testes.Integracao.Caracterizacao
{
    [TestClass]
    public class ExemplosTests
    {
        [TestCleanup]
        public void ProcessarPendentes()
        {
            Ws.ProcessarPendentes();
        }

        [DataTestMethod]
        [DataRow("02-consultar-situacao-lote-rps")]
        [DataRow("03-consultar-lote-rps")]
        [DataRow("03b-consultar-lote-rps-com-erros")]
        [DataRow("03c-consultar-lote-rps-em-processamento")]
        [DataRow("04-consultar-nfse")]
        [DataRow("04b-consultar-nfse-periodo-curto")]
        [DataRow("04c-consultar-nfse-periodo-curto-mais-um-dia")]
        [DataRow("04d-consultar-nfse-sem-resultado")]
        [DataRow("04e-consultar-nfse-por-tomador")]
        [DataRow("06c-cancelar-nfse-prazo-expirado")]
        [DataRow("06d-cancelar-nfse-substituida")]
        [DataRow("06e-cancelar-nfse-inexistente")]
        public void Exemplo(string exemplo)
        {
            string pedido = Exemplos.Pedido(exemplo);
            string resposta;
            switch (exemplo.Substring(0, 2))
            {
                case "02": resposta = Ws.ConsultarSituacaoLoteRps(pedido); break;
                case "03": resposta = Ws.ConsultarLoteRps(pedido); break;
                case "04": resposta = Ws.ConsultarNfse(pedido); break;
                default: resposta = Ws.CancelarNfse(pedido); break;
            }

            Assert.AreEqual(Exemplos.Normalizar(Exemplos.Resposta(exemplo)), Exemplos.Normalizar(resposta));
        }

        [TestMethod]
        public void Exemplo01_RecepcionarLote()
        {
            string resposta = Ws.RecepcionarLoteRps(Exemplos.Pedido("01-recepcionar-lote-rps"));

            Assert.AreEqual(Exemplos.Normalizar(Exemplos.Resposta("01-recepcionar-lote-rps")), Exemplos.Normalizar(resposta));
        }
    }
}

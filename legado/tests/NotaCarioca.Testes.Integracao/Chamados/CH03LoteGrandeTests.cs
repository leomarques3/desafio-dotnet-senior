using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;
using NotaCarioca.Testes.Integracao.Infra;

namespace NotaCarioca.Testes.Integracao.Chamados
{
    [TestClass]
    public class CH03LoteGrandeTests
    {
        [TestMethod]
        public void Lote_MilharesDeErros()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            LoteXml lote = new LoteXml(p);
            for (int i = 1; i <= 5000; i++) lote.Rps(i, valor: 0m);
            string protocolo = Ws.Enviar(lote);

            long antes = GC.GetAllocatedBytesForCurrentThread();
            new ProcessadorLoteRps().ProcessarLote(protocolo);
            long alocado = GC.GetAllocatedBytesForCurrentThread() - antes;

            string resposta = Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo));
            Assert.AreEqual(5000, Ws.Contar(resposta, "//ListaMensagemRetorno/MensagemRetorno"));
            Assert.IsTrue(alocado < 200L * 1024 * 1024, (alocado / 1048576) + " MB alocados");
        }
    }
}

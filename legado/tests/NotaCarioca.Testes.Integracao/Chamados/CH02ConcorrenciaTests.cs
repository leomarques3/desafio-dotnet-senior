using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dados;
using NotaCarioca.Servicos;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Chamados
{
    [TestClass]
    public class CH02ConcorrenciaTests
    {
        private static readonly TimeSpan Espera = TimeSpan.FromSeconds(2);

        [TestCleanup]
        public void ProcessarPendentes()
        {
            Ws.ProcessarPendentes();
        }

        [TestMethod]
        public void Numeracao_Concorrente()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            long numeroB = 0;

            using (OracleConnection a = BancoOracle.Conectar())
            using (OracleTransaction txA = a.BeginTransaction())
            {
                long numeroA = Emitir(a, txA, p.Id, 0);

                Task b = Task.Run(() =>
                {
                    using (OracleConnection cb = BancoOracle.Conectar())
                    using (OracleTransaction txB = cb.BeginTransaction())
                    {
                        numeroB = Emitir(cb, txB, p.Id, 0);
                        txB.Commit();
                    }
                });
                Assert.IsFalse(b.Wait(Espera), "a segunda emissao deveria esperar a primeira");

                txA.Commit();
                b.GetAwaiter().GetResult();
                Assert.AreEqual(1, numeroA);
            }

            Assert.AreEqual(2, numeroB);
            Assert.IsTrue(Invariantes.NumeracaoSemLacunas(p.Id));
        }

        [TestMethod]
        public void Rps_Concorrente()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            RpsRepositorio rps = new RpsRepositorio();

            using (OracleConnection a = BancoOracle.Conectar())
            using (OracleConnection b = BancoOracle.Conectar())
            using (OracleTransaction txA = a.BeginTransaction())
            using (OracleTransaction txB = b.BeginTransaction())
            {
                Assert.IsFalse(rps.Existe(a, txA, p.Id, "C1", 7));
                long rpsA = rps.Inserir(a, txA, p.Id, 7, "C1", 1, DateTime.Now, "NC-A", "11222333000181", "T");
                Emitir(a, txA, p.Id, rpsA);

                Assert.IsFalse(rps.Existe(b, txB, p.Id, "C1", 7));
                txA.Commit();

                try
                {
                    long rpsB = rps.Inserir(b, txB, p.Id, 7, "C1", 1, DateTime.Now, "NC-B", "11222333000181", "T");
                    Emitir(b, txB, p.Id, rpsB);
                    txB.Commit();
                }
                catch (OracleException ex)
                {
                    Assert.AreEqual(1, ex.Number, ex.Message);
                }
            }

            Assert.AreEqual(0, Invariantes.RpsDuplicados(p.Id));
            Assert.AreEqual(1m, Sql.Decimal("SELECT COUNT(*) FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
        }

        [TestMethod]
        public void Guia_Concorrente()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            DateTime competencia = Sql.CompetenciaAtual();

            using (OracleConnection a = BancoOracle.Conectar())
            using (OracleTransaction txA = a.BeginTransaction())
            {
                RecalcularGuia(a, txA, p.Id, competencia);

                Task b = Task.Run(() =>
                {
                    using (OracleConnection cb = BancoOracle.Conectar())
                    using (OracleTransaction txB = cb.BeginTransaction())
                    {
                        RecalcularGuia(cb, txB, p.Id, competencia);
                        txB.Commit();
                    }
                });
                Assert.IsFalse(b.Wait(Espera), "o segundo recalculo deveria esperar o primeiro");

                txA.Commit();
                b.GetAwaiter().GetResult();
            }

            Assert.AreEqual(1m, Sql.Decimal("SELECT COUNT(*) FROM GUIAS WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
        }

        [TestMethod]
        public void Lotes_Simultaneos()
        {
            PrestadorTeste semErro = Fabrica.CriarPrestador();
            PrestadorTeste umErro = Fabrica.CriarPrestador();
            PrestadorTeste soErros = Fabrica.CriarPrestador();
            LoteXml loteSemErro = new LoteXml(semErro);
            LoteXml loteUmErro = new LoteXml(umErro);
            LoteXml loteSoErros = new LoteXml(soErros);
            for (int i = 1; i <= 40; i++)
            {
                loteSemErro.Rps(i);
                loteUmErro.Rps(i, item: i == 40 ? "99.99" : "17.01");
                loteSoErros.Rps(i, valor: 0m);
            }
            string protocoloSemErro = Ws.Enviar(loteSemErro);
            string protocoloUmErro = Ws.Enviar(loteUmErro);
            string protocoloSoErros = Ws.Enviar(loteSoErros);

            Ws.ProcessarPendentes();

            ConferirLote(semErro, protocoloSemErro, 40, 0);
            ConferirLote(umErro, protocoloUmErro, 39, 1);
            ConferirLote(soErros, protocoloSoErros, 0, 40);
        }

        [TestMethod]
        public void MesmoLote_DuasVezes()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            string protocolo = Ws.Enviar(new LoteXml(p).Rps(1).Rps(2));
            ProcessadorLoteRps processador = new ProcessadorLoteRps();

            processador.ProcessarLote(protocolo);
            processador.ProcessarLote(protocolo);

            ConferirLote(p, protocolo, 2, 0);
        }

        private static void ConferirLote(PrestadorTeste p, string protocolo, int notas, int erros)
        {
            string resposta = Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo));
            Assert.AreEqual(notas, Ws.Contar(resposta, "//ListaNfse/CompNfse"), protocolo);
            Assert.AreEqual(erros, Ws.Contar(resposta, "//ListaMensagemRetorno/MensagemRetorno"), protocolo);
        }

        private static long Emitir(OracleConnection conn, OracleTransaction tx, long prestadorId, long rpsId)
        {
            long id, numero;
            string codigo;
            new NfseRepositorio().GerarNfse(conn, tx, rpsId, prestadorId, Sql.CompetenciaAtual(), "17.01", "concorrencia",
                100m, 0m, 0.05m, 5m, false, "11222333000181", "T", out id, out numero, out codigo);
            return numero;
        }

        private static void RecalcularGuia(OracleConnection conn, OracleTransaction tx, long prestadorId, DateTime competencia)
        {
            using (OracleCommand cmd = OracleHelper.CriarProcedure(conn, tx, "PKG_GUIA.RECALCULAR_GUIA"))
            {
                cmd.Parameters.Add(OracleHelper.P("p_prestador_id", OracleDbType.Int64, prestadorId));
                cmd.Parameters.Add(OracleHelper.P("p_competencia", OracleDbType.Date, competencia));
                cmd.ExecuteNonQuery();
            }
        }
    }
}

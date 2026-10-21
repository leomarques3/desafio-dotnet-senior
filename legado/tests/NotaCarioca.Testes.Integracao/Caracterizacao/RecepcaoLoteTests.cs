using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Testes.Integracao.Infra;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Caracterizacao
{
    [TestClass]
    public class RecepcaoLoteTests
    {
        [TestCleanup]
        public void ProcessarPendentes()
        {
            Ws.ProcessarPendentes();
        }

        [TestMethod]
        public void Recepcao_XmlMalFormado_E001()
        {
            string resposta = Ws.RecepcionarLoteRps("<EnviarLoteRpsEnvio><LoteRps>");

            Assert.AreEqual(
                "<EnviarLoteRpsResposta><ListaMensagemRetorno><MensagemRetorno><Codigo>E001</Codigo>" +
                "<Mensagem>XML mal formado ou inválido.</Mensagem><Correcao>Verifique a estrutura do XML enviado.</Correcao>" +
                "</MensagemRetorno></ListaMensagemRetorno></EnviarLoteRpsResposta>", resposta);
        }

        [TestMethod]
        public void Recepcao_RaizErrada_E001()
        {
            Ws.AssertErro("E001", Ws.RecepcionarLoteRps("<OutroEnvio><LoteRps/></OutroEnvio>"));
        }

        [TestMethod]
        public void Recepcao_InscricaoInexistente_E010()
        {
            PrestadorTeste fantasma = new PrestadorTeste { Cnpj = "11222333000181", InscricaoMunicipal = "00000000" };

            Ws.AssertErro("E010", Ws.RecepcionarLoteRps(new LoteXml(fantasma).Rps(1).ToString()));
        }

        [TestMethod]
        public void Recepcao_PrestadorInativo_E010()
        {
            PrestadorTeste inativo = Fabrica.CriarPrestador(ativo: false);

            Ws.AssertErro("E010", Ws.RecepcionarLoteRps(new LoteXml(inativo).Rps(1).ToString()));
        }

        [TestMethod]
        public void Recepcao_LoteValido()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();

            string resposta = Ws.RecepcionarLoteRps(new LoteXml(p, 37).Rps(1).Rps(2).ToString());

            Assert.AreEqual("37", Ws.Texto(resposta, "/EnviarLoteRpsResposta/NumeroLote"));
            string protocolo = Ws.Texto(resposta, "/EnviarLoteRpsResposta/Protocolo");
            Assert.IsTrue(Ws.CasaFormato(protocolo, "^NC" + Sql.AgoraBanco().ToString("yyyyMM", CultureInfo.InvariantCulture) + "\\d{8}$"), protocolo);
            Assert.IsTrue(Ws.CasaFormato(Ws.Texto(resposta, "/EnviarLoteRpsResposta/DataRecebimento"),
                "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}$"), resposta);
            Assert.AreEqual(2m, Sql.Decimal("SELECT QTD_RPS FROM LOTES_RPS WHERE PROTOCOLO = :p", new OracleParameter("p", protocolo)));

            string situacao = Ws.ConsultarSituacaoLoteRps(Ws.PedidoConsultarLote(p, protocolo, "ConsultarSituacaoLoteRpsEnvio"));
            Assert.AreEqual("<ConsultarSituacaoLoteRpsResposta><NumeroLote>37</NumeroLote><Situacao>1</Situacao></ConsultarSituacaoLoteRpsResposta>", situacao);
            Ws.AssertErro("E071", Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo)));
        }

        [TestMethod]
        public void Lote_SemErros()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            DateTime agora = Sql.AgoraBanco();
            string tomadorZero = Fabrica.NovoCnpj(zeroAEsquerda: true);
            string tomadorCpf = Fabrica.NovoCpf();
            LoteXml lote = new LoteXml(p, 5)
                .Rps(1, valor: 1500m, item: "17.01", tomadorCnpj: tomadorZero, dataEmissao: agora)
                .Rps(2, valor: 320.50m, deducoes: 20.50m, item: "01.07", retido: true, tomadorCpf: tomadorCpf, dataEmissao: agora)
                .Rps(3, valor: 1000m, item: "01.01", dataEmissao: agora);

            string protocolo = Ws.EnviarEProcessar(lote);

            Assert.AreEqual("<ConsultarSituacaoLoteRpsResposta><NumeroLote>5</NumeroLote><Situacao>4</Situacao></ConsultarSituacaoLoteRpsResposta>",
                Ws.ConsultarSituacaoLoteRps(Ws.PedidoConsultarLote(p, protocolo, "ConsultarSituacaoLoteRpsEnvio")));

            string resposta = Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo));
            Assert.AreEqual(3, Ws.Contar(resposta, "/ConsultarLoteRpsResposta/ListaNfse/CompNfse"));
            Assert.AreEqual(0, Ws.Contar(resposta, "//ListaMensagemRetorno"));

            string competencia = agora.ToString("yyyy-MM-01'T'00:00:00", CultureInfo.InvariantCulture);
            string esperado =
                "<ConsultarLoteRpsResposta><ListaNfse>" +
                Nota(p, 1, competencia, "1500.00", "0.00", "1500.00", "0.05", "75.00", "2", "17.01", "Servico de teste 1", "Cnpj", tomadorZero.TrimStart('0')) +
                Nota(p, 2, competencia, "320.50", "20.50", "300.00", "0.03", "9.00", "1", "01.07", "Servico de teste 2", "Cpf", tomadorCpf) +
                Nota(p, 3, competencia, "1000.00", "0.00", "1000.00", "0.02", "20.00", "2", "01.01", "Servico de teste 3", "Cnpj", "11222333000181") +
                "</ListaNfse></ConsultarLoteRpsResposta>";
            Assert.AreEqual(esperado, Exemplos.NormalizarEmissao(resposta));

            DateTime comp = new DateTime(agora.Year, agora.Month, 1);
            Assert.AreEqual(95.00m, Sql.ValorGuia(p.Id, comp));
            Assert.AreEqual(comp.AddMonths(1).AddDays(9), Convert.ToDateTime(Sql.Escalar(
                "SELECT DT_VENCIMENTO FROM GUIAS WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id))));
        }

        [TestMethod]
        public void Lote_ComErros()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            LoteXml lote = new LoteXml(p)
                .Rps(1, serie: "A1")
                .Rps(2, serie: "A1", tomadorCnpj: "11222333000182")
                .Rps(3, serie: "A1", item: "99.99")
                .Rps(4, serie: "A1", valor: 0m)
                .Rps(5, serie: "A1", valor: 100m, deducoes: 150m)
                .Rps(1, serie: "A1");

            string protocolo = Ws.EnviarEProcessar(lote);

            Assert.AreEqual("3", Ws.Texto(Ws.ConsultarSituacaoLoteRps(Ws.PedidoConsultarLote(p, protocolo, "ConsultarSituacaoLoteRpsEnvio")),
                "//Situacao"));
            string resposta = Exemplos.NormalizarEmissao(Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo)));
            Assert.AreEqual(1, Ws.Contar(resposta, "//ListaNfse/CompNfse"));
            string erros = resposta.Substring(resposta.IndexOf("<ListaMensagemRetorno>", StringComparison.Ordinal));
            Assert.AreEqual(
                "<ListaMensagemRetorno>" +
                Mensagem(2, "A1", "E020", "CPF/CNPJ inválido.", "Informe um CPF ou CNPJ válido.") +
                Mensagem(3, "A1", "E061", "Item de serviço inexistente.", "Informe um item da lista de serviços (LC 116/2003).") +
                Mensagem(4, "A1", "E062", "Valor de serviços inválido.", "O valor de serviços deve ser maior que zero e maior ou igual às deduções.") +
                Mensagem(5, "A1", "E062", "Valor de serviços inválido.", "O valor de serviços deve ser maior que zero e maior ou igual às deduções.") +
                Mensagem(1, "A1", "E060", "RPS já informado.", "Um RPS só pode originar uma NFS-e.") +
                "</ListaMensagemRetorno></ConsultarLoteRpsResposta>", erros);
        }

        [TestMethod]
        public void Lote_RpsRepetido_E060()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();
            Ws.EnviarEProcessar(new LoteXml(p, 1).Rps(10, serie: "R").Rps(11, serie: "R"));

            string protocolo = Ws.EnviarEProcessar(new LoteXml(p, 2).Rps(11, serie: "R").Rps(12, serie: "R"));

            string resposta = Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo));
            Assert.AreEqual(1, Ws.Contar(resposta, "//ListaNfse/CompNfse"));
            Assert.AreEqual("3", Ws.Texto(resposta, "//CompNfse//Numero"));
            Assert.AreEqual("11", Ws.Texto(resposta, "//MensagemRetorno/IdentificacaoRps/Numero"));
            Ws.AssertErro("E060", resposta);
            Assert.AreEqual(3m, Sql.Decimal("SELECT COUNT(*) FROM NFSE WHERE PRESTADOR_ID = :p", new OracleParameter("p", p.Id)));
        }

        [TestMethod]
        public void Lote_Mei_IssZero()
        {
            PrestadorTeste p = Fabrica.CriarPrestador(regime: "M");

            string protocolo = Ws.EnviarEProcessar(new LoteXml(p).Rps(1, valor: 2500m, item: "17.01"));

            string resposta = Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, protocolo));
            Assert.AreEqual("0", Ws.Texto(resposta, "//Aliquota"));
            Assert.AreEqual("0.00", Ws.Texto(resposta, "//ValorIss"));
            Assert.AreEqual(0m, Sql.ValorGuia(p.Id, Sql.CompetenciaAtual()));
        }

        [TestMethod]
        public void ConsultarLote_ProtocoloDeOutro_E070()
        {
            PrestadorTeste p = Fabrica.CriarPrestador();

            Ws.AssertErro("E070", Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, "NC20990100000001")));
            Ws.AssertErro("E070", Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(p, "NC20261000000001")));
            Ws.AssertErro("E070", Ws.ConsultarSituacaoLoteRps(Ws.PedidoConsultarLote(p, "NC20261000000001", "ConsultarSituacaoLoteRpsEnvio")));
        }

        [TestMethod]
        public void ConsultarLote_PedidoInvalido()
        {
            PrestadorTeste fantasma = new PrestadorTeste { Cnpj = "11222333000181", InscricaoMunicipal = "00000000" };

            Ws.AssertErro("E010", Ws.ConsultarLoteRps(Ws.PedidoConsultarLote(fantasma, "NC20261000000001")));
            Ws.AssertErro("E001", Ws.ConsultarLoteRps("<Outro/>"));
            Ws.AssertErro("E001", Ws.ConsultarSituacaoLoteRps("<ConsultarSituacaoLoteRpsEnvio>"));
        }

        private static string Nota(PrestadorTeste p, int numero, string competencia, string valor, string deducoes, string baseCalculo,
                                   string aliquota, string iss, string retido, string item, string discriminacao, string tagTomador, string tomador)
        {
            return "<CompNfse><Nfse><InfNfse><Numero>" + numero + "</Numero><CodigoVerificacao>X</CodigoVerificacao>" +
                   "<DataEmissao>X</DataEmissao><Competencia>" + competencia + "</Competencia><Servico><Valores>" +
                   "<ValorServicos>" + valor + "</ValorServicos><ValorDeducoes>" + deducoes + "</ValorDeducoes>" +
                   "<BaseCalculo>" + baseCalculo + "</BaseCalculo><Aliquota>" + aliquota + "</Aliquota><ValorIss>" + iss + "</ValorIss>" +
                   "<IssRetido>" + retido + "</IssRetido></Valores><ItemListaServico>" + item + "</ItemListaServico>" +
                   "<Discriminacao>" + discriminacao + "</Discriminacao></Servico><PrestadorServico><IdentificacaoPrestador>" +
                   "<Cnpj>" + p.Cnpj + "</Cnpj><InscricaoMunicipal>" + p.InscricaoMunicipal + "</InscricaoMunicipal></IdentificacaoPrestador>" +
                   "<RazaoSocial>" + p.RazaoSocial + "</RazaoSocial></PrestadorServico><TomadorServico><IdentificacaoTomador><CpfCnpj>" +
                   "<" + tagTomador + ">" + tomador + "</" + tagTomador + "></CpfCnpj></IdentificacaoTomador>" +
                   "<RazaoSocial>TOMADOR TESTE</RazaoSocial></TomadorServico></InfNfse></Nfse></CompNfse>";
        }

        private static string Mensagem(int numero, string serie, string codigo, string mensagem, string correcao)
        {
            return "<MensagemRetorno><IdentificacaoRps><Numero>" + numero + "</Numero><Serie>" + serie + "</Serie></IdentificacaoRps>" +
                   "<Codigo>" + codigo + "</Codigo><Mensagem>" + mensagem + "</Mensagem><Correcao>" + correcao + "</Correcao></MensagemRetorno>";
        }
    }
}

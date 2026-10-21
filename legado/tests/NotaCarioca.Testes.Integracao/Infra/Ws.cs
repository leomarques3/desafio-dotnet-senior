using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;

namespace NotaCarioca.Testes.Integracao.Infra
{
    public static class Ws
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string RecepcionarLoteRps(string xml) { return new RecepcaoLoteServico().Recepcionar(xml); }
        public static string ConsultarSituacaoLoteRps(string xml) { return new ConsultaNfseServico().ConsultarSituacaoLoteRps(xml); }
        public static string ConsultarLoteRps(string xml) { return new ConsultaNfseServico().ConsultarLoteRps(xml); }
        public static string ConsultarNfse(string xml) { return new ConsultaNfseServico().ConsultarNfse(xml); }
        public static string ConsultarNfseServicoTomado(string xml) { return new ConsultaNfseServico().ConsultarNfseServicoTomado(xml); }
        public static string CancelarNfse(string xml) { return new CancelamentoNfseServico().Cancelar(xml); }

        public static void ProcessarPendentes()
        {
            new ProcessadorLoteRps().ProcessarPendentes();
        }

        public static string Enviar(LoteXml lote)
        {
            string resposta = RecepcionarLoteRps(lote.ToString());
            string protocolo = Texto(resposta, "/EnviarLoteRpsResposta/Protocolo");
            Assert.IsNotNull(protocolo, "lote nao foi recebido: " + resposta);
            return protocolo;
        }

        public static string EnviarEProcessar(LoteXml lote)
        {
            string protocolo = Enviar(lote);
            ProcessarPendentes();
            return protocolo;
        }

        public static string PedidoConsultarLote(PrestadorTeste p, string protocolo, string raiz = "ConsultarLoteRpsEnvio")
        {
            return "<" + raiz + "><Prestador><Cnpj>" + p.Cnpj + "</Cnpj><InscricaoMunicipal>" + p.InscricaoMunicipal +
                   "</InscricaoMunicipal></Prestador><Protocolo>" + protocolo + "</Protocolo></" + raiz + ">";
        }

        public static string PedidoConsultarNfse(PrestadorTeste p, DateTime ini, DateTime fim, int pagina = 1,
                                                 string tomadorCnpj = null, string tomadorCpf = null)
        {
            string tomador = "";
            if (tomadorCnpj != null) tomador = "<Tomador><CpfCnpj><Cnpj>" + tomadorCnpj + "</Cnpj></CpfCnpj></Tomador>";
            if (tomadorCpf != null) tomador = "<Tomador><CpfCnpj><Cpf>" + tomadorCpf + "</Cpf></CpfCnpj></Tomador>";
            return "<ConsultarNfseEnvio><Prestador><Cnpj>" + p.Cnpj + "</Cnpj><InscricaoMunicipal>" + p.InscricaoMunicipal +
                   "</InscricaoMunicipal></Prestador>" + Periodo(ini, fim) + tomador + "<Pagina>" + pagina + "</Pagina></ConsultarNfseEnvio>";
        }

        public static string PedidoServicoTomado(string cnpj, string cpf, DateTime ini, DateTime fim, int pagina = 1, string elemento = "Consulente")
        {
            string doc = cnpj != null ? "<Cnpj>" + cnpj + "</Cnpj>" : "<Cpf>" + cpf + "</Cpf>";
            return "<ConsultarNfseServicoTomadoEnvio><" + elemento + "><CpfCnpj>" + doc + "</CpfCnpj></" + elemento + ">" +
                   Periodo(ini, fim) + "<Pagina>" + pagina + "</Pagina></ConsultarNfseServicoTomadoEnvio>";
        }

        public static string PedidoCancelar(PrestadorTeste p, long numero, string motivo = "Servico nao prestado")
        {
            return PedidoCancelar(p.Cnpj, p.InscricaoMunicipal, numero.ToString(Inv), motivo);
        }

        public static string PedidoCancelar(string cnpj, string im, string numero, string motivo = "Servico nao prestado")
        {
            return "<CancelarNfseEnvio><Pedido><InfPedidoCancelamento><IdentificacaoNfse><Numero>" + numero + "</Numero>" +
                   "<Cnpj>" + cnpj + "</Cnpj><InscricaoMunicipal>" + im + "</InscricaoMunicipal></IdentificacaoNfse>" +
                   "<CodigoCancelamento>1</CodigoCancelamento><Motivo>" + motivo + "</Motivo></InfPedidoCancelamento></Pedido></CancelarNfseEnvio>";
        }

        private static string Periodo(DateTime ini, DateTime fim)
        {
            return "<PeriodoEmissao><DataInicial>" + ini.ToString("yyyy-MM-dd", Inv) + "</DataInicial><DataFinal>" +
                   fim.ToString("yyyy-MM-dd", Inv) + "</DataFinal></PeriodoEmissao>";
        }


        public static XmlDocument Doc(string xml)
        {
            XmlDocument d = new XmlDocument();
            d.LoadXml(xml);
            return d;
        }

        public static string Texto(string xml, string xpath)
        {
            XmlNode n = Doc(xml).SelectSingleNode(xpath);
            return n == null ? null : n.InnerText;
        }

        public static int Contar(string xml, string xpath)
        {
            return Doc(xml).SelectNodes(xpath).Count;
        }

        public static string CodigoErro(string xml)
        {
            return Texto(xml, "//ListaMensagemRetorno/MensagemRetorno/Codigo");
        }

        public static void AssertErro(string codigoEsperado, string resposta)
        {
            Assert.AreEqual(codigoEsperado, CodigoErro(resposta), resposta);
        }

        public static bool CasaFormato(string valor, string regex)
        {
            return valor != null && Regex.IsMatch(valor, regex);
        }
    }
}

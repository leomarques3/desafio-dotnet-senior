using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace NotaCarioca.Testes.Integracao.Infra
{
    public static class Exemplos
    {
        public static string Pedido(string nome)
        {
            return Extrair(Ler(nome + ".xml"), "inputXML");
        }

        public static string Resposta(string nome)
        {
            return Extrair(Ler(nome + ".resposta.xml"), "outputXML");
        }

        public static string Normalizar(string xml)
        {
            string t = Regex.Replace(xml, "<(DataRecebimento|DataHora|Protocolo)>[^<]*<", "<$1>X<");
            t = Regex.Replace(t, "NC\\d{14}", "NCX");
            return t;
        }

        public static string NormalizarEmissao(string xml)
        {
            string t = Normalizar(xml);
            t = Regex.Replace(t, "<(DataEmissao|CodigoVerificacao)>[^<]*<", "<$1>X<");
            return t;
        }

        private static string Ler(string arquivo)
        {
            return File.ReadAllText(Path.Combine(BancoOracle.RaizRepositorio(), "tools", "exemplos", arquivo));
        }

        private static string Extrair(string envelope, string elemento)
        {
            Match m = Regex.Match(envelope, "<" + elemento + ">(.*)</" + elemento + ">", RegexOptions.Singleline);
            return WebUtility.HtmlDecode(m.Groups[1].Value);
        }
    }
}

using System;
using System.Globalization;
using System.Security;
using System.Text;

namespace NotaCarioca.Testes.Integracao.Infra
{
    public class LoteXml
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly PrestadorTeste _prestador;
        private readonly long _numeroLote;
        private readonly StringBuilder _rps = new StringBuilder();
        private int _quantidade;
        private DateTime? _agoraBanco;

        public LoteXml(PrestadorTeste prestador, long numeroLote = 1)
        {
            _prestador = prestador;
            _numeroLote = numeroLote;
        }

        public LoteXml Rps(long numero, string serie = "T1", decimal valor = 1000m, decimal deducoes = 0m,
                           bool retido = false, string item = "17.01", string tomadorCnpj = null, string tomadorCpf = null)
        {
            if (tomadorCnpj == null && tomadorCpf == null) tomadorCnpj = "11222333000181";
            if (_agoraBanco == null) _agoraBanco = Sql.AgoraBanco();
            DateTime data = _agoraBanco.Value;
            _quantidade++;
            _rps.Append("<Rps><InfRps Id=\"rps").Append(numero).Append("\">")
                .Append("<IdentificacaoRps><Numero>").Append(numero).Append("</Numero><Serie>").Append(Esc(serie))
                .Append("</Serie><Tipo>1</Tipo></IdentificacaoRps>")
                .Append("<DataEmissao>").Append(data.ToString("yyyy-MM-dd'T'HH:mm:ss", Inv)).Append("</DataEmissao>")
                .Append("<Servico><Valores><ValorServicos>").Append(valor.ToString("0.00", Inv))
                .Append("</ValorServicos><ValorDeducoes>").Append(deducoes.ToString("0.00", Inv))
                .Append("</ValorDeducoes><IssRetido>").Append(retido ? "1" : "2").Append("</IssRetido></Valores>")
                .Append("<ItemListaServico>").Append(Esc(item)).Append("</ItemListaServico>")
                .Append("<Discriminacao>").Append("Servico de teste " + numero).Append("</Discriminacao></Servico>")
                .Append("<Tomador><IdentificacaoTomador><CpfCnpj>");
            if (tomadorCnpj != null) _rps.Append("<Cnpj>").Append(Esc(tomadorCnpj)).Append("</Cnpj>");
            else _rps.Append("<Cpf>").Append(Esc(tomadorCpf)).Append("</Cpf>");
            _rps.Append("</CpfCnpj></IdentificacaoTomador><RazaoSocial>TOMADOR TESTE</RazaoSocial></Tomador>")
                .Append("</InfRps></Rps>");
            return this;
        }

        public override string ToString()
        {
            return "<EnviarLoteRpsEnvio><LoteRps Id=\"lote" + _numeroLote + "\"><NumeroLote>" + _numeroLote + "</NumeroLote>" +
                   "<Prestador><Cnpj>" + _prestador.Cnpj + "</Cnpj><InscricaoMunicipal>" + _prestador.InscricaoMunicipal +
                   "</InscricaoMunicipal></Prestador><QuantidadeRps>" + _quantidade + "</QuantidadeRps><ListaRps>" +
                   _rps + "</ListaRps></LoteRps></EnviarLoteRpsEnvio>";
        }

        private static string Esc(string s)
        {
            return SecurityElement.Escape(s);
        }
    }
}

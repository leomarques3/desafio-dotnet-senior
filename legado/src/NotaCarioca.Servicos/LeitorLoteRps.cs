using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Servicos
{
    public class LoteLido
    {
        public long NumeroLote;
        public string Cnpj;
        public string InscricaoMunicipal;
        public int QuantidadeInformada;
    }

    public static class LeitorLoteRps
    {
        public static XmlDocument Carregar(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
        }

        public static string Texto(XmlNode no, string caminho)
        {
            XmlNode n = no.SelectSingleNode(caminho);
            if (n == null) return null;
            return n.InnerText.Trim();
        }

        public static decimal Decimal(string s)
        {
            decimal d;
            if (s != null && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return d;
            return -1m;
        }

        public static LoteLido LerCabecalho(XmlDocument doc)
        {
            XmlNode raiz = doc.DocumentElement;
            if (raiz == null || raiz.Name != "EnviarLoteRpsEnvio")
                throw new XmlException("Elemento raiz inesperado.");
            XmlNode lote = raiz.SelectSingleNode("LoteRps");
            if (lote == null)
                throw new XmlException("LoteRps ausente.");

            LoteLido l = new LoteLido();
            long numero;
            long.TryParse(Texto(lote, "NumeroLote") ?? "0", out numero);
            l.NumeroLote = numero;
            l.Cnpj = Texto(lote, "Prestador/Cnpj");
            l.InscricaoMunicipal = Texto(lote, "Prestador/InscricaoMunicipal");
            int q;
            int.TryParse(Texto(lote, "QuantidadeRps") ?? "0", out q);
            l.QuantidadeInformada = q;
            return l;
        }

        public static XmlReader CriarLeitor(TextReader texto)
        {
            XmlReaderSettings cfg = new XmlReaderSettings();
            cfg.DtdProcessing = DtdProcessing.Parse;
            cfg.XmlResolver = null;
            cfg.IgnoreWhitespace = true;
            cfg.CloseInput = true;
            return XmlReader.Create(texto, cfg);
        }

        public static LoteLido LerCabecalho(string xml, out int qtdRps)
        {
            XmlDocument cab = new XmlDocument();
            XmlNode lote = null;
            bool primeiroLote = false;
            string[] caminho = new string[4];
            qtdRps = 0;
            using (XmlReader r = CriarLeitor(new StringReader(xml)))
            {
                r.Read();
                while (!r.EOF)
                {
                    if (r.NodeType == XmlNodeType.Element && r.Depth <= 3)
                    {
                        caminho[r.Depth] = r.NamespaceURI.Length == 0 ? r.LocalName : null;
                        if (r.Depth == 0)
                        {
                            cab.AppendChild(cab.CreateElement(r.Prefix, r.LocalName, r.NamespaceURI));
                        }
                        else if (r.Depth == 1)
                        {
                            primeiroLote = caminho[1] == "LoteRps" && lote == null;
                            if (primeiroLote) lote = cab.DocumentElement.AppendChild(cab.CreateElement("LoteRps"));
                        }
                        else if (r.Depth == 2 && primeiroLote && caminho[2] != null && caminho[2] != "ListaRps")
                        {
                            lote.AppendChild(cab.ReadNode(r));
                            continue;
                        }
                        else if (r.Depth == 3 && Caminho(caminho, "EnviarLoteRpsEnvio", "LoteRps", "ListaRps", "Rps"))
                        {
                            qtdRps++;
                        }
                    }
                    r.Read();
                }
            }
            return LerCabecalho(cab);
        }

        public static IEnumerable<RpsLote> LerRps(XmlReader r)
        {
            XmlDocument doc = new XmlDocument();
            string[] caminho = new string[5];
            int i = 0;
            r.Read();
            while (!r.EOF)
            {
                if (r.NodeType == XmlNodeType.Element && r.Depth <= 4)
                {
                    caminho[r.Depth] = r.NamespaceURI.Length == 0 ? r.LocalName : null;
                    if (r.Depth == 4 && caminho[4] == "InfRps" && Caminho(caminho, "EnviarLoteRpsEnvio", "LoteRps", "ListaRps", "Rps"))
                    {
                        if (doc.DocumentElement != null) doc.RemoveChild(doc.DocumentElement);
                        XmlNode no = doc.AppendChild(doc.ReadNode(r));
                        yield return LerRps(no, i++);
                        continue;
                    }
                }
                r.Read();
            }
        }

        private static bool Caminho(string[] caminho, params string[] nomes)
        {
            for (int i = 0; i < nomes.Length; i++)
            {
                if (caminho[i] != nomes[i]) return false;
            }
            return true;
        }

        private static RpsLote LerRps(XmlNode no, int indice)
        {
            RpsLote r = new RpsLote();
            r.Indice = indice;
            long num;
            long.TryParse(Texto(no, "IdentificacaoRps/Numero") ?? "0", out num);
            r.Numero = num;
            r.Serie = Texto(no, "IdentificacaoRps/Serie") ?? "";
            int tipo;
            int.TryParse(Texto(no, "IdentificacaoRps/Tipo") ?? "1", out tipo);
            r.Tipo = tipo;

            DateTime dt;
            if (!DateTime.TryParse(Texto(no, "DataEmissao"), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                dt = DateTime.Now;
            r.DataEmissao = dt;

            r.ValorServicos = Decimal(Texto(no, "Servico/Valores/ValorServicos"));
            string ded = Texto(no, "Servico/Valores/ValorDeducoes");
            r.ValorDeducoes = ded == null ? 0m : Decimal(ded);
            r.IssRetido = Texto(no, "Servico/Valores/IssRetido") == "1";
            r.ItemListaServico = Texto(no, "Servico/ItemListaServico");
            r.Discriminacao = Texto(no, "Servico/Discriminacao");

            string doc2 = Texto(no, "Tomador/IdentificacaoTomador/CpfCnpj/Cnpj");
            if (string.IsNullOrEmpty(doc2))
                doc2 = Texto(no, "Tomador/IdentificacaoTomador/CpfCnpj/Cpf");
            r.TomadorDocumento = doc2;
            r.TomadorRazaoSocial = Texto(no, "Tomador/RazaoSocial");
            return r;
        }
    }
}

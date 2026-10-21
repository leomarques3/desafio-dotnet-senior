using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using System.Xml;
using NotaCarioca.Dados;
using NotaCarioca.Dominio;
using NotaCarioca.Servicos.Modelos;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Servicos
{
    public class ProcessadorLoteRps
    {
        private LoteRepositorio _lotes = new LoteRepositorio();
        private RpsRepositorio _rps = new RpsRepositorio();
        private NfseRepositorio _nfse = new NfseRepositorio();
        private GuiaRepositorio _guias = new GuiaRepositorio();
        private ContribuinteRepositorio _contribuintes = new ContribuinteRepositorio();

        private static int _emAndamento;

        // processa os lotes pendentes em paralelo, um thread por lote (vazao no fechamento do mes)
        public void ProcessarPendentes()
        {
            foreach (Thread th in IniciarPendentes(int.MaxValue))
            {
                th.Join();
            }
        }

        public List<Thread> IniciarPendentes(int maximo)
        {
            List<Thread> threads = new List<Thread>();
            foreach (string protocolo in _lotes.ObterPendentes())
            {
                if (Interlocked.Increment(ref _emAndamento) > maximo)
                {
                    Interlocked.Decrement(ref _emAndamento);
                    break;
                }
                string p = protocolo;
                Thread th = new Thread(delegate ()
                {
                    try
                    {
                        ProcessarLote(p);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[{0:HH:mm:ss}] falha no lote {1}: {2}", DateTime.Now, p, ex.Message);
                        try { _lotes.AtualizarSituacao(p, 1); } catch (Exception) { }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _emAndamento);
                    }
                });
                th.Start();
                threads.Add(th);
            }
            return threads;
        }

        internal void ProcessarLote(string protocolo)
        {
            if (!_lotes.Reservar(protocolo)) return;

            DateTime inicio = DateTime.Now;
            DataTable info = _lotes.ObterProcessamento(protocolo);
            long prestadorId = Convert.ToInt64(info.Rows[0]["PRESTADOR_ID"]);
            string regime = Convert.ToString(info.Rows[0]["REGIME"]);

            Console.WriteLine("[{0:HH:mm:ss}] processando lote {1} ({2} RPS)", DateTime.Now, protocolo, info.Rows[0]["QTD_RPS"]);

            Dictionary<string, decimal> itens = new Dictionary<string, decimal>();
            foreach (DataRow r in _contribuintes.ListarItensServico().Rows)
            {
                itens[Convert.ToString(r["CODIGO"])] = Convert.ToDecimal(r["ALIQUOTA"]);
            }

            StringBuilder erros = new StringBuilder();
            int qtdResultados = 0;
            int qtdErros = 0;
            HashSet<DateTime> competencias = new HashSet<DateTime>();
            using (OracleConnection leitura = OracleHelper.AbrirConexao())
            using (XmlReader xml = LeitorLoteRps.CriarLeitor(_lotes.AbrirXml(leitura, protocolo)))
            using (OracleConnection conn = OracleHelper.AbrirConexao())
            {
                foreach (RpsLote rps in LeitorLoteRps.LerRps(xml))
                {
                    ResultadoRps res = ProcessarRps(conn, rps, prestadorId, regime, protocolo, itens);
                    qtdResultados++;
                    if (res.Sucesso)
                    {
                        competencias.Add(Competencia.PrimeiroDia(rps.DataEmissao));
                    }
                    else
                    {
                        erros.Append(XmlNfseBuilder.MensagemRps(res));
                        qtdErros++;
                    }
                }
            }

            // guia recalculada uma vez por competencia ao final do lote
            foreach (DateTime comp in competencias)
            {
                _guias.Recalcular(prestadorId, comp);
            }

            if (qtdErros > 0)
            {
                _lotes.SalvarErros(protocolo, "<ListaMensagemRetorno>" + erros + "</ListaMensagemRetorno>");
                _lotes.AtualizarSituacao(protocolo, 3);
            }
            else
            {
                _lotes.AtualizarSituacao(protocolo, 4);
            }
            Console.WriteLine("[{0:HH:mm:ss}] lote {1} concluido em {2:0.0}s: {3} resultados, {4} com erro",
                DateTime.Now, protocolo, (DateTime.Now - inicio).TotalSeconds, qtdResultados, qtdErros);
        }

        private ResultadoRps ProcessarRps(OracleConnection conn, RpsLote rps, long prestadorId, string regime, string protocolo, Dictionary<string, decimal> itens)
        {
            ResultadoRps res = NovoResultado(rps);

            if (!ValidadorDocumento.ValidarCpfOuCnpj(rps.TomadorDocumento))
                return Erro(res, CodigosRetorno.E020);
            if (rps.ItemListaServico == null || !itens.ContainsKey(rps.ItemListaServico))
                return Erro(res, CodigosRetorno.E061);
            if (rps.ValorServicos <= 0 || rps.ValorDeducoes < 0 || rps.ValorDeducoes > rps.ValorServicos)
                return Erro(res, CodigosRetorno.E062);

            try
            {
                using (OracleTransaction tx = conn.BeginTransaction())
                {
                    if (!_rps.Existe(conn, tx, prestadorId, rps.Serie, rps.Numero))
                    {
                        long rpsId = _rps.Inserir(conn, tx, prestadorId, rps.Numero, rps.Serie, rps.Tipo, rps.DataEmissao,
                                                  protocolo, rps.TomadorDocumento, rps.TomadorRazaoSocial);

                        decimal aliquota = CalculoIss.AliquotaEfetiva(itens[rps.ItemListaServico], regime);
                        decimal baseCalculo = CalculoIss.CalcularBase(rps.ValorServicos, rps.ValorDeducoes);
                        decimal valorIss = CalculoIss.CalcularValorIss(baseCalculo, aliquota, regime);

                        long nfseId;
                        long numero;
                        string codigo;
                        _nfse.GerarNfse(conn, tx, rpsId, prestadorId, Competencia.PrimeiroDia(rps.DataEmissao),
                                        rps.ItemListaServico, rps.Discriminacao, rps.ValorServicos, rps.ValorDeducoes,
                                        aliquota, valorIss, rps.IssRetido, rps.TomadorDocumento, rps.TomadorRazaoSocial,
                                        out nfseId, out numero, out codigo);
                        tx.Commit();

                        res.Sucesso = true;
                        res.NfseNumero = numero;
                    }
                    else
                    {
                        tx.Rollback();
                        return Erro(res, CodigosRetorno.E060);
                    }
                }
            }
            catch (OracleException ex)
            {
                if (ex.Number == 1 && ex.Message.Contains("UK_RPS_PREST_SERIE_NUM"))
                    return Erro(res, CodigosRetorno.E060);
                res.Sucesso = false;
                res.Codigo = CodigosRetorno.E999;
                res.Mensagem = ex.Message;
            }
            return res;
        }

        private static ResultadoRps NovoResultado(RpsLote rps)
        {
            ResultadoRps res = new ResultadoRps();
            res.Indice = rps.Indice;
            res.NumeroRps = rps.Numero;
            res.Serie = rps.Serie;
            return res;
        }

        private static ResultadoRps Erro(ResultadoRps res, string codigo)
        {
            res.Sucesso = false;
            res.Codigo = codigo;
            res.Mensagem = CodigosRetorno.Mensagem(codigo);
            return res;
        }
    }
}

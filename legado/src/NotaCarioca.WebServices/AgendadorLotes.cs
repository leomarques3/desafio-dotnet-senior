using System;
using System.Threading;
using NotaCarioca.Servicos;

namespace NotaCarioca.WebServices
{
    public class AgendadorLotes
    {
        private static Timer _timer;
        private static ProcessadorLoteRps _processador = new ProcessadorLoteRps();
        private static int _rodando;

        public static void Iniciar()
        {
            _timer = new Timer(Tick, null, 2000, 2000);
        }

        private static void Tick(object state)
        {
            if (Interlocked.CompareExchange(ref _rodando, 1, 0) != 0) return;
            try
            {
                _processador.ProcessarPendentes();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[{0:HH:mm:ss}] agendador: {1}", DateTime.Now, ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _rodando, 0);
            }
        }
    }
}

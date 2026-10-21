using System;
using System.IO;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dados;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao.Infra
{
    [TestClass]
    public static class BancoOracle
    {
        private const string Imagem = "gvenzl/oracle-free:23-slim-faststart";
        private const string Usuario = "NFSE_OWNER";
        private const string Senha = "nfse_dev_123";

        private static IContainer _container;

        public static string ConnectionString { get; private set; }

        [AssemblyInitialize]
        public static void Iniciar(TestContext contexto)
        {
            string externa = Environment.GetEnvironmentVariable("NFSE_TEST_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(externa))
            {
                ConnectionString = externa;
            }
            else
            {
                string db = Path.Combine(RaizRepositorio(), "db");
                _container = new ContainerBuilder(Imagem)
                    .WithEnvironment("ORACLE_PASSWORD", "oracle_dev_123")
                    .WithEnvironment("APP_USER", Usuario)
                    .WithEnvironment("APP_USER_PASSWORD", Senha)
                    .WithEnvironment("NFSE_SEED_VOLUME", "0")
                    .WithEnvironment("TZ", "America/Sao_Paulo")
                    .WithBindMount(db, "/opt/nfse/db", AccessMode.ReadOnly)
                    .WithBindMount(Path.Combine(db, "init.sh"), "/container-entrypoint-initdb.d/10_nfse.sh", AccessMode.ReadOnly)
                    .WithPortBinding(1521, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("DATABASE IS READY TO USE!"))
                    .Build();
                _container.StartAsync().GetAwaiter().GetResult();

                ConnectionString = string.Format(
                    "User Id={0};Password={1};Data Source={2}:{3}/FREEPDB1;Min Pool Size=0;Connection Timeout=30",
                    Usuario, Senha, _container.Hostname, _container.GetMappedPublicPort(1521));
            }

            OracleHelper.ConnectionString = ConnectionString;
        }

        [AssemblyCleanup]
        public static void Encerrar()
        {
            OracleConnection.ClearAllPools();
            if (_container != null)
            {
                _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        public static OracleConnection Conectar()
        {
            OracleConnection conn = new OracleConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static string RaizRepositorio()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "db", "init.sh"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Raiz do repositorio (com db/init.sh) nao encontrada a partir de " + AppContext.BaseDirectory);
        }
    }
}

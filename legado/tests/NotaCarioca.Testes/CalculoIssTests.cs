using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dominio;

namespace NotaCarioca.Testes
{
    [TestClass]
    public class CalculoIssTests
    {
        [TestMethod]
        public void Iss_ValorRedondo()
        {
            Assert.AreEqual(50m, CalculoIss.CalcularValorIss(1000m, 0.05m, "N"));
        }

        [TestMethod]
        public void Iss_Mei_Zero()
        {
            Assert.AreEqual(0m, CalculoIss.CalcularValorIss(1000m, 0.05m, "M"));
        }

        [TestMethod]
        public void Iss_DuasCasas()
        {
            Assert.AreEqual(3.01m, CalculoIss.CalcularValorIss(150.30m, 0.02m, "S"));
        }

        [TestMethod]
        public void Iss_MeioCentavo()
        {
            Assert.AreEqual(0.53m, CalculoIss.CalcularValorIss(10.50m, 0.05m, "N"));
            Assert.AreEqual(12.53m, CalculoIss.CalcularValorIss(250.50m, 0.05m, "N"));
            Assert.AreEqual(5.01m, CalculoIss.CalcularValorIss(100.10m, 0.05m, "N"));
            Assert.AreEqual(37.04m, CalculoIss.CalcularValorIss(1234.50m, 0.03m, "N"));
            Assert.AreEqual(11.27m, CalculoIss.CalcularValorIss(375.50m, 0.03m, "N"));
        }

        [TestMethod]
        public void Base_DescontaDeducoes()
        {
            Assert.AreEqual(900m, CalculoIss.CalcularBase(1000m, 100m));
        }
    }
}

using System;
using NUnit.Framework;
using SpellSimulator.Lab;

namespace SpellSimulator.Tests
{
    public class LabCsvTests
    {
        private static readonly DateTime When = new DateTime(2026, 11, 15, 14, 3, 9);

        [Test]
        public void Header_TieneQuinceColumnas_LasNuevasAlFinal()
        {
            string[] columns = LabCsv.Header.Split(LabCsv.Separator);
            Assert.AreEqual(LabCsv.ColumnCount, columns.Length);
            Assert.AreEqual(15, columns.Length);
            Assert.AreEqual("fecha_hora", columns[0]);
            Assert.AreEqual("notas", columns[10]);
            Assert.AreEqual("spec_sello", columns[11]);
            Assert.AreEqual("receta", columns[12]);
            Assert.AreEqual("semilla", columns[13]);
            Assert.AreEqual("pose_papel", columns[14]);
        }

        [Test]
        public void FormatRow_FechaIsoYComaDecimal()
        {
            var row = new LabLogRow(When, 12.5f, "perilla", "Masa de la caja", 10f, 25f, experimentId: 3);
            Assert.AreEqual("2026-11-15 14:03:09;12,50;perilla;Masa de la caja;10;25;;;;3;;;;;", LabCsv.FormatRow(row));
        }

        [Test]
        public void FormatRow_ResultadoMedido()
        {
            var row = new LabLogRow(When, 0f, "resultado", "Caja", result: "distancia", measuredValue: 3.14159f,
                unit: "m", experimentId: 7, notes: "superpuesto");
            string line = LabCsv.FormatRow(row);
            Assert.AreEqual("2026-11-15 14:03:09;0,00;resultado;Caja;;;distancia;3,142;m;7;superpuesto;;;;", line);
            Assert.AreEqual(LabCsv.ColumnCount, line.Split(LabCsv.Separator).Length);
        }

        [Test]
        public void FormatRow_DecayConComa()
        {
            var row = new LabLogRow(When, 1f, "perilla", "Fuego: debilitamiento", 0.78f, 0.8f);
            StringAssert.Contains(";0,78;0,8;", LabCsv.FormatRow(row));
        }

        [Test]
        public void FormatNumber_VacioSiNoHayValor()
        {
            Assert.AreEqual("", LabCsv.FormatNumber(null));
            Assert.AreEqual("", LabCsv.FormatNumber(float.NaN));
            Assert.AreEqual("1234,5", LabCsv.FormatNumber(1234.5f));
            Assert.AreEqual("-0,25", LabCsv.FormatNumber(-0.25f));
        }

        [Test]
        public void Escape_SeparadorComillasYSaltos()
        {
            Assert.AreEqual("", LabCsv.Escape(null));
            Assert.AreEqual("simple", LabCsv.Escape("simple"));
            Assert.AreEqual("\"a;b\"", LabCsv.Escape("a;b"));
            Assert.AreEqual("\"dijo \"\"hola\"\"\"", LabCsv.Escape("dijo \"hola\""));
            Assert.AreEqual("\"linea1\nlinea2\"", LabCsv.Escape("linea1\nlinea2"));
            Assert.AreEqual("\"a\r\nb\"", LabCsv.Escape("a\r\nb"));
            Assert.AreEqual("coma, sin comillas", LabCsv.Escape("coma, sin comillas"));
        }

        [Test]
        public void FormatRow_EscapaLosCamposDeTexto()
        {
            var row = new LabLogRow(When, 2f, "hechizo", "fuego;viento", notes: "nota \"rara\"");
            string line = LabCsv.FormatRow(row);
            StringAssert.Contains(";\"fuego;viento\";", line);
            StringAssert.EndsWith(";\"nota \"\"rara\"\"\";;;;", line);
        }

        [Test]
        public void FormatRow_ColumnasDelBancoAlFinal()
        {
            var row = new LabLogRow(When, 3f, "hechizo", "viento", result: "lanzado", experimentId: 4,
                sealSpec: "sigilo=wind signo=column×2", recipe: "gust", seed: 0, paperPose: 6);
            string line = LabCsv.FormatRow(row);
            string[] columns = line.Split(LabCsv.Separator);
            Assert.AreEqual(LabCsv.ColumnCount, columns.Length);
            Assert.AreEqual("sigilo=wind signo=column×2", columns[11]);
            Assert.AreEqual("gust", columns[12]);
            Assert.AreEqual("0", columns[13]);
            Assert.AreEqual("6", columns[14]);
        }
    }
}

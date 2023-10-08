using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using CoordImporter;
using Dalamud.Plugin.Services;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Moq;

namespace CoordImporter.Tests
{
    [TestFixture]
    public class Tests
    {
        private Importer _importer;
        private Mock<IChatGui> mockIChatGui;
        private Mock<IPluginLog> mockIPluginLog;

        [SetUp]
        public void Setup()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            mockIChatGui = new Mock<IChatGui>();
            mockIPluginLog = new Mock<IPluginLog>();
        }

        [Test]
        public void Test1()
        {
            _importer = new Importer(mockIChatGui.Object, mockIPluginLog.Object, new Dictionary<string, MapData> { { "Labyrinthos", new MapData(1,1) } });
            Assert.That(_importer.ParsePayload("Labyrinthos ( 12.3 , 45.6 ) Hulder")[0].markName, Is.EqualTo("Hulder"));
        }
    }
}

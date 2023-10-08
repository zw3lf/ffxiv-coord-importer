using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using CoordImporter;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using HarmonyLib;
using Lumina.Excel.GeneratedSheets;
using NSubstitute;

namespace CoordImporter.Tests
{
    public class ConstructableMap : Map
    {
        public ConstructableMap()
        {
            this.OffsetX = 0;
            this.SizeFactor = 1;
            this.OffsetY = 0;
        }
    }
    
    [HarmonyPatch(typeof(MapLinkPayload), nameof(MapLinkPayload.PlaceName), MethodType.Getter)]
    class Patch
    {
        static bool Prefix(MapLinkPayload __instance, ref String __result)
        {
            __result = "Stubbed.";
            return false;
        }
    }
    
    [HarmonyPatch(typeof(MapLinkPayload), nameof(MapLinkPayload.Map), MethodType.Getter)]
    class Patch2
    {
        static bool Prefix(MapLinkPayload __instance, ref Map __result)
        {
            __result = new ConstructableMap();
            return false;
        }
    }
    
    [TestFixture]
    public class Tests
    {
        private Importer _importer;
        private IChatGui mockIChatGui;
        private IPluginLog mockIPluginLog;

        [SetUp]
        public void Setup()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            var harmony = new Harmony("Dalamud.Patch");
            // var original = typeof(MapLinkPayload).GetMethod("get_Placename");
            // var prefix = typeof(Patch).GetMethod("Prefix");

            harmony.PatchAll();
            mockIChatGui = Substitute.For<IChatGui>();
            mockIPluginLog = Substitute.For<IPluginLog>();
        }

        [Test]
        public void Test1()
        {
            var wew = new MapLinkPayload(0, 0, 0.0f, 0.0f);
            Console.WriteLine(wew.PlaceName);
            _importer = new Importer(mockIChatGui, mockIPluginLog, new Dictionary<string, MapData> { { "Labyrinthos", new MapData(1,1) } });
            Assert.That(_importer.ParsePayload("Labyrinthos ( 12.3 , 45.6 ) Hulder")[0].markName, Is.EqualTo("Hulder"));
        }
    }
}

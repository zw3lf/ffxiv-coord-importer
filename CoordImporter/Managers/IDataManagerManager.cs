using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace CoordImporter.Managers;

public interface IDataManagerManager
{
    #region lumina methods
    ExcelSheet<T>? GetExcelSheet<T>() where T : ExcelRow;
    ExcelSheet<T>? GetExcelSheet<T>(ClientLanguage clientLanguage) where T : ExcelRow;
    #endregion
    
    Maybe<uint> GetMobIdByName(string mobName);
    Maybe<MapData> GetMapDataByName(string mapName);
}

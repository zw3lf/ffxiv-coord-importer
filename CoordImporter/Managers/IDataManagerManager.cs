using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud;
using Lumina.Excel;

namespace CoordImporter.Managers;

public interface IDataManagerManager
{
    Maybe<uint> GetMobIdByName(string mobName);
    Maybe<MapData> GetMapDataByName(string mapName);

    #region lumina methods

    ExcelSheet<T>? GetExcelSheet<T>() where T : ExcelRow;
    ExcelSheet<T>? GetExcelSheet<T>(ClientLanguage clientLanguage) where T : ExcelRow;

    #endregion
}

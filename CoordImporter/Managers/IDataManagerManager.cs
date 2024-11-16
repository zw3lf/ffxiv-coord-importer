using CoordImporter.Models;
using CSharpFunctionalExtensions;
using Dalamud.Game;
using Lumina.Excel;

namespace CoordImporter.Managers;

public interface IDataManagerManager
{
    #region lumina methods
    ExcelSheet<T>? GetExcelSheet<T>() where T : struct, IExcelRow<T>;
    ExcelSheet<T>? GetExcelSheet<T>(ClientLanguage clientLanguage) where T : struct, IExcelRow<T>;
    #endregion
    
    Maybe<uint> GetMobIdByName(string mobName);
    Maybe<MapData> GetMapDataByName(string mapName);
}

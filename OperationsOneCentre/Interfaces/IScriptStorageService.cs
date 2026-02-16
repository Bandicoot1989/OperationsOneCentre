using OperationsOneCentre.Models;

namespace OperationsOneCentre.Interfaces;

/// <summary>
/// Interface for Script persistence to Azure Blob Storage
/// </summary>
public interface IScriptStorageService
{
    Task InitializeAsync();
    Task SaveScriptsAsync(List<Script> scripts);
    Task<List<Script>> LoadScriptsAsync();
}

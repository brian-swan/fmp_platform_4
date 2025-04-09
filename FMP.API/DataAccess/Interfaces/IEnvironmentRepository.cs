using FMP.API.Models;
// Use a fully qualified name to avoid ambiguity
using EnvironmentModel = FMP.API.Models.Environment;

namespace FMP.API.DataAccess.Interfaces;

public interface IEnvironmentRepository
{
    Task<List<EnvironmentModel>> GetAllEnvironmentsAsync();
    Task<EnvironmentModel?> GetEnvironmentByIdAsync(string id);
    Task<EnvironmentModel?> GetEnvironmentByKeyAsync(string key);
    Task<EnvironmentModel> CreateEnvironmentAsync(EnvironmentModel environment);
    Task DeleteEnvironmentAsync(string id);
    Task<bool> EnvironmentExistsAsync(string key);
}

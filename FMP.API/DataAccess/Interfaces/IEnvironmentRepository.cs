using FMP.API.Models;

namespace FMP.API.DataAccess.Interfaces;

public interface IEnvironmentRepository
{
    Task<List<Environment>> GetAllEnvironmentsAsync();
    Task<Environment?> GetEnvironmentByIdAsync(string id);
    Task<Environment?> GetEnvironmentByKeyAsync(string key);
    Task<Environment> CreateEnvironmentAsync(Environment environment);
    Task DeleteEnvironmentAsync(string id);
    Task<bool> EnvironmentExistsAsync(string key);
}

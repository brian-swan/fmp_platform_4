using FMP.API.DataAccess.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Environment = FMP.API.Models.Environment;

namespace FMP.API.Controllers;

[ApiController]
[Route("v1/environments")]
public class EnvironmentsController : ControllerBase
{
    private readonly IEnvironmentRepository _repository;
    
    public EnvironmentsController(IEnvironmentRepository repository)
    {
        _repository = repository;
    }
    
    [HttpGet]
    public async Task<ActionResult<Models.EnvironmentListResponse>> GetEnvironments()
    {
        var environments = await _repository.GetAllEnvironmentsAsync();
        
        return Ok(new Models.EnvironmentListResponse
        {
            Environments = environments
        });
    }
    
    [HttpPost]
    public async Task<ActionResult<Environment>> CreateEnvironment(Models.EnvironmentCreateRequest request)
    {
        var environment = new Environment
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description
        };
        
        var createdEnvironment = await _repository.CreateEnvironmentAsync(environment);
        
        return CreatedAtAction(
            nameof(GetEnvironments), 
            createdEnvironment);
    }
    
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteEnvironment(string id)
    {
        await _repository.DeleteEnvironmentAsync(id);
        return NoContent();
    }
}

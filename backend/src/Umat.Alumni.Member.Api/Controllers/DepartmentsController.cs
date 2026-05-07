using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Controllers;

public class DepartmentsController(IAlumniPgRepository<Department> departmentRepo) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List departments", Description = "Get all departments for registration and profile forms")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await departmentRepo.GetAllAsync(_ => true);
        var items = departments
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.ShortCode));
        return new OkObjectResult(new ApiResponse<IEnumerable<DepartmentDto>>
        {
            Code = 200, SubCode = "OK", Message = "Departments retrieved",
            Data = items,
        });
    }
}

public record DepartmentDto(string Id, string Name, string? ShortCode);

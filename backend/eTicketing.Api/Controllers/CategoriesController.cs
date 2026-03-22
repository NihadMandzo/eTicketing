using eTicketing.Api.Controllers.Shared;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class CategoriesController : BaseCRUDController<CategoryResponse, BaseSearchObject, CategoryInsertRequest, CategoryUpdateRequest>
{
    public CategoriesController(ICategoryService service) : base(service)
    {
    }

    [HttpPost]
    public override async Task<IActionResult> CreateAsync([FromForm] CategoryInsertRequest request, CancellationToken cancellationToken = default)
    {
        var result = await Service.CreateAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    [HttpPut("{id}")]
    public override async Task<IActionResult> UpdateAsync(int id, [FromForm] CategoryUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var result = await Service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }
}

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
}

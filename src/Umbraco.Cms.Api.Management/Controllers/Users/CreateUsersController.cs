﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Api.Management.ViewModels.Users;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Cms.Api.Management.Controllers.Users;

public class CreateUsersController : UsersControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserPresentationFactory _presentationFactory;
    private readonly IUmbracoMapper _mapper;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    public CreateUsersController(
        IUserService userService,
        IUserPresentationFactory presentationFactory,
        IUmbracoMapper mapper,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _userService = userService;
        _presentationFactory = presentationFactory;
        _mapper = mapper;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CreateUserResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateUserRequestModel model)
    {
        UserCreateModel createModel = await _presentationFactory.CreateCreationModelAsync(model);

        Attempt<UserCreationResult, UserOperationStatus> result = await _userService.CreateAsync(CurrentUserKey(_backOfficeSecurityAccessor), createModel, true);

        return result.Success
            ? Ok(_mapper.Map<CreateUserResponseModel>(result.Result))
            : UserOperationStatusResult(result.Status, result.Result);
    }
}

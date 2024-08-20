﻿using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace RuiChen.AbpPro.PermissionManagement
{
    public interface IPermissionDefinitionAppService : IApplicationService
    {

        Task<PermissionDefinitionDto> GetAsync(string name);

        Task DeleteAsync(string name);

        Task<PermissionDefinitionDto> CreateAsync(PermissionDefinitionCreateDto input);

        Task<PermissionDefinitionDto> UpdateAsync(string name, PermissionDefinitionUpdateDto input);

        Task<ListResultDto<PermissionDefinitionDto>> GetListAsync(PermissionDefinitionGetListInput input);

    }
}

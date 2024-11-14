﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.Features;
using Volo.Abp.GlobalFeatures;
using Volo.Abp.ObjectExtending;
using Volo.CmsKit.Features;
using Volo.CmsKit.GlobalFeatures;
using Volo.CmsKit.Pages;
using Volo.CmsKit.Permissions;

namespace Volo.CmsKit.Admin.Pages;

[RequiresFeature(CmsKitFeatures.PageEnable)]
[RequiresGlobalFeature(typeof(PagesFeature))]
[Authorize(CmsKitAdminPermissions.Pages.Default)]
public class PageAdminAppService : CmsKitAdminAppServiceBase, IPageAdminAppService
{
    protected IPageRepository PageRepository { get; }

    protected PageManager PageManager { get; }
    
    protected IDistributedCache<PageCacheItem> PageCache { get; }

    public PageAdminAppService(
        IPageRepository pageRepository,
        PageManager pageManager, 
        IDistributedCache<PageCacheItem> pageCache)
    {
        PageRepository = pageRepository;
        PageManager = pageManager;
        PageCache = pageCache;
    }

    public virtual async Task<PageDto> GetAsync(Guid id)
    {
        var page = await PageRepository.GetAsync(id);
        return ObjectMapper.Map<Page, PageDto>(page);
    }

    public virtual async Task<PagedResultDto<PageDto>> GetListAsync(GetPagesInputDto input)
    {
        var count = await PageRepository.GetCountAsync(input.Filter);

        var pages = await PageRepository.GetListAsync(
            input.Filter,
            input.MaxResultCount,
            input.SkipCount,
            input.Sorting
        );

        return new PagedResultDto<PageDto>(
            count,
            ObjectMapper.Map<List<Page>, List<PageDto>>(pages)
        );
    }

    [Authorize(CmsKitAdminPermissions.Pages.Create)]
    public virtual async Task<PageDto> CreateAsync(CreatePageInputDto input)
    {
        var page = await PageManager.CreateAsync(input.Title, input.Slug, input.Content, input.Script, input.Style, input.LayoutName);
        input.MapExtraPropertiesTo(page);
        await PageRepository.InsertAsync(page);

        await PageCache.RemoveAsync(PageCacheItem.GetKey(page.Slug));

        return ObjectMapper.Map<Page, PageDto>(page);
    }

    [Authorize(CmsKitAdminPermissions.Pages.Update)]
    public virtual async Task<PageDto> UpdateAsync(Guid id, UpdatePageInputDto input)
    {
        var page = await PageRepository.GetAsync(id);
        if (page.IsHomePage)
        {
            await InvalidateDefaultHomePageCacheAsync(considerUow: true);
        }
        
        await PageCache.RemoveAsync(PageCacheItem.GetKey(page.Slug));

        await PageManager.SetSlugAsync(page, input.Slug);

        page.SetTitle(input.Title);
        page.SetContent(input.Content);
        page.SetScript(input.Script);
        page.SetStyle(input.Style);
        page.SetLayoutName(input.LayoutName);
        page.SetConcurrencyStampIfNotNull(input.ConcurrencyStamp);
        input.MapExtraPropertiesTo(page);

        await PageRepository.UpdateAsync(page);

        return ObjectMapper.Map<Page, PageDto>(page);
    }

    [Authorize(CmsKitAdminPermissions.Pages.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var page = await PageRepository.GetAsync(id);
        if (page.IsHomePage)
        {
            await InvalidateDefaultHomePageCacheAsync(considerUow: true);
        }
        
        await PageRepository.DeleteAsync(page);
        await PageCache.RemoveAsync(PageCacheItem.GetKey(page.Slug));
    }

    [Authorize(CmsKitAdminPermissions.Pages.SetAsHomePage)]
    public virtual async Task SetAsHomePageAsync(Guid id)
    {
        var page = await PageRepository.GetAsync(id);

		await PageManager.SetHomePageAsync(page);
        await InvalidateDefaultHomePageCacheAsync();
    }

    protected virtual async Task InvalidateDefaultHomePageCacheAsync(bool considerUow = false)
    {
        await PageCache.RemoveAsync(PageCacheItem.GetKey(PageConsts.DefaultHomePageCacheKey), considerUow: considerUow);
    }
}

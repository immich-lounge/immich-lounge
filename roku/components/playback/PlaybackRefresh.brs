' PlaybackRefresh.brs - shared playlist/profile refresh helpers

sub OnRefreshTimerForScene(ctx as Object)
    ctx.top.triggerRefresh = true
end sub

sub OnProfileConfigTimerForScene(ctx as Object, isScreensaver as Boolean)
    TriggerProfileConfigRefreshForScene(ctx, isScreensaver)
end sub

sub OnTriggerRefreshForScene(ctx as Object)
    if not ctx.top.triggerRefresh then return
    ctx.top.triggerRefresh = false
    TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
end sub

sub TriggerProfileConfigRefreshForScene(ctx as Object, isScreensaver as Boolean)
    if ctx.profile = invalid then return
    if ctx.profileConfigTask <> invalid then return
    if ctx.refreshProfileTask <> invalid then return

    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.isScreensaver = isScreensaver
    task.observeField("profileResult", "OnProfileConfigResult")
    ctx.profileConfigTask = task
    task.command = "fetchProfile"
end sub

sub OnProfileConfigResultForScene(ctx as Object, isScreensaver as Boolean)
    task = ctx.profileConfigTask
    ctx.profileConfigTask = invalid
    if task = invalid then
        LogDebug(PlaybackLogScope(ctx), "profile config result arrived after task was cleared")
        return
    end if

    task.unobserveField("profileResult")
    result = task.profileResult
    if result = invalid then
        LogDebug(PlaybackLogScope(ctx), "profile config task returned no result")
        return
    end if

    if result.ok = true and result.profile <> invalid then
        ctx.profile = result.profile
        RegistryWrite(CachedProfileRegistryKey(isScreensaver), FormatJSON(ctx.profile))
        ctx.skipFullRefreshTimerRestart = true
        ApplyProfile()
        ctx.skipFullRefreshTimerRestart = false
    else
        LogDebug(PlaybackLogScope(ctx), "profile config refresh unavailable, continuing with existing profile")
    end if
end sub

sub TriggerPlaylistRefreshForScene(ctx as Object, isScreensaver as Boolean)
    if ctx.profile = invalid then return
    if ctx.profileConfigTask <> invalid then
        ctx.profileConfigTask.unobserveField("profileResult")
        ctx.profileConfigTask = invalid
    end if
    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.isScreensaver = isScreensaver
    task.observeField("profileResult", "OnRefreshProfileResult")
    ctx.refreshProfileTask = task
    task.command = "fetchProfile"
end sub

sub OnRefreshProfileResultForScene(ctx as Object, isScreensaver as Boolean)
    task = ctx.refreshProfileTask
    ctx.refreshProfileTask = invalid
    if task = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh profile result arrived after task was cleared")
        return
    end if

    task.unobserveField("profileResult")
    result = task.profileResult
    if result = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh profile task returned no result")
        return
    end if

    if result.ok = true then
        ctx.profile = result.profile
        RegistryWrite(CachedProfileRegistryKey(isScreensaver), FormatJSON(ctx.profile))
        ApplyProfile()
        task = CreateObject("roSGNode", "CompanionApiTask")
        task.baseUrl = ctx.top.companionUrl
        task.profileId = ctx.top.profileId
        task.isScreensaver = isScreensaver
        task.playlistOffset = ctx.playlistOffset
        task.playlistVersion = ctx.playlistVersion
        task.playlistCount = PlaybackRegistryPlaylistWindowSize()
        task.observeField("playlistResult", "OnRefreshPlaylistResult")
        ctx.refreshPlaylistTask = task
        task.command = "fetchPlaylist"
    else
        LogDebug(PlaybackLogScope(ctx), "refresh profile failed, continuing with existing playlist")
    end if
end sub

sub OnRefreshPlaylistResultForScene(ctx as Object, isScreensaver as Boolean)
    task = ctx.refreshPlaylistTask
    ctx.refreshPlaylistTask = invalid
    if task = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh playlist result arrived after task was cleared")
        return
    end if

    task.unobserveField("playlistResult")
    result = task.playlistResult
    if result = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh playlist task returned no result")
        return
    end if

    if result.ok = true then
        resultPlaylistVersion = NormalizePlaylistVersion(result.playlistVersion)
        currentAssetId = invalid
        if ctx.playlist <> invalid and ctx.playlistIndex >= 0 and ctx.playlistIndex < ctx.playlist.Count() then
            currentEntry = ctx.playlist[ctx.playlistIndex]
            if currentEntry <> invalid and currentEntry.id <> invalid then
                currentAssetId = currentEntry.id
            end if
        end if

        ctx.playlist = result.assets
        if resultPlaylistVersion <> ctx.playlistVersion or result.offset <> ctx.playlistOffset then ctx.portraitPairs = {}
        ctx.playlistOffset = result.offset
        ctx.nextPlaylistOffset = result.nextOffset
        ctx.totalPlaylistCount = result.totalCount
        if resultPlaylistVersion <> "" then
            ctx.playlistVersion = resultPlaylistVersion
        end if
        ctx.pendingBatchAssets = invalid
        ctx.pendingBatchOffset = 0
        ctx.pendingBatchPlaylistVersion = ""
        ctx.pendingNextPlaylistOffset = 0
        ctx.waitingForNextBatchSwap = false
        ctx.playlistIndex = ResolvePlaylistIndexAfterRefresh(ctx.playlist, currentAssetId, ctx.playlistIndex)
        ' A refreshed window may have moved the right photo away from the left.
        ctx.slideAssetCount = PortraitSlideAdvanceCount(ctx.playlist, ctx.playlistIndex, ctx.currentPairAssetId)
        PersistActivePlaylistCacheForScene(ctx, isScreensaver)
    else
        LogDebug(PlaybackLogScope(ctx), "refresh playlist failed, continuing with existing playlist")
    end if
end sub

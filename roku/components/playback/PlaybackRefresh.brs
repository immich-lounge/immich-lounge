' PlaybackRefresh.brs - shared playlist/profile refresh helpers

sub OnRefreshTimerForScene(ctx as Object)
    ctx.top.triggerRefresh = true
end sub

sub OnTriggerRefreshForScene(ctx as Object)
    if not ctx.top.triggerRefresh then return
    ctx.top.triggerRefresh = false
    TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
end sub

sub TriggerPlaylistRefreshForScene(ctx as Object, isScreensaver as Boolean)
    if ctx.profile = invalid then return
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
        task.playlistOffset = 0
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
        ctx.playlist = result.assets
        ctx.playlistOffset = result.offset
        ctx.nextPlaylistOffset = result.nextOffset
        ctx.totalPlaylistCount = result.totalCount
        ctx.pendingBatchAssets = invalid
        ctx.pendingBatchOffset = 0
        ctx.pendingNextPlaylistOffset = 0
        ctx.waitingForNextBatchSwap = false
        if ctx.playlistIndex >= ctx.playlist.Count() then ctx.playlistIndex = 0
        PersistActivePlaylistCacheForScene(ctx, isScreensaver)
    else
        LogDebug(PlaybackLogScope(ctx), "refresh playlist failed, continuing with existing playlist")
    end if
end sub

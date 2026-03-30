' PlaybackBatching.brs - shared slideshow playlist window helpers

function ShouldPrefetchNextPlaylistBatchForScene(ctx as Object) as Boolean
    if ctx.waitingForNextBatchSwap then return false
    if ctx.pendingBatchAssets <> invalid then return false
    if ctx.nextBatchTask <> invalid and ctx.nextBatchTask.control = "RUN" then return false
    if ctx.playlist.Count() = 0 then return false
    if ctx.totalPlaylistCount > 0 and ctx.totalPlaylistCount <= ctx.playlist.Count() then return false

    prefetchThreshold = ctx.playlist.Count() - 3
    if prefetchThreshold < 0 then prefetchThreshold = 0
    if ctx.playlistIndex < prefetchThreshold then return false

    LogDebug(PlaybackLogScope(ctx), "Prefetching next playlist batch offset=" + ctx.nextPlaylistOffset.ToStr() + " index=" + ctx.playlistIndex.ToStr())
    return true
end function

sub FetchNextPlaylistBatchForScene(ctx as Object, isScreensaver as Boolean)
    if ctx.top.companionUrl = "" then return
    if ctx.totalPlaylistCount > 0 and ctx.totalPlaylistCount <= ctx.playlist.Count() then return
    if ctx.nextBatchTask <> invalid and ctx.nextBatchTask.control = "RUN" then return
    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.isScreensaver = isScreensaver
    task.playlistOffset = ctx.nextPlaylistOffset
    task.playlistCount = PlaybackRegistryPlaylistWindowSize()
    task.observeField("playlistResult", "OnNextBatchResult")
    ctx.nextBatchTask = task
    task.command = "fetchPlaylist"
end sub

sub OnNextBatchResultForScene(ctx as Object)
    task = ctx.nextBatchTask
    ctx.nextBatchTask = invalid
    if task = invalid then
        LogDebug(PlaybackLogScope(ctx), "next batch result arrived after task was cleared")
        return
    end if

    task.unobserveField("playlistResult")
    result = task.playlistResult
    if result = invalid then
        LogDebug(PlaybackLogScope(ctx), "next batch task returned no playlist result")
        return
    end if

    if result.ok = true and result.assets.Count() > 0 then
        LogDebug(PlaybackLogScope(ctx), "next batch ready offset=" + result.offset.ToStr() + " count=" + result.assets.Count().ToStr() + " nextOffset=" + result.nextOffset.ToStr())
        ctx.pendingBatchAssets = result.assets
        ctx.pendingBatchOffset = result.offset
        ctx.pendingNextPlaylistOffset = result.nextOffset
        ctx.totalPlaylistCount = result.totalCount
    else
        LogDebug(PlaybackLogScope(ctx), "next batch fetch failed or empty; continuing current batch")
    end if
end sub

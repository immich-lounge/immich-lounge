' PlaybackBatching.brs - shared slideshow playlist window helpers

function PlaylistBatchRetryPendingForScene(ctx as Object) as Boolean
    if ctx.nextBatchRetryAt = invalid then return false
    return UpTime(0) < ctx.nextBatchRetryAt
end function

function ShufflePlaylistLoopsForScene(ctx as Object) as Boolean
    if ctx.totalPlaylistCount <= 1 then return false
    if ctx.profile = invalid then return false
    if ctx.profile.slideshow = invalid then return false
    return ctx.profile.slideshow.shuffle = true
end function

function HasNextPlaylistWindowForScene(ctx as Object) as Boolean
    if ShufflePlaylistLoopsForScene(ctx) then return true
    return ctx.totalPlaylistCount > ctx.playlist.Count() or ctx.totalPlaylistCount = 0
end function

function NextPlaylistVersionForScene(ctx as Object) as String
    if ctx.nextPlaylistOffset = 0 and ShufflePlaylistLoopsForScene(ctx) then
        return NextShuffleCycleVersion(ctx.playlistVersion)
    end if
    return ctx.playlistVersion
end function

function ShouldPrefetchNextPlaylistBatchForScene(ctx as Object) as Boolean
    if ctx.pendingRestartShuffle = true then return false
    if PlaylistBatchRetryPendingForScene(ctx) then return false
    if ctx.waitingForNextBatchSwap then return false
    if ctx.pendingBatchAssets <> invalid then return false
    if ctx.nextBatchTask <> invalid and ctx.nextBatchTask.control = "RUN" then return false
    if ctx.playlist.Count() = 0 then return false
    if not HasNextPlaylistWindowForScene(ctx) then return false

    prefetchThreshold = ctx.playlist.Count() - 3
    if prefetchThreshold < 0 then prefetchThreshold = 0
    if ctx.playlistIndex < prefetchThreshold then return false

    LogDebug(PlaybackLogScope(ctx), "Prefetching next playlist batch offset=" + ctx.nextPlaylistOffset.ToStr() + " index=" + ctx.playlistIndex.ToStr())
    return true
end function

sub FetchNextPlaylistBatchForScene(ctx as Object, isScreensaver as Boolean)
    if ctx.pendingRestartShuffle = true then return
    if PlaylistBatchRetryPendingForScene(ctx) then return
    if ctx.top.companionUrl = "" then return
    if not HasNextPlaylistWindowForScene(ctx) then return
    if ctx.pendingBatchAssets <> invalid then return
    if ctx.nextBatchTask <> invalid and ctx.nextBatchTask.control = "RUN" then return
    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = PlaybackProfileId(ctx.profile, ctx.top.profileId)
    task.isScreensaver = isScreensaver
    task.playlistOffset = ctx.nextPlaylistOffset
    task.playlistVersion = NextPlaylistVersionForScene(ctx)
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
        ctx.nextBatchRetryAt = UpTime(0) + PlaybackRetryDelaySeconds()
        LogDebug(PlaybackLogScope(ctx), "next batch task returned no playlist result")
        return
    end if

    if result.ok = true and result.assets.Count() > 0 then
        ctx.nextBatchRetryAt = 0
        resultPlaylistVersion = NormalizePlaylistVersion(result.playlistVersion)
        currentPlaylistVersion = NormalizePlaylistVersion(ctx.playlistVersion)
        expectedPlaylistVersion = NormalizePlaylistVersion(task.playlistVersion)
        ' A new loop deliberately changes only the cycle suffix. Older companions
        ' may return the unchanged content version when they do not support cycles.
        if currentPlaylistVersion <> "" and resultPlaylistVersion <> "" and resultPlaylistVersion <> expectedPlaylistVersion and resultPlaylistVersion <> currentPlaylistVersion then
            LogDebug(PlaybackLogScope(ctx), "next batch version changed from " + currentPlaylistVersion + " to " + resultPlaylistVersion + "; triggering full refresh")
            ctx.waitingForNextBatchSwap = false
            ctx.pendingBatchAssets = invalid
            ctx.pendingBatchOffset = 0
            ctx.pendingBatchPlaylistVersion = ""
            ctx.pendingNextPlaylistOffset = 0
            TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
            return
        end if

        LogDebug(PlaybackLogScope(ctx), "next batch ready offset=" + result.offset.ToStr() + " count=" + result.assets.Count().ToStr() + " nextOffset=" + result.nextOffset.ToStr())
        ctx.pendingBatchAssets = result.assets
        ctx.pendingBatchOffset = result.offset
        if resultPlaylistVersion <> "" then
            ctx.pendingBatchPlaylistVersion = resultPlaylistVersion
        else
            ctx.pendingBatchPlaylistVersion = currentPlaylistVersion
        end if
        ctx.pendingNextPlaylistOffset = result.nextOffset
        ctx.totalPlaylistCount = result.totalCount
    else
        ctx.nextBatchRetryAt = UpTime(0) + PlaybackRetryDelaySeconds()
        LogDebug(PlaybackLogScope(ctx), "next batch fetch failed or empty; continuing current batch")
    end if
end sub

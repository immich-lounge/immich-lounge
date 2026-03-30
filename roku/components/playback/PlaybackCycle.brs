' PlaybackCycle.brs - shared playback timing and slide load orchestration

sub ShowCurrentSlideForScene(ctx as Object)
    if ctx.playlist.Count() = 0 then return
    if ctx.profile = invalid then return
    if ctx.playlistIndex >= ctx.playlist.Count() then ctx.playlistIndex = 0

    entry = ctx.playlist[ctx.playlistIndex]
    mediaUrl = BuildMediaUrl(entry, ctx.profile)
    bgMediaUrl = BuildBackgroundMediaUrl(entry, ctx.profile)
    ctx.pendingEntry = entry
    ctx.loadingFromSlot = ctx.ringCurrent
    ctx.loadingToSlot = ctx.ringNext
    ctx.loadingMediaUrl = mediaUrl
    ctx.loadingCommitted = false
    ctx.loadingMainFallbackTried = false
    ctx.layoutRetryAttempts = 0
    LogDebug(PlaybackLogScope(ctx), "ShowCurrentSlide idx=" + ctx.playlistIndex.ToStr() + " slot=" + ctx.loadingToSlot.ToStr() + " url=" + mediaUrl)

    HideLoadingIndicatorForScene(ctx)
    ctx.transitionSafetyTimer.control = "stop"
    ctx.isTransitioning = true
    ctx.transitionSafetyTimer.control = "start"

    toNode = ctx.mainPosters[ctx.loadingToSlot]
    bgNode = ctx.bgPosters[ctx.loadingToSlot]

    ctx.nextMainReady = false
    ctx.nextBgReady = false

    if toNode.uri = mediaUrl and toNode.loadStatus = "ready" then
        if ApplyMainPosterLayoutForScene(ctx, toNode) then
            ctx.nextMainReady = true
        else
            QueueLayoutRetryForScene(ctx)
        end if
    else
        toNode.unobserveField("loadStatus")
        toNode.uri = mediaUrl
        if bgNode.uri <> bgMediaUrl then bgNode.uri = ""
        bgNode.uri = bgMediaUrl
        ctx.ringUrls[ctx.loadingToSlot] = mediaUrl
        toNode.observeField("loadStatus", "OnNextPosterLoaded")
    end if

    if not ctx.bgEnabled then
        bgNode.unobserveField("loadStatus")
        ctx.nextBgReady = true
    else if bgNode.uri = bgMediaUrl and bgNode.loadStatus = "ready" then
        ctx.nextBgReady = true
    else
        bgNode.unobserveField("loadStatus")
        bgNode.uri = bgMediaUrl
        bgNode.observeField("loadStatus", "OnNextBgPosterLoaded")
    end if

    if ctx.nextMainReady and ctx.nextBgReady then
        ctx.transitionSafetyTimer.control = "stop"
        CommitSlideForScene(ctx, ctx.loadingFromSlot, ctx.loadingToSlot, entry)
    else
        ctx.loadingTimer.control = "start"
    end if
end sub

sub OnSlideTimerForScene(ctx as Object)
    if ctx.paused then return
    AdvanceToNextForScene(ctx)
end sub

sub OnProgressTickForScene(ctx as Object)
    if ctx.paused then return
    elapsed = UpTime(0) - ctx.slideStartTime
    progress = elapsed / ctx.slideDuration
    if progress > 1.0 then progress = 1.0
    ctx.progressBar.progress = progress
end sub

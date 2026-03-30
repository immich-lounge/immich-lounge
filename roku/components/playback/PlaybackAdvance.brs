' PlaybackAdvance.brs - shared slideshow advance and safety helpers

sub AdvanceToNextForScene(ctx as Object)
    if ctx.playlist.Count() = 0 then return
    ctx.slideTimer.control = "stop"
    ctx.progressTimer.control = "stop"
    ctx.progressBar.progress = 0

    if ApplyPendingBatchSwapForScene(ctx, ctx.top.isScreensaver) then
        ShowCurrentSlideForScene(ctx)
        return
    end if

    if ShouldPrefetchNextPlaylistBatchForScene(ctx) then
        FetchNextPlaylistBatchForScene(ctx, ctx.top.isScreensaver)
    end if

    ctx.playlistIndex = ctx.playlistIndex + 1
    if ctx.playlistIndex >= ctx.playlist.Count() then
        ctx.playlistIndex = 0
        ctx.waitingForNextBatchSwap = true
        FetchNextPlaylistBatchForScene(ctx, ctx.top.isScreensaver)
        if ApplyPendingBatchSwapForScene(ctx, ctx.top.isScreensaver) then
            ShowCurrentSlideForScene(ctx)
            return
        end if
    end if

    ShowCurrentSlideForScene(ctx)
end sub

sub AdvanceToPrevForScene(ctx as Object)
    if ctx.playlist.Count() = 0 then return
    ctx.slideTimer.control = "stop"
    ctx.progressTimer.control = "stop"
    ctx.progressBar.progress = 0

    if ctx.playlistIndex > 0 then
        ctx.playlistIndex = ctx.playlistIndex - 1
    else
        ctx.playlistIndex = ctx.playlist.Count() - 1
    end if

    ShowCurrentSlideForScene(ctx)
end sub

sub OnTransitionSafetyTimeoutForScene(ctx as Object)
    if ctx.isTransitioning then
        if TryFallbackMainPosterLoadForScene(ctx) then return
        LogDebug(PlaybackLogScope(ctx), "Transition safety timeout fired - clearing stuck flag")
        ctx.isTransitioning = false
        ctx.loadingCommitted = true
        HideLoadingIndicatorForScene(ctx)
        if ctx.loadingToSlot >= 0 and ctx.loadingToSlot < ctx.mainPosters.Count() then
            ctx.mainPosters[ctx.loadingToSlot].unobserveField("loadStatus")
        end if
        if ctx.loadingToSlot >= 0 and ctx.loadingToSlot < ctx.bgPosters.Count() then
            ctx.bgPosters[ctx.loadingToSlot].unobserveField("loadStatus")
        end if
        ctx.nextMainReady = false
        ctx.nextBgReady = false
        ctx.nextBgFailed = false
        ctx.loadingFromSlot = -1
        ctx.loadingToSlot = -1
        ctx.layoutRetryAttempts = 0
        ctx.layoutRetryTimer.control = "stop"
        ctx.bgSwapPending = false
        HandleAssetLoadFailureForScene(ctx)
    end if
end sub

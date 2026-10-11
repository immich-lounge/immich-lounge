' PlaybackAdvance.brs - shared slideshow advance and safety helpers

sub AdvanceToNextForScene(ctx as Object)
    if ApplyDailyPlaylistForScene(ctx) then return
    if ctx.playlist.Count() = 0 then return
    ctx.slideTimer.control = "stop"
    ctx.progressTimer.control = "stop"
    ctx.progressBar.progress = 0

    if ApplyScheduledProfileForScene(ctx) then return

    if ApplyPendingBatchSwapForScene(ctx, ctx.top.isScreensaver) then
        ShowCurrentSlideForScene(ctx)
        return
    end if

    if ShouldPrefetchNextPlaylistBatchForScene(ctx) then
        FetchNextPlaylistBatchForScene(ctx, ctx.top.isScreensaver)
    end if

    if ctx.playlistIndex + ctx.slideAssetCount >= ctx.playlist.Count() then
        hasMoreBatches = HasNextPlaylistWindowForScene(ctx)
        if hasMoreBatches then
            ctx.waitingForNextBatchSwap = true
            FetchNextPlaylistBatchForScene(ctx, ctx.top.isScreensaver)
            if ApplyPendingBatchSwapForScene(ctx, ctx.top.isScreensaver) then
                ShowCurrentSlideForScene(ctx)
                return
            end if

            if ctx.nextBatchTask <> invalid and ctx.nextBatchTask.control = "RUN" then
                ' Avoid replaying the current batch head while the next window is still loading.
                ctx.slideTimer.duration = 1
                ctx.slideTimer.control = "start"
                return
            end if
        end if

        ' Replaying the cached window is a new local loop, not a pending swap.
        ctx.waitingForNextBatchSwap = false
        ctx.playlistIndex = 0
    else
        ctx.playlistIndex = NextPortraitSlideIndex(ctx.playlistIndex, ctx.playlist.Count(), ctx.slideAssetCount)
    end if

    ShowCurrentSlideForScene(ctx)
end sub

sub AdvanceToPrevForScene(ctx as Object)
    if ctx.playlist.Count() = 0 then return
    ctx.slideTimer.control = "stop"
    ctx.progressTimer.control = "stop"
    ctx.progressBar.progress = 0

    ctx.playlistIndex = PreviousPortraitSlideIndex(ctx.playlist, ctx.playlistIndex, ctx.portraitPairs, ctx.sideBySidePortraits)

    ShowCurrentSlideForScene(ctx)
end sub

sub OnTransitionSafetyTimeoutForScene(ctx as Object)
    if ctx.isTransitioning then
        if ctx.nextMainReady and not ctx.nextPairReady then
            if TryFallbackPortraitLoadForScene(ctx) then return
            DropPendingPortraitForScene(ctx)
            ctx.transitionSafetyTimer.control = "stop"
            CommitSlideForScene(ctx, ctx.loadingFromSlot, ctx.loadingToSlot, ctx.pendingEntry)
            return
        end if
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

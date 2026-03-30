' PlaybackRecovery.brs - shared startup and asset-load recovery helpers

sub PrepareStartupStatusForScene(ctx as Object)
    ctx.hasShownFirstFrame = false
    if ctx.fallbackPanel <> invalid then
        ctx.fallbackPanel.opacity = 1.0
    else if ctx.fallbackPoster <> invalid then
        ctx.fallbackPoster.opacity = 1.0
    end if
    if ctx.statusLabel <> invalid then
        ctx.statusLabel.text = "Loading photos..."
        ctx.statusLabel.visible = true
    end if
    if ctx.spinner <> invalid then ctx.spinner.visible = true
end sub

sub HandleAssetLoadFailureForScene(ctx as Object)
    HideLoadingIndicatorForScene(ctx)
    ctx.layoutRetryAttempts = 0
    ctx.layoutRetryTimer.control = "stop"
    ctx.isTransitioning = false
    ctx.consecutiveFails = ctx.consecutiveFails + 1

    if not ctx.hasShownFirstFrame then
        if ctx.fallbackPanel <> invalid then
            ctx.fallbackPanel.opacity = 1.0
        else if ctx.fallbackPoster <> invalid then
            ctx.fallbackPoster.opacity = 1.0
        end if
        if ctx.statusLabel <> invalid then
            ctx.statusLabel.text = "Could not load the first photo. Retrying..."
            ctx.statusLabel.visible = true
        end if
    else if ctx.statusLabel <> invalid then
        ctx.statusLabel.text = "Photo load failed. Skipping to the next item..."
        ctx.statusLabel.visible = true
    end if

    if ctx.consecutiveFails > PlaybackConsecutiveFailureThreshold() then
        ShowErrorGroupForScene(ctx, "Having trouble loading photos. Retrying in " + PlaybackRetryDelaySeconds().ToStr() + "s")
        ctx.slideTimer.control = "stop"
        ctx.retryTimer = CreateObject("roSGNode", "Timer")
        ctx.retryTimer.duration = PlaybackRetryDelaySeconds()
        ctx.retryTimer.repeat = false
        ctx.retryTimer.observeField("fire", "OnRetryTimer")
        ctx.retryTimer.control = "start"
    else
        AdvanceToNextForScene(ctx)
    end if
end sub

sub OnRetryTimerForScene(ctx as Object)
    ctx.consecutiveFails = 0
    HideErrorGroupForScene(ctx)
    if ctx.statusLabel <> invalid then
        ctx.statusLabel.text = "Retrying photo load..."
        ctx.statusLabel.visible = true
    end if
    ShowCurrentSlideForScene(ctx)
end sub

sub OnLayoutRetryTimerForScene(ctx as Object)
    if ctx.loadingToSlot < 0 or ctx.loadingToSlot >= ctx.mainPosters.Count() then return
    node = ctx.mainPosters[ctx.loadingToSlot]
    if node.loadStatus <> "ready" then return
    if ApplyMainPosterLayoutForScene(ctx, node) then
        LogDebug(PlaybackLogScope(ctx), "Layout retry succeeded for slot=" + ctx.loadingToSlot.ToStr())
        if ctx.nextMainReady and ctx.nextBgReady and not ctx.loadingCommitted then
            ctx.transitionSafetyTimer.control = "stop"
            CommitSlideForScene(ctx, ctx.loadingFromSlot, ctx.loadingToSlot, ctx.pendingEntry)
        end if
    else
        QueueLayoutRetryForScene(ctx)
    end if
end sub

sub OnLoadingTimerForScene(ctx as Object)
    ShowLoadingIndicatorForScene(ctx)
end sub

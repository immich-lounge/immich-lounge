' PlaybackLoading.brs - shared slideshow load/commit helpers

sub OnNextPosterLoadedForScene(ctx as Object)
    if ctx.loadingToSlot < 0 or ctx.loadingToSlot >= ctx.mainPosters.Count() then return
    toNode = ctx.mainPosters[ctx.loadingToSlot]
    if ctx.loadingCommitted then
        toNode.unobserveField("loadStatus")
        return
    end if
    status = toNode.loadStatus
    LogDebug(PlaybackLogScope(ctx), "OnNextPosterLoaded slot=" + ctx.loadingToSlot.ToStr() + " status=" + status)
    if status = "ready" then
        toNode.unobserveField("loadStatus")
        if ApplyMainPosterLayoutForScene(ctx, toNode) then
            ctx.nextMainReady = true
            if ctx.nextBgReady then
                ctx.transitionSafetyTimer.control = "stop"
                CommitSlideForScene(ctx, ctx.loadingFromSlot, ctx.loadingToSlot, ctx.pendingEntry)
            end if
        else
            QueueLayoutRetryForScene(ctx)
        end if
    else if status = "failed" then
        toNode.unobserveField("loadStatus")
        if TryFallbackMainPosterLoadForScene(ctx) then return
        ctx.transitionSafetyTimer.control = "stop"
        HandleAssetLoadFailureForScene(ctx)
    end if
end sub

sub OnNextBgPosterLoadedForScene(ctx as Object)
    if ctx.loadingToSlot < 0 or ctx.loadingToSlot >= ctx.bgPosters.Count() then return
    bgNode = ctx.bgPosters[ctx.loadingToSlot]
    if ctx.loadingCommitted then
        bgNode.unobserveField("loadStatus")
        return
    end if
    status = bgNode.loadStatus
    LogDebug(PlaybackLogScope(ctx), "OnNextBgPosterLoaded slot=" + ctx.loadingToSlot.ToStr() + " status=" + status)
    if status = "ready" then
        bgNode.unobserveField("loadStatus")
        ctx.nextBgReady = true
        if ctx.nextMainReady then
            ctx.transitionSafetyTimer.control = "stop"
            CommitSlideForScene(ctx, ctx.loadingFromSlot, ctx.loadingToSlot, ctx.pendingEntry)
        end if
    else if status = "failed" then
        bgNode.unobserveField("loadStatus")
        LogDebug(PlaybackLogScope(ctx), "Background blur load failed for slot=" + ctx.loadingToSlot.ToStr())
        ctx.nextBgFailed = true
        if ctx.nextMainReady then
            ctx.transitionSafetyTimer.control = "stop"
            CommitSlideForScene(ctx, ctx.loadingFromSlot, ctx.loadingToSlot, ctx.pendingEntry)
        end if
    end if
end sub

function TryFallbackMainPosterLoadForScene(ctx as Object) as Boolean
    if ctx.loadingMainFallbackTried then return false
    if ctx.profile = invalid or ctx.pendingEntry = invalid then return false
    if ctx.pendingEntry.type = "video" or ctx.pendingEntry.type = "livePhoto" then return false

    quality = "preview"
    if ctx.profile.imageQuality <> invalid then quality = ctx.profile.imageQuality
    if quality = "original" then return false

    if ctx.loadingToSlot < 0 or ctx.loadingToSlot >= ctx.mainPosters.Count() then return false

    fallbackUrl = BuildBackgroundMediaUrl(ctx.pendingEntry, ctx.profile)
    if fallbackUrl = invalid or fallbackUrl = "" then return false
    if fallbackUrl = ctx.loadingMediaUrl then return false

    LogDebug(PlaybackLogScope(ctx), "Retrying main poster with thumbnail fallback for slot=" + ctx.loadingToSlot.ToStr())
    ctx.loadingMainFallbackTried = true
    ctx.loadingMediaUrl = fallbackUrl

    toNode = ctx.mainPosters[ctx.loadingToSlot]
    toNode.unobserveField("loadStatus")
    toNode.uri = ""
    toNode.uri = fallbackUrl
    ctx.ringUrls[ctx.loadingToSlot] = fallbackUrl
    toNode.observeField("loadStatus", "OnNextPosterLoaded")

    ctx.loadingTimer.control = "start"
    ctx.transitionSafetyTimer.control = "stop"
    ctx.transitionSafetyTimer.control = "start"
    return true
end function

sub CommitSlideForScene(ctx as Object, fromSlot as Integer, toSlot as Integer, entry as Object)
    if ctx.loadingCommitted then return
    ctx.loadingCommitted = true
    if toSlot >= 0 and toSlot < ctx.mainPosters.Count() then
        ctx.mainPosters[toSlot].unobserveField("loadStatus")
    end if
    if toSlot >= 0 and toSlot < ctx.bgPosters.Count() then
        ctx.bgPosters[toSlot].unobserveField("loadStatus")
    end if
    LogDebug(PlaybackLogScope(ctx), "CommitSlide from=" + fromSlot.ToStr() + " to=" + toSlot.ToStr() + " bgReady=" + ctx.nextBgReady.ToStr())
    if ctx.spinner.visible then ctx.spinner.visible = false

    if ctx.fallbackPanel <> invalid and not ctx.hasShownFirstFrame then
        ctx.hasShownFirstFrame = true
        ctx.fallbackPanel.opacity = 0.0
    else if ctx.fallbackPoster <> invalid and not ctx.hasShownFirstFrame then
        ctx.hasShownFirstFrame = true
        ctx.fallbackPoster.opacity = 0.0
    end if
    if ctx.statusLabel <> invalid then ctx.statusLabel.visible = false

    if not ctx.launchBeaconFired then
        ctx.launchBeaconFired = true
        ctx.top.signalBeacon("AppLaunchComplete")
    end if

    ctx.ringCurrent = toSlot
    ctx.ringNext = (toSlot + 1) mod ctx.ringSize
    ctx.ringReleasedSlot = fromSlot

    HideLoadingIndicatorForScene(ctx)
    PersistResumeStateForScene(ctx, entry, ctx.top.isScreensaver)
    ApplyAmbilightForEntryForScene(ctx, entry)
    FetchCurrentMetaForScene(ctx, entry)
    PrefetchNextMetaForScene(ctx)
    PreloadRingForScene(ctx, fromSlot)
    StartTransitionForScene(ctx, fromSlot, toSlot, entry)
    ctx.consecutiveFails = 0
    HideErrorGroupForScene(ctx)

    ctx.progressBar.visible = ctx.showTimer
    interval = GetDisplayInterval(entry, ctx.intervalSeconds)
    if interval > 0 then
        ctx.slideStartTime = UpTime(0)
        ctx.slideDuration = interval
        ctx.slideTimer.duration = interval
        ctx.slideTimer.control = "start"
        ctx.progressTimer.control = "start"
    end if
end sub

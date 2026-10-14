' PlaybackPortraits.brs - shared loading and recovery for portrait pairs.

sub ResetPendingPortraitForScene(ctx as Object)
    ' Keep a prefetched partner until its URL is checked against the next slide.
    node = ctx.pairedPosters[ctx.loadingToSlot]
    node.unobserveField("loadStatus")
    node.visible = false
    ctx.pendingPairEntry = invalid
    ctx.pairLoadStarted = false
    ctx.pairFallbackTried = false
    ctx.nextPairReady = true
    ctx.pendingSlideAssetCount = 1
    ctx.slideAssetCount = 1
end sub

sub PreloadNextPortraitForScene(ctx as Object)
    if not ctx.sideBySidePortraits or ctx.profile = invalid then return
    if ctx.playlist.Count() = 0 then return
    slot = ctx.ringNext
    if slot = ctx.ringCurrent or slot = ctx.activeBgSlot then return
    index = (ctx.playlistIndex + ValueOrDefault(ctx.slideAssetCount, 1)) mod ctx.playlist.Count()
    main = ctx.mainPosters[slot]
    if main.uri <> BuildMediaUrl(ctx.playlist[index], ctx.profile) then return
    if main.loadStatus <> "ready" then
        main.observeField("loadStatus", "OnPreloadPortraitMainLoaded")
        return
    end if
    main.unobserveField("loadStatus")
    candidate = PortraitPairCandidateIndex(ctx.playlist, index, main.bitmapWidth, main.bitmapHeight, true)
    if candidate < 0 then
        ReleasePortraitPosterForSlot(ctx, slot)
        return
    end if
    paired = ctx.pairedPosters[slot]
    paired.visible = false
    url = BuildMediaUrl(ctx.playlist[candidate], ctx.profile)
    if paired.uri <> url then
        paired.unobserveField("loadStatus")
        paired.uri = url
        LogDebug(PlaybackLogScope(ctx), "PreloadPortrait slot=" + slot.ToStr() + " index=" + candidate.ToStr())
    end if
end sub

sub TryStartPortraitPairForScene(ctx as Object)
    if ctx.pairLoadStarted or not ctx.nextMainReady then return
    main = ctx.mainPosters[ctx.loadingToSlot]
    candidate = PortraitPairCandidateIndex(ctx.playlist, ctx.playlistIndex, main.bitmapWidth, main.bitmapHeight, ctx.sideBySidePortraits)
    if candidate < 0 then
        ReleasePortraitPosterForSlot(ctx, ctx.loadingToSlot)
        return
    end if

    ctx.pairLoadStarted = true
    ctx.nextPairReady = false
    ctx.pendingPairEntry = ctx.playlist[candidate]
    node = ctx.pairedPosters[ctx.loadingToSlot]
    url = BuildMediaUrl(ctx.pendingPairEntry, ctx.profile)
    if node.uri = url and node.loadStatus = "ready" then
        LogDebug(PlaybackLogScope(ctx), "Using preloaded portrait slot=" + ctx.loadingToSlot.ToStr())
        if not ApplyLoadedPortraitPairForScene(ctx) then QueueLayoutRetryForScene(ctx)
        return
    end if
    ' Retry a failed prefetch through the normal load/fallback path.
    if node.uri = url and node.loadStatus = "failed" then node.uri = ""
    node.observeField("loadStatus", "OnNextPortraitLoaded")
    node.uri = url
end sub

sub DropPendingPortraitForScene(ctx as Object)
    node = ctx.pairedPosters[ctx.loadingToSlot]
    node.unobserveField("loadStatus")
    node.visible = false
    node.uri = ""
    ctx.nextPairReady = true
    ctx.pendingSlideAssetCount = 1
    ApplyMainPosterLayoutForScene(ctx, ctx.mainPosters[ctx.loadingToSlot])
end sub

function ApplyLoadedPortraitPairForScene(ctx as Object) as Boolean
    main = ctx.mainPosters[ctx.loadingToSlot]
    paired = ctx.pairedPosters[ctx.loadingToSlot]
    if paired.bitmapWidth <= 0 or paired.bitmapHeight <= 0 then return false
    layout = PortraitPairLayout(main.bitmapWidth, main.bitmapHeight, paired.bitmapWidth, paired.bitmapHeight, ctx.sceneWidth, ctx.sceneHeight)
    if layout = invalid then
        DropPendingPortraitForScene(ctx)
        return true
    end if

    main.width = layout.left.width
    main.height = layout.left.height
    main.translation = [layout.left.x, layout.left.y]
    main.scale = [1.0, 1.0]
    paired.width = layout.right.width
    paired.height = layout.right.height
    ' The right poster is a child of the left poster, so transitions move both.
    paired.translation = [layout.right.x - layout.left.x, layout.right.y - layout.left.y]
    paired.visible = true
    ctx.nextPairReady = true
    ctx.pendingSlideAssetCount = 2
    return true
end function

sub OnNextPortraitLoadedForScene(ctx as Object)
    if ctx.loadingToSlot < 0 or ctx.loadingToSlot >= ctx.pairedPosters.Count() then return
    node = ctx.pairedPosters[ctx.loadingToSlot]
    if ctx.loadingCommitted then
        node.unobserveField("loadStatus")
        return
    end if
    if node.loadStatus = "ready" then
        node.unobserveField("loadStatus")
        if not ApplyLoadedPortraitPairForScene(ctx) then
            QueueLayoutRetryForScene(ctx)
            return
        end if
    else if node.loadStatus = "failed" then
        if TryFallbackPortraitLoadForScene(ctx) then return
        DropPendingPortraitForScene(ctx)
    else
        return
    end if
    TryCommitLoadedSlideForScene(ctx)
end sub

function TryFallbackPortraitLoadForScene(ctx as Object) as Boolean
    if ctx.pairFallbackTried or ctx.pendingPairEntry = invalid then return false
    if ctx.profile.imageQuality = "original" then return false
    node = ctx.pairedPosters[ctx.loadingToSlot]
    url = BuildBackgroundMediaUrl(ctx.pendingPairEntry, ctx.profile)
    if url = "" or url = node.uri then return false
    ctx.pairFallbackTried = true
    node.unobserveField("loadStatus")
    node.uri = ""
    node.observeField("loadStatus", "OnNextPortraitLoaded")
    node.uri = url
    ctx.transitionSafetyTimer.control = "stop"
    ctx.transitionSafetyTimer.control = "start"
    return true
end function

sub RecordPortraitPairForScene(ctx as Object, entry as Object)
    ctx.slideAssetCount = 1
    ctx.currentPairAssetId = ""
    ' Remove any older overlapping pair after navigation or a setting change.
    if ctx.playlistIndex > 0 then
        previous = ctx.playlist[ctx.playlistIndex - 1]
        if ctx.portraitPairs[previous.id] = entry.id then ctx.portraitPairs.Delete(previous.id)
    end if
    ctx.portraitPairs.Delete(entry.id)
    if ctx.pendingSlideAssetCount = 2 then
        ctx.currentPairAssetId = ctx.pendingPairEntry.id
        ctx.slideAssetCount = PortraitSlideAdvanceCount(ctx.playlist, ctx.playlistIndex, ctx.currentPairAssetId)
        ctx.portraitPairs[entry.id] = ctx.currentPairAssetId
        ctx.portraitPairs.Delete(ctx.currentPairAssetId)
    end if
end sub

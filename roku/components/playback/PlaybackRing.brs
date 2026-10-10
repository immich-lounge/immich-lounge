' PlaybackRing.brs - shared slideshow ring and layout helpers

function ApplyMainPosterLayoutForScene(ctx as Object, node as Object) as Boolean
    bitmapWidth = node.bitmapWidth
    bitmapHeight = node.bitmapHeight
    if bitmapWidth <= 0 or bitmapHeight <= 0 then return false

    scaleX = ctx.sceneWidth / bitmapWidth
    scaleY = ctx.sceneHeight / bitmapHeight
    scale = scaleX
    zoomToFit = ctx.zoomToFit = true and bitmapWidth > bitmapHeight
    if zoomToFit then
        if scaleY > scale then scale = scaleY
    else
        if scaleY < scale then scale = scaleY
    end if

    fittedWidth = Int(bitmapWidth * scale)
    fittedHeight = Int(bitmapHeight * scale)

    if zoomToFit then
        ' Round up so snapping to the 3-pixel grid cannot expose an empty edge.
        fittedWidth = Int(bitmapWidth * scale / 3) * 3
        fittedHeight = Int(bitmapHeight * scale / 3) * 3
        if fittedWidth < bitmapWidth * scale then fittedWidth = fittedWidth + 3
        if fittedHeight < bitmapHeight * scale then fittedHeight = fittedHeight + 3
    else
        fittedWidth = fittedWidth - (fittedWidth mod 3)
        fittedHeight = fittedHeight - (fittedHeight mod 3)
    end if
    if fittedWidth <= 0 then fittedWidth = 3
    if fittedHeight <= 0 then fittedHeight = 3

    offsetX = Int((ctx.sceneWidth - fittedWidth) / 2)
    offsetY = Int((ctx.sceneHeight - fittedHeight) / 2)
    offsetX = offsetX - (offsetX mod 3)
    offsetY = offsetY - (offsetY mod 3)

    node.width = fittedWidth
    node.height = fittedHeight
    node.translation = [offsetX, offsetY]
    node.scale = [1.0, 1.0]
    node.scaleRotateCenter = [Int(fittedWidth / 2), Int(fittedHeight / 2)]
    return true
end function

sub ConstrainPhotoMotionPlanForScene(ctx as Object, node as Object, plan as Dynamic)
    if plan = invalid or ctx.zoomToFit <> true then return
    if node.width <= node.height then return
    ' A profile refresh can enable zoom while the current photo still has its
    ' fitted layout. Only constrain motion after a covering layout is applied.
    if node.width < ctx.sceneWidth or node.height < ctx.sceneHeight then return

    ' Match the center used by the motion animation. Clamping both endpoints
    ' keeps the viewport covered throughout its interpolated scale and pan.
    centerX = Int(node.width / 2)
    centerY = Int(node.height / 2)
    for each stage in ["start", "end"]
        scale = plan[stage + "Scale"]
        translation = plan[stage + "Translation"]
        minX = ctx.sceneWidth - centerX - (node.width - centerX) * scale
        maxX = centerX * (scale - 1.0)
        minY = ctx.sceneHeight - centerY - (node.height - centerY) * scale
        maxY = centerY * (scale - 1.0)
        plan[stage + "Translation"] = [Clamp(translation[0], minX, maxX), Clamp(translation[1], minY, maxY)]
    end for
end sub

sub QueueLayoutRetryForScene(ctx as Object)
    ctx.layoutRetryAttempts = ctx.layoutRetryAttempts + 1
    if ctx.layoutRetryAttempts > PlaybackLayoutRetryMaxAttempts() then
        LogDebug(PlaybackLogScope(ctx), "Layout retry exhausted for slot=" + ctx.loadingToSlot.ToStr())
        return
    end if
    ctx.layoutRetryTimer.control = "stop"
    ctx.layoutRetryTimer.control = "start"
end sub

sub PreloadRingForScene(ctx as Object, fromSlot as Integer)
    if ctx.profile = invalid then return
    n = ctx.playlist.Count()
    if n = 0 then return
    p = ctx.playlistIndex

    nextUrl = BuildMediaUrl(ctx.playlist[(p + 1) mod n], ctx.profile)
    nextBgUrl = BuildBackgroundMediaUrl(ctx.playlist[(p + 1) mod n], ctx.profile)
    nextMainNode = ctx.mainPosters[ctx.ringNext]
    if ctx.ringNext <> ctx.activeBgSlot and nextMainNode.uri <> nextUrl then
        nextMainNode.unobserveField("loadStatus")
        nextMainNode.uri = nextUrl
        if ctx.bgPosters[ctx.ringNext].uri <> nextBgUrl then ctx.bgPosters[ctx.ringNext].uri = ""
        ctx.bgPosters[ctx.ringNext].uri = nextBgUrl
        ctx.ringUrls[ctx.ringNext] = nextUrl
    end if

    slot = (ctx.ringNext + 1) mod ctx.ringSize
    lookahead = 2
    while slot <> ctx.ringCurrent
        if slot <> fromSlot and slot <> ctx.activeBgSlot then
            url = BuildMediaUrl(ctx.playlist[(p + lookahead) mod n], ctx.profile)
            bgUrl = BuildBackgroundMediaUrl(ctx.playlist[(p + lookahead) mod n], ctx.profile)
            mainNode = ctx.mainPosters[slot]
            if mainNode.uri <> url then
                mainNode.unobserveField("loadStatus")
                mainNode.uri = url
                if ctx.bgPosters[slot].uri <> bgUrl then ctx.bgPosters[slot].uri = ""
                ctx.bgPosters[slot].uri = bgUrl
                ctx.ringUrls[slot] = url
            end if
        end if
        slot = (slot + 1) mod ctx.ringSize
        lookahead = lookahead + 1
    end while
end sub

sub FinalizeRingForScene(ctx as Object)
    if ctx.ringReleasedSlot < 0 then return
    if ctx.ringReleasedSlot = ctx.activeBgSlot then
        ctx.ringReleasedSlot = -1
        return
    end if
    n = ctx.playlist.Count()
    if n = 0 then return

    lookahead = ctx.ringSize - 1
    url = BuildMediaUrl(ctx.playlist[(ctx.playlistIndex + lookahead) mod n], ctx.profile)
    bgUrl = BuildBackgroundMediaUrl(ctx.playlist[(ctx.playlistIndex + lookahead) mod n], ctx.profile)
    mainNode = ctx.mainPosters[ctx.ringReleasedSlot]
    if mainNode.uri <> url then
        mainNode.unobserveField("loadStatus")
        mainNode.uri = url
        if ctx.bgPosters[ctx.ringReleasedSlot].uri <> bgUrl then ctx.bgPosters[ctx.ringReleasedSlot].uri = ""
        ctx.bgPosters[ctx.ringReleasedSlot].uri = bgUrl
        ctx.ringUrls[ctx.ringReleasedSlot] = url
    end if
    ctx.ringReleasedSlot = -1
end sub

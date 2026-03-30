' PlaybackTransitions.brs - shared slideshow transition helpers

sub FinishBgTransitionForScene(ctx as Object)
    ResetBgTransitionStateForScene(ctx)
    FinalizeRingForScene(ctx)
    ctx.isTransitioning = false
end sub

sub StartTransitionForScene(ctx as Object, fromSlot as Integer, toSlot as Integer, entry as Object)
    effect = ctx.transitionEffect
    if effect = "random" then
        effects = ["fade", "slide", "zoom"]
        effect = effects[Rnd(3) - 1]
    end if
    if entry.type = "video" or entry.type = "livePhoto" then effect = "fade"
    if effect = "none" then
        ctx.activeTransitionDuration = 0.0
    else
        ctx.activeTransitionDuration = PlaybackTransitionDurationSeconds()
    end if

    fromNode = ctx.mainPosters[fromSlot]
    toNode = ctx.mainPosters[toSlot]
    fromId = "main" + fromSlot.ToStr()
    toId = "main" + toSlot.ToStr()
    fromBaseTranslation = fromNode.translation
    toBaseTranslation = toNode.translation
    ctx.activeMotionPlan = BuildPhotoMotionPlanForMode(ctx.photoMotion, entry, toNode, toBaseTranslation)

    ctx.pendingBgFromSlot = fromSlot
    ctx.pendingBgToSlot = toSlot
    ctx.bgSwapPending = false
    LogDebug(PlaybackLogScope(ctx), "StartTransition effect=" + effect + " from=" + fromSlot.ToStr() + " to=" + toSlot.ToStr() + " bgReady=" + ctx.nextBgReady.ToStr())

    StopMotionAnimationForScene(ctx)
    if ctx.activeAnimOut <> invalid then
        ctx.activeAnimOut.control = "stop"
        ctx.top.removeChild(ctx.activeAnimOut)
        ctx.activeAnimOut = invalid
    end if
    if ctx.activeAnimIn <> invalid then
        ctx.activeAnimIn.control = "stop"
        ctx.top.removeChild(ctx.activeAnimIn)
        ctx.activeAnimIn = invalid
    end if

    fromNode.translation = fromBaseTranslation
    toNode.translation = toBaseTranslation
    fromNode.scale = [1.0, 1.0]
    toNode.scale = [1.0, 1.0]
    if ctx.activeMotionPlan <> invalid then
        toNode.translation = ctx.activeMotionPlan.startTranslation
        toNode.scale = [ctx.activeMotionPlan.startScale, ctx.activeMotionPlan.startScale]
    end if
    fromNode.scaleRotateCenter = [Int(fromNode.width / 2), Int(fromNode.height / 2)]
    toNode.scaleRotateCenter = [Int(toNode.width / 2), Int(toNode.height / 2)]
    fromNode.opacity = 1.0
    toNode.opacity = 0.0

    if effect = "none" then
        fromNode.opacity = 0.0
        toNode.opacity = 1.0
        BgSwapForScene(ctx)
        StartActiveSlideMotionForScene(ctx)
        return
    end if

    ctx.transitionTimer.duration = PlaybackTransitionDurationSeconds()
    ctx.transitionTimer.control = "start"
    if ctx.nextBgReady then
        StartBgCrossfadeForScene(ctx, PlaybackTransitionDurationSeconds())
    end if

    if effect = "fade" then
        animOut = CreateObject("roSGNode", "Animation")
        animOut.duration = PlaybackTransitionDurationSeconds()
        animOut.repeat = false
        fadeOut = animOut.createChild("FloatFieldInterpolator")
        fadeOut.fieldToInterp = fromId + ".opacity"
        fadeOut.key = [0.0, 1.0]
        fadeOut.keyValue = [1.0, 0.0]
        ctx.top.appendChild(animOut)
        animOut.control = "start"
        ctx.activeAnimOut = animOut

        animIn = CreateObject("roSGNode", "Animation")
        animIn.duration = PlaybackTransitionDurationSeconds()
        animIn.repeat = false
        fadeIn = animIn.createChild("FloatFieldInterpolator")
        fadeIn.fieldToInterp = toId + ".opacity"
        fadeIn.key = [0.0, 1.0]
        fadeIn.keyValue = [0.0, 1.0]
        ctx.top.appendChild(animIn)
        animIn.control = "start"
        ctx.activeAnimIn = animIn
    else if effect = "slide" then
        targetTranslation = toBaseTranslation
        if ctx.activeMotionPlan <> invalid then targetTranslation = ctx.activeMotionPlan.startTranslation
        toNode.translation = [targetTranslation[0] + 1920, targetTranslation[1]]
        toNode.opacity = 1.0

        animIn = CreateObject("roSGNode", "Animation")
        animIn.duration = PlaybackTransitionDurationSeconds()
        animIn.repeat = false
        slideIn = animIn.createChild("Vector2DFieldInterpolator")
        slideIn.fieldToInterp = toId + ".translation"
        slideIn.key = [0.0, 1.0]
        slideIn.keyValue = [[targetTranslation[0] + 1920, targetTranslation[1]], [targetTranslation[0], targetTranslation[1]]]
        ctx.top.appendChild(animIn)
        animIn.control = "start"
        ctx.activeAnimIn = animIn

        animOut = CreateObject("roSGNode", "Animation")
        animOut.duration = PlaybackTransitionDurationSeconds()
        animOut.repeat = false
        slideOut = animOut.createChild("Vector2DFieldInterpolator")
        slideOut.fieldToInterp = fromId + ".translation"
        slideOut.key = [0.0, 1.0]
        slideOut.keyValue = [[fromBaseTranslation[0], fromBaseTranslation[1]], [fromBaseTranslation[0] - 1920, fromBaseTranslation[1]]]
        ctx.top.appendChild(animOut)
        animOut.control = "start"
        ctx.activeAnimOut = animOut
    else if effect = "zoom" then
        toNode.opacity = 0.0
        targetScale = 1.0
        if ctx.activeMotionPlan <> invalid then targetScale = ctx.activeMotionPlan.startScale
        toNode.scale = [0.8 * targetScale, 0.8 * targetScale]
        toNode.scaleRotateCenter = [Int(toNode.width / 2), Int(toNode.height / 2)]

        animZoom = CreateObject("roSGNode", "Animation")
        animZoom.duration = PlaybackTransitionDurationSeconds()
        animZoom.repeat = false
        zoomIn = animZoom.createChild("Vector2DFieldInterpolator")
        zoomIn.fieldToInterp = toId + ".scale"
        zoomIn.key = [0.0, 1.0]
        zoomIn.keyValue = [[0.8 * targetScale, 0.8 * targetScale], [targetScale, targetScale]]
        fadeIn = animZoom.createChild("FloatFieldInterpolator")
        fadeIn.fieldToInterp = toId + ".opacity"
        fadeIn.key = [0.0, 1.0]
        fadeIn.keyValue = [0.0, 1.0]
        ctx.top.appendChild(animZoom)
        animZoom.control = "start"
        ctx.activeAnimIn = animZoom

        animOut = CreateObject("roSGNode", "Animation")
        animOut.duration = PlaybackTransitionDurationSeconds()
        animOut.repeat = false
        fadeOut = animOut.createChild("FloatFieldInterpolator")
        fadeOut.fieldToInterp = fromId + ".opacity"
        fadeOut.key = [0.0, 1.0]
        fadeOut.keyValue = [1.0, 0.0]
        ctx.top.appendChild(animOut)
        animOut.control = "start"
        ctx.activeAnimOut = animOut
    end if
end sub

sub OnTransitionCompleteForScene(ctx as Object)
    if ctx.persistentLayer.visible then
        ctx.persistentLayer.opacity = 0.82
    end if
    LogDebug(PlaybackLogScope(ctx), "OnTransitionComplete pendingFrom=" + ctx.pendingBgFromSlot.ToStr() + " pendingTo=" + ctx.pendingBgToSlot.ToStr() + " bgReady=" + ctx.nextBgReady.ToStr())
    if ctx.bgTransitionStarted then
        FinishBgTransitionForScene(ctx)
    else
        BgSwapForScene(ctx)
    end if
    StartActiveSlideMotionForScene(ctx)
end sub

sub BgSwapForScene(ctx as Object)
    if not ctx.bgEnabled then
        StopBgAnimationsForScene(ctx)
        ResetBgTransitionStateForScene(ctx)
        for i = 0 to ctx.bgPosters.Count() - 1
            ctx.bgPosters[i].opacity = 0.0
        end for
        ctx.activeBgSlot = -1
        FinalizeRingForScene(ctx)
        ctx.isTransitioning = false
        return
    end if

    if ctx.pendingBgToSlot >= 0 and not ctx.nextBgReady then
        if ctx.nextBgFailed then
            LogDebug(PlaybackLogScope(ctx), "Keeping existing blur because next blur failed to load")
            ResetBgTransitionStateForScene(ctx)
            FinalizeRingForScene(ctx)
            ctx.isTransitioning = false
            return
        end if
        if ctx.activeBgSlot >= 0 then
            LogDebug(PlaybackLogScope(ctx), "Delaying bg swap until blur is ready for slot=" + ctx.pendingBgToSlot.ToStr())
            ctx.bgSwapPending = true
            return
        end if
        LogDebug(PlaybackLogScope(ctx), "No active blur available; proceeding with bg swap before blur is ready")
    end if

    LogDebug(PlaybackLogScope(ctx), "BgSwap from=" + ctx.pendingBgFromSlot.ToStr() + " to=" + ctx.pendingBgToSlot.ToStr())
    if StartBgCrossfadeForScene(ctx, 0.35) then
        FinishBgTransitionForScene(ctx)
    else
        ResetBgTransitionStateForScene(ctx)
        FinalizeRingForScene(ctx)
        ctx.isTransitioning = false
    end if
end sub

' PlaybackEffects.brs - shared slideshow/screensaver visual helpers

function GetAmbilightPalette(index as Integer) as Object
    palettes = [
        { leftSoft: "#5DA9FF2E", rightSoft: "#FF8A5B24", topStrong: "#FFE08A22", leftStrong: "#5DA9FF44", rightStrong: "#FF8A5B38", topSoft: "#FFE08A18" },
        { leftSoft: "#65D6C22E", rightSoft: "#6EA8FE24", topStrong: "#D9F99D22", leftStrong: "#65D6C244", rightStrong: "#6EA8FE38", topSoft: "#D9F99D18" },
        { leftSoft: "#C084FC2C", rightSoft: "#F472B624", topStrong: "#FDE68A22", leftStrong: "#C084FC42", rightStrong: "#F472B636", topSoft: "#FDE68A18" },
        { leftSoft: "#7DD3FC2E", rightSoft: "#FB718524", topStrong: "#FDE68A22", leftStrong: "#7DD3FC44", rightStrong: "#FB718538", topSoft: "#FDE68A18" },
        { leftSoft: "#34D3992E", rightSoft: "#F59E0B24", topStrong: "#F9FAFB1E", leftStrong: "#34D39944", rightStrong: "#F59E0B38", topSoft: "#F9FAFB16" }
    ]
    return palettes[index]
end function

function HashString(text as String) as Integer
    hash = 0
    for i = 1 to Len(text)
        hash = (hash * 31 + Asc(Mid(text, i, 1))) mod 2147483647
    end for
    if hash < 0 then hash = -hash
    return hash
end function

function RandomCenteredInt(maxAbs as Integer) as Integer
    if maxAbs <= 0 then return 0
    return Int(Rnd(maxAbs * 2 + 1)) - maxAbs
end function

function RandomOffsetPair(baseTranslation as Object, maxOffsetX as Integer, maxOffsetY as Integer) as Object
    return [baseTranslation[0] + RandomCenteredInt(maxOffsetX), baseTranslation[1] + RandomCenteredInt(maxOffsetY)]
end function

function BuildPhotoMotionPlanForMode(photoMotion as String, entry as Object, node as Object, baseTranslation as Object) as Object
    if photoMotion <> "kenBurns" then return invalid
    if entry = invalid or node = invalid then return invalid
    if entry.type = "video" or entry.type = "livePhoto" then return invalid

    motionScale = 1.06
    zoomOut = Int(Rnd(2)) = 1
    if zoomOut then
        startScale = motionScale
        endScale = 1.0
    else
        startScale = 1.0
        endScale = motionScale
    end if

    maxOffsetX = Int((node.width * (motionScale - 1.0)) / 2 * 0.24)
    maxOffsetY = Int((node.height * (motionScale - 1.0)) / 2 * 0.24)

    return {
        startScale: startScale
        endScale: endScale
        startTranslation: RandomOffsetPair(baseTranslation, maxOffsetX, maxOffsetY)
        endTranslation: RandomOffsetPair(baseTranslation, maxOffsetX, maxOffsetY)
    }
end function

function GetDisplayInterval(entry as Object, intervalSeconds as Integer) as Integer
    if entry.type = "video" or entry.type = "livePhoto" then return 0
    return intervalSeconds
end function

sub ResetBgTransitionStateForScene(ctx as Object)
    ctx.pendingBgFromSlot = -1
    ctx.pendingBgToSlot = -1
    ctx.bgSwapPending = false
    ctx.bgTransitionStarted = false
    ctx.nextBgReady = false
    ctx.nextBgFailed = false
end sub

sub StopBgAnimationsForScene(ctx as Object)
    if ctx.activeBgAnimOut <> invalid then
        ctx.activeBgAnimOut.control = "stop"
        ctx.top.removeChild(ctx.activeBgAnimOut)
        ctx.activeBgAnimOut = invalid
    end if
    if ctx.activeBgAnimIn <> invalid then
        ctx.activeBgAnimIn.control = "stop"
        ctx.top.removeChild(ctx.activeBgAnimIn)
        ctx.activeBgAnimIn = invalid
    end if
end sub

sub StopMotionAnimationForScene(ctx as Object)
    if ctx.activeMotionAnim <> invalid then
        ctx.activeMotionAnim.control = "stop"
        ctx.top.removeChild(ctx.activeMotionAnim)
        ctx.activeMotionAnim = invalid
    end if
end sub

sub ApplyBackgroundPosterLayoutForScene(ctx as Object, width as Integer, height as Integer, translation as Object)
    for i = 0 to ctx.bgPosters.Count() - 1
        node = ctx.bgPosters[i]
        node.width = width
        node.height = height
        node.translation = translation
    end for
end sub

sub ApplyAmbilightForEntryForScene(ctx as Object, entry as Object)
    if ctx.backgroundEffect <> "ambilight" then return
    if ctx.bgGlowLeft = invalid or ctx.bgGlowRight = invalid or ctx.bgGlowTop = invalid then return

    node = ctx.mainPosters[ctx.ringCurrent]
    isPortrait = false
    if node <> invalid and node.height > node.width then isPortrait = true

    paletteIndex = HashString(ValueOrDefault(entry.id, "") + "|" + ValueOrDefault(entry.sourceLabel, "")) mod 5
    palette = GetAmbilightPalette(paletteIndex)

    if isPortrait then
        ctx.bgGlowLeft.width = 390
        ctx.bgGlowRight.width = 390
        ctx.bgGlowRight.translation = [1530, 0]
        ctx.bgGlowTop.height = 150
        ctx.bgGlowLeft.color = palette.leftStrong
        ctx.bgGlowRight.color = palette.rightStrong
        ctx.bgGlowTop.color = palette.topSoft
    else
        ctx.bgGlowLeft.width = 300
        ctx.bgGlowRight.width = 300
        ctx.bgGlowRight.translation = [1620, 0]
        ctx.bgGlowTop.height = 180
        ctx.bgGlowLeft.color = palette.leftSoft
        ctx.bgGlowRight.color = palette.rightSoft
        ctx.bgGlowTop.color = palette.topStrong
    end if
    ctx.bgGlowTop.translation = [0, 0]
end sub

function StartBgCrossfadeForScene(ctx as Object, duration as Float) as Boolean
    if ctx.pendingBgToSlot < 0 or not ctx.nextBgReady then return false

    LogDebug("SlideshowScene", "StartBgCrossfade from=" + ctx.pendingBgFromSlot.ToStr() + " to=" + ctx.pendingBgToSlot.ToStr() + " duration=" + Str(duration).Trim())
    StopBgAnimationsForScene(ctx)

    if ctx.pendingBgFromSlot >= 0 then
        fromId = "bg" + ctx.pendingBgFromSlot.ToStr()
        animOut = CreateObject("roSGNode", "Animation")
        animOut.duration = duration
        animOut.repeat = false
        fadeOut = animOut.createChild("FloatFieldInterpolator")
        fadeOut.fieldToInterp = fromId + ".opacity"
        fadeOut.key = [0.0, 1.0]
        fadeOut.keyValue = [ctx.bgPosters[ctx.pendingBgFromSlot].opacity, 0.0]
        ctx.top.appendChild(animOut)
        animOut.control = "start"
        ctx.activeBgAnimOut = animOut
    end if

    toId = "bg" + ctx.pendingBgToSlot.ToStr()
    ctx.bgPosters[ctx.pendingBgToSlot].opacity = 0.0
    animIn = CreateObject("roSGNode", "Animation")
    animIn.duration = duration
    animIn.repeat = false
    fadeIn = animIn.createChild("FloatFieldInterpolator")
    fadeIn.fieldToInterp = toId + ".opacity"
    fadeIn.key = [0.0, 1.0]
    fadeIn.keyValue = [0.0, ctx.bgTargetOpacity]
    ctx.top.appendChild(animIn)
    animIn.control = "start"
    ctx.activeBgAnimIn = animIn
    ctx.activeBgSlot = ctx.pendingBgToSlot
    ctx.bgTransitionStarted = true
    return true
end function

sub ShowLoadingIndicatorForScene(ctx as Object)
    ctx.mainPosters[ctx.ringCurrent].opacity = 0.6
    ctx.loadingIndicator.visible = true
    ctx.loadingPulse.control = "start"
end sub

sub HideLoadingIndicatorForScene(ctx as Object)
    ctx.loadingTimer.control = "stop"
    ctx.loadingPulse.control = "stop"
    ctx.loadingIndicator.visible = false
    ctx.mainPosters[ctx.ringCurrent].opacity = 1.0
end sub

sub ShowErrorGroupForScene(ctx as Object, message as String)
    ctx.errorLabel.text = message
    ctx.errorGroup.visible = true
end sub

sub HideErrorGroupForScene(ctx as Object)
    ctx.errorGroup.visible = false
end sub

sub ResumeSlideshowForScene(ctx as Object)
    ctx.paused = false
    if ctx.playlist.Count() > 0 then
        entry = ctx.playlist[ctx.playlistIndex]
        interval = GetDisplayInterval(entry, ctx.intervalSeconds)
        if interval > 0 then
            ctx.slideStartTime = UpTime(0)
            ctx.slideDuration = interval
            ctx.slideTimer.duration = interval
            ctx.slideTimer.control = "start"
            ctx.progressTimer.control = "start"
        end if
    end if
end sub

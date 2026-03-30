' PlaybackProfile.brs - shared scene profile application helpers

sub ApplyProfileForScene(ctx as Object)
    if ctx.profile = invalid then return

    display = ctx.profile.display
    formatting = ResolveDisplayFormatting(display)
    if display <> invalid then
        ctx.overlay.overlayStyle = ValueOrDefault(display.overlayStyle, "none")
        ctx.backgroundEffect = ValueOrDefault(display.backgroundEffect, "blur")
        ctx.overlay.overlayFields = ValueOrDefault(display.overlayFields, [])
        ctx.overlay.overlayBehavior = ValueOrDefault(display.overlayBehavior, "manual")
        ctx.overlay.fadeSeconds = ValueOrDefault(display.overlayFadeSeconds, 5.0)
        ctx.overlay.clockFormat = formatting.clockFormat
        ctx.overlay.dateFormat = formatting.dateFormat
        ctx.overlay.locale = formatting.locale

        ctx.showTimer = ValueOrDefault(display.showTimer, true)
        ctx.progressBar.visible = ctx.showTimer
    else
        ctx.backgroundEffect = "blur"
        ctx.overlay.clockFormat = formatting.clockFormat
        ctx.overlay.dateFormat = formatting.dateFormat
        ctx.overlay.locale = formatting.locale
    end if

    ApplyBackgroundStyleForScene(ctx)

    slideshow = ctx.profile.slideshow
    if slideshow <> invalid then
        ctx.intervalSeconds = ValueOrDefault(slideshow.intervalSeconds, PlaybackSlideIntervalDefaultSeconds())
        ctx.transitionEffect = ValueOrDefault(slideshow.transitionEffect, "none")
        ctx.photoMotion = ValueOrDefault(slideshow.photoMotion, "none")
        ctx.refreshIntervalMinutes = ValueOrDefault(slideshow.refreshIntervalMinutes, PlaybackRefreshIntervalDefaultMinutes())
    else
        ctx.intervalSeconds = PlaybackSlideIntervalDefaultSeconds()
        ctx.transitionEffect = "none"
        ctx.photoMotion = "none"
        ctx.refreshIntervalMinutes = PlaybackRefreshIntervalDefaultMinutes()
    end if

    ctx.refreshTimer.duration = ctx.refreshIntervalMinutes * PlaybackClockDatePollSeconds()
    ctx.refreshTimer.control = "start"

    ApplyWeatherAndClockProfileForScene(ctx, display, ctx.profile.weather)

    if ctx.profile.immich <> invalid and ctx.profile.immich.apiKey <> invalid then
        authHeaders = { "x-api-key": ctx.profile.immich.apiKey }
        for i = 0 to ctx.ringSize - 1
            ctx.mainPosters[i].SetHeaders(authHeaders)
            ctx.bgPosters[i].SetHeaders(authHeaders)
        end for
    end if
end sub

sub ApplyBackgroundStyleForScene(ctx as Object)
    effect = ctx.backgroundEffect
    if effect = "none" then
        ctx.bgEnabled = false
        ctx.bgTargetOpacity = 0.0
        if ctx.bgTint <> invalid then ctx.bgTint.visible = false
        if ctx.bgGlowLeft <> invalid then ctx.bgGlowLeft.visible = false
        if ctx.bgGlowRight <> invalid then ctx.bgGlowRight.visible = false
        if ctx.bgGlowTop <> invalid then ctx.bgGlowTop.visible = false
        ApplyBackgroundPosterLayoutForScene(ctx, 2106, 1185, [-93, -54])
        for i = 0 to ctx.bgPosters.Count() - 1
            ctx.bgPosters[i].opacity = 0.0
        end for
        ctx.activeBgSlot = -1
    else if effect = "ambilight" then
        ctx.bgEnabled = true
        ctx.bgTargetOpacity = 0.68
        ApplyBackgroundPosterLayoutForScene(ctx, 2268, 1278, [-174, -99])
        if ctx.bgTint <> invalid then
            ctx.bgTint.visible = true
            ctx.bgTint.color = "#03070D22"
        end if
        if ctx.bgGlowLeft <> invalid then ctx.bgGlowLeft.visible = true
        if ctx.bgGlowRight <> invalid then ctx.bgGlowRight.visible = true
        if ctx.bgGlowTop <> invalid then ctx.bgGlowTop.visible = true
        if ctx.activeBgSlot >= 0 then ctx.bgPosters[ctx.activeBgSlot].opacity = ctx.bgTargetOpacity
    else
        ctx.bgEnabled = true
        ctx.bgTargetOpacity = 0.34
        ApplyBackgroundPosterLayoutForScene(ctx, 2106, 1185, [-93, -54])
        if ctx.bgTint <> invalid then
            ctx.bgTint.visible = true
            ctx.bgTint.color = "#08101888"
        end if
        if ctx.bgGlowLeft <> invalid then ctx.bgGlowLeft.visible = false
        if ctx.bgGlowRight <> invalid then ctx.bgGlowRight.visible = false
        if ctx.bgGlowTop <> invalid then ctx.bgGlowTop.visible = false
        if ctx.activeBgSlot >= 0 then ctx.bgPosters[ctx.activeBgSlot].opacity = ctx.bgTargetOpacity
    end if
end sub

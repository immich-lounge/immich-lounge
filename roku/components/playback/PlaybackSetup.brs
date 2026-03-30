' PlaybackSetup.brs - shared scene initialization helpers

sub InitPlaybackSceneForScene(ctx as Object)
    ctx.sceneWidth = 1920
    ctx.sceneHeight = 1080
    ctx.bgTargetOpacity = 0.34
    ctx.backgroundEffect = "blur"
    ctx.photoMotion = "none"

    ctx.overlay = ctx.top.findNode("overlay")
    ctx.toast = ctx.top.findNode("toast")
    ctx.progressBar = ctx.top.findNode("progressBar")
    ctx.errorGroup = ctx.top.findNode("errorGroup")
    ctx.errorLabel = ctx.top.findNode("errorLabel")
    ctx.persistentLayer = ctx.top.findNode("persistentLayer")
    ctx.bgTint = ctx.top.findNode("bgTint")
    ctx.bgGlowLeft = ctx.top.findNode("bgGlowLeft")
    ctx.bgGlowRight = ctx.top.findNode("bgGlowRight")
    ctx.bgGlowTop = ctx.top.findNode("bgGlowTop")
    ctx.statusLabel = ctx.top.findNode("statusLabel")
    ctx.fallbackPoster = ctx.top.findNode("fallbackPoster")
    ctx.fallbackPanel = ctx.top.findNode("fallbackPanel")
    ctx.spinner = ctx.top.findNode("spinner")
    ctx.spinner.poster.uri = "pkg:/images/busyspinner_hd.png"
    ctx.spinner.poster.observeField("loadStatus", "OnSpinnerImageLoaded")

    ctx.ringSize = 4
    ctx.mainPosters = []
    ctx.bgPosters = []
    ctx.ringUrls = []
    for i = 0 to ctx.ringSize - 1
        ctx.mainPosters.Push(ctx.top.findNode("main" + i.ToStr()))
        ctx.bgPosters.Push(ctx.top.findNode("bg" + i.ToStr()))
        ctx.ringUrls.Push("")
    end for

    ctx.playlist = []
    ctx.playlistIndex = 0
    ctx.playlistOffset = 0
    ctx.nextPlaylistOffset = 0
    ctx.totalPlaylistCount = 0
    ctx.pendingBatchAssets = invalid
    ctx.pendingBatchOffset = 0
    ctx.pendingNextPlaylistOffset = 0
    ctx.waitingForNextBatchSwap = false
    ctx.consecutiveFails = 0
    ctx.paused = false
    ctx.overlayVisible = true
    ctx.cachedWeather = invalid
    ctx.profile = invalid
    ctx.hasShownFirstFrame = false
    ctx.slideStartTime = 0
    ctx.slideDuration = PlaybackSlideIntervalDefaultSeconds()
    ctx.activeTransitionDuration = 0.0
    ctx.activeAnimOut = invalid
    ctx.activeAnimIn = invalid
    ctx.activeBgAnimOut = invalid
    ctx.activeBgAnimIn = invalid
    ctx.activeMotionAnim = invalid
    ctx.launchBeaconFired = false
    ctx.showTimer = true
    ctx.showDate = false
    ctx.dateStr = ""
    ctx.clockFormat = "HH:mm"

    ctx.ringCurrent = 0
    ctx.ringNext = 1
    ctx.ringReleasedSlot = -1

    ctx.pendingBgFromSlot = -1
    ctx.pendingBgToSlot = -1
    ctx.activeBgSlot = -1
    ctx.bgSwapPending = false
    ctx.bgTransitionStarted = false
    ctx.loadingFromSlot = -1
    ctx.loadingToSlot = -1

    ctx.nextMainReady = false
    ctx.nextBgReady = false
    ctx.nextBgFailed = false
    ctx.loadingCommitted = false
    ctx.loadingMainFallbackTried = false
    ctx.isTransitioning = false

    ctx.slideTimer = CreateObject("roSGNode", "Timer")
    ctx.slideTimer.repeat = false
    ctx.slideTimer.observeField("fire", "OnSlideTimer")

    ctx.progressTimer = CreateObject("roSGNode", "Timer")
    ctx.progressTimer.duration = PlaybackProgressTickSeconds()
    ctx.progressTimer.repeat = true
    ctx.progressTimer.observeField("fire", "OnProgressTick")

    ctx.clockTimer = CreateObject("roSGNode", "Timer")
    ctx.clockTimer.duration = 1
    ctx.clockTimer.repeat = true
    ctx.clockTimer.observeField("fire", "OnClockTick")
    ctx.clockTimer.control = "stop"

    ctx.refreshTimer = CreateObject("roSGNode", "Timer")
    ctx.refreshTimer.repeat = false
    ctx.refreshTimer.observeField("fire", "OnRefreshTimer")

    ctx.transitionTimer = CreateObject("roSGNode", "Timer")
    ctx.transitionTimer.repeat = false
    ctx.transitionTimer.observeField("fire", "OnTransitionComplete")

    ctx.top.observeField("profile", "OnProfileSet")
    ctx.top.observeField("playlist", "OnPlaylistSet")

    ctx.pauseIndicator = ctx.top.findNode("pauseIndicator")
    ctx.loadingIndicator = ctx.top.findNode("loadingIndicator")

    ctx.loadingTimer = CreateObject("roSGNode", "Timer")
    ctx.loadingTimer.duration = PlaybackLoadingIndicatorDelaySeconds()
    ctx.loadingTimer.repeat = false
    ctx.loadingTimer.observeField("fire", "OnLoadingTimer")

    ctx.transitionSafetyTimer = CreateObject("roSGNode", "Timer")
    ctx.transitionSafetyTimer.duration = PlaybackTransitionSafetyTimeoutSeconds()
    ctx.transitionSafetyTimer.repeat = false
    ctx.transitionSafetyTimer.observeField("fire", "OnTransitionSafetyTimeout")

    ctx.layoutRetryTimer = CreateObject("roSGNode", "Timer")
    ctx.layoutRetryTimer.duration = PlaybackLayoutRetryDelaySeconds()
    ctx.layoutRetryTimer.repeat = false
    ctx.layoutRetryTimer.observeField("fire", "OnLayoutRetryTimer")

    ctx.loadingPulse = CreateObject("roSGNode", "Animation")
    ctx.loadingPulse.duration = 0.7
    ctx.loadingPulse.repeat = true
    pulse = ctx.loadingPulse.createChild("FloatFieldInterpolator")
    pulse.fieldToInterp = "loadingIndicator.opacity"
    pulse.key = [0.0, 0.5, 1.0]
    pulse.keyValue = [0.3, 1.0, 0.3]
    ctx.top.appendChild(ctx.loadingPulse)
end sub

sub OnSpinnerImageLoadedForScene(ctx as Object)
    if ctx.spinner.poster.loadStatus = "ready" then
        cx = Int((1920 - ctx.spinner.poster.bitmapWidth) / 2)
        cy = Int((1080 - ctx.spinner.poster.bitmapHeight) / 2)
        if ctx.fallbackPanel <> invalid then
            cy = 750
        end if
        ctx.spinner.translation = [cx, cy]
    end if
end sub

function AssignProfileFromTopForScene(ctx as Object) as Boolean
    ctx.profile = ctx.top.profile
    return ctx.profile <> invalid
end function

sub ApplyPlaylistFromTopForScene(ctx as Object)
    ctx.playlist = ctx.top.playlist
    ctx.playlistIndex = ctx.top.startPlaylistIndex
    if ctx.playlistIndex < 0 or ctx.playlistIndex >= ctx.playlist.Count() then
        ctx.playlistIndex = 0
    end if
    ctx.playlistOffset = ctx.top.playlistOffset
    ctx.nextPlaylistOffset = ctx.top.nextPlaylistOffset
    ctx.totalPlaylistCount = ctx.top.totalPlaylistCount
    ctx.pendingBatchAssets = invalid
    ctx.pendingBatchOffset = 0
    ctx.pendingNextPlaylistOffset = 0
    ctx.waitingForNextBatchSwap = false
    PrepareStartupStatusForScene(ctx)
    ShowCurrentSlideForScene(ctx)
end sub

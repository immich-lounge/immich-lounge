' SlideshowScene.brs — Full-screen photo/video slideshow
'
' Ring buffer design:
'   RING_SIZE paired slots (main full-res + bg low-res) indexed 0..RING_SIZE-1.
'   m.ringCurrent = currently visible slot index
'   m.ringNext    = slot being loaded for the next transition
'   After CommitSlide, PreloadRing fills remaining slots with lookahead entries
'   (skipping fromSlot while its animation runs). FinalizeRing recycles fromSlot
'   once the transition animation completes.

sub init()
    InitPlaybackSceneForScene(m)
    m.top.setFocus(true)
    m.screensaverControl = m.top.findNode("screensaverControl")

    m.openSettingsTimer = CreateObject("roSGNode", "Timer")
    m.openSettingsTimer.duration = 0.1
    m.openSettingsTimer.repeat = false
    m.openSettingsTimer.observeField("fire", "OnOpenSettingsTimer")
end sub

sub OnSpinnerImageLoaded()
    OnSpinnerImageLoadedForScene(m)
end sub

sub OnProfileSet()
    if not AssignProfileFromTopForScene(m) then return
    ApplyProfile()
end sub

' Apply all derived settings from m.profile — called on initial load and every refresh.
sub ApplyProfile()
    preventScreensaver = false
    slideshow = invalid
    if m.profile <> invalid then slideshow = m.profile.slideshow
    if slideshow <> invalid then
        preventScreensaver = ValueOrDefault(slideshow.preventScreensaver, false)
    end if
    ApplyProfileForScene(m)

    ' Screensaver prevention: only active in channel mode (not screensaver app) and
    ' only when the profile opts in via slideshow.preventScreensaver.
    if not m.top.isScreensaver then
        m.screensaverControl.disableScreenSaver = preventScreensaver
    end if
end sub

sub OnPlaylistSet()
    ApplyPlaylistFromTopForScene(m)
    if m.top.openSettingsOnStart then
        m.top.openSettingsOnStart = false
        m.openSettingsTimer.control = "start"
    end if
end sub

sub OnOpenSettingsTimer()
    ShowSettingsDialogForScene(m)
end sub

sub OnLayoutRetryTimer()
    OnLayoutRetryTimerForScene(m)
end sub

' ---- Slide Advance ----

sub OnNextPosterLoaded()
    OnNextPosterLoadedForScene(m)
end sub

sub OnNextBgPosterLoaded()
    OnNextBgPosterLoadedForScene(m)
end sub

' Shared transition commit: advance ring indices, preload lookahead, start animation.
' Called after the animation completes — swap bg posters then recycle the released slot.
sub OnTransitionComplete()
    OnTransitionCompleteForScene(m)
end sub

sub OnSlideTimer()
    OnSlideTimerForScene(m)
end sub

sub OnProgressTick()
    OnProgressTickForScene(m)
end sub

' ---- Metadata + Overlay ----

sub OnMetaResult()
    OnMetaResultForScene(m)
end sub

' ---- Clock ----

sub OnClockTick()
    UpdateClockDisplayForScene(m)
end sub

' Safety timeout: if loading gets stuck for 10+ seconds, force clear the flag and hide indicator
sub OnTransitionSafetyTimeout()
    OnTransitionSafetyTimeoutForScene(m)
end sub

sub OnClockDateTimer()
    FetchClockDateForScene(m)
end sub

sub OnClockDateResult()
    ApplyClockDateResultForScene(m)
end sub

' ---- Weather ----

sub OnWeatherTimer()
    FetchWeatherForScene(m)
end sub

sub OnWeatherResult()
    ApplyWeatherResultForScene(m)
end sub

' ---- Playlist batch fetch (triggered when the 50-item window is exhausted) ----

sub MaybePrefetchNextPlaylistBatch()
    if ShouldPrefetchNextPlaylistBatchForScene(m) then
        FetchNextPlaylistBatchForScene(m, m.top.isScreensaver)
    end if
end sub

sub OnNextBatchResult()
    OnNextBatchResultForScene(m)
end sub

' ---- Refresh ----

sub OnRefreshTimer()
    OnRefreshTimerForScene(m)
end sub

sub OnTriggerRefresh()
    OnTriggerRefreshForScene(m)
end sub

sub OnRefreshProfileResult()
    OnRefreshProfileResultForScene(m, m.top.isScreensaver)
end sub

sub OnRefreshPlaylistResult()
    OnRefreshPlaylistResultForScene(m, m.top.isScreensaver)
end sub

' ---- Error handling ----

sub OnLoadingTimer()
    OnLoadingTimerForScene(m)
end sub

sub OnRetryTimer()
    OnRetryTimerForScene(m)
end sub

sub OnPausedChanged()
    OnPausedChangedForScene(m)
end sub

' ---- Remote key handling ----

function onKeyEvent(key as String, press as Boolean) as Boolean
    if not press then return false

    ' Screensavers must not accept user input — any key press exits via the system.
    if m.top.isScreensaver then return false

    LogDebug("SlideshowScene", "key=" + key + " press=true transitioning=" + m.isTransitioning.ToStr() + " current=" + m.ringCurrent.ToStr() + " next=" + m.ringNext.ToStr())

    ' Block prev/next navigation during loading and transitions to prevent race conditions
    if key = "left" then
        if not m.isTransitioning then AdvanceToPrevForScene(m)
        return true
    else if key = "right" then
        if not m.isTransitioning then AdvanceToNextForScene(m)
        return true
    else if key = "OK" then
        ToggleOverlayForScene(m)
        return true
    else if key = "up" then
        ShowOverlayForScene(m)
        return true
    else if key = "down" then
        HideOverlayForScene(m)
        return true
    else if key = "play" then
        m.top.paused = not m.top.paused
        return true
    else if key = "options" then   ' * key
        ShowSettingsDialogForScene(m)
        return true
    else if key = "back" then
        ShowExitDialogForScene(m)
        return true
    end if

    return false
end function

sub OnSettingsButton()
    OnSettingsButtonForScene(m)
end sub

sub OnSettingsDismissed()
    OnSettingsDismissedForScene(m)
end sub

sub OnExitButton()
    OnExitButtonForScene(m)
end sub

sub OnExitDismissed()
    OnExitDismissedForScene(m)
end sub

' ---- Utility functions (also used by tests) ----


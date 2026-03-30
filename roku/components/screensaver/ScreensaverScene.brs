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
    ApplyProfileForScene(m)
end sub

sub OnPlaylistSet()
    ApplyPlaylistFromTopForScene(m)
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
        FetchNextPlaylistBatchForScene(m, true)
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
    OnRefreshProfileResultForScene(m, true)
end sub

sub OnRefreshPlaylistResult()
    OnRefreshPlaylistResultForScene(m, true)
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

' ---- Utility functions (also used by tests) ----


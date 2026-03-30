' PlaybackState.brs - shared overlay/meta/state helpers

sub FetchCurrentMetaForScene(ctx as Object, entry as Object)
    ctx.currentEntry = entry
    if ctx.overlay <> invalid then
        ctx.overlay.assetMeta = {}
        ctx.overlay.sourceLabel = ValueOrDefault(entry.sourceLabel, "")
    end if

    if ctx.profile = invalid then return
    if ctx.profile.immich = invalid then return
    if ctx.currentMetaTask <> invalid then
        ctx.currentMetaTask.unobserveField("metaResult")
    end if
    task = CreateObject("roSGNode", "AssetMetaTask")
    task.assetId = entry.id
    task.immichBaseUrl = ctx.profile.immich.serverUrl
    task.apiKey = ctx.profile.immich.apiKey
    task.companionUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.formatSource = ctx.formatSource
    task.useLocalDateFormatting = ctx.top.useLocalDateFormatting
    task.dateFormat = ctx.overlay.dateFormat
    task.locale = ctx.overlay.locale
    task.observeField("metaResult", "OnMetaResult")
    ctx.currentMetaTask = task
    task.command = "fetchMeta"
end sub

sub OnMetaResultForScene(ctx as Object)
    result = ctx.currentMetaTask.metaResult
    if result.ok = true then
        ctx.overlay.assetMeta = result.data
        ctx.overlay.sourceLabel = ValueOrDefault(ctx.currentEntry.sourceLabel, "")
    else if ctx.overlay <> invalid then
        ' Keep the current slide's source label, but clear stale per-asset metadata
        ' when we cannot refresh details for a cached/offline slide.
        ctx.overlay.assetMeta = {}
        ctx.overlay.sourceLabel = ValueOrDefault(ctx.currentEntry.sourceLabel, "")
    end if
end sub

sub PrefetchNextMetaForScene(ctx as Object)
    if ctx.profile = invalid then return
    if ctx.profile.immich = invalid then return
    if ctx.playlist.Count() = 0 then return
    nextIdx = ctx.playlistIndex + 1
    if nextIdx >= ctx.playlist.Count() then nextIdx = 0
    nextEntry = ctx.playlist[nextIdx]

    task = CreateObject("roSGNode", "AssetMetaTask")
    task.assetId = nextEntry.id
    task.immichBaseUrl = ctx.profile.immich.serverUrl
    task.apiKey = ctx.profile.immich.apiKey
    task.companionUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.formatSource = ctx.formatSource
    task.useLocalDateFormatting = ctx.top.useLocalDateFormatting
    task.dateFormat = ctx.overlay.dateFormat
    task.locale = ctx.overlay.locale
    ctx.prefetchTask = task
    task.command = "fetchMeta"
end sub

sub ToggleOverlayForScene(ctx as Object)
    if ctx.overlay.overlayStyle = "none" then return
    ctx.overlayVisible = not ctx.overlayVisible
    ctx.overlay.visible = ctx.overlayVisible
end sub

sub ShowOverlayForScene(ctx as Object)
    if ctx.overlay.overlayStyle = "none" then return
    ctx.overlayVisible = true
    ctx.overlay.visible = true
end sub

sub HideOverlayForScene(ctx as Object)
    ctx.overlayVisible = false
    ctx.overlay.visible = false
end sub

sub HistoryPushEntry(hist as Object, entry as Object)
    hist.Push(entry)
    while hist.Count() > PlaybackHistoryLimit()
        hist.Shift()
    end while
end sub

function HistoryPopEntry(hist as Object) as Dynamic
    if hist.Count() = 0 then return invalid
    idx = hist.Count() - 1
    entry = hist[idx]
    hist.Delete(idx)
    return entry
end function

function ResetFailureCounterValue(counter as Integer) as Integer
    if counter < 0 then return 0
    return 0
end function

function ApplyPendingBatchSwapForScene(ctx as Object, isScreensaver as Boolean) as Boolean
    return ApplyPendingBatchSwapState(ctx, isScreensaver)
end function

sub PersistResumeStateForScene(ctx as Object, entry as Object, isScreensaver as Boolean)
    PersistResumeStateForMode(entry, ctx.top, ctx.playlistIndex, ctx.playlistOffset, ctx.nextPlaylistOffset, ctx.totalPlaylistCount, isScreensaver)
end sub

sub PersistActivePlaylistCacheForScene(ctx as Object, isScreensaver as Boolean)
    PersistActivePlaylistCacheForMode(ctx.playlist, isScreensaver)
end sub

sub OnPausedChangedForScene(ctx as Object)
    ctx.paused = ctx.top.paused
    ctx.pauseIndicator.visible = ctx.paused
    if ctx.paused then
        ctx.slideTimer.control = "stop"
        ctx.progressTimer.control = "stop"
    else
        ResumeSlideshowForScene(ctx)
    end if
end sub

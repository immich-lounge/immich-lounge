' PlaybackState.brs - shared overlay/meta/state helpers

sub FetchCurrentMetaForScene(ctx as Object, entry as Object)
    ConfigurePhotoOverlaysForScene(ctx)
    ctx.currentEntry = entry
    if ctx.overlay <> invalid then
        ctx.overlay.sourceLabel = ValueOrDefault(entry.sourceLabel, "")
        ctx.overlay.birthdayCaption = ValueOrDefault(entry.birthdayCaption, "")
        ctx.overlay.assetMeta = {}
    end if

    if ctx.currentMetaTask <> invalid then
        ctx.currentMetaTask.unobserveField("metaResult")
        ctx.currentMetaTask = invalid
    end if
    if ctx.currentPairMetaTask <> invalid then
        ctx.currentPairMetaTask.unobserveField("metaResult")
        ctx.currentPairMetaTask = invalid
    end if
    if ctx.slideAssetCount = 2 then
        ctx.pairedOverlay.sourceLabel = ValueOrDefault(ctx.pendingPairEntry.sourceLabel, "")
        ctx.pairedOverlay.birthdayCaption = ValueOrDefault(ctx.pendingPairEntry.birthdayCaption, "")
        ctx.pairedOverlay.assetMeta = {}
    end if

    if ctx.profile = invalid then return
    if ctx.profile.immich = invalid then return
    if ctx.slideAssetCount = 2 then
        ctx.currentPairMetaTask = CreatePhotoMetaTaskForScene(ctx, ctx.pendingPairEntry, "OnPairedMetaResult")
    end if
    ctx.currentMetaTask = CreatePhotoMetaTaskForScene(ctx, entry, "OnMetaResult")
end sub

function CreatePhotoMetaTaskForScene(ctx as Object, entry as Object, callback as String) as Object
    task = CreateObject("roSGNode", "AssetMetaTask")
    task.assetId = entry.id
    task.immichBaseUrl = ctx.profile.immich.serverUrl
    task.apiKey = ctx.profile.immich.apiKey
    task.companionUrl = ctx.top.companionUrl
    task.profileId = PlaybackProfileId(ctx.profile, ctx.top.profileId)
    task.formatSource = ctx.formatSource
    task.useLocalDateFormatting = ctx.top.useLocalDateFormatting
    task.dateFormat = ctx.overlay.dateFormat
    task.locale = ctx.overlay.locale
    task.observeField("metaResult", callback)
    task.command = "fetchMeta"
    return task
end function

sub ConfigurePhotoOverlaysForScene(ctx as Object)
    ctx.pairedOverlayTimer.control = "stop"
    paired = ctx.slideAssetCount = 2
    ' Loading the next slide resets slideAssetCount before the current pair leaves.
    ctx.overlayPaired = paired
    ctx.overlay.externallyManaged = paired
    ctx.overlay.viewportWidth = 1920
    ctx.pairedOverlay.visible = false
    ctx.pairedOverlay.assetMeta = {}
    ctx.pairedOverlay.sourceLabel = ""
    ctx.pairedOverlay.birthdayCaption = ""
    if not paired then
        ' Restore visibility after the pair's scene timer hid both nodes.
        ctx.overlay.visible = ctx.overlayVisible
        return
    end if

    ctx.overlay.viewportWidth = 960
    brightness = PairedOverlayBrightness(ctx.profile)
    ctx.overlay.pairedTextBrightness = brightness
    ctx.pairedOverlay.pairedTextBrightness = brightness
    ctx.pairedOverlay.textAlignment = PairedOverlayAlignment(ctx.profile)
    for each field in ["overlayStyle", "overlayFields", "locationFormat", "overlayBehavior", "fadeSeconds", "clockFormat", "dateFormat", "locale"]
        ctx.pairedOverlay[field] = ctx.overlay[field]
    end for
    visible = ctx.overlayVisible or ctx.overlay.overlayBehavior = "always"
    SetPairedOverlayVisibilityForScene(ctx, visible)
end sub

function PairedOverlayAlignment(profile as Dynamic) as String
    if profile <> invalid then
        if profile.display <> invalid then
            if profile.display.rightAlignPairedOverlay = false then return "left"
        end if
    end if
    return "right"
end function

function PairedOverlayBrightness(profile as Dynamic) as Integer
    if profile = invalid then return 85
    if profile.display = invalid then return 85
    value = profile.display.pairedOverlayBrightness
    valueType = LCase(Type(value))
    if valueType <> "integer" and valueType <> "roint" and valueType <> "rointeger" and valueType <> "float" and valueType <> "rofloat" and valueType <> "double" and valueType <> "rodouble" then return 85
    if value < 50 or value > 100 then return 85
    return Int(value)
end function

sub SetPairedOverlayVisibilityForScene(ctx as Object, visible as Boolean)
    ctx.pairedOverlayTimer.control = "stop"
    ctx.overlay.visible = visible
    ctx.pairedOverlay.visible = visible and ctx.overlayPaired = true
    if visible and ctx.overlayPaired = true and ctx.overlay.overlayBehavior = "fade" then
        ctx.pairedOverlayTimer.duration = ctx.overlay.fadeSeconds
        ctx.pairedOverlayTimer.control = "start"
    end if
end sub

sub OnPairedMetaResultForScene(ctx as Object)
    if ctx.overlayPaired <> true or ctx.currentPairMetaTask = invalid then return
    result = ctx.currentPairMetaTask.metaResult
    if result = invalid then return
    if result.ok = true then
        ctx.pairedOverlay.assetMeta = result.data
    else
        ctx.pairedOverlay.assetMeta = {}
    end if
end sub

sub OnMetaResultForScene(ctx as Object)
    if ctx.currentMetaTask = invalid then return
    result = ctx.currentMetaTask.metaResult
    if result = invalid then return
    if result.ok = true then
        ctx.overlay.sourceLabel = ValueOrDefault(ctx.currentEntry.sourceLabel, "")
        ctx.overlay.birthdayCaption = ValueOrDefault(ctx.currentEntry.birthdayCaption, "")
        ctx.overlay.assetMeta = result.data
    else if ctx.overlay <> invalid then
        ' Keep the current slide's source label, but clear stale per-asset metadata
        ' when we cannot refresh details for a cached/offline slide.
        ctx.overlay.sourceLabel = ValueOrDefault(ctx.currentEntry.sourceLabel, "")
        ctx.overlay.birthdayCaption = ValueOrDefault(ctx.currentEntry.birthdayCaption, "")
        ctx.overlay.assetMeta = {}
    end if
end sub

sub PrefetchNextMetaForScene(ctx as Object)
    if ctx.profile = invalid then return
    if ctx.profile.immich = invalid then return
    if ctx.playlist.Count() = 0 then return
    nextIdx = ctx.playlistIndex + ctx.slideAssetCount
    if nextIdx >= ctx.playlist.Count() then nextIdx = 0
    nextEntry = ctx.playlist[nextIdx]

    task = CreateObject("roSGNode", "AssetMetaTask")
    task.assetId = nextEntry.id
    task.immichBaseUrl = ctx.profile.immich.serverUrl
    task.apiKey = ctx.profile.immich.apiKey
    task.companionUrl = ctx.top.companionUrl
    task.profileId = PlaybackProfileId(ctx.profile, ctx.top.profileId)
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
    if ctx.overlayPaired = true then
        SetPairedOverlayVisibilityForScene(ctx, ctx.overlayVisible)
    else
        ctx.overlay.visible = ctx.overlayVisible
    end if
end sub

sub ShowOverlayForScene(ctx as Object)
    if ctx.overlay.overlayStyle = "none" then return
    ctx.overlayVisible = true
    if ctx.overlayPaired = true then
        SetPairedOverlayVisibilityForScene(ctx, true)
    else
        ctx.overlay.visible = true
    end if
end sub

sub HideOverlayForScene(ctx as Object)
    ctx.overlayVisible = false
    SetPairedOverlayVisibilityForScene(ctx, false)
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
    PersistResumeStateForMode(entry, { profileId: ctx.top.profileId, companionUrl: ctx.top.companionUrl, profile: ctx.profile, memoryDay: ctx.memoryDay }, ctx.playlistIndex, ctx.playlistOffset, ctx.nextPlaylistOffset, ctx.totalPlaylistCount, ctx.playlistVersion, isScreensaver)
end sub

sub PersistActivePlaylistCacheForScene(ctx as Object, isScreensaver as Boolean)
    PersistActivePlaylistCacheForMode(ctx.playlist, isScreensaver, ctx.top.companionUrl, ctx.top.profileId)
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

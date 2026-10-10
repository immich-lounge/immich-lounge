' PlaybackRefresh.brs - shared playlist/profile refresh helpers

sub OnRefreshTimerForScene(ctx as Object)
    ctx.top.triggerRefresh = true
end sub

sub OnProfileConfigTimerForScene(ctx as Object, isScreensaver as Boolean)
    TriggerProfileConfigRefreshForScene(ctx, isScreensaver)
end sub

sub OnTriggerRefreshForScene(ctx as Object)
    if not ctx.top.triggerRefresh then return
    ctx.top.triggerRefresh = false
    TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
end sub

sub TriggerProfileConfigRefreshForScene(ctx as Object, isScreensaver as Boolean)
    if ctx.profile = invalid then return
    if ctx.profileConfigTask <> invalid then return
    if ctx.refreshProfileTask <> invalid then return
    if ctx.refreshPlaylistTask <> invalid then return

    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.isScreensaver = isScreensaver
    task.observeField("profileResult", "OnProfileConfigResult")
    ctx.profileConfigTask = task
    task.command = "fetchProfile"
end sub

sub OnProfileConfigResultForScene(ctx as Object, isScreensaver as Boolean)
    task = ctx.profileConfigTask
    ctx.profileConfigTask = invalid
    if task = invalid then
        LogDebug(PlaybackLogScope(ctx), "profile config result arrived after task was cleared")
        return
    end if

    task.unobserveField("profileResult")
    result = task.profileResult
    if result = invalid then
        LogDebug(PlaybackLogScope(ctx), "profile config task returned no result")
        return
    end if

    if result.ok = true and result.profile <> invalid then
        if PlaybackProfileId(result.profile, ctx.top.profileId) <> PlaybackProfileId(ctx.profile, ctx.top.profileId) then
            BeginProfilePlaylistRefreshForScene(ctx, result.profile, isScreensaver)
            return
        end if
        ctx.pendingScheduledProfile = invalid
        ctx.pendingScheduledPlaylist = invalid
        ctx.profile = result.profile
        PersistProfileCacheForMode(ctx.profile, ctx.top.companionUrl, ctx.top.profileId, isScreensaver)
        ctx.skipFullRefreshTimerRestart = true
        ApplyProfile()
        ctx.skipFullRefreshTimerRestart = false
    else
        LogDebug(PlaybackLogScope(ctx), "profile config refresh unavailable, continuing with existing profile")
    end if
end sub

sub TriggerPlaylistRefreshForScene(ctx as Object, isScreensaver as Boolean)
    if ctx.profile = invalid then return
    if ctx.refreshProfileTask <> invalid or ctx.refreshPlaylistTask <> invalid then return
    if ctx.profileConfigTask <> invalid then
        ctx.profileConfigTask.unobserveField("profileResult")
        ctx.profileConfigTask = invalid
    end if
    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.isScreensaver = isScreensaver
    task.observeField("profileResult", "OnRefreshProfileResult")
    ctx.refreshProfileTask = task
    task.command = "fetchProfile"
end sub

sub OnRefreshProfileResultForScene(ctx as Object, isScreensaver as Boolean)
    task = ctx.refreshProfileTask
    ctx.refreshProfileTask = invalid
    if task = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh profile result arrived after task was cleared")
        return
    end if

    task.unobserveField("profileResult")
    result = task.profileResult
    if result = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh profile task returned no result")
        return
    end if

    if result.ok = true then
        BeginProfilePlaylistRefreshForScene(ctx, result.profile, isScreensaver)
    else
        LogDebug(PlaybackLogScope(ctx), "refresh profile failed, continuing with existing playlist")
    end if
end sub

sub OnRefreshPlaylistResultForScene(ctx as Object, isScreensaver as Boolean)
    task = ctx.refreshPlaylistTask
    ctx.refreshPlaylistTask = invalid
    if task = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh playlist result arrived after task was cleared")
        return
    end if

    task.unobserveField("playlistResult")
    result = task.playlistResult
    refreshedProfile = ctx.pendingRefreshProfile
    ctx.pendingRefreshProfile = invalid
    if result = invalid then
        LogDebug(PlaybackLogScope(ctx), "refresh playlist task returned no result")
        return
    end if

    if result.ok = true then
        if refreshedProfile <> invalid and PlaybackProfileId(refreshedProfile, ctx.top.profileId) <> PlaybackProfileId(ctx.profile, ctx.top.profileId) then
            if result.assets.Count() > 0 then
                ctx.pendingScheduledProfile = refreshedProfile
                ctx.pendingScheduledPlaylist = result
                ctx.pendingScheduledAt = UpTime(0)
                ApplyScheduledProfileForScene(ctx)
            end if
            return
        end if
        if refreshedProfile <> invalid then
            ctx.profile = refreshedProfile
            PersistProfileCacheForMode(ctx.profile, ctx.top.companionUrl, ctx.top.profileId, isScreensaver)
            ApplyProfile()
        end if
        resultPlaylistVersion = NormalizePlaylistVersion(result.playlistVersion)
        hadNoPhotos = true
        if ctx.playlist <> invalid then hadNoPhotos = ctx.playlist.Count() = 0
        currentAssetId = invalid
        if ctx.playlist <> invalid and ctx.playlistIndex >= 0 and ctx.playlistIndex < ctx.playlist.Count() then
            currentEntry = ctx.playlist[ctx.playlistIndex]
            if currentEntry <> invalid and currentEntry.id <> invalid then
                currentAssetId = currentEntry.id
            end if
        end if

        ctx.playlist = result.assets
        if resultPlaylistVersion <> ctx.playlistVersion or result.offset <> ctx.playlistOffset then ctx.portraitPairs = {}
        ctx.playlistOffset = result.offset
        ctx.nextPlaylistOffset = result.nextOffset
        ctx.totalPlaylistCount = result.totalCount
        if resultPlaylistVersion <> "" then
            ctx.playlistVersion = resultPlaylistVersion
        end if
        ctx.pendingBatchAssets = invalid
        ctx.pendingBatchOffset = 0
        ctx.pendingBatchPlaylistVersion = ""
        ctx.pendingNextPlaylistOffset = 0
        ctx.waitingForNextBatchSwap = false
        ctx.playlistIndex = ResolvePlaylistIndexAfterRefresh(ctx.playlist, currentAssetId, ctx.playlistIndex)
        ' A refreshed window may have moved the right photo away from the left.
        ctx.slideAssetCount = PortraitSlideAdvanceCount(ctx.playlist, ctx.playlistIndex, ctx.currentPairAssetId)
        PersistActivePlaylistCacheForScene(ctx, isScreensaver)
        if hadNoPhotos and ctx.playlist.Count() > 0 then
            ctx.resumeNeedsSlideLoad = true
            if not ctx.paused then ResumeSlideshowForScene(ctx)
        end if
    else
        LogDebug(PlaybackLogScope(ctx), "refresh playlist failed, continuing with existing playlist")
    end if
end sub

sub BeginProfilePlaylistRefreshForScene(ctx as Object, profile as Object, isScreensaver as Boolean)
    if ctx.refreshPlaylistTask <> invalid then return
    ctx.pendingScheduledProfile = invalid
    ctx.pendingScheduledPlaylist = invalid
    ctx.pendingRefreshProfile = profile
    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = PlaybackProfileId(profile, ctx.top.profileId)
    task.isScreensaver = isScreensaver
    if task.profileId = PlaybackProfileId(ctx.profile, ctx.top.profileId) then
        task.playlistOffset = ctx.playlistOffset
        task.playlistVersion = ctx.playlistVersion
    end if
    task.playlistCount = PlaybackRegistryPlaylistWindowSize()
    task.observeField("playlistResult", "OnRefreshPlaylistResult")
    ctx.refreshPlaylistTask = task
    task.command = "fetchPlaylist"
end sub

function ApplyScheduledProfileForScene(ctx as Object) as Boolean
    if ctx.pendingScheduledProfile = invalid or ctx.pendingScheduledPlaylist = invalid then return false
    if UpTime(0) - ctx.pendingScheduledAt >= 60 then
        ctx.pendingScheduledProfile = invalid
        ctx.pendingScheduledPlaylist = invalid
        return false
    end if
    if ctx.isTransitioning or ctx.paused then return false
    if ctx.profileConfigTask <> invalid or ctx.refreshProfileTask <> invalid or ctx.refreshPlaylistTask <> invalid then return false
    result = ctx.pendingScheduledPlaylist
    ctx.profile = ctx.pendingScheduledProfile
    ctx.pendingScheduledProfile = invalid
    ctx.pendingScheduledPlaylist = invalid
    if ctx.nextBatchTask <> invalid then
        ctx.nextBatchTask.unobserveField("playlistResult")
        ctx.nextBatchTask = invalid
    end if
    ctx.slideTimer.control = "stop"
    ctx.progressTimer.control = "stop"
    StopMotionAnimationForScene(ctx)
    ctx.playlist = result.assets
    ctx.playlistIndex = 0
    ctx.playlistOffset = result.offset
    ctx.nextPlaylistOffset = result.nextOffset
    ctx.totalPlaylistCount = result.totalCount
    ctx.playlistVersion = NormalizePlaylistVersion(result.playlistVersion)
    ctx.pendingBatchAssets = invalid
    ctx.pendingBatchOffset = 0
    ctx.pendingBatchPlaylistVersion = ""
    ctx.pendingNextPlaylistOffset = 0
    ctx.waitingForNextBatchSwap = false
    ctx.portraitPairs = {}
    ctx.currentPairAssetId = ""
    ctx.slideAssetCount = 1
    ctx.top.useLocalDateFormatting = false
    PersistProfileCacheForMode(ctx.profile, ctx.top.companionUrl, ctx.top.profileId, ctx.top.isScreensaver)
    PersistActivePlaylistCacheForScene(ctx, ctx.top.isScreensaver)
    PersistResumeStateForScene(ctx, ctx.playlist[0], ctx.top.isScreensaver)
    ApplyProfile()
    emptyGroup = ctx.top.findNode("emptyPlaylistGroup")
    if emptyGroup <> invalid then emptyGroup.visible = false
    ShowCurrentSlideForScene(ctx)
    return true
end function

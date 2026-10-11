' StartupFlow.brs - shared startup fetch helpers for channel and screensaver

function StartupPlaylistWindowSize() as Integer
    return PlaybackRegistryPlaylistWindowSize()
end function

function CreateStartupRequestTask() as Object
    return CreateObject("roSGNode", "CompanionApiTask")
end function

function FetchStartupProfile(companionUrl as String, profileId as String, isScreensaver as Boolean, port as Object, logScope as String, loadingScene = invalid as Dynamic) as Object
    LogDebug(logScope, "fetching profile id=" + profileId + " from " + companionUrl)

    cachedProfile = LoadCachedProfileForMode(isScreensaver, companionUrl, profileId)
    waitLimitMs = 10000
    if cachedProfile <> invalid then waitLimitMs = 2000

    profileTask = CreateStartupRequestTask()
    profileTask.baseUrl = companionUrl
    profileTask.profileId = profileId
    profileTask.observeField("profileResult", port)
    profileTask.command = "fetchProfile"

    loadingShown = false
    clock = CreateObject("roTimespan")

    while true
        msg = Wait(250, port)
        if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then
            profileTask.unobserveField("profileResult")
            profileTask.control = "STOP"
            LogDebug(logScope, "screen closed during profile fetch")
            return { status: "exit" }
        else if type(msg) = "roSGNodeEvent" and msg.getField() = "profileResult" then
            result = profileTask.profileResult
            profileTask.unobserveField("profileResult")
            LogDebug(logScope, "profileResult ok=" + result.ok.ToStr())

            if result.ok = true then
                profile = result.profile
                PersistProfileCacheForMode(profile, companionUrl, profileId, isScreensaver)
                return {
                    status: "live"
                    profile: profile
                    schemaWarning: result.DoesExist("schemaWarning") and result.schemaWarning = true
                }
            end if

            if result.DoesExist("notFound") and result.notFound = true then
                return { status: "notFound" }
            end if

            statusCode = -1
            if result.DoesExist("statusCode") and result.statusCode <> invalid then statusCode = result.statusCode
            LogDebug(logScope, "profile fetch failed statusCode=" + statusCode.ToStr())

            if cachedProfile <> invalid then
                return {
                    status: "cached"
                    profile: cachedProfile
                }
            end if

            return { status: "unavailable" }
        end if
        elapsedMs = clock.TotalMilliseconds()
        if not loadingShown and loadingScene <> invalid and elapsedMs >= 500 then
            ShowPlaybackStartupStatusInScene(loadingScene, "Loading photos...")
            loadingShown = true
        end if
        if elapsedMs >= waitLimitMs then
            profileTask.unobserveField("profileResult")
            profileTask.control = "STOP"
            if cachedProfile <> invalid then
                return {
                    status: "cached"
                    profile: cachedProfile
                }
            end if
            return { status: "unavailable" }
        end if
    end while
end function

function FetchStartupPlaylist(companionUrl as String, profileId as String, isScreensaver as Boolean, port as Object, logScope as String, loadingScene = invalid as Dynamic, profile = invalid as Dynamic) as Object
    resumeState = LoadResumeState(isScreensaver, profileId, companionUrl)
    resumeOffset = ResolveStartupPlaylistOffset(profile, resumeState)
    resumePlaylistVersion = ResolveResumePlaylistVersion(resumeState)
    cachedPlaylist = LoadCachedPlaylistForMode(isScreensaver, companionUrl, profileId)
    playbackId = PlaybackProfileId(profile, profileId)
    if not CachedPlaylistMatchesPlayback(resumeState, profileId, playbackId) then
        resumeState = invalid
        cachedPlaylist = []
        resumeOffset = 0
        resumePlaylistVersion = ""
    end if

    LogDebug(logScope, "fetching playlist offset=" + resumeOffset.ToStr() + " cachedCount=" + cachedPlaylist.Count().ToStr())

    playlistTask = CreateStartupRequestTask()
    playlistTask.baseUrl = companionUrl
    playlistTask.profileId = playbackId
    playlistTask.isScreensaver = isScreensaver
    playlistTask.playlistOffset = resumeOffset
    playlistTask.playlistVersion = resumePlaylistVersion
    playlistTask.playlistCount = StartupPlaylistWindowSize()
    waitLimitMs = 90000
    if cachedPlaylist.Count() > 0 then
        playlistTask.maxWaitSec = 3
        waitLimitMs = 3000
    end if
    playlistTask.observeField("playlistResult", port)
    playlistTask.command = "fetchPlaylist"
    loadingShown = false
    clock = CreateObject("roTimespan")

    while true
        msg = Wait(250, port)
        if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then
            playlistTask.unobserveField("playlistResult")
            playlistTask.control = "STOP"
            LogDebug(logScope, "screen closed during playlist fetch")
            return { status: "exit" }
        else if type(msg) = "roSGNodeEvent" and msg.getField() = "playlistResult" then
            result = playlistTask.playlistResult
            playlistTask.unobserveField("playlistResult")
            LogDebug(logScope, "playlistResult ok=" + result.ok.ToStr())

            if result.ok = true then
                playlist = result.assets
                result.playlistVersion = NormalizePlaylistVersion(result.playlistVersion)
                LogDebug(logScope, "playlist count=" + playlist.Count().ToStr())
                PersistActivePlaylistCacheForMode(playlist, isScreensaver, companionUrl, profileId)
                startPlaylistIndex = ResolveResumePlaylistIndex(playlist, resumeState)
                if result.playlistVersion <> "" and resumePlaylistVersion <> "" and result.playlistVersion <> resumePlaylistVersion then
                    startPlaylistIndex = ResolvePlaylistIndexAfterRefresh(playlist, ValueOrDefault(resumeState.assetId, ""), 0)
                end if
                if NormalizePlaylistVersion(result.memoryDay) <> "" then
                    previousMemoryDay = ""
                    if resumeState <> invalid then previousMemoryDay = NormalizePlaylistVersion(resumeState.memoryDay)
                    if result.memoryDay <> previousMemoryDay then startPlaylistIndex = 0
                end if
                return {
                    status: "live"
                    playlist: playlist
                    launchPlaylistResult: result
                    startPlaylistIndex: startPlaylistIndex
                }
            end if

            if cachedPlaylist.Count() > 0 then
                LogDebug(logScope, "falling back to cached playlist after playlist fetch miss or timeout")
                return {
                    status: "cached"
                    playlist: cachedPlaylist
                    launchPlaylistResult: BuildLaunchPlaylistResultFromResume(cachedPlaylist, resumeState)
                    startPlaylistIndex: ResolveResumePlaylistIndex(cachedPlaylist, resumeState)
                }
            end if

            return { status: "unavailable" }
        end if
        elapsedMs = clock.TotalMilliseconds()
        if not loadingShown and loadingScene <> invalid and elapsedMs >= 500 then
            ShowPlaybackStartupStatusInScene(loadingScene, "Loading photos...")
            loadingShown = true
        end if
        if elapsedMs >= waitLimitMs then
            playlistTask.unobserveField("playlistResult")
            playlistTask.control = "STOP"
            if cachedPlaylist.Count() > 0 then
                LogDebug(logScope, "falling back to cached playlist after playlist fetch timeout")
                return {
                    status: "cached"
                    playlist: cachedPlaylist
                    launchPlaylistResult: BuildLaunchPlaylistResultFromResume(cachedPlaylist, resumeState)
                    startPlaylistIndex: ResolveResumePlaylistIndex(cachedPlaylist, resumeState)
                }
            end if
            return { status: "unavailable" }
        end if
    end while
end function

function ResolveStartupPlaylistOffset(profile as Dynamic, resumeState as Dynamic) as Integer
    offset = ResolveResumePlaylistOffset(resumeState)
    if not UsesDailyMemories(profile) then return offset
    if resumeState = invalid then return 0
    ' Older packages saved an offset but no memory day. Fetch the whole playlist
    ' head on the first live startup; cached offline fallback retains its window.
    if NormalizePlaylistVersion(resumeState.memoryDay) = "" then return 0
    return offset
end function

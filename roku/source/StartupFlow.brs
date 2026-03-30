' StartupFlow.brs - shared startup fetch helpers for channel and screensaver

function StartupPlaylistWindowSize() as Integer
    return PlaybackRegistryPlaylistWindowSize()
end function

function LoadCachedProfileForMode(isScreensaver as Boolean) as Dynamic
    cachedProfileJson = RegistryRead(CachedProfileRegistryKey(isScreensaver))
    if cachedProfileJson = invalid then return invalid
    return ParseJson_Safe(cachedProfileJson)
end function

function LoadCachedPlaylistForMode(isScreensaver as Boolean) as Object
    cachedPlaylistJson = RegistryRead(CachedPlaylistRegistryKey(isScreensaver))
    if cachedPlaylistJson = invalid then return []

    parsed = ParseJson_Safe(cachedPlaylistJson)
    if parsed = invalid then return []
    return parsed
end function

function FetchStartupProfile(companionUrl as String, profileId as String, isScreensaver as Boolean, port as Object, logScope as String, loadingScene = invalid as Dynamic) as Object
    LogDebug(logScope, "fetching profile id=" + profileId + " from " + companionUrl)

    profileTask = CreateObject("roSGNode", "CompanionApiTask")
    profileTask.baseUrl = companionUrl
    profileTask.profileId = profileId
    profileTask.observeField("profileResult", port)
    profileTask.command = "fetchProfile"

    cachedProfile = LoadCachedProfileForMode(isScreensaver)
    loadingShown = false
    elapsedMs = 0

    while true
        msg = Wait(250, port)
        if msg = invalid then
            elapsedMs = elapsedMs + 250
            if not loadingShown and loadingScene <> invalid and elapsedMs >= 500 then
                ShowPlaybackStartupStatusInScene(loadingScene, "Loading photos...")
                loadingShown = true
            end if
            if elapsedMs >= 10000 then
                profileTask.unobserveField("profileResult")
                if cachedProfile <> invalid then
                    return {
                        status: "cached"
                        profile: cachedProfile
                    }
                end if
                return { status: "unavailable" }
            end if
        else if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then
            profileTask.unobserveField("profileResult")
            LogDebug(logScope, "screen closed during profile fetch")
            return { status: "exit" }
        else if type(msg) = "roSGNodeEvent" and msg.getField() = "profileResult" then
            result = profileTask.profileResult
            profileTask.unobserveField("profileResult")
            LogDebug(logScope, "profileResult ok=" + result.ok.ToStr())

            if result.ok = true then
                profile = result.profile
                RegistryWrite(CachedProfileRegistryKey(isScreensaver), FormatJSON(profile))
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
    end while
end function

function FetchStartupPlaylist(companionUrl as String, profileId as String, isScreensaver as Boolean, port as Object, logScope as String, loadingScene = invalid as Dynamic) as Object
    resumeState = LoadResumeState(isScreensaver, profileId)
    resumeOffset = ResolveResumePlaylistOffset(resumeState)
    cachedPlaylist = LoadCachedPlaylistForMode(isScreensaver)

    LogDebug(logScope, "fetching playlist offset=" + resumeOffset.ToStr() + " cachedCount=" + cachedPlaylist.Count().ToStr())

    playlistTask = CreateObject("roSGNode", "CompanionApiTask")
    playlistTask.baseUrl = companionUrl
    playlistTask.profileId = profileId
    playlistTask.isScreensaver = isScreensaver
    playlistTask.playlistOffset = resumeOffset
    playlistTask.playlistCount = StartupPlaylistWindowSize()
    if cachedPlaylist.Count() > 0 then
        playlistTask.maxWaitSec = 3
    end if
    playlistTask.observeField("playlistResult", port)
    playlistTask.command = "fetchPlaylist"
    loadingShown = false
    elapsedMs = 0

    while true
        msg = Wait(250, port)
        if msg = invalid then
            elapsedMs = elapsedMs + 250
            if not loadingShown and loadingScene <> invalid and elapsedMs >= 500 then
                ShowPlaybackStartupStatusInScene(loadingScene, "Loading photos...")
                loadingShown = true
            end if
            if elapsedMs >= 90000 then
                playlistTask.unobserveField("playlistResult")
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
        else if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then
            playlistTask.unobserveField("playlistResult")
            LogDebug(logScope, "screen closed during playlist fetch")
            return { status: "exit" }
        else if type(msg) = "roSGNodeEvent" and msg.getField() = "playlistResult" then
            result = playlistTask.playlistResult
            playlistTask.unobserveField("playlistResult")
            LogDebug(logScope, "playlistResult ok=" + result.ok.ToStr())

            if result.ok = true then
                playlist = result.assets
                LogDebug(logScope, "playlist count=" + playlist.Count().ToStr())
                RegistryWrite(CachedPlaylistRegistryKey(isScreensaver), FormatJSON(TruncatePlaylistForRegistry(playlist)))
                return {
                    status: "live"
                    playlist: playlist
                    launchPlaylistResult: result
                    startPlaylistIndex: ResolveResumePlaylistIndex(playlist, resumeState)
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
    end while
end function

' AppControllerScreensaver.brs - screensaver-only startup flow

sub AppControllerScreensaver(screen as Object, scene as Object, port as Object)
    m.screen = screen
    m.scene = scene
    m.port = port

    companionUrl = RegistryRead("companionUrl")
    profileId = GetSavedProfileId(true)

    RunScreensaverFlow(companionUrl, profileId, scene, port)
end sub

sub RunScreensaverFlow(companionUrl as Dynamic, profileId as Dynamic, scene as Object, port as Object)
    if companionUrl = invalid or companionUrl = "" or profileId = invalid or profileId = "" then
        ShowSetupPrompt(scene)
        return
    end if

    StartScreensaverSlideshow(companionUrl, profileId, scene, port)
end sub

sub StartScreensaverSlideshow(companionUrl as String, profileId as String, scene as Object, port as Object)
    retrySeconds = 120
    ShowPlaybackStartupStatusInScene(scene, "Loading photos...")

    while true
        latestCompanionUrl = RegistryRead("companionUrl")
        latestProfileId = GetSavedProfileId(true)
        if latestCompanionUrl <> invalid and latestCompanionUrl <> "" then companionUrl = latestCompanionUrl
        if latestProfileId <> invalid and latestProfileId <> "" then profileId = latestProfileId

        shouldRetry = false
        profileResult = FetchStartupProfile(companionUrl, profileId, true, port, "AppControllerScreensaver", scene)
        if profileResult.status = "exit" then return
        if profileResult.status = "notFound" then
            RegistryDelete(ScreensaverProfileIdKey())
            ShowFullScreenError(scene, "The saved profile is no longer available." + Chr(10) + "Open Roku Screensaver Settings and choose another Immich Lounge profile.")
            return
        end if
        if profileResult.status = "unavailable" then
            ShowFullScreenError(scene, "Cannot reach the companion at " + companionUrl + "." + Chr(10) + "Retrying automatically in about " + retrySeconds.ToStr() + " seconds." + Chr(10) + "Open Roku Screensaver Settings for Immich Lounge to update the saved address or profile.")
            action = WaitForScreensaverStartupRetry(port, retrySeconds)
            if action = "exit" then return
            shouldRetry = true
        end if

        if not shouldRetry then
            profile = profileResult.profile
            if profile = invalid then return
            useLocalDateFormatting = (profileResult.status = "cached")
            if profileResult.status = "cached" then
                ShowToast(scene, "Using cached config")
            end if

            playlistResult = FetchStartupPlaylist(companionUrl, profileId, true, port, "AppControllerScreensaver", scene)
            if playlistResult.status = "exit" then return
            if playlistResult.status = "unavailable" then
                ShowFullScreenError(scene, "Could not load a playlist." + Chr(10) + "Retrying automatically in about " + retrySeconds.ToStr() + " seconds." + Chr(10) + "Open Roku Screensaver Settings for Immich Lounge to check the saved setup.")
                action = WaitForScreensaverStartupRetry(port, retrySeconds)
                if action = "exit" then return
                shouldRetry = true
            end if
        end if

        if not shouldRetry then
            playlist = playlistResult.playlist
            launchPlaylistResult = playlistResult.launchPlaylistResult
            startPlaylistIndex = playlistResult.startPlaylistIndex
            if playlistResult.status = "cached" then
                ShowToast(scene, "Using cached playlist")
            end if

            if playlist = invalid or playlist.Count() = 0 then
                ShowFullScreenError(scene, "No photos found." + Chr(10) + "Add content sources to the selected profile, then confirm the screensaver setup in Roku Screensaver Settings.")
                return
            end if

            LogDebug("AppControllerScreensaver", "starting screensaver slideshow startIndex=" + startPlaylistIndex.ToStr() + " playlistCount=" + playlist.Count().ToStr())
            ConfigureSlideshowSceneForLaunch(scene, profile, playlist, companionUrl, profileId, true, launchPlaylistResult, startPlaylistIndex, useLocalDateFormatting)
            WaitForSlideshowSceneAction(scene, port, false)
            return
        end if
    end while
end sub

sub ShowSetupPrompt(scene as Object)
    ShowSetupPromptWithMessage(scene, "Open Roku Screensaver Settings to complete Immich Lounge screensaver setup.")
end sub

sub ShowToast(scene as Object, message as String)
    ShowToastInNode(scene, "toast", message)
end sub

sub ShowFullScreenError(scene as Object, message as String)
    ShowFullScreenErrorInStatusLabel(scene, message)
end sub

function WaitForScreensaverStartupRetry(port as Object, retrySeconds as Integer) as String
    while true
        msg = Wait(retrySeconds * 1000, port)
        if msg = invalid then return "retry"
        if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then return "exit"
    end while
end function

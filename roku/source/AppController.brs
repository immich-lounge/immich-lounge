' AppController.brs — Top-level startup state machine
' Called from both RunUserInterface (isScreensaver=false) and RunScreenSaver (isScreensaver=true)

sub AppController(screen as Object, scene as Object, port as Object, isScreensaver as Boolean, args = {} as Object)
    m.screen = screen
    m.scene = scene
    m.port = port
    m.isScreensaver = isScreensaver

    if args.mediaType <> invalid then
        contentIdStr = "(none)"
        if args.contentId <> invalid then contentIdStr = args.contentId.ToStr()
        LogDebug("AppController", "deep link launch mediaType=" + args.mediaType.ToStr() + " contentId=" + contentIdStr)
    end if
    if args.lastExitOrTerminationReason <> invalid then
        LogDebug("AppController", "lastExitOrTerminationReason=" + args.lastExitOrTerminationReason.ToStr())
    end if

    companionUrl = RegistryRead("companionUrl")
    profileId    = GetSavedProfileId(isScreensaver)

    if isScreensaver then
        RunScreensaverFlow(companionUrl, profileId, scene, port)
        return
    end if

    changeProfileMode = false
    changeCompanionMode = false
    allowCancelToSlideshow = false
    reopenSettingsOnStart = false

    while true
        action = RunChannelFlow(companionUrl, profileId, scene, port, changeProfileMode, changeCompanionMode, allowCancelToSlideshow, reopenSettingsOnStart)
        if action = "changeProfile" then
            companionUrl = invalid
            profileId = invalid
            changeProfileMode = true
            changeCompanionMode = false
            allowCancelToSlideshow = true
            reopenSettingsOnStart = false
        else if action = "changeCompanion" then
            companionUrl = invalid
            profileId = invalid
            changeProfileMode = false
            changeCompanionMode = true
            allowCancelToSlideshow = true
            reopenSettingsOnStart = false
        else if action = "restartSetup" then
            companionUrl = ""
            profileId = ""
            changeProfileMode = false
            changeCompanionMode = false
            allowCancelToSlideshow = false
            reopenSettingsOnStart = false
        else if action = "cancel" then
            companionUrl = RegistryRead("companionUrl")
            profileId = GetSavedProfileId(false)
            changeProfileMode = false
            changeCompanionMode = false
            allowCancelToSlideshow = false
            reopenSettingsOnStart = true
        else
            return
        end if
    end while
end sub

sub RunScreensaverFlow(companionUrl as Dynamic, profileId as Dynamic, scene as Object, port as Object)
    if companionUrl = invalid or profileId = invalid then
        discoveryScene = m.screen.CreateScene("DiscoveryScene")
        discoveryScene.isScreensaver = true
        m.screen.show()
        discoveryScene.observeField("setupComplete", port)

        while true
            msg = Wait(0, port)
            if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then return
            if type(msg) = "roSGNodeEvent" then
                if msg.getField() = "setupComplete" and msg.getData() = true then
                    LogDebug("AppController", "Screensaver setup complete - exiting")
                    discoveryScene.unobserveField("setupComplete")
                    return
                end if
            end if
        end while
        return
    end if

    StartSlideshowFlow(companionUrl, profileId, scene, port, true)
end sub

function RunChannelFlow(companionUrl as Dynamic, profileId as Dynamic, scene as Object, port as Object, changeProfileMode = false as Boolean, changeCompanionMode = false as Boolean, allowCancelToSlideshow = false as Boolean, openSettingsOnStart = false as Boolean) as String
    activeScene = scene

    if companionUrl = invalid or companionUrl = "" or profileId = invalid or profileId = "" then
        discoveryScene = m.screen.CreateScene("DiscoveryScene")
        discoveryScene.isScreensaver = false
        discoveryScene.setupComplete = false
        discoveryScene.cancelled = false
        discoveryScene.changeProfileMode = changeProfileMode
        discoveryScene.changeCompanionMode = changeCompanionMode
        discoveryScene.allowCancelToSlideshow = allowCancelToSlideshow
        discoveryScene.suspendStartup = false
        m.screen.show()

        if not allowCancelToSlideshow and not changeProfileMode then
            discoveryScene.signalBeacon("AppDialogInitiate")
        end if

        discoveryScene.observeField("setupComplete", port)
        discoveryScene.observeField("cancelled", port)

        while true
            msg = Wait(0, port)
            if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then return "exit"
            if type(msg) = "roInputEvent" then
                info = msg.getInfo()
                if info.mediaType <> invalid then LogDebug("AppController", "roInputEvent during setup mediaType=" + info.mediaType)
            else if type(msg) = "roSGNodeEvent" then
                if msg.getField() = "setupComplete" and msg.getData() = true then
                    companionUrl = RegistryRead("companionUrl")
                    profileId    = GetSavedProfileId(false)
                    LogDebug("AppController", "DiscoveryScene setupComplete observed url=" + companionUrl + " profile=" + profileId)
                    if not allowCancelToSlideshow and not changeProfileMode then
                        discoveryScene.signalBeacon("AppDialogComplete")
                    end if
                    discoveryScene.unobserveField("setupComplete")
                    discoveryScene.unobserveField("cancelled")
                    exit while
                else if msg.getField() = "cancelled" and msg.getData() = true then
                    LogDebug("AppController", "DiscoveryScene cancelled back to slideshow")
                    discoveryScene.unobserveField("setupComplete")
                    discoveryScene.unobserveField("cancelled")
                    return "cancel"
                end if
            end if
        end while

        activeScene = discoveryScene
    end if

    LogDebug("AppController", "setupComplete url=" + companionUrl + " profile=" + profileId)
    return StartSlideshowFlow(companionUrl, profileId, activeScene, port, false, openSettingsOnStart)
end function

function StartSlideshowFlow(companionUrl as String, profileId as String, scene as Object, port as Object, isScreensaver as Boolean, openSettingsOnStart = false as Boolean) as String
    promptedForSetup = false
    useLocalDateFormatting = false

    while true
        latestCompanionUrl = RegistryRead("companionUrl")
        latestProfileId = GetSavedProfileId(isScreensaver)
        if latestCompanionUrl <> invalid and latestCompanionUrl <> "" then companionUrl = latestCompanionUrl
        if latestProfileId <> invalid and latestProfileId <> "" then profileId = latestProfileId

        shouldRetry = false
        profileResult = FetchStartupProfile(companionUrl, profileId, isScreensaver, port, "AppController")
        if profileResult.status = "exit" then return "exit"
        if profileResult.status = "notFound" then
            LogDebug("AppController", "profile not found, restarting setup")
            ShowToast(scene, "Profile no longer available on companion")
            RegistryDelete(ProfileIdRegistryKey(isScreensaver))
            return "restartSetup"
        end if
        if profileResult.status = "unavailable" then
            action = HandleStartupUnavailable(scene, port, companionUrl, "Cannot reach the companion.", promptedForSetup)
            promptedForSetup = true
            if action = "changeCompanion" then return "changeCompanion"
            if action = "exit" then return "exit"
            shouldRetry = true
        end if

        if not shouldRetry then
            profile = profileResult.profile
            if profile = invalid then return "exit"
            useLocalDateFormatting = (profileResult.status = "cached")
            if profileResult.status = "cached" then
                ShowToast(scene, "Using cached config (companion offline)")
            else if profileResult.DoesExist("schemaWarning") and profileResult.schemaWarning = true then
                ShowToast(scene, "Config may be incompatible — update the channel")
            end if

            playlistResult = FetchStartupPlaylist(companionUrl, profileId, isScreensaver, port, "AppController")
            if playlistResult.status = "exit" then return "exit"
            if playlistResult.status = "unavailable" then
                action = HandleStartupUnavailable(scene, port, companionUrl, "Could not load a playlist from the companion.", promptedForSetup)
                promptedForSetup = true
                if action = "changeCompanion" then return "changeCompanion"
                if action = "exit" then return "exit"
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
                LogDebug("AppController", "playlist is empty")
                ShowFullScreenError(scene, "No photos found." + Chr(10) + "Add content sources to your profile on the companion." + Chr(10) + Chr(10) + "Press Back to exit and retry after adding content.")
                return "exit"
            end if

            LogDebug("AppController", "launching slideshow startIndex=" + startPlaylistIndex.ToStr() + " playlistCount=" + playlist.Count().ToStr())
            return LaunchSlideshow(profile, playlist, companionUrl, profileId, isScreensaver, port, launchPlaylistResult, startPlaylistIndex, useLocalDateFormatting, openSettingsOnStart)
        end if
    end while
end function

function LaunchSlideshow(profile as Object, playlist as Object, companionUrl as String, profileId as String, isScreensaver as Boolean, port as Object, playlistResult as Object, startPlaylistIndex as Integer, useLocalDateFormatting = false as Boolean, openSettingsOnStart = false as Boolean) as String
    LogDebug("AppController", "creating SlideshowScene")
    slideshowScene = m.screen.CreateScene("SlideshowScene")
    ConfigureSlideshowSceneForLaunch(slideshowScene, profile, playlist, companionUrl, profileId, isScreensaver, playlistResult, startPlaylistIndex, useLocalDateFormatting, openSettingsOnStart)
    m.screen.show()
    if not isScreensaver then
        slideshowScene.setFocus(true)
    end if
    return WaitForSlideshowSceneAction(slideshowScene, port, true)
end function

sub ShowSetupPrompt(scene as Object)
    ShowSetupPromptWithMessage(scene, "Open the Immich Lounge channel to complete setup.")
end sub

sub ShowToast(scene as Object, message as String)
    ShowToastInNode(scene, "toast", message)
end sub

sub ShowFullScreenError(scene as Object, message as String)
    ShowFullScreenErrorInStatusLabel(scene, message)
end sub

function HandleStartupUnavailable(scene as Object, port as Object, companionUrl as String, reason as String, promptedForSetup as Boolean) as String
    retrySeconds = 120
    ShowFullScreenError(scene, reason + Chr(10) + companionUrl + Chr(10) + Chr(10) + "Retrying automatically in about " + retrySeconds.ToStr() + " seconds.")

    if promptedForSetup then
        return WaitForStartupRetry(port, retrySeconds)
    end if

    dlg = CreateObject("roSGNode", "StandardMessageDialog")
    dlg.title = "Companion Unavailable"
    dlg.message = [reason, companionUrl, "Open setup now, or keep trying in the background?"]
    dlg.buttons = ["Keep Trying", "Open Setup", "Exit"]
    dlg.observeField("buttonSelected", port)
    dlg.observeField("wasClosed", port)
    scene.dialog = dlg

    while true
        msg = Wait(retrySeconds * 1000, port)
        if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then
            CloseStartupDialog(dlg, scene)
            return "exit"
        end if

        if msg = invalid then
            CloseStartupDialog(dlg, scene)
            return "retry"
        end if

        if type(msg) = "roSGNodeEvent" then
            field = msg.getField()
            if field = "buttonSelected" then
                idx = dlg.buttonSelected
                CloseStartupDialog(dlg, scene)
                if idx = 1 then return "changeCompanion"
                if idx = 2 then return "exit"
                return "retry"
            else if field = "wasClosed" then
                CloseStartupDialog(dlg, scene)
                return "retry"
            end if
        end if
    end while
end function

function WaitForStartupRetry(port as Object, retrySeconds as Integer) as String
    while true
        msg = Wait(retrySeconds * 1000, port)
        if msg = invalid then return "retry"
        if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then return "exit"
    end while
end function

sub CloseStartupDialog(dlg as Dynamic, scene as Object)
    if dlg <> invalid then
        dlg.unobserveField("buttonSelected")
        dlg.unobserveField("wasClosed")
    end if
    if scene <> invalid then scene.dialog = invalid
end sub

' Settings are now handled as a Dialog within SlideshowScene (see SlideshowScene.brs)

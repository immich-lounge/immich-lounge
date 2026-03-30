' AppControllerFlow.brs - shared slideshow scene launch helpers

sub ConfigureSlideshowSceneForLaunch(scene as Object, profile as Object, playlist as Object, companionUrl as String, profileId as String, isScreensaver as Boolean, playlistResult as Object, startPlaylistIndex as Integer, useLocalDateFormatting = false as Boolean, openSettingsOnStart = false as Boolean)
    scene.companionUrl = companionUrl
    scene.profileId = profileId
    scene.useLocalDateFormatting = useLocalDateFormatting
    scene.openSettingsOnStart = openSettingsOnStart
    scene.startPlaylistIndex = startPlaylistIndex
    if playlistResult <> invalid then
        scene.playlistOffset = playlistResult.offset
        scene.nextPlaylistOffset = playlistResult.nextOffset
        scene.totalPlaylistCount = playlistResult.totalCount
    end if
    scene.isScreensaver = isScreensaver
    scene.profile = profile
    scene.playlist = playlist
end sub

function WaitForSlideshowSceneAction(scene as Object, port as Object, handleRequests = true as Boolean) as String
    if handleRequests then
        scene.observeField("requestChangeProfile", port)
        scene.observeField("requestChangeCompanion", port)
        scene.observeField("requestExit", port)
    end if

    exitAction = "exit"
    while true
        msg = Wait(0, port)
        if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then
            exit while
        else if handleRequests and type(msg) = "roSGNodeEvent" then
            if msg.getField() = "requestChangeProfile" and msg.getData() = true then
                exitAction = "changeProfile"
                exit while
            else if msg.getField() = "requestChangeCompanion" and msg.getData() = true then
                exitAction = "changeCompanion"
                exit while
            else if msg.getField() = "requestExit" and msg.getData() = true then
                exitAction = "exit"
                exit while
            end if
        end if
    end while

    if handleRequests then
        scene.unobserveField("requestChangeProfile")
        scene.unobserveField("requestChangeCompanion")
        scene.unobserveField("requestExit")
    end if

    return exitAction
end function

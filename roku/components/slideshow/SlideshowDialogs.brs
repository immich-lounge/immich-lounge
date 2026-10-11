' SlideshowDialogs.brs - channel-only dialog helpers

sub PauseForDialogForScene(ctx as Object)
    ctx.paused = true
    ctx.slideTimer.control = "stop"
    ctx.progressTimer.control = "stop"
end sub

sub ResumeAfterDialogForScene(ctx as Object)
    ctx.top.dialog = invalid
    ctx.top.setFocus(true)
    ResumeSlideshowForScene(ctx)
end sub

sub ShowSettingsDialogForScene(ctx as Object)
    PauseForDialogForScene(ctx)

    dlg = CreateObject("roSGNode", "StandardMessageDialog")
    dlg.title = "Settings"
    profileName = ""
    if ctx.profile <> invalid and ctx.profile.name <> invalid then profileName = ctx.profile.name
    dlg.message = ["Profile: " + profileName, ctx.top.companionUrl]
    if IsScheduleSelectionId(ctx.top.profileId) then
        scheduleName = ValueOrDefault(ctx.profile.selectionName, Mid(ctx.top.profileId, 10))
        dlg.message = ["Schedule: " + scheduleName, "Playing profile: " + profileName, ctx.top.companionUrl]
    end if
    dlg.buttons = SettingsDialogButtonsForScene(ctx)
    dlg.observeField("buttonSelected", "OnSettingsButton")
    dlg.observeField("wasClosed", "OnSettingsDismissed")
    ctx.top.dialog = dlg
end sub

function SettingsDialogButtonsForScene(ctx as Object) as Object
    buttons = ["Refresh Now"]
    if ShufflePlaylistLoopsForScene(ctx) then buttons.Push("Restart with fresh shuffle")
    buttons.Append(["Choose Profile or Schedule", "Change Companion", "Clear Cache", "Clear Saved Setup", "Close"])
    return buttons
end function

sub OnSettingsButtonForScene(ctx as Object)
    dlg = ctx.top.dialog
    if dlg = invalid then return
    idx = dlg.buttonSelected
    action = dlg.buttons[idx]
    LogDebug("SlideshowScene", "Settings dialog button=" + idx.ToStr())
    ResumeAfterDialogForScene(ctx)

    if action = "Refresh Now" then
        TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
    else if action = "Restart with fresh shuffle" then
        RestartShuffleForScene(ctx)
    else if action = "Choose Profile or Schedule" then
        ctx.top.requestChangeProfile = true
    else if action = "Change Companion" then
        ctx.top.requestChangeCompanion = true
    else if action = "Clear Cache" then
        RegistryClearCachedData()
        TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
    else if action = "Clear Saved Setup" then
        RegistryClearSavedSetup()
        ctx.top.requestChangeCompanion = true
    end if
end sub

sub OnSettingsDismissedForScene(ctx as Object)
    if ctx.top.dialog = invalid then return
    ResumeAfterDialogForScene(ctx)
end sub

sub ShowExitDialogForScene(ctx as Object)
    PauseForDialogForScene(ctx)

    dlg = CreateObject("roSGNode", "StandardMessageDialog")
    dlg.title = "Exit Immich Lounge"
    dlg.message = ["Are you sure you want to exit?"]
    dlg.buttons = ["Exit", "Cancel"]
    dlg.observeField("buttonSelected", "OnExitButton")
    dlg.observeField("wasClosed", "OnExitDismissed")
    ctx.top.dialog = dlg
end sub

sub OnExitButtonForScene(ctx as Object)
    dlg = ctx.top.dialog
    if dlg = invalid then return
    LogDebug("SlideshowScene", "Exit dialog button=" + dlg.buttonSelected.ToStr())
    if dlg.buttonSelected = 0 then
        ctx.top.dialog = invalid
        ctx.top.requestExit = true
    else
        ResumeAfterDialogForScene(ctx)
    end if
end sub

sub OnExitDismissedForScene(ctx as Object)
    if ctx.top.dialog = invalid then return
    ResumeAfterDialogForScene(ctx)
end sub

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
    dlg.buttons = ["Refresh Now", "Change Profile", "Change Companion", "Clear Cache", "Clear Saved Setup", "Close"]
    dlg.observeField("buttonSelected", "OnSettingsButton")
    dlg.observeField("wasClosed", "OnSettingsDismissed")
    ctx.top.dialog = dlg
end sub

sub OnSettingsButtonForScene(ctx as Object)
    dlg = ctx.top.dialog
    if dlg = invalid then return
    idx = dlg.buttonSelected
    LogDebug("SlideshowScene", "Settings dialog button=" + idx.ToStr())
    ResumeAfterDialogForScene(ctx)

    if idx = 0 then
        TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
    else if idx = 1 then
        ctx.top.requestChangeProfile = true
    else if idx = 2 then
        ctx.top.requestChangeCompanion = true
    else if idx = 3 then
        RegistryClearCachedData()
        TriggerPlaylistRefreshForScene(ctx, ctx.top.isScreensaver)
    else if idx = 4 then
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

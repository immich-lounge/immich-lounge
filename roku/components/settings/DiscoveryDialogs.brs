' DiscoveryDialogs.brs - shared dialog helpers for DiscoveryScene

sub ShowValidationDialogForScene(ctx as Object, title as String, message as String)
    dlg = CreateObject("roSGNode", "StandardMessageDialog")
    dlg.title = title
    dlg.message = [message]
    dlg.buttons = ["OK"]
    dlg.observeField("buttonSelected", "OnValidationDialogClosed")
    dlg.observeField("wasClosed", "OnValidationDialogClosed")
    ctx.validationDlg = dlg
    ctx.top.dialog = dlg
end sub

sub ShowCompletionDialogForScene(ctx as Object, title as String, message as String)
    HideAllPanelsForScene(ctx)
    dlg = CreateObject("roSGNode", "StandardMessageDialog")
    dlg.title = title
    dlg.message = [message]
    dlg.buttons = ["OK"]
    dlg.observeField("buttonSelected", "OnCompletionDialogClosed")
    dlg.observeField("wasClosed", "OnCompletionDialogClosed")
    ctx.completionDlg = dlg
    ctx.top.dialog = dlg
end sub

sub RestoreDiscoveryFocusForScene(ctx as Object)
    if ctx.manualForm.visible then
        ctx.manualForm.focusIndex = ctx.manualForm.selectedIndex
        ctx.manualForm.setFocus(true)
    else if ctx.profileSelector.visible then
        ctx.profileSelector.setFocus(true)
    else if ctx.companionSelector.visible then
        ctx.companionSelector.setFocus(true)
    else
        ctx.top.setFocus(true)
    end if
end sub

sub OnValidationDialogClosedForScene(ctx as Object)
    dlg = ctx.validationDlg
    if dlg = invalid then return
    dlg.unobserveField("buttonSelected")
    dlg.unobserveField("wasClosed")
    dlg.close = true
    ctx.validationDlg = invalid
    if ctx.top.dialog <> invalid then ctx.top.dialog = invalid
    RestoreDiscoveryFocusForScene(ctx)
end sub

sub OnCompletionDialogClosedForScene(ctx as Object)
    dlg = ctx.completionDlg
    if dlg = invalid then return
    dlg.unobserveField("buttonSelected")
    dlg.unobserveField("wasClosed")
    dlg.close = true
    ctx.completionDlg = invalid
    if ctx.top.dialog <> invalid then ctx.top.dialog = invalid

    ctx.top.setupComplete = true
    profileId = ""
    if ctx.top.profileSelected <> invalid and ctx.top.profileSelected.id <> invalid then
        profileId = ctx.top.profileSelected.id
    end if
    LogDebug("DiscoveryScene", "setupComplete set true after confirmation for profile=" + profileId)
end sub

sub CloseKeyboardDialogForScene(ctx as Object, restoreFocus as Boolean)
    dlg = ctx.keyboardDlg
    if dlg <> invalid then
        dlg.unobserveField("buttonSelected")
        dlg.unobserveField("wasClosed")
        dlg.close = true
    end if
    ctx.keyboardDlg = invalid
    if ctx.top.dialog <> invalid then ctx.top.dialog = invalid
    ctx.editingField = ""
    if restoreFocus and ctx.manualForm.visible then
        ctx.manualForm.focusIndex = ctx.manualForm.selectedIndex
        ctx.manualForm.setFocus(true)
    end if
end sub

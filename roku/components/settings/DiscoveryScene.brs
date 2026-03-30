' DiscoveryScene.brs — Refactored using child components

sub init()
    ' UI components
    m.statusLabel = m.top.findNode("statusLabel")
    m.companionSelector = m.top.findNode("companionSelector")
    m.profileSelector = m.top.findNode("profileSelector")
    m.manualForm = m.top.findNode("manualForm")

    ' State
    m.companions    = []
    m.profiles      = []
    m.selectedBase  = ""
    m.prevState     = ""
    m.editingField  = ""
    m.discoveryTask = invalid
    m.discoveryDlg  = invalid
    m.keyboardDlg   = invalid
    m.validationDlg = invalid
    m.completionDlg = invalid
    m.pendingTask   = invalid
    m.fetchTimeoutTimer = CreateObject("roSGNode", "Timer")
    m.fetchTimeoutTimer.duration = 12
    m.fetchTimeoutTimer.repeat = false
    m.fetchTimeoutTimer.observeField("fire", "OnFetchProfilesTimeout")
    m.startupTimer = CreateObject("roSGNode", "Timer")
    m.startupTimer.duration = 0.05
    m.startupTimer.repeat = false
    m.startupTimer.observeField("fire", "OnStartupTimer")
    m.state = "discovery"  ' discovery | companionPick | manualEntry | profilePick

    ' Observe component events
    m.companionSelector.observeField("companionSelected", "OnCompanionSelected")
    m.profileSelector.observeField("profileSelected", "OnProfileSelectedEvent")
    m.manualForm.observeField("editField", "OnEditField")
    m.manualForm.observeField("connectRequested", "OnConnectRequested")

    m.top.setFocus(true)
    m.startupTimer.control = "start"
end sub

sub OnSuspendStartupChanged()
    OnSuspendStartupChangedForScene(m)
end sub

sub OnStartupTimer()
    OnStartupTimerForScene(m)
end sub

sub InitializeFormDefaults()
    InitializeFormDefaultsForScene(m)
end sub

sub ParseUrlIntoForm(url as String)
    ParseUrlIntoFormForScene(m, url)
end sub

function GetSubnetPrefix() as String
    return GetSubnetPrefixForScene()
end function

' ── Discovery ────────────────────────────────────────────────────────────────

sub StartDiscovery()
    StartDiscoveryForScene(m)
end sub

sub StopDiscovery()
    StopDiscoveryForScene(m)
end sub

sub OnDiscoveryState()
    state = m.discoveryTask.discoveryState
    LogDebug("DiscoveryScene", "discoveryState=" + state)
    if state <> "found" and state <> "timeout" then return

    if m.state = "manualEntry" and state = "found" then
        m.companions = m.discoveryTask.companions
        ShowDiscoveryPrompt()
        return
    end if

    if m.state <> "discovery" then return

    if state = "found" then
        m.companions = m.discoveryTask.companions
        if m.companions.Count() = 1 then
            SelectCompanion(m.companions[0])
        else
            ShowCompanionPicker()
        end if
    else if state = "timeout" then
        ShowManualEntry()
    end if
end sub

sub ShowDiscoveryPrompt()
    companion = m.companions[0]
    dlg = CreateObject("roSGNode", "StandardMessageDialog")
    dlg.title = "Companion Found"
    if m.companions.Count() = 1 then
        dlg.message = [companion.friendlyName, companion.baseUrl]
    else
        dlg.message = [m.companions.Count().ToStr() + " companions found nearby"]
    end if
    dlg.buttons = ["Use Discovered", "Keep Manual Entry"]
    dlg.observeField("buttonSelected", "OnDiscoveryPromptButton")
    m.discoveryDlg = dlg
    m.top.dialog = dlg
end sub

sub OnDiscoveryPromptButton()
    idx = m.discoveryDlg.buttonSelected
    m.top.dialog = invalid
    m.discoveryDlg = invalid
    if idx = 0 then
        if m.companions.Count() = 1 then
            SelectCompanion(m.companions[0])
        else
            ShowCompanionPicker()
        end if
    end if
end sub

' ── Companion Selection ──────────────────────────────────────────────────────

sub ShowCompanionPicker()
    m.state = "companionPick"
    HideAllPanels()
    m.companionSelector.companions = m.companions
    m.companionSelector.visible = true
    m.companionSelector.setFocus(true)
    LogDebug("DiscoveryScene", "Showing companion picker count=" + m.companions.Count().ToStr())
end sub

sub OnCompanionSelected()
    companion = m.companionSelector.companionSelected
    if companion <> invalid and companion.baseUrl <> invalid then
        SelectCompanion(companion)
    end if
end sub

sub SelectCompanion(companion as Object)
    m.selectedBase = companion.baseUrl
    LogDebug("DiscoveryScene", "Selected companion " + companion.baseUrl)
    FetchProfiles(companion.baseUrl)
end sub

' ── Manual Entry ─────────────────────────────────────────────────────────────

sub ShowManualEntry()
    ShowManualEntryForScene(m)
end sub

sub OnEditField()
    fieldInfo = m.manualForm.editField
    if fieldInfo = invalid or fieldInfo.name = invalid then return

    CloseKeyboardDialogForScene(m, false)
    if fieldInfo.index <> invalid then
        m.manualForm.selectedIndex = fieldInfo.index
    end if
    m.editingField = fieldInfo.name

    dlg = CreateObject("roSGNode", "StandardKeyboardDialog")
    if fieldInfo.name = "host" then
        dlg.title = "Enter Companion Address"
        dlg.message = ["Enter the hostname or IP address of your companion service"]
        dlg.text = m.manualForm.host
        dlg.keyboardDomain = "generic"
    else if fieldInfo.name = "port" then
        dlg.title = "Enter Port Number"
        dlg.message = ["Enter the port number (default: 4383)"]
        dlg.text = m.manualForm.port
        dlg.keyboardDomain = "numeric"
        if dlg.textEditBox <> invalid then dlg.textEditBox.maxTextLength = 5
    end if
    dlg.buttons = ["OK", "Cancel"]
    dlg.observeField("buttonSelected", "OnKeyboardButtonSelected")
    dlg.observeField("wasClosed", "OnKeyboardClosed")
    m.keyboardDlg = dlg
    m.top.dialog = dlg
end sub

sub OnKeyboardButtonSelected()
    dlg = m.keyboardDlg
    if dlg = invalid then return  ' wasClosed already cleaned up
    if dlg.buttonSelected = 0 then  ' OK
        text = dlg.text.Trim()
        if text <> "" then
            if m.editingField = "host" then
                m.manualForm.host = text
            else if m.editingField = "port" then
                normalizedPort = NormalizePortText(text)
                if normalizedPort = invalid then
                    ShowValidationDialog("Invalid Port", "Enter a whole number from 1 to 65535.")
                    return
                end if
                m.manualForm.port = normalizedPort
            end if
        end if
    end if
    CloseKeyboardDialogForScene(m, true)
end sub

' Fires after buttonSelected (button press) or alone (Back-key dismiss).
' Either way: clear state and restore focus to the form list.
sub OnKeyboardClosed()
    CloseKeyboardDialogForScene(m, true)
end sub

sub CloseKeyboardDialog(restoreFocus as Boolean)
    CloseKeyboardDialogForScene(m, restoreFocus)
end sub

sub OnConnectRequested()
    host = m.manualForm.host.Trim()
    if host = "" then
        ShowValidationDialog("Missing Address", "Enter the hostname or IP address of your companion service.")
        m.manualForm.setFocus(true)
        return
    end if

    port = NormalizePortText(m.manualForm.port)
    if port = invalid then
        ShowValidationDialog("Invalid Port", "Enter a whole number from 1 to 65535.")
        m.manualForm.setFocus(true)
        return
    end if

    m.manualForm.host = host
    m.manualForm.port = port
    url = m.manualForm.protocol + host + ":" + port
    m.selectedBase = url
    LogDebug("DiscoveryScene", "Manual connect requested " + url)
    FetchProfiles(url)
end sub

' ── Profile Selection ────────────────────────────────────────────────────────

sub FetchProfiles(baseUrl as String)
    FetchProfilesForScene(m, baseUrl)
end sub

sub OnProfilesResult()
    OnProfilesResultForScene(m)
end sub

sub OnPendingTaskState()
    OnPendingTaskStateForScene(m)
end sub

sub OnFetchProfilesTimeout()
    OnFetchProfilesTimeoutForScene(m)
end sub

sub HandleProfilesResult(result as Object)
    HandleProfilesResultForScene(m, result)
end sub

sub ShowProfilePicker()
    ShowProfilePickerForScene(m)
end sub

sub OnProfileSelectedEvent()
    profile = m.profileSelector.profileSelected
    if profile <> invalid and profile.id <> invalid then
        CommitProfileSelectionForScene(m, profile)
    end if
end sub

sub CommitProfileSelection(profile as Object)
    CommitProfileSelectionForScene(m, profile)
end sub

' ── Helpers ──────────────────────────────────────────────────────────────────

sub HideAllPanels()
    HideAllPanelsForScene(m)
end sub

function NormalizePortText(text as String) as Dynamic
    trimmed = text.Trim()
    if trimmed = "" then return invalid

    for i = 1 to Len(trimmed)
        ch = Mid(trimmed, i, 1)
        if ch < "0" or ch > "9" then return invalid
    end for

    port = Val(trimmed)
    if port < 1 or port > 65535 then return invalid
    return port.ToStr()
end function

sub ShowValidationDialog(title as String, message as String)
    ShowValidationDialogForScene(m, title, message)
end sub

sub ShowCompletionDialog(title as String, message as String)
    ShowCompletionDialogForScene(m, title, message)
end sub

sub OnValidationDialogClosed()
    OnValidationDialogClosedForScene(m)
end sub

sub OnCompletionDialogClosed()
    OnCompletionDialogClosedForScene(m)
end sub

function DescribeProfilesError(result as Object) as String
    return DescribeProfilesErrorForScene(result)
end function

function DescribeTransportError(err as String) as String
    return DescribeTransportErrorForScene(err)
end function

function onKeyEvent(key as String, press as Boolean) as Boolean
    if not press then return false

    if m.state = "discovery" and key = "options" then
        ShowManualEntry()
        return true
    end if

    if key = "back" then
        if m.top.isScreensaver and m.state <> "profilePick" then
            LogDebug("DiscoveryScene", "Closing screensaver settings")
            m.top.cancelled = true
            LogDebug("DiscoveryScene", "cancelled set true")
            return true
        end if
        if m.top.allowCancelToSlideshow and not m.top.isScreensaver then
            if m.state = "profilePick" or m.state = "manualEntry" or m.state = "companionPick" or m.state = "discovery" then
                LogDebug("DiscoveryScene", "Cancelling discovery flow back to slideshow")
                m.top.cancelled = true
                return true
            end if
        end if
        if m.state = "companionPick" then
            if m.top.isScreensaver then
                ShowManualEntry()
            else
                StartDiscovery()
            end if
            return true
        else if m.state = "manualEntry" then
            if m.top.isScreensaver then
                LogDebug("DiscoveryScene", "Closing screensaver settings")
                m.top.cancelled = true
                LogDebug("DiscoveryScene", "cancelled set true")
            else
                StartDiscovery()
            end if
            return true
        else if m.state = "profilePick" then
            if m.prevState = "manualEntry" then
                ShowManualEntry()
            else if m.companions.Count() > 1 then
                ShowCompanionPicker()
            else
                StartDiscovery()
            end if
            return true
        end if
    end if

    return false
end function

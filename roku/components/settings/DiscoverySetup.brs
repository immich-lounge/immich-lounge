' DiscoverySetup.brs - shared startup/setup helpers for DiscoveryScene

sub OnSuspendStartupChangedForScene(ctx as Object)
    if ctx.top.suspendStartup then
        ctx.startupTimer.control = "stop"
        return
    end if
    ctx.startupTimer.control = "stop"
    ctx.startupTimer.control = "start"
end sub

sub OnStartupTimerForScene(ctx as Object)
    if ctx.top.suspendStartup then return

    ctx.savedProfileId = GetSavedProfileId(ctx.top.isScreensaver)
    savedUrl = RegistryRead("companionUrl")

    if ctx.top.changeCompanionMode then
        LogDebug("DiscoveryScene", "Startup in changeCompanionMode")
        InitializeFormDefaultsForScene(ctx)
        if savedUrl <> invalid and savedUrl <> "" then
            ParseUrlIntoFormForScene(ctx, savedUrl)
            ctx.selectedBase = savedUrl
        end if
        ShowManualEntryForScene(ctx)
    else if savedUrl <> invalid and savedUrl <> "" then
        ParseUrlIntoFormForScene(ctx, savedUrl)
        ctx.selectedBase = savedUrl
        if ctx.top.changeProfileMode then
            LogDebug("DiscoveryScene", "Startup in changeProfileMode")
            FetchProfilesForScene(ctx, savedUrl)
        else
            ctx.statusLabel.text = "Reconnecting..."
            ctx.statusLabel.visible = true
            FetchProfilesForScene(ctx, savedUrl)
        end if
    else
        InitializeFormDefaultsForScene(ctx)
        StartDiscoveryForScene(ctx)
    end if
end sub

sub InitializeFormDefaultsForScene(ctx as Object)
    ctx.manualForm.protocol = "http://"
    ctx.manualForm.host = GetSubnetPrefixForScene()
    ctx.manualForm.port = "4383"
end sub

sub ParseUrlIntoFormForScene(ctx as Object, url as String)
    if Left(url, 8) = "https://" then
        ctx.manualForm.protocol = "https://"
        rest = Mid(url, 9)
    else
        ctx.manualForm.protocol = "http://"
        rest = Mid(url, 8)
    end if
    colonPos = Instr(1, rest, ":")
    if colonPos > 0 then
        ctx.manualForm.host = Left(rest, colonPos - 1)
        ctx.manualForm.port = Mid(rest, colonPos + 1)
    else
        ctx.manualForm.host = rest
        ctx.manualForm.port = "4383"
    end if
end sub

function GetSubnetPrefixForScene() as String
    di = CreateObject("roDeviceInfo")
    addrs = di.GetIPAddrs()
    for each iface in addrs
        ip = addrs[iface]
        if Left(ip, 3) <> "127" and ip <> "" then
            parts = ip.Split(".")
            if parts.Count() = 4 then
                return parts[0] + "." + parts[1] + "." + parts[2] + "."
            end if
        end if
    end for
    return ""
end function

sub StartDiscoveryForScene(ctx as Object)
    StopDiscoveryForScene(ctx)
    ctx.state = "discovery"
    HideAllPanelsForScene(ctx)
    ctx.statusLabel.text = "Looking for companion..." + Chr(10) + Chr(10) + "Press * to enter address manually"
    ctx.statusLabel.visible = true

    ctx.discoveryTask = CreateObject("roSGNode", "DiscoveryTask")
    ctx.discoveryTask.observeField("discoveryState", "OnDiscoveryState")
    LogDebug("DiscoveryScene", "Starting discovery")
    ctx.discoveryTask.command = "start"
end sub

sub StopDiscoveryForScene(ctx as Object)
    if ctx.discoveryTask <> invalid then
        ctx.discoveryTask.unobserveField("discoveryState")
        ctx.discoveryTask = invalid
    end if
end sub

sub ShowManualEntryForScene(ctx as Object)
    StopDiscoveryForScene(ctx)
    ctx.state = "manualEntry"
    HideAllPanelsForScene(ctx)
    ctx.manualForm.visible = true
    ctx.manualForm.focusIndex = ctx.manualForm.selectedIndex
    ctx.manualForm.setFocus(true)
    LogDebug("DiscoveryScene", "Showing manual entry")
end sub

sub HideAllPanelsForScene(ctx as Object)
    ctx.statusLabel.visible = false
    ctx.companionSelector.visible = false
    ctx.profileSelector.visible = false
    ctx.manualForm.visible = false
end sub

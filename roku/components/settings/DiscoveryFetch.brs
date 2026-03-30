' DiscoveryFetch.brs - shared profile fetch helpers for DiscoveryScene

sub FetchProfilesForScene(ctx as Object, baseUrl as String)
    StopDiscoveryForScene(ctx)
    ctx.prevState = ctx.state
    HideAllPanelsForScene(ctx)
    ctx.statusLabel.text = "Testing connection..."
    ctx.statusLabel.visible = true
    ctx.fetchTimeoutTimer.control = "stop"

    if ctx.pendingTask <> invalid then
        ctx.pendingTask.unobserveField("profilesResult")
        ctx.pendingTask.unobserveField("state")
        ctx.pendingTask = invalid
    end if

    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = baseUrl
    task.observeField("profilesResult", "OnProfilesResult")
    task.observeField("state", "OnPendingTaskState")
    ctx.pendingTask = task
    LogDebug("DiscoveryScene", "Fetching profiles from " + baseUrl)
    task.command = "fetchProfiles"
    ctx.fetchTimeoutTimer.control = "start"
end sub

sub OnProfilesResultForScene(ctx as Object)
    if ctx.pendingTask = invalid then return
    result = ctx.pendingTask.profilesResult
    if result = invalid or result.ok = invalid then return
    HandleProfilesResultForScene(ctx, result)
end sub

sub OnPendingTaskStateForScene(ctx as Object)
    if ctx.pendingTask = invalid then return
    state = ctx.pendingTask.state
    LogDebug("DiscoveryScene", "pendingTask.state=" + state)
    if state <> "done" and state <> "stop" then return

    result = ctx.pendingTask.profilesResult
    if result <> invalid and result.ok <> invalid then
        HandleProfilesResultForScene(ctx, result)
    else
        ctx.statusLabel.text = "Could not fetch profiles." + Chr(10) + "Check the companion address and try again."
        ShowManualEntryForScene(ctx)
    end if
end sub

sub OnFetchProfilesTimeoutForScene(ctx as Object)
    if ctx.pendingTask = invalid then return
    LogDebug("DiscoveryScene", "Fetch profiles timed out")
    if ctx.state <> "manualEntry" and ctx.state <> "profilePick" then
        if ctx.pendingTask <> invalid then
            ctx.pendingTask.unobserveField("profilesResult")
            ctx.pendingTask.unobserveField("state")
            ctx.pendingTask = invalid
        end if
        ctx.statusLabel.text = "Could not reach companion." + Chr(10) + "Check the address and port, then try again."
        ShowManualEntryForScene(ctx)
        ShowValidationDialogForScene(ctx, "Connection Timed Out", "Could not reach the companion. Check that it is running and that the address and port are correct.")
    end if
end sub

sub HandleProfilesResultForScene(ctx as Object, result as Object)
    ctx.fetchTimeoutTimer.control = "stop"
    if ctx.pendingTask <> invalid then
        ctx.pendingTask.unobserveField("profilesResult")
        ctx.pendingTask.unobserveField("state")
        ctx.pendingTask = invalid
    end if

    if result.ok = true then
        LogDebug("DiscoveryScene", "Profiles fetched count=" + result.profiles.Count().ToStr())
        ctx.profiles = result.profiles
        if not ctx.top.changeProfileMode and ctx.savedProfileId <> invalid and ctx.savedProfileId <> "" then
            for each p in ctx.profiles
                if p.id = ctx.savedProfileId then
                    CommitProfileSelectionForScene(ctx, p)
                    return
                end if
            end for
        end if
        ShowProfilePickerForScene(ctx)
    else
        LogDebug("DiscoveryScene", "Profile fetch failed: " + DescribeProfilesErrorForScene(result))
        ctx.statusLabel.text = "Could not fetch profiles." + Chr(10) + "Check the companion address and try again."
        ShowManualEntryForScene(ctx)
        ShowValidationDialogForScene(ctx, "Connection Failed", DescribeProfilesErrorForScene(result))
    end if
end sub

sub ShowProfilePickerForScene(ctx as Object)
    ctx.state = "profilePick"
    HideAllPanelsForScene(ctx)
    ctx.profileSelector.profiles = ctx.profiles
    ctx.profileSelector.visible = true
    ctx.profileSelector.setFocus(true)
    LogDebug("DiscoveryScene", "Showing profile picker count=" + ctx.profiles.Count().ToStr())
end sub

sub CommitProfileSelectionForScene(ctx as Object, profile as Object)
    RegistryWrite("companionUrl", ctx.selectedBase)
    RegistryWrite(ProfileIdRegistryKey(ctx.top.isScreensaver), profile.id)
    ctx.top.profileSelected = profile

    if ctx.top.isScreensaver then
        ShowCompletionDialogForScene(ctx, "Settings Saved", "Your screensaver companion and profile have been updated.")
    else
        HideAllPanelsForScene(ctx)
        ctx.statusLabel.text = "Starting slideshow..."
        ctx.statusLabel.visible = true
        ctx.top.setupComplete = true
        LogDebug("DiscoveryScene", "setupComplete set true for profile=" + profile.id)
    end if
end sub

function DescribeProfilesErrorForScene(result as Object) as String
    if result = invalid then return "Could not load profiles from the companion."
    statusCode = 0
    if result.statusCode <> invalid then statusCode = result.statusCode

    if result.error = "timeout" then
        return "The companion did not respond in time. Check that it is running and reachable on your network."
    else if statusCode = 404 then
        return "A server responded, but it does not look like an Immich Lounge companion at this address."
    else if statusCode > 0 then
        return "The companion responded with HTTP " + statusCode.ToStr() + "."
    else if result.error <> invalid and result.error <> "" then
        return DescribeTransportErrorForScene(result.error)
    end if

    return "Could not load profiles from the companion."
end function

function DescribeTransportErrorForScene(err as String) as String
    lowerErr = LCase(err)

    if Instr(1, lowerErr, "quictls") > 0 or Instr(1, lowerErr, "ssl") > 0 or Instr(1, lowerErr, "tls") > 0 then
        return "Secure connection failed. Check whether the companion URL should use HTTP or HTTPS, and verify the server certificate if HTTPS is enabled."
    else if Instr(1, lowerErr, "certificate") > 0 then
        return "Secure connection failed because the server certificate was rejected."
    else if Instr(1, lowerErr, "connection refused") > 0 then
        return "The companion refused the connection. Check that it is running and listening on this port."
    else if Instr(1, lowerErr, "host") > 0 and Instr(1, lowerErr, "resolve") > 0 then
        return "Could not find that host name on your network. Check the address and try again."
    else if Instr(1, lowerErr, "name or service not known") > 0 then
        return "Could not find that host name on your network. Check the address and try again."
    else if Instr(1, lowerErr, "timed out") > 0 or Instr(1, lowerErr, "timeout") > 0 then
        return "The companion did not respond in time. Check that it is running and reachable on your network."
    else if Instr(1, lowerErr, "reset") > 0 then
        return "The connection was interrupted while talking to the companion."
    else if Instr(1, lowerErr, "refused") > 0 then
        return "The companion refused the connection. Check that it is running and listening on this port."
    end if

    return "Could not reach the companion. Check the address, port, and whether HTTP or HTTPS is correct."
end function

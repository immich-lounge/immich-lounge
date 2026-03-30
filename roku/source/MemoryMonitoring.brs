' MemoryMonitoring.brs - certification-oriented Roku memory monitor setup

sub EnableMemoryMonitoring(logScope as String)
    deviceInfo = CreateObject("roDeviceInfo")
    if deviceInfo <> invalid then
        deviceInfo.EnableLowGeneralMemoryEvent(true)
    end if

    memoryMonitor = CreateObject("roAppMemoryMonitor")
    if memoryMonitor <> invalid then
        memoryMonitor.EnableMemoryWarningEvent(true)

        availableMemory = memoryMonitor.GetChannelAvailableMemory()
        memoryLimit = memoryMonitor.GetChannelMemoryLimit()
        memoryPercent = memoryMonitor.GetMemoryLimitPercent()

        if logScope <> invalid and logScope <> "" then
            memoryLimitSummary = "available"
            if memoryLimit = invalid then memoryLimitSummary = "invalid"
            LogDebug(logScope, "memory monitor enabled available=" + availableMemory.ToStr() + " limitPercent=" + memoryPercent.ToStr() + " limitState=" + memoryLimitSummary)
        end if
    end if
end sub

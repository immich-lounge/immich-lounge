' main_screensaver.brs — Screensaver entry point + settings

sub RunScreenSaverSettings()
    ' Direct settings flow for the standalone screensaver.
    screen = CreateObject("roSGScreen")
    port = CreateObject("roMessagePort")
    screen.setMessagePort(port)
    EnableMemoryMonitoring("MainScreensaverSettings")

    scene = screen.CreateScene("DiscoveryScene")
    scene.isScreensaver = true
    scene.setupComplete = false
    scene.cancelled = false
    scene.changeCompanionMode = true
    scene.changeProfileMode = true
    scene.observeField("setupComplete", port)
    scene.observeField("cancelled", port)
    screen.show()
    scene.setFocus(true)

    while true
        msg = Wait(100, port)
        if scene.setupComplete = true or scene.cancelled = true then
            LogDebug("MainScreensaverSettings", "Closing settings screen setupComplete=" + scene.setupComplete.ToStr() + " cancelled=" + scene.cancelled.ToStr())
            scene.unobserveField("setupComplete")
            scene.unobserveField("cancelled")
            screen.close()
            return
        end if
        if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then
            scene.unobserveField("setupComplete")
            scene.unobserveField("cancelled")
            screen.close()
            return
        end if
        if type(msg) = "roSGNodeEvent" then
            if msg.getField() = "setupComplete" and msg.getData() = true then
                LogDebug("MainScreensaverSettings", "Observed setupComplete event")
                scene.unobserveField("setupComplete")
                scene.unobserveField("cancelled")
                screen.close()
                return
            end if
            if msg.getField() = "cancelled" and msg.getData() = true then
                LogDebug("MainScreensaverSettings", "Observed cancelled event")
                scene.unobserveField("setupComplete")
                scene.unobserveField("cancelled")
                screen.close()
                return
            end if
        end if
    end while
end sub

sub RunScreenSaver(_aa as Object)
    ' Screensaver entry point
    screen = CreateObject("roSGScreen")
    m.port = CreateObject("roMessagePort")
    screen.setMessagePort(m.port)
    EnableMemoryMonitoring("MainScreensaver")

    scene = screen.CreateScene("ScreensaverScene")
    screen.show()
    ' vscode_rdb_on_device_component_entry

    AppControllerScreensaver(screen, scene, m.port)

    screen.close()
end sub

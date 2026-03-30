' main.brs — Channel entry point (RunUserInterface only)

sub RunUserInterface(aa as Object)
    ' Channel entry point
    screen = CreateObject("roSGScreen")
    m.port = CreateObject("roMessagePort")
    screen.setMessagePort(m.port)
    EnableMemoryMonitoring("Main")

    ' Register for deep-link input events (cert requirement 5.2 / supports_input_launch=1).
    ' When the channel is already running, Roku routes a launch command here as roInputEvent
    ' rather than restarting — AppController handles it in its message loop.
    input = CreateObject("roInput")
    input.setMessagePort(m.port)

    scene = screen.CreateScene("DiscoveryScene")
    scene.isScreensaver = false
    scene.suspendStartup = true
    screen.show()
    ' vscode_rdb_on_device_component_entry

    ' Hand control to AppController, passing launch args so it can handle deep links.
    AppController(screen, scene, m.port, false, aa)

    screen.close()
end sub

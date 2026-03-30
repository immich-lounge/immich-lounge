' test_main.brs — Test entry point for the BrighterScript + rooibos build.
' Mapped to source/main.brs by bsconfig.test.json.
'
' The rooibos-roku plugin injects the Rooibos framework, but this app still
' enters through RunUserInterface rather than main(). Call Rooibos_init
' directly so the test runner actually starts on device.

sub RunUserInterface(aa as Object)
    Rooibos_init("RooibosScene")
end sub

sub RunScreenSaver(aa as Object)
    ' No-op: not used in test builds.
end sub

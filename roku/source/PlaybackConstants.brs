' PlaybackConstants.brs - shared Roku playback constants and helper labels

function PlaybackHistoryLimit() as Integer
    return 20
end function

function PlaybackConsecutiveFailureThreshold() as Integer
    return 20
end function

function PlaybackRetryDelaySeconds() as Integer
    return 60
end function

function PlaybackLoadingIndicatorDelaySeconds() as Float
    return 0.4
end function

function PlaybackTransitionSafetyTimeoutSeconds() as Integer
    return 10
end function

function PlaybackLayoutRetryDelaySeconds() as Float
    return 0.05
end function

function PlaybackLayoutRetryMaxAttempts() as Integer
    return 3
end function

function PlaybackProgressTickSeconds() as Float
    return 0.1
end function

function PlaybackTransitionDurationSeconds() as Float
    return 0.5
end function

' Old or invalid cached profiles keep the original transition speed.
function ResolvePlaybackTransitionDuration(value as Dynamic, intervalSeconds as Integer) as Float
    kind = LCase(Type(value))
    if kind <> "integer" and kind <> "roint" and kind <> "longinteger" and kind <> "rolonginteger" and kind <> "float" and kind <> "rofloat" and kind <> "double" and kind <> "rodouble" then return PlaybackTransitionDurationSeconds()
    if value >= 0.1 and value <= 5.0 and value < intervalSeconds then return value
    return PlaybackTransitionDurationSeconds()
end function

function PlaybackClockDatePollSeconds() as Integer
    return 60
end function

function PlaybackSlideIntervalDefaultSeconds() as Integer
    return 10
end function

function PlaybackRefreshIntervalDefaultMinutes() as Integer
    return 60
end function

function PlaybackProfileConfigRefreshSeconds() as Integer
    return 300
end function

function PlaybackLogScope(ctx as Object) as String
    if ctx = invalid then
        return "SlideshowScene"
    end if

    top = ctx.top
    if top = invalid then
        return "SlideshowScene"
    end if

    if top.isScreensaver = true then
        return "ScreensaverScene"
    end if

    return "SlideshowScene"
end function

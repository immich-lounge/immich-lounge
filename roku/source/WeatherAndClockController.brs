' WeatherAndClockController.brs - lifecycle/orchestration for clock/date/weather

sub ApplyWeatherAndClockProfileForScene(ctx as Object, display as Dynamic, weather as Dynamic)
    formatting = ResolveDisplayFormatting(display)
    ctx.formatSource = formatting.formatSource
    ctx.locale = formatting.locale
    ctx.dateFormat = formatting.dateFormat
    ctx.clockFormat = formatting.clockFormat

    if display <> invalid then
        ctx.clockAlwaysVisible = ValueOrDefault(display.clockAlwaysVisible, true)
        ctx.weatherUnit = ValueOrDefault(display.weatherUnit, "celsius")
        ctx.showDate = ValueOrDefault(display.showDate, true)
    else
        ctx.clockAlwaysVisible = true
        ctx.weatherUnit = "celsius"
        ctx.showDate = true
    end if

    ApplyClockControllerForScene(ctx)
    ApplyWeatherControllerForScene(ctx, weather)
end sub

sub ApplyClockControllerForScene(ctx as Object)
    if ctx.clockAlwaysVisible then
        ctx.persistentLayer.visible = true
        ctx.clockTimer.control = "start"
    else
        ctx.persistentLayer.visible = false
        ctx.clockTimer.control = "stop"
    end if

    if ctx.showDate and ctx.clockAlwaysVisible then
        EnsureClockDateTimerForScene(ctx)
        ctx.clockDateTimer.control = "start"
        FetchClockDateForScene(ctx)
    else if ctx.clockDateTimer <> invalid then
        ctx.clockDateTimer.control = "stop"
    end if

    UpdateClockDisplayForScene(ctx)
end sub

sub EnsureClockDateTimerForScene(ctx as Object)
    if ctx.clockDateTimer <> invalid then return
    ctx.clockDateTimer = CreateObject("roSGNode", "Timer")
    ctx.clockDateTimer.duration = PlaybackClockDatePollSeconds()
    ctx.clockDateTimer.repeat = true
    ctx.clockDateTimer.observeField("fire", "OnClockDateTimer")
end sub

sub ApplyWeatherControllerForScene(ctx as Object, weather as Dynamic)
    StopWeatherControllerForScene(ctx)
    if weather <> invalid and weather.enabled = true then
        ctx.weatherEnabled = true
        StartWeatherPollForScene(ctx, weather)
    else
        ctx.weatherEnabled = false
        ctx.cachedWeather = invalid
        ctx.persistentLayer.weatherVisible = false
    end if
end sub

sub StopWeatherControllerForScene(ctx as Object)
    if ctx.weatherTimer <> invalid then
        ctx.weatherTimer.control = "stop"
    end if
    if ctx.weatherTask <> invalid then
        ctx.weatherTask.unobserveField("weatherResult")
        ctx.weatherTask = invalid
    end if
end sub

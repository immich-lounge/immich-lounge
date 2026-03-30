' WeatherAndClock.brs - shared clock/date/weather polling helpers

sub UpdateClockDisplayForScene(ctx as Object)
    if ctx.profile = invalid then return
    if not ctx.clockAlwaysVisible then return
    ctx.persistentLayer.clockText = FormatTime(NowSeconds(), ctx.clockFormat)
    if ctx.showDate and (ctx.dateStr = invalid or ctx.dateStr = "") then
        ctx.dateStr = FormatCurrentLocalDateForDisplay(ctx.dateFormat, ctx.locale)
    end if
    ctx.persistentLayer.dateText = ctx.dateStr
    ctx.persistentLayer.showDate = ctx.showDate
    ctx.persistentLayer.showClock = ctx.clockAlwaysVisible
end sub

sub FetchClockDateForScene(ctx as Object)
    if ctx.top.companionUrl = "" or ctx.top.profileId = "" then return
    if ctx.clockDateTask <> invalid then
        ctx.clockDateTask.unobserveField("clockResult")
    end if
    task = CreateObject("roSGNode", "CompanionApiTask")
    task.baseUrl = ctx.top.companionUrl
    task.profileId = ctx.top.profileId
    task.observeField("clockResult", "OnClockDateResult")
    ctx.clockDateTask = task
    task.command = "fetchClock"
end sub

sub ApplyClockDateResultForScene(ctx as Object)
    result = ctx.clockDateTask.clockResult
    if result.ok = true and ctx.formatSource = "profile" and result.formattedDate <> invalid and result.formattedDate <> "" then
        ctx.dateStr = result.formattedDate
    else if result.ok = true and result.dateIso <> invalid then
        ctx.dateStr = FormatIsoDateForDisplay(result.dateIso, ctx.dateFormat, ctx.locale)
    else
        ctx.dateStr = FormatCurrentLocalDateForDisplay(ctx.dateFormat, ctx.locale)
    end if
end sub

sub StartWeatherPollForScene(ctx as Object, weather as Object)
    ctx.weatherPollMinutes = ValueOrDefault(weather.pollIntervalMinutes, 20)
    ctx.weatherLat = weather.latitude
    ctx.weatherLon = weather.longitude

    if ctx.weatherTimer = invalid then
        ctx.weatherTimer = CreateObject("roSGNode", "Timer")
        ctx.weatherTimer.repeat = true
        ctx.weatherTimer.observeField("fire", "OnWeatherTimer")
    else
        ctx.weatherTimer.control = "stop"
    end if
    ctx.weatherTimer.duration = ctx.weatherPollMinutes * 60
    ctx.weatherTimer.control = "start"

    FetchWeatherForScene(ctx)
end sub

sub FetchWeatherForScene(ctx as Object)
    if ctx.weatherTask <> invalid then
        ctx.weatherTask.unobserveField("weatherResult")
    end if
    task = CreateObject("roSGNode", "WeatherTask")
    task.latitude = ctx.weatherLat
    task.longitude = ctx.weatherLon
    task.temperatureUnit = ctx.weatherUnit
    task.observeField("weatherResult", "OnWeatherResult")
    ctx.weatherTask = task
    task.command = "fetch"
end sub

sub ApplyWeatherResultForScene(ctx as Object)
    result = ctx.weatherTask.weatherResult
    if result.ok = true then
        ctx.cachedWeather = result
        ctx.persistentLayer.temperature = result.temperature
        ctx.persistentLayer.iconName = result.iconName
        ctx.persistentLayer.unit = result.unit
        ctx.persistentLayer.weatherVisible = true
    end if
end sub

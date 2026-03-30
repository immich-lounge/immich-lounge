sub init()
    m.top.observeField("command", "OnCommand")
end sub

sub OnCommand()
    if m.top.command = "fetch" then
        m.top.functionName = "FetchWeather"
        m.top.control = "RUN"
    end if
end sub

sub FetchWeather()
    lat  = m.top.latitude
    lon  = m.top.longitude
    unit = m.top.temperatureUnit
    url  = "https://api.open-meteo.com/v1/forecast"
    url  = url + "?latitude=" + Str(lat).Trim()
    url  = url + "&longitude=" + Str(lon).Trim()
    url  = url + "&current=temperature_2m,weather_code"
    url  = url + "&temperature_unit=" + unit

    r = HttpGetJson(url, {})
    if r.ok and r.data.current <> invalid then
        temp = r.data.current.temperature_2m
        code = r.data.current.weather_code
        icon = WmoToIconName(code)
        m.top.weatherResult = {
            ok: true,
            temperature: temp,
            weatherCode: code,
            iconName: icon,
            unit: unit
        }
    else
        m.top.weatherResult = { ok: false, error: r.error }
    end if
end sub

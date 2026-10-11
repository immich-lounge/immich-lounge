' Utils.brs — Shared helpers

function FormatLogTimestamp() as String
    dt = CreateObject("roDateTime")
    dt.ToLocalTime()
    hh = Right("0" + Str(dt.GetHours()).Trim(), 2)
    mm = Right("0" + Str(dt.GetMinutes()).Trim(), 2)
    ss = Right("0" + Str(dt.GetSeconds()).Trim(), 2)
    return hh + ":" + mm + ":" + ss
end function

sub LogDebug(component as String, message as String)
    uptimeSeconds = FormatNumber(UpTime(0), 3)
    print "[" + FormatLogTimestamp() + "][" + uptimeSeconds + "s][" + component + "] " + message
end sub

function FormatNumber(value as Dynamic, decimals as Integer) as String
    text = Str(value).Trim()
    dotPos = Instr(1, text, ".")

    if decimals <= 0 then
        if dotPos > 0 then return Left(text, dotPos - 1)
        return text
    end if

    if dotPos = 0 then
        frac = ""
        for i = 1 to decimals
            frac = frac + "0"
        end for
        return text + "." + frac
    end if

    whole = Left(text, dotPos - 1)
    frac = Mid(text, dotPos + 1)
    if Len(frac) > decimals then
        frac = Left(frac, decimals)
    end if
    while Len(frac) < decimals
        frac = frac + "0"
    end while
    return whole + "." + frac
end function

' Safe JSON parse; returns invalid on failure
function ParseJson_Safe(jsonStr as String) as Object
    result = ParseJSON(jsonStr)
    if result = invalid then
        LogDebug("Utils", "ParseJson_Safe failed: " + Left(jsonStr, 200))
    end if
    return result
end function

' Clamp a number between lo and hi
function Clamp(val as Float, lo as Float, hi as Float) as Float
    if val < lo then return lo
    if val > hi then return hi
    return val
end function

' Truncate string to maxLen characters
function TruncateStr(s as String, maxLen as Integer) as String
    if Len(s) <= maxLen then return s
    return Left(s, maxLen)
end function

' Current epoch seconds
function NowSeconds() as LongInteger
    dt = CreateObject("roDateTime")
    return dt.AsSeconds()
end function

' Format epoch seconds to HH:mm or hh:mm a (local time)
function FormatTime(epochSec as LongInteger, fmt as String) as String
    dt = CreateObject("roDateTime")
    dt.FromSeconds(epochSec)
    dt.ToLocalTime()
    h = dt.GetHours()
    mnt = dt.GetMinutes()
    if fmt = "hh:mm a" then
        ampm = "AM"
        if h >= 12 then ampm = "PM"
        h = h mod 12
        if h = 0 then h = 12
        return Right("0" + Str(h).Trim(), 2) + ":" + Right("0" + Str(mnt).Trim(), 2) + " " + ampm
    end if
    ' Default HH:mm
    return Right("0" + Str(h).Trim(), 2) + ":" + Right("0" + Str(mnt).Trim(), 2)
end function

' WMO code → icon filename
function WmoToIconName(code as Integer) as String
    if code = 0 then return "clear"
    if code >= 1 and code <= 3 then return "partly_cloudy"
    if code >= 45 and code <= 48 then return "fog"
    if code >= 51 and code <= 57 then return "drizzle"
    if code >= 61 and code <= 67 then return "rain"
    if code >= 71 and code <= 77 then return "snow"
    if code >= 80 and code <= 82 then return "rain"
    if code >= 85 and code <= 86 then return "snow"
    if code >= 95 and code <= 99 then return "thunderstorm"
    return "overcast"
end function

function WeatherTemperatureSuffix(unit as Dynamic) as String
    if unit = "fahrenheit" then return "°F"
    return "°C"
end function

function ValueOrDefault(val as Dynamic, default as Dynamic) as Dynamic
    if val = invalid then return default
    return val
end function
' Normalize the base URL before appending an Immich API path.
function NormalizeImmichServerUrl(serverUrl as Dynamic) as String
    if serverUrl = invalid then return ""

    base = serverUrl.ToStr().Trim()
    while Len(base) > 0 and Right(base, 1) = "/"
        base = Left(base, Len(base) - 1)
    end while
    return base
end function
function PlaybackProfileId(profile as Dynamic, selectedId as String) as String
    if profile <> invalid and profile.playbackProfileId <> invalid and profile.playbackProfileId <> "" then return profile.playbackProfileId
    return selectedId
end function

function UsesDailyMemories(profile as Dynamic) as Boolean
    if profile = invalid then return false
    if profile.photoSource = invalid then return false
    if profile.memories <> invalid then
        if profile.memories.includeBirthdays = true then return true
    end if
    if profile.contentSources <> invalid then
        for each source in profile.contentSources
            if source.type = "memories" then return true
        end for
    end if
    return false
end function

function IsScheduleSelectionId(selectionId as String) as Boolean
    return Left(selectionId, 9) = "schedule:"
end function

function CachedPlaylistMatchesPlayback(resumeState as Dynamic, selectedId as String, playbackId as String) as Boolean
    if resumeState <> invalid then
        if resumeState.profileId <> invalid and resumeState.profileId <> selectedId then return false
    end if
    return PlaybackProfileId(resumeState, selectedId) = playbackId
end function

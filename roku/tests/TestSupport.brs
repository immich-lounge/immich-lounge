' TestSupport.brs - test-build-only helpers mapped into the source scope.
' These mirror small pure helpers that otherwise live in component scopes.

' Component callback boundary for shared refresh helpers in the test source scope.
' Actual rendering is exercised by the production scene; tests stub slide loading and persistence.
sub ApplyProfile()
end sub

' Startup tests use no loading scene; production UI is exercised on the device.
sub ShowPlaybackStartupStatusInScene(scene as Object, message as String)
end sub

function BuildPlaylistUrl(baseUrl as String, profileId as String, count as Integer, offset = 0 as Integer) as String
    return baseUrl + "/api/profiles/" + profileId + "/playlist?count=" + Str(count).Trim() + "&offset=" + Str(offset).Trim()
end function

function BuildPlaylistUrlWithVersion(baseUrl as String, profileId as String, count as Integer, offset as Integer, playlistVersion as String) as String
    url = BuildPlaylistUrl(baseUrl, profileId, count, offset)
    if playlistVersion <> invalid and playlistVersion <> "" then
        url = url + "&playlistVersion=" + playlistVersion
    end if
    return url
end function

function BuildMSearchPacket() as String
    ST = "urn:immich-lounge:service:companion:1"
    crlf = Chr(13) + Chr(10)
    pkt = "M-SEARCH * HTTP/1.1" + crlf
    pkt = pkt + "HOST: 239.255.255.250:1900" + crlf
    pkt = pkt + "MAN: " + Chr(34) + "ssdp:discover" + Chr(34) + crlf
    pkt = pkt + "MX: 3" + crlf
    pkt = pkt + "ST: " + ST + crlf
    pkt = pkt + crlf
    return pkt
end function

function ParseSsdpLocation(response as String) as Dynamic
    lines = response.Split(Chr(10))
    for each line in lines
        trimmed = line.Trim()
        upper = UCase(trimmed)
        if Left(upper, 9) = "LOCATION:" then
            return trimmed.Mid(9).Trim()
        end if
    end for
    return invalid
end function

function BuildPeopleString(meta as Object) as String
    if meta.people = invalid then return ""

    names = []
    for each p in meta.people
        if p.name <> invalid and p.name <> "" then names.Push(p.name)
    end for

    result = ""
    for i = 0 to names.Count() - 1
        if i > 0 then result = result + ", "
        result = result + names[i]
    end for
    return result
end function

sub HistoryPush(hist as Object, entry as Object)
    hist.Push(entry)
    while hist.Count() > 20
        hist.Shift()
    end while
end sub

function HistoryPop(hist as Object) as Dynamic
    if hist.Count() = 0 then return invalid
    idx = hist.Count() - 1
    entry = hist[idx]
    hist.Delete(idx)
    return entry
end function

function ResetFailureCounter(counter as Integer) as Integer
    if counter < 0 then return 0
    return 0
end function

' CompanionApiTask.brs — Companion HTTP API: profiles, enriched profile, playlist, clock

sub init()
    m.top.observeField("command", "OnCommand")
end sub

sub OnCommand()
    cmd = m.top.command
    if cmd = "fetchProfiles" then
        m.top.functionName = "FetchProfiles"
        m.top.control = "RUN"
    else if cmd = "fetchProfile" then
        m.top.functionName = "FetchProfile"
        m.top.control = "RUN"
    else if cmd = "fetchPlaylist" then
        m.top.functionName = "FetchPlaylist"
        m.top.control = "RUN"
    else if cmd = "fetchClock" then
        m.top.functionName = "FetchClock"
        m.top.control = "RUN"
    end if
end sub

' GET /api/profiles → { ok, profiles: [{id, name, description}] }
sub FetchProfiles()
    url = m.top.baseUrl + "/api/profiles"
    r = HttpGetJson(url, {})
    if r.ok then
        m.top.profilesResult = { ok: true, profiles: r.data }
    else
        m.top.profilesResult = { ok: false, error: r.error, statusCode: r.statusCode }
    end if
end sub

' GET /api/profiles/:id → { ok, profile: enrichedProfile }
sub FetchProfile()
    url = m.top.baseUrl + "/api/profiles/" + m.top.profileId
    r = HttpGetJson(url, {})
    result = { ok: false, statusCode: r.statusCode, error: r.error }
    if r.statusCode = 200 then
        result.ok = true
        result.profile = r.data
        ' Check schema version
        if r.data.schemaVersion <> invalid and r.data.schemaVersion > 1 then
            result.schemaWarning = true
        end if
    else if r.statusCode = 404 then
        result.notFound = true
    end if
    m.top.profileResult = result
end sub

' GET /api/profiles/:id/playlist?count=500 → { ok, assets, building, cached }
' Polls until building=false (up to maxWaitSec)
sub FetchPlaylist()
    isScreensaver = m.top.isScreensaver
    maxWaitSec = 60
    if isScreensaver = true then maxWaitSec = 15
    if m.top.maxWaitSec > 0 then maxWaitSec = m.top.maxWaitSec
    pollInterval = 5

    offset = m.top.playlistOffset
    count = m.top.playlistCount
    if count <= 0 then count = 50
    if offset < 0 then offset = 0
    url = BuildPlaylistUrl(m.top.baseUrl, m.top.profileId, count, offset)
    deadline = UpTime(0) + maxWaitSec

    result = { ok: false, assets: [], building: false }
    while true
        r = HttpGetJson(url, {})
        if r.ok then
            data = r.data
            if data.building = true then
                if UpTime(0) >= deadline then
                    result.timedOut = true
                    exit while
                end if
                ' Poll again after interval
                sleep = pollInterval * 1000
                Wait(sleep, CreateObject("roMessagePort"))
            else
                result.ok = true
                result.assets = data.assets
                result.generatedAt = data.generatedAt
                result.cached = data.cached
                result.offset = data.offset
                result.nextOffset = data.nextOffset
                result.totalCount = data.totalCount
                exit while
            end if
        else
            result.error = r.error
            result.statusCode = r.statusCode
            exit while
        end if
    end while
    m.top.playlistResult = result
end sub

' GET /api/profiles/:id/clock → { ok, dateIso: "2026-03-16", formattedDate?: "16. März 2026" }
sub FetchClock()
    url = m.top.baseUrl + "/api/profiles/" + m.top.profileId + "/clock"
    r = HttpGetJson(url, {})
    if r.ok and r.data.dateIso <> invalid then
        m.top.clockResult = {
            ok: true
            dateIso: r.data.dateIso
            formattedDate: r.data.formattedDate
        }
    else
        m.top.clockResult = { ok: false }
    end if
end sub

function BuildPlaylistUrl(baseUrl as String, profileId as String, count as Integer, offset = 0 as Integer) as String
    return baseUrl + "/api/profiles/" + profileId + "/playlist?count=" + Str(count).Trim() + "&offset=" + Str(offset).Trim()
end function

' TruncatePlaylistForRegistry and PrepareEntryForRegistry are defined in Utils.brs
' (included via CompanionApiTask.xml) — no duplication needed here.

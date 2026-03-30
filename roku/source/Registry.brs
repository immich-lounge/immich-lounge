' Registry.brs — Roku registry read/write helpers

' Section name shared by all keys in this channel.
' (Inlined as a literal because bsc does not support 'const' in plain .brs files.)

function RegistrySectionName() as String
    return "ImmichFrame"
end function

function ChannelProfileIdKey() as String
    return "channelProfileId"
end function

function ScreensaverProfileIdKey() as String
    return "screensaverProfileId"
end function

function ChannelCachedProfileKey() as String
    return "cachedChannelProfile"
end function

function ScreensaverCachedProfileKey() as String
    return "cachedScreensaverProfile"
end function

function ChannelCachedPlaylistKey() as String
    return "cachedChannelPlaylist"
end function

function ScreensaverCachedPlaylistKey() as String
    return "cachedScreensaverPlaylist"
end function

function ChannelResumeStateKey() as String
    return "channelResumeState"
end function

function ScreensaverResumeStateKey() as String
    return "screensaverResumeState"
end function

function ProfileIdRegistryKey(isScreensaver as Boolean) as String
    if isScreensaver then
        return ScreensaverProfileIdKey()
    end if
    return ChannelProfileIdKey()
end function

function CachedProfileRegistryKey(isScreensaver as Boolean) as String
    if isScreensaver then
        return ScreensaverCachedProfileKey()
    end if
    return ChannelCachedProfileKey()
end function

function CachedPlaylistRegistryKey(isScreensaver as Boolean) as String
    if isScreensaver then
        return ScreensaverCachedPlaylistKey()
    end if
    return ChannelCachedPlaylistKey()
end function

function ResumeStateRegistryKey(isScreensaver as Boolean) as String
    if isScreensaver then
        return ScreensaverResumeStateKey()
    end if
    return ChannelResumeStateKey()
end function

function GetSavedProfileId(isScreensaver as Boolean) as Dynamic
    if isScreensaver then
        profileId = RegistryRead(ScreensaverProfileIdKey())
        if profileId <> invalid and profileId <> "" then return profileId
        return RegistryRead(ChannelProfileIdKey())
    end if
    return RegistryRead(ChannelProfileIdKey())
end function

function RegistryRead(key as String) as Dynamic
    sec = CreateObject("roRegistrySection", RegistrySectionName())
    if sec.Exists(key) then return sec.Read(key)
    return invalid
end function

sub RegistryWrite(key as String, value as String)
    sec = CreateObject("roRegistrySection", RegistrySectionName())
    sec.Write(key, value)
    sec.Flush()
end sub

sub RegistryDelete(key as String)
    sec = CreateObject("roRegistrySection", RegistrySectionName())
    sec.Delete(key)
    sec.Flush()
end sub

sub RegistryClearAll()
    reg = CreateObject("roRegistry")
    reg.Delete(RegistrySectionName())
end sub

sub RegistryClearCachedData()
    RegistryDelete(ChannelCachedProfileKey())
    RegistryDelete(ScreensaverCachedProfileKey())
    RegistryDelete(ChannelCachedPlaylistKey())
    RegistryDelete(ScreensaverCachedPlaylistKey())
    RegistryDelete(ChannelResumeStateKey())
    RegistryDelete(ScreensaverResumeStateKey())
end sub

sub RegistryClearSavedSetup()
    RegistryDelete("companionUrl")
    RegistryDelete(ChannelProfileIdKey())
    RegistryDelete(ScreensaverProfileIdKey())
    RegistryClearCachedData()
end sub

' Convenience: read and parse JSON from registry
function RegistryReadJson(key as String) as Dynamic
    raw = RegistryRead(key)
    if raw = invalid then return invalid
    return ParseJson(raw)
end function

' Convenience: stringify and write JSON to registry
sub RegistryWriteJson(key as String, value as Object)
    sec = CreateObject("roRegistrySection", RegistrySectionName())
    sec.Write(key, FormatJSON(value))
    sec.Flush()
end sub

function LoadResumeState(isScreensaver as Boolean, profileId as String) as Dynamic
    raw = RegistryRead(ResumeStateRegistryKey(isScreensaver))
    if raw = invalid then return invalid

    parsed = ParseJson_Safe(raw)
    if parsed = invalid then return invalid
    if parsed.profileId = invalid or parsed.profileId <> profileId then return invalid

    return parsed
end function

function ResolveResumePlaylistOffset(resumeState as Dynamic) as Integer
    if resumeState = invalid then return 0
    if resumeState.playlistOffset = invalid then return 0
    if resumeState.playlistOffset < 0 then return 0
    return resumeState.playlistOffset
end function

function ResolveResumePlaylistIndex(playlist as Object, resumeState as Dynamic) as Integer
    if playlist = invalid or playlist.Count() = 0 then return 0
    if resumeState = invalid then return 0

    if resumeState.assetId <> invalid and resumeState.assetId <> "" then
        idx = FindPlaylistIndexByAssetId(playlist, resumeState.assetId)
        if idx >= 0 then return idx
    end if

    if resumeState.playlistIndex <> invalid and resumeState.playlistIndex >= 0 and resumeState.playlistIndex < playlist.Count() then
        return resumeState.playlistIndex
    end if

    return 0
end function

function FindPlaylistIndexByAssetId(playlist as Object, assetId as String) as Integer
    for i = 0 to playlist.Count() - 1
        entry = playlist[i]
        if entry <> invalid and entry.id <> invalid and entry.id = assetId then
            return i
        end if
    end for
    return -1
end function

function BuildLaunchPlaylistResultFromResume(playlist as Object, resumeState as Dynamic) as Object
    result = {
        offset: 0
        nextOffset: 0
        totalCount: playlist.Count()
    }

    if resumeState = invalid then return result
    if resumeState.playlistOffset <> invalid and resumeState.playlistOffset >= 0 then result.offset = resumeState.playlistOffset
    if resumeState.nextPlaylistOffset <> invalid and resumeState.nextPlaylistOffset >= 0 then result.nextOffset = resumeState.nextPlaylistOffset
    if resumeState.totalPlaylistCount <> invalid and resumeState.totalPlaylistCount > 0 then result.totalCount = resumeState.totalPlaylistCount

    return result
end function

function ApplyPendingBatchSwapState(ctx as Object, isScreensaver as Boolean) as Boolean
    if not ctx.waitingForNextBatchSwap then return false
    if ctx.pendingBatchAssets = invalid then return false
    if ctx.playlistIndex <> 0 then return false

    ctx.playlist = ctx.pendingBatchAssets
    ctx.playlistOffset = ctx.pendingBatchOffset
    ctx.nextPlaylistOffset = ctx.pendingNextPlaylistOffset
    ctx.pendingBatchAssets = invalid
    ctx.pendingBatchOffset = 0
    ctx.pendingNextPlaylistOffset = 0
    ctx.waitingForNextBatchSwap = false
    ctx.playlistIndex = 0
    PersistActivePlaylistCacheForMode(ctx.playlist, isScreensaver)
    return true
end function

sub PersistResumeStateForMode(entry as Object, top as Object, playlistIndex as Integer, playlistOffset as Integer, nextPlaylistOffset as Integer, totalPlaylistCount as Integer, isScreensaver as Boolean)
    if entry = invalid then return
    state = {
        profileId: top.profileId
        assetId: ValueOrDefault(entry.id, "")
        playlistIndex: playlistIndex
        playlistOffset: playlistOffset
        nextPlaylistOffset: nextPlaylistOffset
        totalPlaylistCount: totalPlaylistCount
    }
    RegistryWriteJson(ResumeStateRegistryKey(isScreensaver), state)
end sub

sub PersistActivePlaylistCacheForMode(playlist as Object, isScreensaver as Boolean)
    truncated = TruncatePlaylistForRegistry(playlist)
    RegistryWrite(CachedPlaylistRegistryKey(isScreensaver), FormatJSON(truncated))
end sub

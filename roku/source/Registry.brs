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

function CacheIdentityRegistryKey(isScreensaver as Boolean) as String
    if isScreensaver then return "cachedScreensaverIdentity"
    return "cachedChannelIdentity"
end function

function RegistryCacheMatchesSelection(isScreensaver as Boolean, companionUrl as String, profileId as String) as Boolean
    raw = RegistryRead(CacheIdentityRegistryKey(isScreensaver))
    if raw = invalid then return false
    identity = ParseJson_Safe(raw)
    if type(identity) <> "roAssociativeArray" then return false
    return identity.companionUrl = companionUrl and identity.profileId = profileId
end function

sub RegistryPrepareCacheForMode(isScreensaver as Boolean, companionUrl as String, profileId as String)
    if RegistryCacheMatchesSelection(isScreensaver, companionUrl, profileId) then return
    RegistryClearCachedDataForMode(isScreensaver)
    RegistryWriteJson(CacheIdentityRegistryKey(isScreensaver), { companionUrl: companionUrl, profileId: profileId })
end sub

sub RegistrySaveProfileSelection(companionUrl as String, profileId as String, isScreensaver as Boolean)
    if RegistryRead("companionUrl") <> companionUrl then
        RegistryClearCachedData()
    else
        if GetSavedProfileId(isScreensaver) <> profileId then RegistryClearCachedDataForMode(isScreensaver)
        ' An unconfigured screensaver inherits the channel's profile selection.
        if not isScreensaver then
            screensaverProfileId = RegistryRead(ScreensaverProfileIdKey())
            if screensaverProfileId = invalid or screensaverProfileId = "" then
                if GetSavedProfileId(true) <> profileId then RegistryClearCachedDataForMode(true)
            end if
        end if
    end if
    RegistryWrite("companionUrl", companionUrl)
    RegistryWrite(ProfileIdRegistryKey(isScreensaver), profileId)
end sub

function LoadCachedProfileForMode(isScreensaver as Boolean, companionUrl as String, profileId as String) as Dynamic
    if not RegistryCacheMatchesSelection(isScreensaver, companionUrl, profileId) then return invalid
    raw = RegistryRead(CachedProfileRegistryKey(isScreensaver))
    if raw = invalid then return invalid
    profile = ParseJson_Safe(raw)
    if type(profile) <> "roAssociativeArray" then return invalid
    if profile.id <> profileId then return invalid
    return profile
end function

function LoadCachedPlaylistForMode(isScreensaver as Boolean, companionUrl as String, profileId as String) as Object
    if not RegistryCacheMatchesSelection(isScreensaver, companionUrl, profileId) then return []
    raw = RegistryRead(CachedPlaylistRegistryKey(isScreensaver))
    if raw = invalid then return []
    playlist = ParseJson_Safe(raw)
    if type(playlist) <> "roArray" then return []
    return playlist
end function

sub PersistProfileCacheForMode(profile as Object, companionUrl as String, profileId as String, isScreensaver as Boolean)
    RegistryPrepareCacheForMode(isScreensaver, companionUrl, profileId)
    RegistryWrite(CachedProfileRegistryKey(isScreensaver), FormatJSON(profile))
end sub

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
    RegistryClearCachedDataForMode(false)
    RegistryClearCachedDataForMode(true)
end sub

sub RegistryClearCachedDataForMode(isScreensaver as Boolean)
    RegistryDelete(CachedProfileRegistryKey(isScreensaver))
    RegistryDelete(CachedPlaylistRegistryKey(isScreensaver))
    RegistryDelete(ResumeStateRegistryKey(isScreensaver))
    RegistryDelete(CacheIdentityRegistryKey(isScreensaver))
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

function LoadResumeState(isScreensaver as Boolean, profileId as String, companionUrl as String) as Dynamic
    if not RegistryCacheMatchesSelection(isScreensaver, companionUrl, profileId) then return invalid
    raw = RegistryRead(ResumeStateRegistryKey(isScreensaver))
    if raw = invalid then return invalid

    parsed = ParseJson_Safe(raw)
    if type(parsed) <> "roAssociativeArray" then return invalid
    if parsed.profileId = invalid or parsed.profileId <> profileId then return invalid

    return parsed
end function

function ResolveResumePlaylistOffset(resumeState as Dynamic) as Integer
    if resumeState = invalid then return 0
    if resumeState.playlistOffset = invalid then return 0
    if resumeState.playlistOffset < 0 then return 0
    return resumeState.playlistOffset
end function

function NormalizePlaylistVersion(version as Dynamic) as String
    if version = invalid then return ""
    return version
end function

function ResolveResumePlaylistVersion(resumeState as Dynamic) as String
    if resumeState = invalid then return ""
    return NormalizePlaylistVersion(resumeState.playlistVersion)
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

function ResolvePlaylistIndexAfterRefresh(playlist as Object, currentAssetId as Dynamic, fallbackIndex as Integer) as Integer
    if playlist = invalid or playlist.Count() = 0 then return 0

    if currentAssetId <> invalid and currentAssetId <> "" then
        idx = FindPlaylistIndexByAssetId(playlist, currentAssetId)
        if idx >= 0 then return idx
    end if

    if fallbackIndex >= 0 and fallbackIndex < playlist.Count() then
        return fallbackIndex
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
        playlistVersion: ""
    }

    if resumeState = invalid then return result
    if resumeState.playlistOffset <> invalid and resumeState.playlistOffset >= 0 then result.offset = resumeState.playlistOffset
    if resumeState.nextPlaylistOffset <> invalid and resumeState.nextPlaylistOffset >= 0 then result.nextOffset = resumeState.nextPlaylistOffset
    if resumeState.totalPlaylistCount <> invalid and resumeState.totalPlaylistCount > 0 then result.totalCount = resumeState.totalPlaylistCount
    result.playlistVersion = ResolveResumePlaylistVersion(resumeState)

    return result
end function

function ApplyPendingBatchSwapState(ctx as Object, isScreensaver as Boolean) as Boolean
    if not ctx.waitingForNextBatchSwap then return false
    if ctx.pendingBatchAssets = invalid then return false

    ctx.playlist = ctx.pendingBatchAssets
    ctx.portraitPairs = {}
    ctx.playlistOffset = ctx.pendingBatchOffset
    ctx.nextPlaylistOffset = ctx.pendingNextPlaylistOffset
    ctx.playlistVersion = NormalizePlaylistVersion(ctx.pendingBatchPlaylistVersion)
    ctx.pendingBatchAssets = invalid
    ctx.pendingBatchOffset = 0
    ctx.pendingBatchPlaylistVersion = ""
    ctx.pendingNextPlaylistOffset = 0
    ctx.waitingForNextBatchSwap = false
    ctx.playlistIndex = 0
    PersistActivePlaylistCacheForMode(ctx.playlist, isScreensaver, ctx.top.companionUrl, ctx.top.profileId)
    return true
end function

sub PersistResumeStateForMode(entry as Object, top as Object, playlistIndex as Integer, playlistOffset as Integer, nextPlaylistOffset as Integer, totalPlaylistCount as Integer, playlistVersion as String, isScreensaver as Boolean)
    if entry = invalid then return
    RegistryPrepareCacheForMode(isScreensaver, top.companionUrl, top.profileId)
    state = {
        profileId: top.profileId
        assetId: ValueOrDefault(entry.id, "")
        playlistIndex: playlistIndex
        playlistOffset: playlistOffset
        nextPlaylistOffset: nextPlaylistOffset
        totalPlaylistCount: totalPlaylistCount
        playlistVersion: NormalizePlaylistVersion(playlistVersion)
    }
    RegistryWriteJson(ResumeStateRegistryKey(isScreensaver), state)
end sub

sub PersistActivePlaylistCacheForMode(playlist as Object, isScreensaver as Boolean, companionUrl as String, profileId as String)
    RegistryPrepareCacheForMode(isScreensaver, companionUrl, profileId)
    truncated = TruncatePlaylistForRegistry(playlist)
    RegistryWrite(CachedPlaylistRegistryKey(isScreensaver), FormatJSON(truncated))
end sub

' MediaUrls.brs - Immich asset URL helpers

' Build Immich media URL from playlist entry and profile.
' Authentication uses the x-api-key header set via SetHeaders() on each Poster node
' in ApplyProfile() - the API key is never embedded in URLs.
function BuildMediaUrl(entry as Object, profile as Object) as String
    if profile.immich = invalid then return ""
    base = NormalizeImmichServerUrl(profile.immich.serverUrl)
    quality = "preview"
    if profile.imageQuality <> invalid then quality = profile.imageQuality

    if entry.type = "video" or entry.type = "livePhoto" then
        videoId = entry.id
        if entry.type = "livePhoto" and entry.livePhotoVideoId <> invalid then
            videoId = entry.livePhotoVideoId
        end if
        return base + "/api/assets/" + videoId + "/video/playback"
    end if

    if quality = "original" then
        return base + "/api/assets/" + entry.id + "/original"
    end if
    return base + "/api/assets/" + entry.id + "/thumbnail?size=preview"
end function

function BuildBackgroundMediaUrl(entry as Object, profile as Object) as String
    if profile.immich = invalid then return ""
    base = NormalizeImmichServerUrl(profile.immich.serverUrl)

    if entry.type = "video" or entry.type = "livePhoto" then
        return BuildMediaUrl(entry, profile)
    end if

    return base + "/api/assets/" + entry.id + "/thumbnail?size=thumbnail"
end function

function NormalizeImmichServerUrl(serverUrl as Dynamic) as String
    if serverUrl = invalid then return ""

    base = serverUrl.ToStr().Trim()
    while Len(base) > 0 and Right(base, 1) = "/"
        base = Left(base, Len(base) - 1)
    end while

    return base
end function

' PlaylistRegistry.brs - helpers for cached playlist storage

function PlaybackRegistryPlaylistWindowSize() as Integer
    return 50
end function

' Truncate playlist to 50 entries for registry storage so cached fallback
' matches the startup playlist window requested from the companion.
function TruncatePlaylistForRegistry(entries as Object) as Object
    maxEntries = PlaybackRegistryPlaylistWindowSize()
    if entries.Count() <= maxEntries then return entries
    result = []
    for i = 0 to maxEntries - 1
        result.Push(PrepareEntryForRegistry(entries[i]))
    end for
    return result
end function

' Prepare single playlist entry for registry (truncate sourceLabel to 20 chars)
function PrepareEntryForRegistry(entry as Object) as Object
    out = { id: entry.id, type: entry.type }
    if entry.sourceLabel <> invalid then
        out.sourceLabel = TruncateStr(entry.sourceLabel, 20)
    end if
    if entry.livePhotoVideoId <> invalid then
        out.livePhotoVideoId = entry.livePhotoVideoId
    end if
    return out
end function

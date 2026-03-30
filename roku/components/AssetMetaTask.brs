' AssetMetaTask.brs — Fetch asset metadata from Immich for the next slide

sub init()
    m.top.observeField("command", "OnCommand")
end sub

sub OnCommand()
    if m.top.command = "fetchMeta" then
        m.top.functionName = "FetchMeta"
        m.top.control = "RUN"
    end if
end sub

sub FetchMeta()
    assetId = m.top.assetId
    base    = m.top.immichBaseUrl
    key     = m.top.apiKey
    url     = base + "/api/assets/" + assetId
    headers = BuildImmichHeaders(key)
    r = HttpGetJson(url, headers)
    if r.ok then
        AttachFormattedDate(r.data)
        m.top.metaResult = { ok: true, data: r.data }
    else
        m.top.metaResult = { ok: false, error: r.error, statusCode: r.statusCode }
    end if
end sub

sub AttachFormattedDate(meta as Object)
    if meta = invalid then return

    dateValue = ExtractAssetDateValue(meta)
    if dateValue = "" then return

    if LCase(m.top.formatSource) = "profile" and m.top.useLocalDateFormatting <> true then
        formattedDate = FetchFormattedDateFromCompanion(dateValue)
        if formattedDate <> "" then
            meta.formattedDate = formattedDate
            return
        end if
    end if

    fallbackDate = FormatIsoDateForDisplay(dateValue, m.top.dateFormat, m.top.locale)
    if fallbackDate <> "" then
        meta.formattedDate = fallbackDate
    end if
end sub

function ExtractAssetDateValue(meta as Object) as String
    if meta.exifInfo <> invalid and meta.exifInfo.dateTimeOriginal <> invalid then
        return meta.exifInfo.dateTimeOriginal
    end if
    if meta.fileCreatedAt <> invalid then
        return meta.fileCreatedAt
    end if
    return ""
end function

function FetchFormattedDateFromCompanion(dateValue as String) as String
    if m.top.companionUrl = "" or m.top.profileId = "" then return ""

    url = m.top.companionUrl + "/api/profiles/" + m.top.profileId + "/format-date"
    headers = { "Content-Type": "application/json" }
    body = FormatJson({ value: dateValue })
    r = HttpPostJson(url, body, headers)
    if r.ok and r.data <> invalid and r.data.formattedDate <> invalid then
        return r.data.formattedDate
    end if

    return ""
end function

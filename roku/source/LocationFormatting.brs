' LocationFormatting.brs - shared metadata label formatting.
function FormatAssetLocation(meta as Dynamic, format as Dynamic) as String
    if GetInterface(meta, "ifAssociativeArray") = invalid then return ""
    exif = meta.exifInfo
    if GetInterface(exif, "ifAssociativeArray") = invalid then return ""
    if GetInterface(format, "ifString") = invalid then format = "cityCountry"

    fields = ["city", "country"]
    if format = "cityStateCountry" then
        fields = ["city", "state", "country"]
    else if format = "city" then
        fields = ["city"]
    else if format = "stateCountry" then
        fields = ["state", "country"]
    else if format = "country" then
        fields = ["country"]
    end if

    seen = {}
    result = ""
    for each field in fields
        value = exif[field]
        if GetInterface(value, "ifString") <> invalid then
            value = value.Trim()
            key = LCase(value)
            if value <> "" and not seen.DoesExist(key) then
                if result <> "" then result = result + ", "
                result = result + value
                seen[key] = true
            end if
        end if
    end for
    return result
end function

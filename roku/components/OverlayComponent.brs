sub init()
    m.dateShadowLine     = m.top.findNode("dateShadowLine")
    m.locationShadowLine = m.top.findNode("locationShadowLine")
    m.albumShadowLine    = m.top.findNode("albumShadowLine")
    m.peopleShadowLine   = m.top.findNode("peopleShadowLine")
    m.dateLine     = m.top.findNode("dateLine")
    m.locationLine = m.top.findNode("locationLine")
    m.albumLine    = m.top.findNode("albumLine")
    m.peopleLine   = m.top.findNode("peopleLine")
    m.cornerDateShadow = m.top.findNode("cornerDateShadow")
    m.cornerLocShadow  = m.top.findNode("cornerLocationShadow")
    m.cornerDate   = m.top.findNode("cornerDate")
    m.cornerLoc    = m.top.findNode("cornerLocation")

    m.fadeTimer = CreateObject("roSGNode", "Timer")
    m.fadeTimer.repeat = false
    m.fadeTimer.observeField("fire", "OnFadeTimer")

    ' Track which fields should be visible based on content
    m.shouldShowDate     = false
    m.shouldShowLocation = false
    m.shouldShowAlbum    = false
    m.shouldShowPeople   = false
end sub

sub OnVisibleChanged()
    v = m.top.visible
    ApplyVisibility(v)
    if v and m.top.overlayBehavior = "fade" then
        m.fadeTimer.duration = m.top.fadeSeconds
        m.fadeTimer.control = "start"
    end if
end sub

sub OnFadeTimer()
    ApplyVisibility(false)
end sub

sub ApplyVisibility(v as Boolean)
    style = m.top.overlayStyle

    ' Only show labels if overlay is visible AND the field has content
    m.dateShadowLine.visible     = v and style = "bottom" and m.shouldShowDate
    m.locationShadowLine.visible = v and style = "bottom" and m.shouldShowLocation
    m.albumShadowLine.visible    = v and style = "bottom" and m.shouldShowAlbum
    m.peopleShadowLine.visible   = v and style = "bottom" and m.shouldShowPeople
    m.dateLine.visible     = v and style = "bottom" and m.shouldShowDate
    m.locationLine.visible = v and style = "bottom" and m.shouldShowLocation
    m.albumLine.visible    = v and style = "bottom" and m.shouldShowAlbum
    m.peopleLine.visible   = v and style = "bottom" and m.shouldShowPeople
    m.cornerDateShadow.visible   = v and style = "corner" and m.shouldShowDate
    m.cornerLocShadow.visible    = v and style = "corner" and m.shouldShowLocation
    m.cornerDate.visible   = v and style = "corner" and m.shouldShowDate
    m.cornerLoc.visible    = v and style = "corner" and m.shouldShowLocation
end sub

sub OnMetaChanged()
    meta = m.top.assetMeta
    if meta = invalid then return

    fields = m.top.overlayFields
    style  = m.top.overlayStyle
    if style = "none" then return

    ' Update text content and track which fields should be visible
    ' But don't set visibility directly to avoid flicker - let ApplyVisibility handle it
    
    ' Date
    showDate = IsFieldEnabled("date", fields)
    dateStr = FormatAssetDate(meta)
    if style = "bottom" then
        m.dateShadowLine.text = iif(showDate, dateStr, "")
        m.dateLine.text = iif(showDate, dateStr, "")
    else if style = "corner" then
        m.cornerDateShadow.text = iif(showDate, dateStr, "")
        m.cornerDate.text = iif(showDate, dateStr, "")
    end if
    m.shouldShowDate = showDate and dateStr <> ""

    ' Location
    showLoc = IsFieldEnabled("location", fields)
    locStr  = BuildLocationString(meta)
    if style = "bottom" then
        m.locationShadowLine.text = iif(showLoc, locStr, "")
        m.locationLine.text = iif(showLoc, locStr, "")
    else if style = "corner" then
        m.cornerLocShadow.text = iif(showLoc, locStr, "")
        m.cornerLoc.text = iif(showLoc, locStr, "")
    end if
    m.shouldShowLocation = showLoc and locStr <> ""

    ' Album
    showAlbum = IsFieldEnabled("album", fields) and style = "bottom"
    albumStr  = m.top.sourceLabel
    if albumStr = invalid then albumStr = ""
    m.albumShadowLine.text = iif(showAlbum, albumStr, "")
    m.albumLine.text = iif(showAlbum, albumStr, "")
    m.shouldShowAlbum = showAlbum and albumStr <> ""

    ' People
    showPeople = IsFieldEnabled("people", fields) and style = "bottom"
    peopleStr  = BuildPeopleString(meta)
    m.peopleShadowLine.text = iif(showPeople, peopleStr, "")
    m.peopleLine.text = iif(showPeople, peopleStr, "")
    m.shouldShowPeople = showPeople and peopleStr <> ""

    ' Apply visibility if overlay is currently visible
    if m.top.visible then
        ApplyVisibility(true)
    end if

    ' Handle overlay behavior for fade timer
    behavior = m.top.overlayBehavior
    if behavior = "always" then
        ' Always visible - ensure it's shown
        if not m.top.visible then
            m.top.visible = true
        end if
    else if behavior = "fade" then
        ' Fade behavior - only restart timer if overlay is already visible
        if m.top.visible then
            m.fadeTimer.control = "stop"
            m.fadeTimer.duration = m.top.fadeSeconds
            m.fadeTimer.control = "start"
        end if
    end if
end sub

function IsFieldEnabled(fieldName as String, fields as Object) as Boolean
    for each f in fields
        if f = fieldName then return true
    end for
    return false
end function

' Format asset date using the profile locale/date format; fallback to a readable localized default.
function FormatAssetDate(meta as Object) as String
    if meta <> invalid and meta.formattedDate <> invalid and meta.formattedDate <> "" then
        return meta.formattedDate
    end if
    return FormatAssetDateForDisplay(meta, m.top.dateFormat, m.top.locale)
end function

' City, Country from exifInfo
function BuildLocationString(meta as Object) as String
    if meta.exifInfo = invalid then return ""
    city    = meta.exifInfo.city
    country = meta.exifInfo.country
    if city <> invalid and country <> invalid then return city + ", " + country
    if city <> invalid then return city
    if country <> invalid then return country
    return ""
end function

' "Alice, Bob"
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

function iif(cond as Boolean, t as Dynamic, f as Dynamic) as Dynamic
    if cond then return t
    return f
end function

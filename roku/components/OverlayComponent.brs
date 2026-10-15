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
    m.bottomBackdrop = m.top.findNode("bottomBackdrop")
    m.cornerBackdrop = m.top.findNode("cornerBackdrop")
    ' A soft dark gradient keeps bright and detailed photos from swallowing captions.
    alpha = [0, 16, 32, 56, 88, 120, 152, 176, 192, 200, 208, 208, 208, 208]
    for i = 0 to 103
        band = m.bottomBackdrop.createChild("Rectangle")
        band.translation = [0, 768 + i * 3]
        band.width = m.top.viewportWidth
        band.height = 3
        band.color = "#000000FF"
        stepIndex = Int(i / 8)
        fraction = (i mod 8) / 8.0
        band.opacity = (alpha[stepIndex] + (alpha[stepIndex + 1] - alpha[stepIndex]) * fraction) / 255.0
    end for

    m.fadeTimer = CreateObject("roSGNode", "Timer")
    m.fadeTimer.repeat = false
    m.fadeTimer.observeField("fire", "OnFadeTimer")

    ' Track which fields should be visible based on content
    m.shouldShowDate     = false
    m.shouldShowLocation = false
    m.shouldShowAlbum    = false
    m.shouldShowPeople   = false
    ' visible is inherited from Group; observe it explicitly on device.
    m.top.observeField("visible", "OnVisibleChanged")
    OnViewportChanged()
end sub

sub OnVisibleChanged()
    m.fadeTimer.control = "stop"
    v = m.top.visible
    ApplyVisibility(v)
    if v and not m.top.externallyManaged and m.top.overlayBehavior = "fade" then
        m.fadeTimer.duration = m.top.fadeSeconds
        m.fadeTimer.control = "start"
    end if
end sub

sub OnVisibilityModeChanged()
    m.fadeTimer.control = "stop"
end sub

sub OnViewportChanged()
    ' Keep text inside its photo's half; retain the original single-photo widths.
    for each id in ["dateLine", "dateShadowLine", "locationLine", "locationShadowLine", "peopleLine", "peopleShadowLine"]
        m.top.findNode(id).width = m.top.viewportWidth - 120
    end for
    width = 900
    if m.top.viewportWidth < 1920 then width = m.top.viewportWidth - 120
    m.albumLine.width = width
    m.albumShadowLine.width = width
    paired = m.top.viewportWidth < 1920
    alignment = "left"
    if paired and m.top.textAlignment = "right" then alignment = "right"
    for each id in ["dateLine", "dateShadowLine", "locationLine", "locationShadowLine", "albumLine", "albumShadowLine", "peopleLine", "peopleShadowLine", "cornerDate", "cornerDateShadow", "cornerLocation", "cornerLocationShadow"]
        m.top.findNode(id).horizAlign = alignment
    end for
    for each band in m.bottomBackdrop.getChildren(-1, 0)
        band.width = m.top.viewportWidth
    end for
    if paired then
        primaryColor = PairedCaptionColor(255, m.top.pairedTextBrightness)
        secondaryColor = PairedCaptionColor(238, m.top.pairedTextBrightness)
        m.dateLine.color = primaryColor
        m.locationLine.color = primaryColor
        m.albumLine.color = secondaryColor
        m.peopleLine.color = secondaryColor
        m.cornerDate.color = primaryColor
        m.cornerLoc.color = primaryColor
    else
        m.dateLine.color = "#FFFFFFEE"
        m.locationLine.color = "#DDDDDDCC"
        m.albumLine.color = "#AAAAAA99"
        m.peopleLine.color = "#AAAAAA99"
        m.cornerDate.color = "#FFFFFFEE"
        m.cornerLoc.color = "#CCCCCCCC"
    end if
    ' Right corner captions sit below the shared clock/weather area.
    cornerX = 55
    cornerY = 50
    if paired then
        cornerX = 60
        cornerY = 54
        if alignment = "right" then
            cornerX = m.top.viewportWidth - 456
            cornerY = 198
        end if
    end if
    m.cornerDate.translation = [cornerX, cornerY]
    m.cornerDateShadow.translation = [cornerX + 3, cornerY + 3]
    m.cornerLoc.translation = [cornerX, cornerY + 42]
    m.cornerLocShadow.translation = [cornerX + 3, cornerY + 45]
    if not paired then
        m.cornerDateShadow.translation = [57, 52]
        m.cornerLoc.translation = [55, 90]
        m.cornerLocShadow.translation = [57, 92]
    end if
    m.cornerBackdrop.translation = [cornerX - 24, cornerY - 18]
    m.cornerBackdrop.width = 450
    ApplyVisibility(m.top.visible)
end sub

function PairedCaptionColor(peak as Integer, brightness as Integer) as String
    if brightness < 50 then brightness = 50
    if brightness > 100 then brightness = 100
    level = Int(peak * brightness / 100.0)
    digits = "0123456789ABCDEF"
    shade = digits.Mid(Int(level / 16), 1) + digits.Mid(level mod 16, 1)
    ' Change the text luminance, keeping it opaque over the contrast background.
    return "#" + shade + shade + shade + "FF"
end function

sub OnFadeTimer()
    ApplyVisibility(false)
end sub

sub ApplyVisibility(v as Boolean)
    style = m.top.overlayStyle
    hasContent = m.shouldShowDate or m.shouldShowLocation or m.shouldShowAlbum or m.shouldShowPeople
    m.bottomBackdrop.visible = v and m.top.viewportWidth < 1920 and style = "bottom" and hasContent
    m.cornerBackdrop.visible = v and m.top.viewportWidth < 1920 and style = "corner" and (m.shouldShowDate or m.shouldShowLocation)

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
    caption = ValueOrDefault(m.top.birthdayCaption, "")
    if not showDate then dateStr = ""
    if caption <> "" then
        if dateStr <> "" then
            dateStr = caption + " · " + dateStr
        else
            dateStr = caption
        end if
        showDate = true
    end if
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
    if m.top.externallyManaged then return
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

' Selected location fields from Immich metadata.
function BuildLocationString(meta as Object) as String
    return FormatAssetLocation(meta, m.top.locationFormat)
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

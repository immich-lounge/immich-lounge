sub init()
    m.icon      = m.top.findNode("icon")
    m.tempShadowLabel = m.top.findNode("tempShadowLabel")
    m.tempLabel = m.top.findNode("tempLabel")
end sub

sub OnDataChanged()
    iconName = m.top.iconName
    if iconName <> "" then
        m.icon.uri = "pkg:/images/wmo_icons/" + iconName + ".png"
    end if

    temp = m.top.temperature
    unit = m.top.unit
    suffix = "°C"
    if unit = "fahrenheit" then suffix = "°F"
    m.tempShadowLabel.text = Str(Int(temp)).Trim() + suffix
    m.tempLabel.text = Str(Int(temp)).Trim() + suffix
end sub

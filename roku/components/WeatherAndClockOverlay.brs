sub init()
    m.clockShadowLabel = m.top.findNode("clockShadowLabel")
    m.clockLabel = m.top.findNode("clockLabel")
    m.dateShadowLabel = m.top.findNode("dateShadowLabel")
    m.dateLabel = m.top.findNode("dateLabel")
    m.weatherWidget = m.top.findNode("weatherWidget")
end sub

sub OnDataChanged()
    showClock = m.top.showClock
    m.top.visible = showClock
    if not showClock then return

    m.clockShadowLabel.text = m.top.clockText
    m.clockLabel.text = m.top.clockText

    if m.top.showDate and m.top.dateText <> "" then
        m.dateShadowLabel.text = m.top.dateText
        m.dateShadowLabel.visible = true
        m.dateLabel.text = m.top.dateText
        m.dateLabel.visible = true
    else
        m.dateShadowLabel.visible = false
        m.dateLabel.visible = false
    end if

    m.weatherWidget.temperature = m.top.temperature
    m.weatherWidget.iconName = m.top.iconName
    m.weatherWidget.unit = m.top.unit
    m.weatherWidget.visible = m.top.weatherVisible
end sub

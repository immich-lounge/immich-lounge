sub init()
    m.list = m.top.findNode("list")
    
    m.top.observeField("protocol", "BuildList")
    m.top.observeField("host", "BuildList")
    m.top.observeField("port", "BuildList")
    m.top.observeField("visible", "OnVisibleChanged")
    m.top.observeField("focusIndex", "OnFocusIndexChanged")
    
    m.list.observeField("itemSelected", "OnItemSelected")
    
    BuildList()
end sub

sub BuildList()
    content = CreateObject("roSGNode", "ContentNode")
    
    ' Protocol toggle
    node = content.createChild("ContentNode")
    node.title = "Protocol: " + m.top.protocol
    
    ' Host
    node = content.createChild("ContentNode")
    hostLabel = "URL: "
    if m.top.host <> "" then
        hostLabel = hostLabel + m.top.host
    else
        hostLabel = hostLabel + "(not set)"
    end if
    node.title = hostLabel
    
    ' Port
    node = content.createChild("ContentNode")
    node.title = "Port: " + m.top.port
    
    ' Connect button
    node = content.createChild("ContentNode")
    node.title = "→ Connect"
    
    m.list.content = content
    ApplyFocusIndex(m.top.selectedIndex)
end sub

sub OnVisibleChanged()
    if m.top.visible then
        m.list.setFocus(true)
        ApplyFocusIndex(m.top.selectedIndex)
    end if
end sub

sub OnFocusIndexChanged()
    ApplyFocusIndex(m.top.focusIndex)
end sub

sub ApplyFocusIndex(idx as Integer)
    if idx < 0 then idx = 0
    if idx > 3 then idx = 3
    m.top.selectedIndex = idx
    m.list.jumpToItem = idx
end sub

sub OnItemSelected()
    idx = m.list.itemSelected
    m.top.selectedIndex = idx
    
    if idx = 0 then
        ' Toggle protocol
        if m.top.protocol = "http://" then
            m.top.protocol = "https://"
        else
            m.top.protocol = "http://"
        end if
    else if idx = 1 then
        ' Edit host
        m.top.editField = { name: "host", value: m.top.host, index: idx }
    else if idx = 2 then
        ' Edit port
        m.top.editField = { name: "port", value: m.top.port, index: idx }
    else if idx = 3 then
        ' Connect
        m.top.connectRequested = true
    end if
end sub


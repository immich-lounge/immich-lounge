sub init()
    m.list = m.top.findNode("list")
    m.top.observeField("companions", "OnCompanionsChanged")
    m.top.observeField("visible", "OnVisibleChanged")
    m.list.observeField("itemSelected", "OnItemSelected")
end sub

sub OnCompanionsChanged()
    companions = m.top.companions
    if companions = invalid or companions.Count() = 0 then
        return
    end if
    
    items = []
    for each comp in companions
        label = ""
        if comp.friendlyName <> invalid and comp.friendlyName <> "" then
            label = comp.friendlyName
        else if comp.baseUrl <> invalid then
            label = comp.baseUrl
        end if
        if comp.baseUrl <> invalid and comp.baseUrl <> "" and label <> comp.baseUrl then
            label = label + " (" + comp.baseUrl + ")"
        end if
        items.Push(label)
    end for
    
    content = CreateObject("roSGNode", "ContentNode")
    for each item in items
        node = content.createChild("ContentNode")
        node.title = item
    end for
    m.list.content = content
end sub

sub OnVisibleChanged()
    if m.top.visible then
        m.list.setFocus(true)
    end if
end sub

sub OnItemSelected()
    idx = m.list.itemSelected
    companions = m.top.companions
    if idx >= 0 and idx < companions.Count() then
        m.top.companionSelected = companions[idx]
    end if
end sub


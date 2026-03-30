sub init()
    m.list = m.top.findNode("list")
    m.top.observeField("profiles", "OnProfilesChanged")
    m.top.observeField("visible", "OnVisibleChanged")
    m.list.observeField("itemSelected", "OnItemSelected")
end sub

sub OnProfilesChanged()
    profiles = m.top.profiles
    if profiles = invalid or profiles.Count() = 0 then
        return
    end if
    
    items = []
    for each p in profiles
        desc = p.name
        if p.description <> invalid and p.description <> "" then
            desc = p.name + " — " + p.description
        end if
        items.Push(desc)
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
    profiles = m.top.profiles
    if idx >= 0 and idx < profiles.Count() then
        m.top.profileSelected = profiles[idx]
    end if
end sub


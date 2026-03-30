sub init()
    m.fill = m.top.findNode("fill")
end sub

sub OnProgressChanged()
    p = m.top.progress
    if p < 0 then p = 0
    if p > 1 then p = 1
    m.fill.width = 1920 * p
end sub

sub init()
    m.label = m.top.findNode("label")
    m.top.visible = false
end sub

sub OnMessageChanged()
    m.label.text = m.top.message
    m.top.visible = true
    m.timer = CreateObject("roSGNode", "Timer")
    m.timer.duration = 5
    m.timer.repeat = false
    m.timer.observeField("fire", "OnTimerFire")
    m.timer.control = "start"
end sub

sub OnTimerFire()
    m.top.visible = false
end sub

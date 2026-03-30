' UiHelpers.brs - shared UI convenience helpers

sub ShowSetupPromptWithMessage(scene as Object, message as String)
    label = scene.findNode("statusLabel")
    if label <> invalid then
        label.text = message
        label.visible = true
    end if
end sub

sub ShowToastInNode(scene as Object, toastNodeId as String, message as String)
    toast = scene.findNode(toastNodeId)
    if toast = invalid then
        toast = CreateObject("roSGNode", "ToastComponent")
        toast.id = toastNodeId
        scene.appendChild(toast)
    end if
    toast.message = message
    toast.visible = true
end sub

sub ShowFullScreenErrorInStatusLabel(scene as Object, message as String)
    label = scene.findNode("statusLabel")
    if label <> invalid then
        label.text = message
        label.visible = true
    end if
end sub

sub ShowPlaybackStartupStatusInScene(scene as Object, message as String)
    panel = scene.findNode("fallbackPanel")
    if panel <> invalid then panel.opacity = 1.0

    poster = scene.findNode("fallbackPoster")
    if poster <> invalid and panel = invalid then poster.opacity = 1.0

    label = scene.findNode("statusLabel")
    if label <> invalid then
        label.text = message
        label.visible = true
    end if

    spinner = scene.findNode("spinner")
    if spinner <> invalid then spinner.visible = true
end sub

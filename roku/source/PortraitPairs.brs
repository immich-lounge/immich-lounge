' PortraitPairs.brs - shared selection, layout, navigation, and poster cleanup.

function PortraitPairCandidateIndex(playlist as Object, index as Integer, bitmapWidth as Float, bitmapHeight as Float, enabled as Boolean) as Integer
    if not enabled or bitmapWidth <= 0 or bitmapHeight <= bitmapWidth then return -1
    if index < 0 or index + 1 >= playlist.Count() then return -1
    first = playlist[index]
    second = playlist[index + 1]
    if first.type <> "photo" or second.type <> "photo" then return -1
    if first.id = second.id then return -1
    return index + 1
end function

function PortraitCellLayout(bitmapWidth as Float, bitmapHeight as Float, cellX as Integer, cellWidth as Integer, screenHeight as Integer) as Object
    scale = cellWidth / bitmapWidth
    heightScale = screenHeight / bitmapHeight
    if heightScale < scale then scale = heightScale
    width = Int(bitmapWidth * scale / 3) * 3
    height = Int(bitmapHeight * scale / 3) * 3
    if width < 3 then width = 3
    if height < 3 then height = 3
    x = cellX + Int((cellWidth - width) / 6) * 3
    y = Int((screenHeight - height) / 6) * 3
    return { width: width, height: height, x: x, y: y }
end function

function PortraitPairLayout(leftWidth as Float, leftHeight as Float, rightWidth as Float, rightHeight as Float, screenWidth = 1920 as Integer, screenHeight = 1080 as Integer) as Dynamic
    if leftWidth <= 0 or rightWidth <= 0 then return invalid
    if leftHeight <= leftWidth or rightHeight <= rightWidth then return invalid
    cellWidth = Int(screenWidth / 6) * 3
    return {
        left: PortraitCellLayout(leftWidth, leftHeight, 0, cellWidth, screenHeight)
        right: PortraitCellLayout(rightWidth, rightHeight, cellWidth, cellWidth, screenHeight)
    }
end function

function NextPortraitSlideIndex(index as Integer, count as Integer, consumed as Integer) as Integer
    if consumed <> 2 then consumed = 1
    nextIndex = index + consumed
    if nextIndex >= count then return 0
    return nextIndex
end function

function PortraitSlideAdvanceCount(playlist as Object, index as Integer, rightAssetId as String) as Integer
    if rightAssetId = "" or index < 0 or index + 1 >= playlist.Count() then return 1
    if playlist[index + 1].id = rightAssetId then return 2
    return 1
end function

sub ReleasePortraitPosterForSlot(ctx as Object, slot as Integer)
    node = ctx.pairedPosters[slot]
    node.unobserveField("loadStatus")
    node.visible = false
    node.uri = ""
end sub

function PreviousPortraitSlideIndex(playlist as Object, index as Integer, knownPairs as Object, enabled as Boolean) as Integer
    if playlist.Count() = 0 then return 0
    previous = index - 1
    if previous < 0 then previous = playlist.Count() - 1
    if enabled and previous > 0 then
        left = playlist[previous - 1]
        right = playlist[previous]
        if knownPairs[left.id] = right.id then previous = previous - 1
    end if
    return previous
end function

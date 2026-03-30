' DiscoveryTask.brs — SSDP M-SEARCH for companion service

sub init()
    m.top.observeField("command", "OnCommand")
end sub

sub OnCommand()
    if m.top.command = "start" then
        m.top.functionName = "RunDiscovery"
        m.top.control = "RUN"
    end if
end sub

sub RunDiscovery()
    print "[DiscoveryTask] RunDiscovery started"
    ' SSDP disabled — ifSocket.bind() is declared in ifSocket but unimplemented on
    ' this firmware, crashing the whole app (&hf4). No try/catch in BrightScript.
    ' TODO: restore SSDP once a safe probe mechanism is available.
    m.top.discoveryState = "timeout"
    print "[DiscoveryTask] discoveryState set to timeout"
end sub

' Build SSDP M-SEARCH UDP packet string
function BuildMSearchPacket() as String
    ST   = "urn:immich-lounge:service:companion:1"
    crlf = Chr(13) + Chr(10)
    pkt  = "M-SEARCH * HTTP/1.1" + crlf
    pkt  = pkt + "HOST: 239.255.255.250:1900" + crlf
    pkt  = pkt + "MAN: " + Chr(34) + "ssdp:discover" + Chr(34) + crlf
    pkt  = pkt + "MX: 3" + crlf
    pkt  = pkt + "ST: " + ST + crlf
    pkt  = pkt + crlf
    return pkt
end function

' Extract LOCATION header value from SSDP response string
function ParseSsdpLocation(response as String) as Dynamic
    lines = response.Split(Chr(10))
    for each line in lines
        trimmed = line.Trim()
        upper = UCase(trimmed)
        if Left(upper, 9) = "LOCATION:" then
            return trimmed.Mid(9).Trim()
        end if
    end for
    return invalid
end function

' Strip path from URL to get base URL
function ExtractBaseUrl(url as String) as String
    ' Find third slash (after http://)
    idx = InStr(8, url, "/")
    if idx > 0 then return Left(url, idx - 1)
    return url
end function

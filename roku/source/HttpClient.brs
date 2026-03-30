' HttpClient.brs — Synchronous HTTP helpers using roUrlTransfer
' All calls are blocking; use from Task nodes only (never from SceneGraph render thread).

' Result AA: { statusCode: Integer, body: String, error: String }
function HttpGet(url as String, headers as Object) as Object
    result = { statusCode: -1, body: "", error: "" }
    xfer = CreateObject("roUrlTransfer")
    xfer.SetUrl(url)
    xfer.RetainBodyOnError(true)
    xfer.SetCertificatesFile("common:/certs/ca-bundle.crt")

    for each key in headers
        xfer.AddHeader(key, headers[key])
    end for

    port = CreateObject("roMessagePort")
    xfer.SetMessagePort(port)
    xfer.AsyncGetToString()

    timeout = 10000 ' 10 seconds
    startMs = UpTime(0)
    while true
        msg = Wait(timeout, port)
        if type(msg) = "roUrlEvent" then
            result.statusCode = msg.GetResponseCode()
            result.body = msg.GetString()
            if result.statusCode < 0 then
                result.error = msg.GetFailureReason()
            end if
            exit while
        end if
        if UpTime(0) - startMs > timeout / 1000.0 then
            result.error = "timeout"
            exit while
        end if
    end while
    return result
end function

' Build standard Immich API headers
function BuildImmichHeaders(apiKey as String) as Object
    return { "x-api-key": apiKey }
end function

' GET and parse JSON body; returns { ok: bool, data: Object, statusCode: int, error: String }
function HttpGetJson(url as String, headers as Object) as Object
    raw = HttpGet(url, headers)
    result = { ok: false, data: invalid, statusCode: raw.statusCode, error: raw.error }
    if raw.statusCode = 200 then
        parsed = ParseJson_Safe(raw.body)
        if parsed <> invalid then
            result.ok = true
            result.data = parsed
        else
            result.error = "JSON parse failure"
        end if
    end if
    return result
end function

function HttpPost(url as String, body as String, headers as Object) as Object
    result = { statusCode: -1, body: "", error: "" }
    xfer = CreateObject("roUrlTransfer")
    xfer.SetUrl(url)
    xfer.RetainBodyOnError(true)
    xfer.SetCertificatesFile("common:/certs/ca-bundle.crt")

    for each key in headers
        xfer.AddHeader(key, headers[key])
    end for

    port = CreateObject("roMessagePort")
    xfer.SetMessagePort(port)
    xfer.AsyncPostFromString(body)

    timeout = 5000
    startMs = UpTime(0)
    while true
        msg = Wait(timeout, port)
        if type(msg) = "roUrlEvent" then
            result.statusCode = msg.GetResponseCode()
            result.body = msg.GetString()
            if result.statusCode < 0 then
                result.error = msg.GetFailureReason()
            end if
            exit while
        end if
        if UpTime(0) - startMs > timeout / 1000.0 then
            result.error = "timeout"
            exit while
        end if
    end while
    return result
end function

function HttpPostJson(url as String, body as String, headers as Object) as Object
    raw = HttpPost(url, body, headers)
    result = { ok: false, data: invalid, statusCode: raw.statusCode, error: raw.error }
    if raw.statusCode = 200 then
        parsed = ParseJson_Safe(raw.body)
        if parsed <> invalid then
            result.ok = true
            result.data = parsed
        else
            result.error = "JSON parse failure"
        end if
    end if
    return result
end function

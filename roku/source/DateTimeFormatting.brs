' DateTimeFormatting.brs - shared locale/date/time formatting helpers

function ResolveDisplayFormatting(display as Dynamic) as Object
    formatSource = "roku"
    if display <> invalid and display.formatSource <> invalid and display.formatSource <> "" then
        formatSource = LCase(display.formatSource)
    end if

    locale = GetEffectiveLocale(formatSource, invalid)
    dateFormat = GetDefaultDateFormatForLocale(locale)
    clockFormat = GetDeviceClockFormatPattern()

    if formatSource = "profile" and display <> invalid then
        locale = GetEffectiveLocale(formatSource, display.locale)
        if display.dateFormat <> invalid and display.dateFormat <> "" then
            dateFormat = display.dateFormat
        end if
        if display.clockFormat <> invalid and display.clockFormat <> "" then
            clockFormat = display.clockFormat
        end if
    end if

    return {
        formatSource: formatSource
        locale: locale
        dateFormat: dateFormat
        clockFormat: clockFormat
    }
end function

function GetEffectiveLocale(formatSource as String, profileLocale as Dynamic) as String
    if formatSource = "profile" and profileLocale <> invalid and profileLocale <> "" then
        return NormalizeLocaleCode(profileLocale)
    end if

    di = CreateObject("roDeviceInfo")
    locale = ""
    if di <> invalid then locale = di.GetCurrentLocale()
    return NormalizeLocaleCode(locale)
end function

function NormalizeLocaleCode(locale as Dynamic) as String
    if locale = invalid or locale = "" then return "en-US"

    normalized = locale
    normalized = normalized.Replace("_", "-")
    if Len(normalized) = 2 then
        if normalized = "de" then return "de-DE"
        if normalized = "nl" then return "nl-NL"
        if normalized = "fr" then return "fr-FR"
        if normalized = "es" then return "es-ES"
        if normalized = "it" then return "it-IT"
        if normalized = "pt" then return "pt-BR"
        if normalized = "sv" then return "sv-SE"
        if normalized = "da" then return "da-DK"
        if normalized = "no" or normalized = "nb" then return "nb-NO"
        if normalized = "fi" then return "fi-FI"
        if normalized = "pl" then return "pl-PL"
        if normalized = "ja" then return "ja-JP"
        if normalized = "zh" then return "zh-CN"
        return normalized + "-" + UCase(normalized)
    end if

    return normalized
end function

function GetDeviceClockFormatPattern() as String
    di = CreateObject("roDeviceInfo")
    if di <> invalid and di.GetClockFormat() = "12h" then return "hh:mm a"
    return "HH:mm"
end function

function GetDefaultDateFormatForLocale(locale as String) as String
    key = NormalizeLocaleCode(locale)
    if key = "en-US" then return "MMMM d, yyyy"
    if key = "ja-JP" or key = "zh-CN" then return "yyyy-MM-dd"
    return "d MMMM yyyy"
end function

function FormatAssetDateForDisplay(meta as Object, dateFormat as String, locale as String) as String
    dateStr = ""
    if meta.exifInfo <> invalid and meta.exifInfo.dateTimeOriginal <> invalid then
        dateStr = meta.exifInfo.dateTimeOriginal
    else if meta.fileCreatedAt <> invalid then
        dateStr = meta.fileCreatedAt
    end if
    if dateStr = "" then return ""

    return FormatIsoDateForDisplay(dateStr, dateFormat, locale)
end function

function FormatIsoDateForDisplay(dateStr as String, dateFormat as String, locale as String) as String
    parsed = ParseIsoDate(dateStr)
    if parsed = invalid then return ""
    return FormatLocalizedDate(parsed, dateFormat, locale)
end function

function FormatCurrentLocalDateForDisplay(dateFormat as String, locale as String) as String
    dt = CreateObject("roDateTime")
    if dt = invalid then return ""
    dt.ToLocalTime()

    year = dt.GetYear()
    month = dt.GetMonth()
    day = dt.GetDayOfMonth()
    if year <= 0 or month < 1 or month > 12 or day < 1 or day > 31 then return ""

    return FormatLocalizedDate({
        year: year
        month: month
        day: day
        weekday: CalculateWeekday(year, month, day)
    }, dateFormat, locale)
end function

function ParseIsoDate(dateStr as String) as Dynamic
    parts = dateStr.Split("T")
    if parts.Count() < 1 then return invalid

    dateOnly = parts[0]
    if Instr(1, dateOnly, " ") > 0 then
        dateOnly = dateOnly.Split(" ")[0]
    end if

    dateParts = dateOnly.Split("-")
    if dateParts.Count() < 3 then return invalid

    year = Val(dateParts[0])
    month = Val(dateParts[1])
    day = Val(dateParts[2])
    if year <= 0 or month < 1 or month > 12 or day < 1 or day > 31 then return invalid

    return {
        year: year
        month: month
        day: day
        weekday: CalculateWeekday(year, month, day)
    }
end function

function FormatLocalizedDate(parts as Object, dateFormat as String, locale as String) as String
    key = NormalizeLocaleCode(locale)
    names = GetLocaleDateNames(key)
    monthName = names.months[parts.month - 1]
    weekdayName = names.weekdays[parts.weekday]
    dayNoPad = parts.day.ToStr()
    dayPad = Right("0" + dayNoPad, 2)
    monthPad = Right("0" + parts.month.ToStr(), 2)
    yearText = parts.year.ToStr()

    if dateFormat = "MMMM d, yyyy" then
        return monthName + " " + dayNoPad + ", " + yearText
    else if dateFormat = "dd/MM/yyyy" then
        return dayPad + "/" + monthPad + "/" + yearText
    else if dateFormat = "MM/dd/yyyy" then
        return monthPad + "/" + dayPad + "/" + yearText
    else if dateFormat = "yyyy-MM-dd" then
        return yearText + "-" + monthPad + "-" + dayPad
    else if dateFormat = "dddd, d MMMM" then
        return weekdayName + ", " + dayNoPad + " " + monthName
    end if

    if key = "de-DE" then
        return dayNoPad + ". " + monthName + " " + yearText
    end if

    return dayNoPad + " " + monthName + " " + yearText
end function

function GetLocaleDateNames(locale as Dynamic) as Object
    key = NormalizeLocaleCode(locale)
    names = {
        months: ["January","February","March","April","May","June","July","August","September","October","November","December"]
        weekdays: ["Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"]
    }

    if key = "en-GB" then
        return names
    else if key = "nl-NL" then
        return {
            months: ["januari","februari","maart","april","mei","juni","juli","augustus","september","oktober","november","december"]
            weekdays: ["zondag","maandag","dinsdag","woensdag","donderdag","vrijdag","zaterdag"]
        }
    else if key = "de-DE" then
        return {
            months: ["Januar","Februar","März","April","Mai","Juni","Juli","August","September","Oktober","November","Dezember"]
            weekdays: ["Sonntag","Montag","Dienstag","Mittwoch","Donnerstag","Freitag","Samstag"]
        }
    else if key = "fr-FR" then
        return {
            months: ["janvier","février","mars","avril","mai","juin","juillet","août","septembre","octobre","novembre","décembre"]
            weekdays: ["dimanche","lundi","mardi","mercredi","jeudi","vendredi","samedi"]
        }
    else if key = "es-ES" then
        return {
            months: ["enero","febrero","marzo","abril","mayo","junio","julio","agosto","septiembre","octubre","noviembre","diciembre"]
            weekdays: ["domingo","lunes","martes","miércoles","jueves","viernes","sábado"]
        }
    else if key = "it-IT" then
        return {
            months: ["gennaio","febbraio","marzo","aprile","maggio","giugno","luglio","agosto","settembre","ottobre","novembre","dicembre"]
            weekdays: ["domenica","lunedì","martedì","mercoledì","giovedì","venerdì","sabato"]
        }
    else if key = "pt-BR" then
        return {
            months: ["janeiro","fevereiro","março","abril","maio","junho","julho","agosto","setembro","outubro","novembro","dezembro"]
            weekdays: ["domingo","segunda-feira","terça-feira","quarta-feira","quinta-feira","sexta-feira","sábado"]
        }
    else if key = "pt-PT" then
        return {
            months: ["janeiro","fevereiro","março","abril","maio","junho","julho","agosto","setembro","outubro","novembro","dezembro"]
            weekdays: ["domingo","segunda-feira","terça-feira","quarta-feira","quinta-feira","sexta-feira","sábado"]
        }
    else if key = "sv-SE" then
        return {
            months: ["januari","februari","mars","april","maj","juni","juli","augusti","september","oktober","november","december"]
            weekdays: ["söndag","måndag","tisdag","onsdag","torsdag","fredag","lördag"]
        }
    else if key = "da-DK" then
        return {
            months: ["januar","februar","marts","april","maj","juni","juli","august","september","oktober","november","december"]
            weekdays: ["søndag","mandag","tirsdag","onsdag","torsdag","fredag","lørdag"]
        }
    else if key = "nb-NO" then
        return {
            months: ["januar","februar","mars","april","mai","juni","juli","august","september","oktober","november","desember"]
            weekdays: ["søndag","mandag","tirsdag","onsdag","torsdag","fredag","lørdag"]
        }
    else if key = "fi-FI" then
        return {
            months: ["tammikuu","helmikuu","maaliskuu","huhtikuu","toukokuu","kesäkuu","heinäkuu","elokuu","syyskuu","lokakuu","marraskuu","joulukuu"]
            weekdays: ["sunnuntai","maanantai","tiistai","keskiviikko","torstai","perjantai","lauantai"]
        }
    else if key = "pl-PL" then
        return {
            months: ["stycznia","lutego","marca","kwietnia","maja","czerwca","lipca","sierpnia","września","października","listopada","grudnia"]
            weekdays: ["niedziela","poniedziałek","wtorek","środa","czwartek","piątek","sobota"]
        }
    else if key = "ja-JP" then
        return {
            months: ["1月","2月","3月","4月","5月","6月","7月","8月","9月","10月","11月","12月"]
            weekdays: ["日曜日","月曜日","火曜日","水曜日","木曜日","金曜日","土曜日"]
        }
    else if key = "zh-CN" then
        return {
            months: ["1月","2月","3月","4月","5月","6月","7月","8月","9月","10月","11月","12月"]
            weekdays: ["星期日","星期一","星期二","星期三","星期四","星期五","星期六"]
        }
    end if

    return names
end function

function CalculateWeekday(year as Integer, month as Integer, day as Integer) as Integer
    y = year
    m = month
    if m < 3 then
        m = m + 12
        y = y - 1
    end if

    k = y mod 100
    j = Int(y / 100)
    h = (day + Int((13 * (m + 1)) / 5) + k + Int(k / 4) + Int(j / 4) + (5 * j)) mod 7
    return (h + 6) mod 7
end function

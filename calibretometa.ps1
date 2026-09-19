param(
    [string]$Path = ".",
    [ValidateSet("Book", "Hardcover", "Paperback", "Comic", "Manga", "Graphic Novel", "Magazine")]
    [string]$Type = "Hardcover",
    [switch]$Force
)

function Get-OpfValue {
    param(
        [System.Xml.XmlNode]$Node
    )

    if ($null -eq $Node) {
        return ""
    }

    return $Node.InnerText.Trim()
}

function Convert-LanguageCode {
    param([string]$Language)

    $map = @{
        "eng" = "en"
        "nor" = "no"
        "nob" = "no"
        "nno" = "no"
        "jpn" = "ja"
        "deu" = "de"
        "ger" = "de"
        "fra" = "fr"
        "fre" = "fr"
        "spa" = "es"
        "ita" = "it"
        "kor" = "ko"
        "zho" = "zh"
        "chi" = "zh"
    }

    if ($map.ContainsKey($Language)) {
        return $map[$Language]
    }

    return $Language
}

function ConvertFrom-HtmlSummary {
    param([string]$Summary)

    if ([string]::IsNullOrWhiteSpace($Summary)) {
        return ""
    }

    $plainText = $Summary -replace '(?i)<br\s*/?>', "`n"
    $plainText = $plainText -replace '(?i)</(?:p|div|li|h[1-6])\s*>', "`n"
    $plainText = $plainText -replace '<[^>]+>', ''
    $plainText = [System.Net.WebUtility]::HtmlDecode($plainText)
    $plainText = $plainText -replace "[ `t]+", ' '
    $plainText = $plainText -replace "(?:\r?\n\s*){3,}", "`n`n"
    return $plainText.Trim()
}

Get-ChildItem -Path $Path -Filter "metadata.opf" -File -Recurse | ForEach-Object {

    $opfFile = $_
    $outputFile = Join-Path $opfFile.DirectoryName "meta.json"

    if ((Test-Path $outputFile) -and -not $Force) {
        Write-Host "Skipping existing: $outputFile"
        return
    }

    Write-Host "Converting: $($opfFile.FullName)"

    try {
        [xml]$xml = Get-Content -LiteralPath $opfFile.FullName -Raw -Encoding UTF8

        $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $ns.AddNamespace("opf", "http://www.idpf.org/2007/opf")
        $ns.AddNamespace("dc", "http://purl.org/dc/elements/1.1/")

        $metadata = $xml.SelectSingleNode("//opf:metadata", $ns)

        if ($null -eq $metadata) {
            Write-Warning "No OPF metadata found in $($opfFile.FullName)"
            return
        }

        $titleNode     = $metadata.SelectSingleNode("dc:title", $ns)
        $authorNode    = $metadata.SelectSingleNode("dc:creator[@opf:role='aut']", $ns)
        $publisherNode = $metadata.SelectSingleNode("dc:publisher", $ns)
        $languageNode  = $metadata.SelectSingleNode("dc:language", $ns)
        $summaryNode   = $metadata.SelectSingleNode("dc:description", $ns)

        # Fall back to first creator if there isn't one explicitly marked "aut".
        if ($null -eq $authorNode) {
            $authorNode = $metadata.SelectSingleNode("dc:creator", $ns)
        }

        # Prefer Calibre's UUID for BookID.
        $uuidNode = $metadata.SelectSingleNode(
            "dc:identifier[@opf:scheme='uuid']",
            $ns
        )

        $isbnNode = $metadata.SelectSingleNode(
            "dc:identifier[translate(@opf:scheme, 'isbn', 'ISBN')='ISBN']",
            $ns
        )
        if ($null -eq $isbnNode) {
            $isbnNode = $metadata.SelectSingleNode(
                "dc:identifier[contains(translate(@id, 'isbn', 'ISBN'), 'ISBN')]",
                $ns
            )
        }

        # Calibre series metadata, if present.
        $seriesNode = $metadata.SelectSingleNode(
            "opf:meta[@name='calibre:series']",
            $ns
        )

        $seriesIndexNode = $metadata.SelectSingleNode(
            "opf:meta[@name='calibre:series_index']",
            $ns
        )

        $title     = Get-OpfValue $titleNode
        $author    = Get-OpfValue $authorNode
        $publisher = Get-OpfValue $publisherNode
        $language  = Convert-LanguageCode (Get-OpfValue $languageNode)
        $summary   = ConvertFrom-HtmlSummary (Get-OpfValue $summaryNode)
        $bookId    = Get-OpfValue $uuidNode
        $isbn      = (Get-OpfValue $isbnNode) -replace '(?i)^urn:isbn:', ''

        if ([string]::IsNullOrWhiteSpace($bookId)) {
            $bookId = [guid]::NewGuid().ToString()
        }

        $series = ""
        if ($null -ne $seriesNode) {
            $series = $seriesNode.GetAttribute("content")
        }

        $volume = ""

        if ($null -ne $seriesIndexNode) {
            $volume = $seriesIndexNode.GetAttribute("content").Trim()
        }

        $book = [ordered]@{
            Version   = 2
            BookID    = $bookId
            Title     = $title
            Series    = $series
            Author    = $author
            Publisher = $publisher
            Language  = $language
            Type      = $Type
            Summary   = $summary
        }

        # Volume is optional display metadata, not a runtime identifier. Only add
        # it when Calibre actually supplies a series index.
        if (-not [string]::IsNullOrWhiteSpace($volume)) {
            $book["Volume"] = $volume
        }
        if (-not [string]::IsNullOrWhiteSpace($isbn)) {
            $book["ISBN"] = $isbn.Trim()
        }

        $book |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $outputFile -Encoding UTF8

        Write-Host "Created: $outputFile"
    }
    catch {
        Write-Warning "Failed: $($opfFile.FullName)"
        Write-Warning $_.Exception.Message
    }
}

Write-Host ""
Write-Host "Done."

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

        # Fall back to first creator if there isn't one explicitly marked "aut".
        if ($null -eq $authorNode) {
            $authorNode = $metadata.SelectSingleNode("dc:creator", $ns)
        }

        # Prefer Calibre's UUID for BookID.
        $uuidNode = $metadata.SelectSingleNode(
            "dc:identifier[@opf:scheme='uuid']",
            $ns
        )

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
        $bookId    = Get-OpfValue $uuidNode

        if ([string]::IsNullOrWhiteSpace($bookId)) {
            $bookId = [guid]::NewGuid().ToString()
        }

        $series = ""
        if ($null -ne $seriesNode) {
            $series = $seriesNode.GetAttribute("content")
        }

        $volume = 1

        if ($null -ne $seriesIndexNode) {
            $parsedVolume = 0.0

            if ([double]::TryParse(
                $seriesIndexNode.GetAttribute("content"),
                [System.Globalization.NumberStyles]::Any,
                [System.Globalization.CultureInfo]::InvariantCulture,
                [ref]$parsedVolume
            )) {
                $volume = $parsedVolume
            }
        }

        $book = [ordered]@{
            Version   = 1
            BookID    = $bookId
            Title     = $title
            Series    = $series
            Volume    = $volume
            Author    = $author
            Publisher = $publisher
            Language  = $language
            Type      = $Type
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
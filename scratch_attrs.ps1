$dir = "C:\Users\Ololade\Desktop\portfolio\logisim-evolution-4.1.0\src\main\java\com\cburch\logisim\std\wiring"
$files = @("Clock.java", "Pin.java", "PinAttributes.java", "Power.java", "ProbeAttributes.java")

foreach ($f in $files) {
    $full = Join-Path $dir $f
    Write-Output "=== $f ==="
    Get-Content $full | Where-Object { 
        $_ -match 'ATTRIBUTES\s*=' -or $_ -match 'ATTR_' -or $_ -match 'new Attribute\[\]' -or $_ -match 'Attributes\.for' -or $_ -match 'StdAttr\.'
    } | ForEach-Object {
        $_.Trim()
    }
}

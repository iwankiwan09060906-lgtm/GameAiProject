# AssetSource 원본에서 Art/ 스프라이트와 사격음 2개를 다시 만든다.
$src = $PSScriptRoot
$project = Split-Path $src -Parent
$code = Get-Content -LiteralPath "$src\build_assets.cs" -Raw -Encoding UTF8
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[AssetBuild]::Run($src, $project)

# AssetSource/newasset 시트에서 Art/deco/deco_*.png 와 Art/tiles/floor_*.png 를 다시 만든다.
$src = $PSScriptRoot
$project = Split-Path $src -Parent
$code = Get-Content -LiteralPath "$src\build_deco.cs" -Raw -Encoding UTF8
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[DecoBuild]::Run($src, $project)

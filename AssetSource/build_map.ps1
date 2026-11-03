# 맵 1(map_01.json)과 미리보기(AssetSource/map_preview.png)를 다시 만든다. 경로 검사 결과를 출력한다.
$src = $PSScriptRoot
$project = Split-Path $src -Parent
$code = Get-Content -LiteralPath "$src\build_map.cs" -Raw -Encoding UTF8
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[MapBuild]::Run($src, $project)

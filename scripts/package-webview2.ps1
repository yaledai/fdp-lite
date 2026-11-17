param([Parameter(Mandatory=$true)][string]$BuildPath, [Parameter(Mandatory=$true)][string]$ZipPath, [long]$MaxBytes=5000000)
$ErrorActionPreference='Stop'
$build=(Resolve-Path -LiteralPath $BuildPath).Path
if(!(Test-Path -LiteralPath (Join-Path $build 'FDP-Lite.exe'))){throw 'Build directory does not contain FDP-Lite.exe.'}
$zip=[IO.Path]::GetFullPath($ZipPath)
if([IO.Path]::GetExtension($zip) -ne '.zip'){throw 'Output must have a .zip extension.'}
if(Test-Path -LiteralPath $zip){throw 'Output already exists.'}
$parent=Split-Path -Parent $zip
if(!(Test-Path -LiteralPath $parent)){throw 'Output directory does not exist.'}
$tmp=Join-Path $parent ('.fdp-package-'+[Guid]::NewGuid().ToString('N')+'.zip')
try {
  Compress-Archive -Path (Join-Path $build '*') -DestinationPath $tmp -CompressionLevel Optimal
  $size=(Get-Item -LiteralPath $tmp).Length
  if($size -gt $MaxBytes){throw "Package is $size bytes; limit is $MaxBytes bytes."}
  Move-Item -LiteralPath $tmp -Destination $zip
  Write-Host "Created $zip ($size bytes; limit $MaxBytes bytes)"
} finally { if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp} }

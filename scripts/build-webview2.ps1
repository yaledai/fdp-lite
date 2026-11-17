param([Parameter(Mandatory=$true)][string]$SdkPath, [string]$OutputPath)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$sdk=(Resolve-Path -LiteralPath $SdkPath).Path
if ($OutputPath) { $out=[IO.Path]::GetFullPath($OutputPath) } else { $out=Join-Path $project 'dist\FDP-WebView2-prototype' }
if(Test-Path $out){throw 'Output already exists; use a clean output directory.'}
$lib=Join-Path $sdk 'lib\net462'
$loader=Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll'
foreach($file in @((Join-Path $lib 'Microsoft.Web.WebView2.Core.dll'),(Join-Path $lib 'Microsoft.Web.WebView2.WinForms.dll'),$loader)){if(!(Test-Path $file)){throw "Missing SDK file: $file"}}
New-Item -ItemType Directory -Force -Path $out,(Join-Path $out 'site\vendor\pdfjs') | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 "/out:$out\FDP-Lite.exe" "/win32icon:$project\assets\fdp-app.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$lib\Microsoft.Web.WebView2.Core.dll" "/reference:$lib\Microsoft.Web.WebView2.WinForms.dll" "$project\webview2\FdpLite.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item -Path @("$lib\Microsoft.Web.WebView2.Core.dll","$lib\Microsoft.Web.WebView2.WinForms.dll",$loader,"$project\assets\fdp-app.ico","$project\assets\fdp-document.ico") -Destination $out
Copy-Item "$project\webview2\index.html","$project\webview2\lite.mjs","$project\webview2\lite.css","$project\webview2\lite-annotations.mjs","$project\webview2\text-annotations-system.mjs","$project\styles.css","$project\text-annotations.mjs","$project\highlight-annotations.mjs","$project\note-annotations.mjs" -Destination "$out\site"
Copy-Item "$project\vendor\pdfjs\pdf.mjs","$project\vendor\pdfjs\pdf.worker.mjs" -Destination "$out\site\vendor\pdfjs"
Copy-Item "$project\vendor\cmaps","$project\vendor\standard_fonts","$project\vendor\wasm" -Destination "$out\site\vendor" -Recurse
Copy-Item "$project\vendor\pdfjs-LICENSE","$project\vendor\pdf-lib.min.js","$project\vendor\pdf-lib-LICENSE.md","$project\vendor\pdf-fontkit.min.js","$project\vendor\pdf-fontkit-LICENSE.txt" -Destination "$out\site\vendor"
New-Item -ItemType Directory -Path "$out\site\assets" | Out-Null
Copy-Item "$project\assets\fdp-pig-ui.png" -Destination "$out\site\assets\fdp-pig-line.png"
Copy-Item "$sdk\LICENSE.txt","$sdk\NOTICE.txt" -Destination $out
Write-Host "Built $out"


Copy-Item "$project\webview2\README.md" -Destination $out

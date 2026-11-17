# Folio Dot Pig · FDP Lite

FDP Lite 是一款面向 Windows 的轻量 PDF 阅读与编辑工具，使用 WebView2，本地处理文件。目标是发布压缩包不超过 5,000,000 字节；目前处于试用阶段。

支持阅读、搜索、打印、多标签、页面整理、批注、文字框和签名图片。详细使用方式、系统要求及已知限制见 [使用说明](webview2/README.md)。

## 下载和使用

发布后可在本仓库的 Releases 页面下载便携版压缩包，解压到固定目录，运行 `FDP-Lite.exe`。需要 Windows x64、Microsoft Edge WebView2 Runtime 和 .NET Framework 4.7.2 或更高版本。当前版本处于试用阶段。

## 构建

下载并解压 [Microsoft.Web.WebView2 1.0.3537.50 NuGet 包](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3537.50)，保留包内目录结构（包括 `lib/net462` 和 `runtimes/win-x64/native`），在 Windows PowerShell 中运行：

```powershell
./scripts/build-webview2.ps1 -SdkPath <SDK目录> -OutputPath <新输出目录>
```

输出目录必须不存在。构建完成后，可运行 `./scripts/package-webview2.ps1 -BuildPath <构建目录> -ZipPath <输出zip>`，脚本会检查压缩包不超过 5,000,000 字节。脚本使用 Windows .NET Framework 的 C# 编译器；源码中的 WebView2 程序、界面和 PDF 依赖均在本仓库中。已在现有 Windows 环境使用该官方 SDK 成功构建并打包；尚未验证干净系统构建或逐字节可复现。

## 参与

使用 Issue 反馈问题、提出建议；代码更改通过 Pull Request 审核。参见 [贡献指南](CONTRIBUTING.md)。提交反馈前请移除文档中的敏感信息。

## 许可

项目代码采用 [MIT 许可证](LICENSE)。第三方组件及资源适用各自许可证，详见 [第三方说明](THIRD_PARTY.md)；公开分发前仍需核对依赖和素材权利。

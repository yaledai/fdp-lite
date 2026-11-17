# 第三方组件与许可

本表依据当前源码目录中的文件整理；公开发布前还需核对完整依赖链、来源和分发条件。

- PDF.js 6.2.108：Apache License 2.0，见 `vendor/pdfjs-LICENSE`。
- pdf-lib 1.17.1：MIT，见 `vendor/pdf-lib-LICENSE.md`。项目中的 `vendor/pdf-lib.min.js` 与 npm 发布的 1.17.1 文件在统一换行符后逐字节一致。
- pdf-fontkit 1.8.9：项目中的 `vendor/pdf-fontkit.min.js` 与 npm 发布的 1.8.9 UMD 文件在统一换行符后逐字节一致。该包声明 MIT，见 `vendor/pdf-fontkit-LICENSE.txt` 和 `vendor/pdf-fontkit-package.json`；上游发布包未附独立 LICENSE，现有版权声明及其打包依赖的许可仍需进一步核对。
- PDF.js 图像解码资源及相关许可证：见 `vendor/wasm/LICENSE_*`。
- PDF.js CMap 资源：见 `vendor/cmaps/LICENSE`。
- PDF.js 标准字体：分别见 `vendor/standard_fonts/LICENSE_FOXIT` 和 `LICENSE_LIBERATION`。
- Microsoft WebView2 SDK：构建时由开发者提供，构建脚本将 SDK 的 LICENSE 和 NOTICE 复制进分发目录；当前 1.0.8 试用包中 Core DLL 文件版本为 1.0.3537.50；SDK 包来源、各组件版本及分发条款仍需在发布前确认。
- 微软雅黑、Calibri 从用户系统读取，不随程序包分发；文字框保存时按字体嵌入权限检查并嵌入所需字形。

项目所有者已确认小猪图标可以公开分发；其他素材的权利归属仍需在公开前确认。本文件不是法律意见，也不是完整许可审计结果。

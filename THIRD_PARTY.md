# 第三方组件与许可

本表依据当前源码目录中的文件整理；公开发布前还需核对完整依赖链、来源和分发条件。

- PDF.js 5.6.205：Apache License 2.0，见 `vendor/pdfjs-LICENSE`。
- pdf-lib（原项目开发依赖标注 1.17.1）：MIT，见 `vendor/pdf-lib-LICENSE.md`；打包文件版本仍需进一步核对。
- pdf-fontkit 1.8.9：MIT，见 `vendor/pdf-fontkit-LICENSE.txt` 和 `vendor/pdf-fontkit-package.json`；其打包依赖及声明需进一步核对。
- PDF.js 标准字体：分别见 `vendor/standard_fonts/LICENSE_FOXIT` 和 `LICENSE_LIBERATION`。
- Microsoft WebView2 SDK：构建时由开发者提供，构建脚本将 SDK 的 LICENSE 和 NOTICE 复制进分发目录；SDK 版本和分发条款需在发布前确认。
- 微软雅黑、Calibri 从用户系统读取，不随程序包分发；文字框保存时按字体嵌入权限检查并嵌入所需字形。

应用图标和其他资源的权利归属也需要在公开前确认。本文件不是法律意见，也不是完整许可审计结果。

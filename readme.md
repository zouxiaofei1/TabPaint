<div align="center">
  <table>
    <tr>
      <td align="center" style="border: none;">
        <img src="./TabPaint/Resources/TabPaint.ico" width="100" height="100" alt="Tab Paint Logo">
      </td>
      <td align="left" style="border: none; vertical-align: middle;">
        <h1 style="margin: 0; font-size: 48px;">TabPaint</h1>
        <p style="margin: 0; font-size: 18px;"><b>更好用的图片编辑器</b></p>
      </td>
    </tr>
  </table>

  <p>
    多标签页管理 · 看图画图双模式 · AI 智能辅助 · 无缝拖拽操作
  </p>

  <!-- Badges -->
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue" alt="Platform">
  <img src="https://img.shields.io/badge/Language-C%23%20%7C%20WPF-purple" alt="Language">
  <img src="https://img.shields.io/badge/Status-Beta%20v0.9.7-orange" alt="Status">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</div>

<div align="center">
  <strong>简体中文</strong> | <a href="./README.EN.md">English</a>
</div>

---

![App Screenshot](./TabPaint/Resources/screenshot1.png)

## Key Features

*   **`Tab`** 一键切换看图画图模式
*   多标签页
*   免保存
*   抠图、超分、擦除、OCR等离线AI模型
*   拖拽图片至桌面/word等编辑器
*   ICO编辑与保存
*   十余种小工具
*   多语言/主题色/黑暗模式/Mica

---

## 快捷键

| 快捷键 | 功能描述 |
| :--- | :--- |
| **`Tab`** | **切换 看图 / 画图 模式** |
| `Ctrl` + `N` | 新建画布 |
| `Ctrl` + `W` | 关闭当前标签页 |
| `Ctrl` + `Alt` + `P` | 剪切板截图监听 |
| `Ctrl` + `L` / `R` | 向左 / 向右 旋转图片 |
| `Space` + 拖动 | 移动画布 |
| `Del` | 删除文件至回收站 (可撤销，需在设置开启) |
| `Ctrl` + `Wheel` | 缩放画布 |

---

## 安装

*   **操作系统**: Windows 10 / 11 
*   **运行环境**: .NET 8.0 Desktop Runtime 或更高版本

1.  [Github Releases](https://github.com/zouxiaofei1/TabPaint/releases)
2.  [网盘链接](https://wwauw.lanzouu.com/b0j16fyij) 密码adb9
3.  [官网](https://tabpaint.cc/)

---

## FAQ

**Q: 点击 AI 抠图没有反应或报错？**
A: AI 抠图功能依赖微软的 C++ 运行库。安装下方组件后重试：
[Visual C++ Redistributable for Visual Studio 2015-2022 (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)

**Q: 需要联网吗？**
A: 不需要。

**Q: 支持哪些图片格式？**
A: 完全支持: JPG, PNG, BMP, WEBP, ICO, HEIC, TIF
部分支持: GIF, SVG
暂不支持:PSD

---

## License & Contact

本项目采用 **MIT License** 开源。

使用了以下依赖：`MicaWPF`, `SkiaSharp`, `XamlAnimatedGif`, `OnnxRuntime`, `WriteableBitmapEx`

*   **反馈与建议**: 请提交 [Issues](https://github.com/zouxiaofei1/TabPaint/issues) 


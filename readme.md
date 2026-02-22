<div align="center">
  <table>
    <tr>
      <td align="center" style="border: none;">
        <img src="./TabPaint/Resources/TabPaint.ico" width="100" height="100" alt="Tab Paint Logo">
      </td>
      <td align="left" style="border: none; vertical-align: middle;">
        <h1 style="margin: 0; font-size: 48px;">Tab Paint</h1>
        <p style="margin: 0; font-size: 18px;"><b>Windows 上的“图片版 Notepad++”</b></p>
      </td>
    </tr>
  </table>

  <p>
    多标签页管理 · 看图画图双模式 · AI 智能辅助 · 无缝拖拽操作
  </p>

  <!-- Badges -->
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue" alt="Platform">
  <img src="https://img.shields.io/badge/Language-C%23%20%7C%20WPF-purple" alt="Language">
  <img src="https://img.shields.io/badge/Status-Beta%20v0.9.5-orange" alt="Status">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</div>

<div align="center">
  <strong>简体中文</strong> | <a href="./README.EN.md">English</a>
</div>

---

![App Screenshot](./TabPaint/Resources/screenshot1.png)
![App Screenshot](./TabPaint/Resources/gif1.gif)

## Features

### · 🖼️ 看图画图双模式
*   **看图模式**：沉浸式图片浏览界面支持滚轮缩放、GIF 播放。
*   **画图模式**：按下 **`Tab`** ，工具栏即刻弹出，无缝进入编辑状态。


### · 📑 像管理代码一样管理图片
* ** 多标签页共存 **: 同时打开十几张截图，通过图片栏快速切换、对比、批量处理。
* ** 无需保存 **: 编辑后的状态重启后仍然存在

### · 🤖 现代工具箱
*   **AI 一键抠图**：集成 ONNX 模型，本地离线抠除背景。
*   **OCR 文字识别**：截图提取文字，不再需要额外工具。
*   **辅助工具**：屏幕取色器、智能裁切空白、一键加边框、4x超分辨率、智能擦除

### · 🖱️ 无缝拖拽
*   **剪贴板监听**：截图后自动弹出提示，Ctrl+V 粘贴为新标签页。
*   **支持多种拖拽功能**：
    *   拖拽图片文件或网页图片 -> 插入画板
    *   拖拽缩略图 -> 生成文件到桌面/ 插入 Word / 发送给 QQ 微信
    *   拖拽选区 -> 直接插入 PPT 或文档

### · 多语言/样式支持
*   🌎 提供简体中文 / English 
*   🎨 支持黑暗模式 / 主题色选择

---

## ⌨️ 常用快捷键

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

## 📥 下载与安装

### 系统要求
*   **操作系统**: Windows 10 或 Windows 11
*   **运行环境**: .NET 8.0 Desktop Runtime 或更高版本

### 获取方式
1.  [Github Releases](https://github.com/zouxiaofei1/TabPaint/releases)
2.  [网盘链接](https://wwauw.lanzouu.com/b0j16fyij) 密码adb9
3.  [官网](https://tabpaint.cc/)

---

## ❓ 常见问题 (FAQ)

**Q: 点击 AI 抠图没有反应或报错？**
A: 缺少必要的系统运行库。 AI 抠图功能依赖微软的 C++ 运行库。请安装下方组件后重试：
[Visual C++ Redistributable for Visual Studio 2015-2022 (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)

**Q: 需要联网吗？**
A: 不需要。抠图、超分等功能基于本地 ONNX 运行时，联网仅用于下载运行库

**Q: 支持哪些图片格式？**
A: 支持 JPG, PNG, BMP, WEBP, ICO, GIF (查看与播放), HEIC, TIF 等主流格式。暂不支持PSD。

---

## 📄 版权与联系 (License & Contact)

本项目采用 **MIT License** 开源。
使用了以下依赖：`MicaWPF`, `SkiaSharp`, `XamlAnimatedGif`, `OnnxRuntime`, `WriteableBitmapEx`

*   **反馈与建议**: 请提交 [Issues](https://github.com/zouxiaofei1/TabPaint/issues) 


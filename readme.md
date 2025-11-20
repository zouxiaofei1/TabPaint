# 🎨 SodiumPaint (Alpha)

![Platform](https://img.shields.io/badge/Platform-Windows-blue) ![Language](https://img.shields.io/badge/Language-C%23%20%7C%20WPF-purple) ![Status](https://img.shields.io/badge/Status-Alpha%20v0.6-orange) ![License](https://img.shields.io/badge/license-MIT-green)

> **English** | [中文](#chinese)

---

## 🇬🇧 English Introduction

**SodiumPaint** is a lightweight image editor and viewer tailored for Windows, built with C# and WPF. 

It is designed for the **"10-second edit" workflow**: when you just need to screenshot, circle a highlight, add a note, and paste it into a document. It combines the familiarity of the classic MS Paint with modern efficiency features like multi-tabs and seamless drag-and-drop.

### 🚧 Alpha Warning (Please Read)
**Current Version: v0.6 (Under Active Development)**
This software is currently in the **Alpha Testing** stage. It is **NOT** stable yet.
*   ⚠️ **Data Safety**: There are known bugs with the Undo/Redo stack that may cause image data loss during cropping.
*   ⚠️ **Stability**: You may encounter white screens or crashes during specific operations.
*   **Recommendation**: Please do not use it for critical work at this moment. Feel free to test and report bugs!

### ✨ Key Features (Implemented)
*   **Multi-Tab Interface**: Open and edit multiple images simultaneously (Say goodbye to opening 10 MSPaint windows).
*   **Classic Experience**: UI mimics the classic MS Paint for zero learning curve.
*   **Seamless Workflow**: 
    *   Select an area -> **Drag it directly** into Word, PowerPoint, or other editors.
    *   Drag the selection to the Desktop to instantly create an image file.
*   **View & Edit**: Acts as both an image viewer and a quick editor.

### 🗺️ Roadmap & Status

| Feature | Status | Note |
| :--- | :---: | :--- |
| **Core Painting Tools** | ✅ | Pencil, Brush, Shapes, Eraser |
| **Multi-Tab Support** | ✅ | Switch between images easily |
| **Smart Drag & Drop** | ✅ | Drag selection to Word/Desktop |
| **Notepad++ Style Session** | 🚧 | v0.8 Goal: Remember open files after restart |
| **View/Edit Mode Split** | 📅 | v0.8 Goal: Separate viewer and editor UI |
| **Dark Mode** | 📅 | Planned for v0.9 |
| **High DPI / 4K Support** | 🐛 | Buggy in v0.6, fixing in v0.7 |

### 🐛 Known Critical Issues (v0.6)
*   [High Priority] **Data Loss**: Undo/Redo after cropping a selection may cause the area to disappear.
*   [High Priority] **White Screen**: Dragging a selection preview may occasionally turn the screen white.
*   [UI] **High DPI**: Interface may look blurry or misaligned on non-100% scale screens (e.g., 125%, 150%).
*   [UI] **Performance**: Resizing the window or canvas might be laggy with large images.

---
<a name="chinese"></a>

## 🇨🇳 中文介绍

**SodiumPaint** 是一款基于 C# WPF 开发的轻量级 Windows 图片编辑与查看工具。

它的开发初衷是为了解决 **“10秒内快速修图”** 的痛点：当你只需要截图、圈出重点、写个备注，然后发给同事或插入文档时，PS 太重，原生画图功能又太弱（且不支持多开）。SodiumPaint 完美结合了经典画图的低上手门槛和现代工具的高效特性。

### 🚧 Alpha 版本预警（必读）
**当前版本：v0.6 (开发测试版)**
本项目目前处于 **Alpha 内测阶段**，功能尚未完全稳定。
*   ⚠️ **数据风险**：目前的撤销/重做（Undo/Redo）功能存在 Bug，在裁剪操作后可能会导致图像区域丢失。
*   ⚠️ **稳定性**：在特定操作下可能会出现白屏或闪退。
*   **建议**：目前仅供尝鲜和测试，请勿用于处理重要或唯一的图片文件。

### ✨ 核心功能（已实现）
*   **多标签页支持 (Multi-Tabs)**：像浏览器一样同时打开多张图片，无需再开启无数个画图窗口。
*   **零上手成本**：复刻经典 MS Paint 界面布局，打开就会用。
*   **无缝工作流**：
    *   框选图片区域 -> **直接拖入** Word、PPT 或其他编辑器中。
    *   框选区域拖到桌面 -> 自动生成图片文件。
*   **看图/修图合一**：既是轻量的看图软件，也是便捷的编辑器。

### 🗺️ 开发计划与进度

| 功能特性 | 状态 | 说明 |
| :--- | :---: | :--- |
| **基础绘图工具** | ✅ | 铅笔、画笔、形状、橡皮擦等 |
| **多标签页系统** | ✅ | 顶部 Tab 切换 |
| **智能拖拽交互** | ✅ | 框选区域直接拖出使用 |
| **Notepad++式会话保存** | 🚧 | v0.8 目标：关闭软件不丢文件，下次打开自动恢复 |
| **看图/画图模式分离** | 📅 | v0.8 目标：根据用途切换界面布局 |
| **黑暗模式 (Dark Mode)** | 📅 | 计划于 v0.9 加入 |
| **高分屏适配 (High DPI)** | 🐛 | v0.6 存在错位问题，将在 v0.7 修复 |

### 🐛 已知严重问题 (v0.6)
*   **[严重]** 裁剪选区（Crop Selection）后进行撤销/重做，可能导致相关区域图像消失。
*   **[严重]** 拖动选区预览时，偶尔会导致界面白屏。
*   **[UI]** 非 96px (100%缩放) 的屏幕下，选区和图标可能会出现错位。
*   **[性能]** 调整画布大小时性能有待优化。

---

### 📥 Download / 下载
Please check the [Releases](../../releases) page for the latest build.
请前往 [Releases](../../releases) 页面下载最新构建版本。

### 🛠️ Build from Source / 源码构建
Requirements:
*   Visual Studio 2022 or later
*   .NET 6.0 / .NET 8.0 SDK (WPF Workload)

```bash
git clone https://github.com/YourUsername/SodiumPaint.git
cd SodiumPaint
dotnet build

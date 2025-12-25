# 🎨 TabPaint (Alpha)

![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue) ![Language](https://img.shields.io/badge/Language-C%23%20%7C%20WPF-purple) ![Status](https://img.shields.io/badge/Status-Alpha%20v0.6.4-orange) ![License](https://img.shields.io/badge/license-MIT-green)

![App Screenshot](./TabPaint/Resources/screenshot.png)

> **English** | [中文](#chinese)

---

## 🇬🇧 English Introduction

**TabPaint** is a lightweight image editor and viewer tailored for Windows, built with C# and WPF / .NET.

It is designed for the **"10-second edit" workflow**: when you just need to screenshot, circle a highlight, add a note, and paste it into a document. It combines the familiarity of the classic MS Paint with modern efficiency features like **browser-style tabs** and seamless drag-and-drop integration.

### 🚧 Alpha Warning (v0.6.4)
**Current Status: Active Development**
This software is currently in the **Alpha Testing** stage. 
*   ⚠️ **Data Safety**: While stability has improved in v0.6.4, complex Undo/Redo operations (especially after cropping) may still carry a risk of data loss.
*   ⚠️ **Performance**: Working with very large images (>4K) or high zoom levels may result in UI lag.
*   **Recommendation**: Great for quick edits and screenshots. Please save often!

### ✨ Key Features
*   **Multi-Tab Interface (ImageBar)**: 
    *   Open and edit multiple images simultaneously. 
    *   Support for **Middle-click to close** tabs.
    *   Auto-caches "Untitled" files to prevent loss on accidental close.
*   **Classic & Modern**: 
    *   UI mimics classic MS Paint for zero learning curve.
    *   Enhanced with Win11 Mica effects and fluid animations.
*   **Seamless Workflow**: 
    *   Select an area -> **Drag it directly** into Word, PowerPoint, or Discord.
    *   Drag the selection to the Desktop to instantly create a file.
*   **Canvas Control**: 
    *   8-point handle system for resizing the canvas.
    *   Smart auto-expand when pasting large images.

### 🗺️ Roadmap & Status

| Feature | Status | Note |
| :--- | :---: | :--- |
| **Multi-Tab System** | ✅ | Core stable. ImageBar with drag/scroll support. |
| **Basic Tools** | ✅ | Pencil, Brush, Eraser, Color Picker. |
| **Smart Drag & Drop** | ✅ | Drag selection to Clipboard/File. |
| **Canvas Resizing** | ✅ | 8-point drag handles (v0.6.1). |
| **Rulers & Guides** | 🔨 | **Target for v0.7**: Precision layout tools. |
| **Vector Shapes** | 📅 | **Target for v0.7**: Line, Circle, Rect, Arrows. |
| **Transparency** | 📅 | **Target for v0.7**: Transparent background support. |
| **High DPI Support** | 🐛 | Improving. Partial fixes in v0.6.4, aiming for perfection in v0.7. |
| **Session Manager** | 🚧 | Remembering open files across restarts (Partial support). |

### 🐛 Known Issues
*   **Undo/Redo Stack**: Can be unpredictable after "Crop Selection" operations.
*   **High DPI**: Selection borders and text input may look misaligned on 125%/150% scaling.
*   **ImageBar**: Occasional rendering glitches when loading 10+ large images rapidly.

---
<a name="chinese"></a>

## 🇨🇳 中文介绍

**TabPaint** 是一款基于 C# WPF 开发的现代化 Windows 图片编辑与查看工具。

它的开发初衷是为了解决 **“10秒内快速修图”** 的痛点：当你只需要截图、圈出重点、写个备注，然后发给同事或插入文档时，PS 太重，原生画图不支持多开。TabPaint 完美结合了经典画图的低上手门槛和类似浏览器的多标签页体验。

### 🚧 Alpha 版本预警 (v0.6.4)
**当前状态：活跃开发中**
本项目目前处于 **Alpha 内测阶段**。
*   ⚠️ **数据风险**：尽管 v0.6.4 修复了大量 Bug，但在复杂的“裁剪+撤销”操作后仍有极小概率丢失图像数据。
*   ⚠️ **性能**：在处理 4K 以上大图或极高倍数缩放时，界面可能不够流畅。
*   **建议**：非常适合日常截图标注和轻量修图，建议养成随手保存的习惯。

### ✨ 核心功能
*   **多标签页系统 (ImageBar)**：
    *   像浏览器一样管理图片，支持 **鼠标中键关闭** 标签。
    *   智能缓存“未命名”图片，意外关闭也不怕。
*   **新旧融合**：
    *   保留 MS Paint 经典布局，打开即用。
    *   融入 Win11 Mica 云母材质与圆角 UI 设计。
*   **无缝工作流**：
    *   框选区域 -> **直接拖入** Word、微信或 PPT。
    *   框选区域拖到桌面 -> 自动生成图片文件。
*   **画布控制**：
    *   支持通过边缘 8 个控制点调整画布大小。
    *   粘贴大图时画布自动扩容。

### 🗺️ 开发计划与进度

| 功能特性 | 状态 | 说明 |
| :--- | :---: | :--- |
| **多标签页支持** | ✅ | 核心功能已稳定，支持拖拽、滚动。 |
| **基础绘图工具** | ✅ | 铅笔、画笔、橡皮擦、取色器。 |
| **智能拖拽交互** | ✅ | 选区可直接拖出为文件或剪贴板对象。 |
| **画布尺寸调整** | ✅ | v0.6.1 已实装 8 向拖拽手柄。 |
| **标尺 (Rulers)** | 🔨 | **v0.7 重点**：增加精确绘图辅助。 |
| **矢量形状工具** | 📅 | **v0.7 重点**：直线、圆、矩形、箭头工具。 |
| **透明背景支持** | 📅 | **v0.7 重点**：支持 Alpha 通道绘图。 |
| **高分屏适配** | 🐛 | 持续优化中，v0.7 将彻底解决坐标错位问题。 |
| **会话管理** | 🚧 | 重启后恢复上次打开的图片 (部分实装)。 |

### 📜 最近更新 (Changelog)

<details>
<summary>点击展开 v0.6.x 更新日志</summary>

**v0.6.4**
*   修复：中键关闭标签页体验优化。
*   修复：保存新图片时默认路径改为当前文件夹。
*   修复：ImageBar 选中图片无法居中及加载不全的 Bug。
*   新增：未命名图片自动编号逻辑 (Untitled-1, Untitled-2)。
*   优化：大量未保存图片的缓存与恢复逻辑。

**v0.6.1 - v0.6.3**
*   新增：画布边缘 8 向调整手柄。
*   新增：左侧工具栏清空/保存/放弃所有编辑按钮。
*   修复：Selection 选区拖拽生成文件损坏的问题。
*   优化：文本控件 (TextBox) 的边框交互。
</details>

---

### 📥 Download / 下载
Please check the [Releases](../../releases) page for the latest build.
请前往 [Releases](../../releases) 页面下载最新构建版本。

### 🛠️ Build from Source / 源码构建
Environment:
*   Visual Studio 2022
*   .NET 6.0 / .NET 8.0 SDK (WPF Workload)

```bash
git clone https://github.com/YourUsername/TabPaint.git
cd TabPaint
dotnet build

<div align="center">
  <table>
    <tr>
      <td align="center" style="border: none;">
        <img src="./TabPaint/Resources/TabPaint.ico" width="100" height="100" alt="Tab Paint Logo">
      </td>
      <td align="left" style="border: none; vertical-align: middle;">
        <h1 style="margin: 0; font-size: 48px;">TabPaint</h1>
        <p style="margin: 0; font-size: 18px;"><b>Mejor editor de imágenes</b></p>
      </td>
    </tr>
  </table>

  <p>
    Gestión de pestañas múltiples · Modo dual ver imágenes y dibujar · Asistencia inteligente de IA · Operación de arrastre sin interrupciones
  </p>

  <!-- Badges -->
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue" alt="Platform">
  <img src="https://img.shields.io/badge/Language-C%23%20%7C%20WPF-purple" alt="Language">
  <img src="https://img.shields.io/badge/Status-Beta%20v0.9.7-orange" alt="Status">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</div>

<div align="center">
  <strong>Español | <a href="./README.EN.md">English</a></strong>
</div>

---

![App Screenshot](./TabPaint/Resources/screenshot1.png)

## Características Principales

*   **`Tab`** Cambiar modo de una sola vez para ver/dibujar
*   Pestañas múltiples
*   Sin guardar
*   Modelos de IA sin conexión para recortar, superres, borrar, OCR, etc.
*   Arrastrar imágenes al escritorio/word u otros editores
*   Editar y guardar en formato ICO
*   Más de diez herramientas
*   Múltiples idiomas, colores de tema, modo oscuro, Mica

---

## Teclas de Atajo

| Teclas de Atajo | Descripción |
| :--- | :--- |
| **`Tab`** | **Cambiar modo Ver/Dibujar** |
| `Ctrl` + `N` | Nuevo lienzo |
| `Ctrl` + `W` | Cerrar pestaña actual |
| `Ctrl` + `Alt` + `P` | Escuchar captura de pantalla del portapapeles |
| `Ctrl` + `L` / `R` | Rotar imagen a izquierda/derecha |
| `Space` + arrastrar | Mover lienzo |
| `Del` | Eliminar a la papelera (deshacer posible, requiere habilitar en ajustes) |
| `Ctrl` + `Rueda del ratón` | Ampliar/reducir lienzo |

---

## Instalación

*   **Sistema operativo**: Windows 10 / 11 
*   **Entorno de ejecución**: .NET 8.0 Desktop Runtime o superior

1.  [Github Releases](https://github.com/zouxiaofei1/TabPaint/releases)
2.  [Enlace de nube](https://wwauw.lanzouu.com/b0j16fyij) Contraseña adb9
3.  [Sitio web oficial](https://tabpaint.cc/)

---

## Preguntas Frecuentes

**P: ¿Por qué el recorte de IA no funciona o da error?**
R: La funcionalidad de recorte de IA depende de las bibliotecas C++ de Microsoft. Instale el componente siguiente y vuelva a intentarlo:
[Visual C++ Redistributable for Visual Studio 2015-2022 (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)

**P: ¿Necesita conexión a internet?**
R: No.

**P: ¿Qué formatos de imagen admiten?**
R: Soporte completo: JPG, PNG, BMP, WEBP, ICO, HEIC, TIF
Soporte parcial: GIF, SVG
No admitidos: PSD

---

## Licencia y Contacto

Este proyecto utiliza la licencia **MIT**.

Usó las siguientes dependencias: `MicaWPF`, `SkiaSharp`, `XamlAnimatedGif`, `OnnxRuntime`, `WriteableBitmapEx`

*   **Comentarios y sugerencias**: envíe problemas a [Issues](https://github.com/zouxiaofei1/TabPaint/issues)

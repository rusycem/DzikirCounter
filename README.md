# Dzikir Counter Utility (WinUI 3)

> A modern, productivity-focused Dzikir/Tasbih counter built with **WinUI 3** and **.NET 8**.

Unlike standard counters, this utility features **Global Input Hooks**, allowing you to recite and count while working, reading documents, or browsing the web, even when the application is minimized or in the background.

<div align="center">
   
![WinUI 3](https://img.shields.io/badge/WinUI-3.0-blue) ![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-lightgrey) ![License](https://img.shields.io/badge/License-MIT-green)

</div>

---
## ✨ Key Features

### 🚀 Productivity & Background Counting

* **Global Mouse Hooks:** Increment the counter using your mouse's **Side Buttons** (Forward/Back) or **Left Click** without focusing the app.
* **Custom Bindings:** Record and bind *any* keyboard key or specific mouse button to the counter.
* **Workflow Integration:** Perfect for counting while reading PDFs, scrolling through articles, or gaming.

### 📿 Content & Goals (Certification Compliant)

* **Curated Presets:** Select from standard Islamic Tasbih counts:
    * *SubhanAllah* (33)
    * *Istighfar* (100)
    * *Tahlil* (1000)
* **Custom Goals:** Input any target number for specific wirid/adhkar.
* **Visual Progress:** A dynamic progress bar tracks your journey to the target.
* **Completion Logic:**
    * **Visual Cue:** Counter turns **Green**.
    * **Audio Cue:** Plays a "Success" chime.
    * **Auto-Loop:** Automatically resets to 1 on the next click for continuous sets.

### ⏱️ Integrated Session Timer

* **Stopwatch:** Tracks the duration of your Dzikir session.
* **Smart Pause:** The timer automatically pauses when you reach your target goal, giving you accurate session metrics.

### 🎨 Modern UI & Customization

* **Mica Material:** Uses the latest Windows design language with translucent backgrounds.
* **Theme Aware:** Fully supports System Light/Dark modes, plus a manual override toggle.
* **Audio Feedback:** Satisfying "Pop" sound for counting and "Chime" for success (Toggleable).

<div align="center">
  <table>
    <tr>
      <td align="center">
        <img src="https://github.com/user-attachments/assets/4c9cd694-6dd4-4c0b-acf0-2f91696e050b" width="280" alt="UI for Counter" />
        <br />
        <b>Figure 1. UI for Counter</b>
      </td>
      <td align="center">
        <img src="https://github.com/user-attachments/assets/f95bfac6-7e39-4523-93ba-c6d118ba5a62" width="280" alt="UI for Timer" />
        <br />
        <b>Figure 2. UI for Timer</b>
      </td>
      <td align="center">
        <img src="https://github.com/user-attachments/assets/c9936cf2-e05b-4073-aba4-f7f1b8de0843" width="280" alt="UI for Settings" />
        <br />
        <b>Figure 3. UI for Settings</b>
      </td>
    </tr>
  </table>
</div>

## 🛠️ Technical Stack

* **Framework:** Windows App SDK (WinUI 3)
* **Language:** C# (.NET 8)
* **Architecture:** MVVM (Model-View-ViewModel)
* **Win32 Interop (P/Invoke):**
    * `SetWindowsHookEx`: Implements `WH_MOUSE_LL` and `WH_KEYBOARD_LL` for global input interception.
    * `SetWindowSubclass`: Enforces minimum window size constraints via `WM_GETMINMAXINFO`.
    * `RemoveWindowSubclass`: Handles safe teardown on `WM_DESTROY` to prevent access violations.

## 📦 Installation & Setup

1. **Prerequisites:**
   * Windows 10 (1809+) or Windows 11.
   * Visual Studio 2022 with "Windows App SDK" workload.

2. **Build:**
   * Clone the repository.
   * Open `DzikirCounter.sln`.
   * Set platform to **x64**.
   * Build and Run.

3. **Assets:**
   * Ensure `sound/pop.mp3` and `sound/success.mp3` are present in the project directory with **Build Action: Content**.
   * Ensure `Assets/icon.ico` is set up for the window title bar.

4. **Trusting the Certificate (Required for MSIX Installation):**
   * To install the generated `.msixbundle` as an app (Sideloading), you must first trust the certificate:
   * Locate the certificate file (usually in the build output folder alongside the bundle).
   * Double-click or right-click the certificate and select Install Certificate.
   * Select `Local Machine` (requires Admin rights) and click Next.
   * Choose Place all certificates in the following store.
   * Click Browse and select `Trusted Root Certification Authorities`.
   * Click OK -> Next -> Finish.

You can now double-click the .msixbundle to install the app.

## ⚠️ Permissions & Antivirus Note

This application uses **Global Low-Level Hooks** to function in the background.

* **Why?** This allows the app to detect clicks when it is *not* the active window (e.g., while you are reading a PDF).
* **False Positives:** Some security software may flag this behavior as keylogging. The code is open source and strictly counts clicks/keys only when the toggles are active.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

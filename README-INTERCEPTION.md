# Kathana Bot: Interception setup and integration

This guide describes the Interception route for the original Kathana Bot. **The backend is not implemented yet. Installing the driver alone will not change the bot's input method.** The checkout inspected for this guide uses `ForegroundWindowsInput` with `NativeInputPlatform` and SendInput.

Interception controls keyboard/mouse input through installed drivers. It does not create a pretend Logitech device. A custom virtual-HID driver is a separate development option, described below. Neither route guarantees that the game accepts input or stops disconnecting.

## 1. Get the official package

Download the binary ZIP from the [official Interception releases](https://github.com/oblitum/Interception/releases), not the GitHub source ZIP. Extract it to a dedicated folder and retain the installer for removal later.

The [project README](https://github.com/oblitum/Interception) documents administrator installation and testing through Windows 10. Treat compatibility with your exact Windows version as unverified until tested. Read its license before redistributing the library or driver with Kathana Bot; commercial distribution has separate terms.

Use a local desktop for initial setup, with your work saved. If Windows blocks the driver, record the error and stop; this guide does not require disabling Windows security features. Some anti-cheat systems explicitly block Interception; [FACEIT's documentation](https://support.faceit.com/hc/en-us/articles/360014237259--Forbidden-driver-error-message-and-blocked-drivers) includes removal instructions. Use it only where this type of automation is permitted.

## 2. Install and restart

Open **PowerShell as administrator**. Navigate to the extracted folder containing `install-interception.exe`. Replace the example path with your actual folder:

```powershell
Set-Location -LiteralPath 'C:\Tools\Interception\command line installer'
.\install-interception.exe
```

Read the installer's help first. For the standard package, installation is:

```powershell
.\install-interception.exe /install
```

Check the result, then restart Windows as instructed. The upstream README directs users to this command-line installer's help as the authority for their package. Do not copy driver files manually into Windows folders.

## 3. Identify the devices

Use the upstream [hardware-ID sample](https://github.com/oblitum/Interception/blob/master/samples/hardwareid/hardwareid.cpp) as the reference for a diagnostic utility. Its source observes keyboard and left-button events, displays hardware IDs, and forwards captured events. Escape exits the sample. If your package does not include a compiled utility, building one is a development task; the existing bot has no device-selection screen for this backend.

In the proposed bot setup, select the keyboard and mouse by observing one deliberate physical input from each. Do not assume that device 1 is your Logitech keyboard. Revalidate selection after reconnecting devices or restarting. A Logitech receiver can expose more than one input interface.

## 4. Implement the bot adapter — required development

The following are proposed changes, not available settings or completed features:

1. Add `InterceptionInputPlatform.vb` under `ui/KathanaBotControlPanel`, implementing the existing `InputPlatform` abstraction. Keep foreground checks and held-input cleanup in `ForegroundWindowsInput.vb`.
2. Add native bindings using the exact layouts and calling convention in [interception.h](https://github.com/oblitum/Interception/blob/master/library/interception.h). Use the x64 library for a `win-x64` build. The API provides context creation/destruction, device identification, receive, and send operations.
3. Translate keyboard events into scan-code strokes with separate press/release and extended-key flags. Translate mouse events into mouse strokes. Do not reuse Windows flag values directly; the enums differ.
4. Implement explicit device selection, missing-driver reporting, and context disposal. Check the number of strokes accepted by `interception_send`; do not treat that as proof the game acted.
5. Add backend selection at `WindowsInput.DefaultInput` construction. The existing `WindowsInput.Current` override is a test seam; switching it alone would leave binding and release monitoring attached to the old backend. Backend changes must stop actions, release held input, and replace/dispose the backend consistently.
6. Keep foreground verification before every new action. Driver input is not addressed to a window handle. Track which device owns each press so stop, cancellation, focus loss, and shutdown release it correctly.
7. Implement text separately. Do not assume the current Unicode-input path translates directly into scan codes. Test keyboard-layout mapping and modifiers; explicitly report unsupported characters rather than silently changing text or falling back to PostMessage.
8. During device discovery, forward captured physical events and use cancellable waits. Restore filters and dispose the context on exit so the utility cannot leave normal input trapped.
9. Log the selected backend, device, operation, result, and time. A failed driver load should stop that backend, not silently switch input methods.

These steps are the proposed Kathana integration design. The header establishes the API; it is not a complete .NET wrapper or application implementation.

## 5. Validate before game use

After implementation, test in a local diagnostic window or Notepad first:

- One key press/release; arrows; modifiers; held-key cancellation.
- Mouse movement and button releases, including multiple monitors.
- Focus loss, device disconnection, missing DLL/driver, and shutdown while holding input.
- Exact case, punctuation, and unsupported-character handling in text.
- Combat, skill shortcuts, loot, chat/trade, quiz, relaunch, and disconnect-dialog actions all use the selected backend.

Add adapter tests and run the repository suite:

```powershell
.\run-all-tests.ps1
```

Only then compare a small fixed action sequence in an environment that permits it. Keep the sequence and rate equal between backends. Record actual actions and the disconnect message/time. A successful driver call does not establish why a server disconnects.

## 6. Package the application

Once implementation and tests are complete, publish the original project:

```powershell
dotnet publish ui/KathanaBotControlPanel/KathanaBotControlPanel.vbproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/KathanaBot_Interception
```

Configure the project to distribute the matching native DLL and required license notices. For the first build, an explicit `interception.dll` beside the EXE makes dependency checks easier. Test the published output, not just the development build.

**Single-file .NET publishing does not install the driver.** Each target PC needs a separate driver installation and restart. Do not label an unchanged publish as an Interception-enabled release.

## 7. Remove Interception

Stop applications using it. Open administrator PowerShell in the installer folder, check its help, then run:

```powershell
.\install-interception.exe /uninstall
```

Restart Windows and verify normal input. Keep the installer output if removal fails; do not manually delete keyboard/mouse drivers or registry filters. Follow the package instructions or the linked [removal guidance](https://support.faceit.com/hc/en-us/articles/360014237259--Forbidden-driver-error-message-and-blocked-drivers).

## Alternative: a custom virtual-HID backend

For a newly enumerated virtual device, use [Microsoft's Virtual HID Framework](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-). This requires a kernel-mode HID source driver, report descriptors, and input reports. It is not an Interception setting.

The development sequence would be: define keyboard/mouse reports, build a driver using the WDK, expose a controlled application interface, implement a Kathana adapter, validate in a driver-test environment, then arrange driver signing and deployment. The [Microsoft HID minidriver sample](https://github.com/microsoft/Windows-driver-samples/blob/main/hid/vhidmini2/README.md) is another reference architecture, not a ready-to-install Kathana driver or a drop-in VHF implementation.

This README documents the route only. No driver was installed and no Interception-enabled EXE was built as part of writing it.

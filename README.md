# Razer plugin for Fan Control

The unofficial Razer plugin for [Fan Control](https://github.com/Rem0o/FanControl.Releases).

[![Support](https://img.shields.io/badge/Support-Venmo-blue?style=for-the-badge&logo=venmo&color=3D95CE)](https://www.venmo.com/u/EvanMulawski)
[![Support](https://img.shields.io/badge/Support-Buy_Me_A_Coffee-yellow?style=for-the-badge&logo=buy%20me%20a%20coffee&color=FFDD00)](https://www.buymeacoffee.com/evanmulawski)

## Device Support

| Device             | PID    | Status       | Read Fan/Pump RPM | Set Fan/Pump Power | Read Temp Sensor |
| ------------------ | ------ | ------------ | ----------------- | ------------------ | ---------------- |
| PWM Fan Controller | `0f3c` | Full Support | ✅                | ✅ <sup>1</sup>    | n/a              |

1. The device appears to set a fan to full speed at 85%+. Users must take this into consideration when building a fan curve. Please provide feedback using Discussions and include the fan model(s).

## Installation

⚠ This plugin will not function correctly if Razer Synapse is running. This software should be stopped before running Fan Control. Running other programs that attempt to communicate with these devices while Fan Control is running is not currently a supported scenario.

⚠ This plugin requires the .NET Framework build of Fan Control. Install Fan Control using the `FanControl_net_4_8.zip` release files.

1. Download a [release](https://github.com/EvanMulawski/FanControl.Razer/releases).
2. Unblock the downloaded ZIP file. (Right-click, Properties, check Unblock, OK)
3. Exit Fan Control.
4. Copy `FanControl.Razer.dll` to the Fan Control `Plugins` directory.
5. Start Fan Control.

## Interoperability

This plugin implements a non-standard global mutex (`Global\RazerReadWriteGuardMutex`) to synchronize device communication.

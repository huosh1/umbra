# Umbra privacy policy

Effective date: August 11, 2026

Umbra is designed to work locally on the user's Windows device. Umbra does not
operate an analytics service, advertising service, or user-account backend.

## Data processed by the Windows application

Umbra stores focus sessions, schedules, blocklists, blocked-attempt counters,
music playback statistics, appearance settings, and application preferences in
`%APPDATA%\UmbraNative\data`.

Spotify and other media information is read from the Windows Global System
Media Transport Controls interface. Umbra does not request or store Spotify
credentials.

## Data processed by the browser extension

The browser extension needs access to navigation URLs so it can compare their
domains with the blocklist selected in Umbra. When a navigation matches an
active blocked domain, the extension:

1. redirects the navigation to its local blocked page; and
2. sends the matched domain to the local Umbra native messaging host so the
   blocked-attempt counter can be updated.

The extension does not transmit browsing history, page content, or blocked
domains to an Umbra-operated remote server. Native messaging occurs only
between the extension and the Umbra process on the same computer.

## Network access

Umbra may open GitHub or the Chrome Web Store when the user asks to install an
extension, view a release, or obtain support. The application does not upload
its local history to those services.

## Retention and deletion

Data remains on the device until it is removed through Umbra or by deleting
`%APPDATA%\UmbraNative\data` while Umbra is not running. Uninstalling Umbra
does not delete this directory, which prevents an update or reinstall from
unexpectedly erasing user history.

## Contact

Privacy questions can be submitted through the repository's
[issue tracker](https://github.com/zixload/umbra/issues).

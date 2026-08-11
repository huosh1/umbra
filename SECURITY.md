# Security policy

## Supported versions

Security fixes are provided for the most recent Umbra release.

## Reporting a vulnerability

Please do not open a public issue for a vulnerability. Use GitHub's
**Security > Report a vulnerability** flow for this repository so details can
be reviewed privately.

Include the Umbra version, Windows version, affected browser, reproduction
steps, and the expected impact. Do not include personal browsing history or
other private data in the report.

## Privileged component

Umbra can start an elevated watchdog when application or system-level blocking
is enabled. The browser extension communicates with a per-user native messaging
host restricted to the published Umbra extension ID.

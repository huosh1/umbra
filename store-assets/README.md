# Chrome Web Store assets

These files are ready to upload in the Chrome Web Store Developer Dashboard.

## Graphics

- Store icon: `icon-128.png`
- Screenshots, in order: `screenshots/01-blocked-site.png` through
  `screenshots/05-settings.png`
- Small promotional image: `promo-small-440x280.png`
- Marquee promotional image: `promo-marquee-1400x560.png`
- Promotional YouTube video: leave empty for now

The five screenshots are 1280 x 800 PNG files encoded as 24-bit RGB. Both
promotional images are also 24-bit RGB and have the exact canvas sizes required
by the Store. The icon is a 128 x 128 PNG with transparent padding, as required
for irregular icons.

## Listing URLs

- Official URL: select **None** until a domain is verified in Google Search
  Console.
- Homepage URL: `https://github.com/huosh1/umbra`
- Support URL: `https://github.com/huosh1/umbra/issues`
- Privacy policy URL:
  `https://github.com/huosh1/umbra/blob/main/PRIVACY.md`
- Adult content: **No**
- Item support: enable it and use the support URL above.

## Suggested listing copy

Single purpose:

> Block distracting websites selected in the Umbra desktop application while
> a focus session is active.

Description:

> Umbra Blocker connects to Umbra for Windows and blocks websites from your
> selected blocklist during focus sessions. It works locally through the native
> messaging bridge installed with Umbra. The extension records only attempts to
> open domains that match the active blocklist and does not send browsing data
> to an Umbra server.

Do not submit the item for review until its Chrome Web Store item ID has been
copied into the Umbra application and installer native-messaging allowlist.

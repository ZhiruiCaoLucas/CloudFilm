# OHIF Viewer

CloudFilm deploys the upstream `ohif/app` image unmodified. Two files are mounted
over the image defaults:

| File | Mounted at | Purpose |
| --- | --- | --- |
| `app-config.js` | `/usr/share/nginx/html/app-config.js` | Points OHIF at CloudFilm's DICOMweb proxy and disables the study list. |
| `default.conf` | `/etc/nginx/conf.d/default.conf` | Serves the bundle and proxies `/dicom-web` to Orthanc. |

## Why an iframe, not a React dependency

The portal embeds the viewer by URL rather than importing it. That boundary means:

- OHIF can be upgraded, or replaced, without touching portal code.
- Viewer JavaScript never shares an execution context with the portal session.
- The viewer's heavy bundle is loaded only when a study is actually opened,
  which is what keeps portal start-up under two seconds.

## Extending the viewer

Customisations belong in an OHIF **extension** or **mode**, loaded through
`app-config.js`. Do not fork the OHIF source; a fork cannot be upgraded and would
become the most expensive thing in this repository to maintain.
